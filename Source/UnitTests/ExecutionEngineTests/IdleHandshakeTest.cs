using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;
using Microsoft.Boogie.VCExprAST;
using NUnit.Framework;
using VC;

namespace ExecutionEngineTests;

[TestFixture]
public class IdleHandshakeTest {
  // No sleeps: the response stays withheld until the test explicitly supplies it.
  private sealed class HeldPingSolver : UnsatSolver {
    public readonly TaskCompletionSource Ping = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Closed;
    public bool Hold;
    public override Task PingPong() => Hold ? Ping.Task : Task.CompletedTask;
    public override void Close() { Closed = true; }
  }

  private sealed class Handler : ProverInterface.ErrorHandler {
    public Handler(SMTLibOptions options) : base(options) { }
  }

  private sealed class RecordingSolver : UnsatSolver {
    public readonly List<string> Commands = new();
    public override void Send(string command) { Commands.Add(command); base.Send(command); }
  }

  // WaitAsync's real timer is replaced with a manually advanced, one-shot clock.
  private sealed class Clock : TimeProvider {
    private Timer timer;
    public TimeSpan Now;
    public TimeSpan Deadline => timer.Deadline;
    public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period) {
      Assert.AreEqual(Timeout.InfiniteTimeSpan, period);
      timer = new Timer(this, callback, state, dueTime);
      return timer;
    }
    public void Advance(TimeSpan amount) {
      Now += amount;
      if (!timer.Disposed && Now >= timer.Deadline) { timer.Callback(timer.State); }
    }
    private sealed class Timer : ITimer {
      public readonly TimerCallback Callback;
      public readonly object State;
      public readonly TimeSpan Deadline;
      public bool Disposed;
      public Timer(Clock clock, TimerCallback callback, object state, TimeSpan dueTime) {
        Callback = callback; State = state; Deadline = clock.Now + dueTime;
      }
      public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
      public void Dispose() { Disposed = true; }
      public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
  }

  private static async Task<Checker> BusyChecker(CheckerPool pool) {
    var checker = await pool.FindCheckerFor(null, null, CancellationToken.None);
    await checker.BeginCheck("idle-test", VCExpressionGenerator.True, new Handler(pool.Options), 0, 0,
      CancellationToken.None);
    await checker.ProverTask;
    Assert.AreEqual(SolverOutcome.Valid, checker.ReadOutcome());
    return checker;
  }

  [Test]
  public async Task LegacyDeadlineRetiresHeldHealthyHandshake() {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, "/legacySolverIdleTimeout:1", "/solverIdleTimeout:7");
    options.VcsCores = 1;
    var solver = new HeldPingSolver();
    options.CreateSolver = (_, _) => solver;
    var pool = new CheckerPool(options);
    try {
      var checker = await BusyChecker(pool);
      solver.Hold = true;
      var clock = new Clock();
      var idle = checker.GoBackToIdle(clock);
      Assert.AreEqual(TimeSpan.FromMilliseconds(100), clock.Deadline);
      clock.Advance(TimeSpan.FromMilliseconds(100));
      await idle.WaitAsync(TimeSpan.FromSeconds(10));
      Assert.True(checker.IsClosed);
      Assert.True(solver.Closed);
      Assert.False(solver.Ping.Task.IsCompleted, "The solver has neither failed nor replied");
    } finally {
      solver.Ping.TrySetResult();
      pool.Dispose();
    }
  }

  [Test]
  public async Task SlowHealthyHandshakeKeepsTheSameSolver() {
    var options = CommandLineOptions.FromArguments(TextWriter.Null);
    options.VcsCores = 1;
    var solver = new HeldPingSolver();
    var created = 0;
    options.CreateSolver = (_, _) => { created++; return solver; };
    var pool = new CheckerPool(options);
    try {
      var checker = await BusyChecker(pool);
      solver.Hold = true;
      var clock = new Clock();
      var idle = checker.GoBackToIdle(clock);
      Assert.AreEqual(TimeSpan.FromSeconds(300), clock.Deadline);
      clock.Advance(TimeSpan.FromSeconds(1));
      Assert.False(idle.IsCompleted, "A second is ten times the old deadline");
      Assert.False(solver.Closed);
      var queued = pool.FindCheckerFor(null, null, CancellationToken.None);
      Assert.False(queued.IsCompleted, "A checker must not be reused before its pong");
      solver.Ping.SetResult();
      await idle.WaitAsync(TimeSpan.FromSeconds(10));
      var reused = await queued.WaitAsync(TimeSpan.FromSeconds(10));
      Assert.AreSame(checker, reused);
      Assert.AreEqual(1, created);
      Assert.False(solver.Closed);
      solver.Hold = false;
      await reused.GoBackToIdle();
    } finally {
      solver.Ping.TrySetResult();
      pool.Dispose();
    }
  }

  [TestCase("death")]
  [TestCase("io")]
  [TestCase("disposed")]
  [TestCase("hang")]
  public async Task FailedHandshakeReleasesTheSlotAndReplacesTheSolver(string failure) {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, "/solverIdleTimeout:7");
    options.VcsCores = 1;
    var solver = new HeldPingSolver();
    var replacement = new UnsatSolver();
    var created = 0;
    options.CreateSolver = (_, _) => ++created == 1 ? solver : replacement;
    var pool = new CheckerPool(options);
    try {
      var checker = await BusyChecker(pool);
      solver.Hold = true;
      var clock = new Clock();
      var idle = checker.GoBackToIdle(clock);
      var queued = pool.FindCheckerFor(null, null, CancellationToken.None);
      Assert.False(queued.IsCompleted);
      Assert.AreEqual(TimeSpan.FromSeconds(7), clock.Deadline);
      switch (failure) {
        case "death": solver.Ping.SetException(new ProverDiedException()); break;
        case "io": solver.Ping.SetException(new IOException("broken pipe")); break;
        case "disposed": solver.Ping.SetException(new ObjectDisposedException("solver")); break;
        case "hang": clock.Advance(TimeSpan.FromSeconds(7)); break;
      }
      await idle.WaitAsync(TimeSpan.FromSeconds(10));
      var next = await queued.WaitAsync(TimeSpan.FromSeconds(10));
      Assert.True(checker.IsClosed);
      Assert.True(solver.Closed);
      Assert.AreNotSame(checker, next);
      Assert.AreEqual(2, created);
      // The replacement is usable, not merely a released semaphore permit.
      await next.BeginCheck("replacement", VCExpressionGenerator.True, new Handler(options), 0, 0,
        CancellationToken.None);
      await next.ProverTask;
      Assert.AreEqual(SolverOutcome.Valid, next.ReadOutcome());
      await next.GoBackToIdle();
    } finally {
      solver.Ping.TrySetResult();
      pool.Dispose();
    }
  }

  [Test]
  public async Task ProcessExitWakesPendingPing() {
    if (OperatingSystem.IsWindows()) { Assert.Ignore("Uses a POSIX shell as a deterministic exited solver"); }
    var options = CommandLineOptions.FromArguments(TextWriter.Null);
    var solverOptions = new SMTLibSolverOptions(options) { ProverPath = "/bin/sh" };
    // The process consumes the ping and exits without a pong. No timer or real solver is involved.
    solverOptions.SolverArguments.Add("-c");
    solverOptions.SolverArguments.Add("\"read line; exit 0\"");
    var process = new SMTLibProcess(options, solverOptions);
    try {
      Assert.ThrowsAsync<ProverDiedException>(() => process.PingPong().WaitAsync(TimeSpan.FromSeconds(10)));
    } finally {
      process.Close();
    }
    await Task.CompletedTask;
  }

  [Test]
  public void OptionsHaveSafeDefaultsAndRejectInvalidBounds() {
    var options = CommandLineOptions.FromArguments(TextWriter.Null);
    Assert.AreEqual(300, options.SolverIdleTimeout);
    Assert.False(options.LegacySolverIdleTimeout);
    Assert.False(CommandLineOptions.FromArguments(TextWriter.Null, "/legacySolverIdleTimeout:0").LegacySolverIdleTimeout);
    foreach (var invalid in new[] { "0", "86401", "-1" }) {
      Assert.False(new CommandLineOptions(TextWriter.Null, new ConsolePrinter()).Parse(new[] { "/solverIdleTimeout:" + invalid }));
    }
  }

  [Test]
  public async Task SuccessfulHandshakesSendIdenticalCommandsInBothModes() {
    async Task<List<string>> Commands(bool legacy) {
      var options = CommandLineOptions.FromArguments(TextWriter.Null);
      options.VcsCores = 1;
      options.LegacySolverIdleTimeout = legacy;
      var solver = new RecordingSolver();
      var created = 0;
      options.CreateSolver = (_, _) => { created++; return solver; };
      var pool = new CheckerPool(options);
      try {
        for (var i = 0; i < 2; i++) {
          var checker = await BusyChecker(pool);
          Assert.AreEqual(0, checker.GetProverResourceCount());
          await checker.GoBackToIdle();
        }
        Assert.AreEqual(1, created);
        return solver.Commands;
      } finally { pool.Dispose(); }
    }
    CollectionAssert.AreEqual(await Commands(false), await Commands(true));
  }
}
