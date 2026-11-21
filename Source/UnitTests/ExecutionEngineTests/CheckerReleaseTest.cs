using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Boogie;
using NUnit.Framework;

namespace ExecutionEngineTests;

/// <summary>
/// <see cref="VC.CheckerPool.Release"/> makes the idle checkers last set up for a program forget it and
/// drop their prover's context.  It must free the program and change nothing the solver receives.
/// </summary>
[TestFixture]
public class CheckerReleaseTest
{
  private const string First = @"
type T;
function f(x: int): real;
function g(x: int): int;
axiom (forall x: int :: {f(x)} f(x) == real(x));
axiom (forall x: int :: {g(x)} g(x) > x);
const c: T;

procedure P(x: int) returns (r: int)
{
  r := x + x;
  assert r == 2 * x;
  assert g(r) > r;
  assert f(r) == real(r);
}

procedure Q(x: int)
{
  assert g(x) > x;
  assert x > 0;
}
";

  private const string Second = @"
function h(x: int): int;
axiom (forall x: int :: {h(x)} h(x) < x);

procedure R(x: int)
{
  assert h(x) < x;
  assert h(x + 1) < x + 1;
}
";

  private static CommandLineOptions Options(string arguments)
  {
    return CommandLineOptions.FromArguments(TextWriter.Null,
      arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
  }

  private static Program Parse(CommandLineOptions options, string text, string name)
  {
    Assert.AreEqual(0, Parser.Parse(text, name, out var program));
    Assert.AreEqual(0, program.Resolve(options));
    Assert.AreEqual(0, program.Typecheck(options));
    return program;
  }

  /// <summary>Each parse numbers skolem ids on from a counter of the process; see SplitSetupTest.</summary>
  private static string FromLeastSkolemId(string log)
  {
    var ids = Regex.Matches(log, @":skolemid \|(\d+)\|").Select(m => int.Parse(m.Groups[1].Value)).ToList();
    if (ids.Count == 0) {
      return log;
    }
    var least = ids.Min();
    return Regex.Replace(log, @":skolemid \|(\d+)\|", m => $":skolemid |{int.Parse(m.Groups[1].Value) - least}|");
  }

  private static async Task Run(ExecutionEngine engine, Program program)
  {
    var tasks = await engine.GetVerificationTasks(program);
    await Task.WhenAll(tasks.Select(task => task.TryRun()!.ToTask()).ToList());
  }

  /// <summary>
  /// Verifies the first program, optionally releases it, then verifies the second, with one prover
  /// logging everything it gets.  Returns that log and the number of checkers the release released.
  /// </summary>
  private static async Task<(string Log, int Released)> Log(string arguments, bool release)
  {
    var options = Options(arguments);
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    options.ProverLogFilePath = Path.Combine(directory, "solver.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    var released = 0;
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options)) {
      var first = Parse(options, First, "first.bpl");
      await Run(engine, first);
      if (release) {
        released = engine.CheckerPool.Release(first);
      }
      await Run(engine, Parse(options, Second, "second.bpl"));
      await Run(engine, first);
    }
    var log = await File.ReadAllTextAsync(Path.Combine(directory, "solver.smt2"));
    Directory.Delete(directory, true);
    return (FromLeastSkolemId(log), released);
  }

  private static IEnumerable<string> Configurations()
  {
    foreach (var encoding in new[] { "/typeEncoding:a", "/typeEncoding:p" }) {
      foreach (var more in new[] { "/prune:1", "/prune:0", "/prune:1 /vcsSplitOnEveryAssert",
                 "/prune:1 /vcsSplitOnEveryAssert /cacheBackground",
                 "/prune:1 /vcsSplitOnEveryAssert /cacheBackground /scheduleByImplementation",
                 "/prune:0 /vcsSplitOnEveryAssert /trackVerificationCoverage" }) {
        yield return $"{encoding} /vcsCores:1 {more}";
      }
    }
  }

  /// <summary>
  /// The solver gets the same commands, byte for byte, with and without a release between programs,
  /// including when the released program is verified again afterwards.
  /// </summary>
  [Test]
  public async Task TheSolverGetsTheSameCommands()
  {
    foreach (var configuration in Configurations()) {
      var (expected, _) = await Log(configuration, false);
      var (actual, released) = await Log(configuration, true);
      Assert.AreEqual(1, released, configuration);
      Assert.AreEqual(expected, actual, configuration);
    }
  }

  // The program is verified, and possibly released, in a method of its own, so that no local of the
  // test keeps it alive.
  [MethodImpl(MethodImplOptions.NoInlining)]
  private static async Task<(WeakReference Program, int Released)> VerifyAndForget(ExecutionEngine engine,
    CommandLineOptions options, bool release)
  {
    var program = Parse(options, First, "first.bpl");
    await Run(engine, program);
    var released = release ? engine.CheckerPool.Release(program) : 0;
    return (new WeakReference(program), released);
  }

  // The continuations of a finished check, which return its checker to the pool, may still hold the
  // program for a moment after the tasks complete.
  private static async Task<bool> Collected(WeakReference reference)
  {
    for (var i = 0; i < 20 && reference.IsAlive; i++) {
      await Task.Delay(100);
      GC.Collect();
      GC.WaitForPendingFinalizers();
    }
    return !reference.IsAlive;
  }

  /// <summary>
  /// Positive control: without a release, the idle checker keeps the last program it verified alive.
  /// With one, nothing in the engine does.
  /// </summary>
  [Test]
  public async Task AReleasedProgramCanBeCollected()
  {
    foreach (var arguments in new[] { "/vcsCores:1 /prune:1", "/vcsCores:1 /prune:0",
               "/vcsCores:2 /prune:1 /vcsSplitOnEveryAssert /cacheBackground /scheduleByImplementation" }) {
      var options = Options(arguments);
      options.ProverOptions.Add("SOLVER=noop");
      using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
      var (kept, none) = await VerifyAndForget(engine, options, false);
      Assert.AreEqual(0, none);
      Assert.IsFalse(await Collected(kept), $"{arguments}: an idle checker no longer holds its last program; the release is untested");

      var (released, count) = await VerifyAndForget(engine, options, true);
      Assert.Greater(count, 0, arguments);
      Assert.IsTrue(await Collected(released), $"{arguments}: a released program is still reachable");
    }
  }

  /// <summary>
  /// A release made while the checker is still finishing the program's last check, as when the
  /// caller acts on a task's result, takes effect once the checker is back in the pool.
  /// </summary>
  [Test]
  public async Task AReleaseDuringTheLastCheckIsDeferred()
  {
    var options = Options("/vcsCores:1 /prune:1");
    options.ProverOptions.Add("SOLVER=noop");
    using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
    var (reference, released, busy) = await VerifyReleasingOnCompletion(engine, options);
    Assert.IsTrue(busy, "the checker was already idle at completion; the deferred release is untested");
    Assert.AreEqual(1, released);
    Assert.IsTrue(await Collected(reference), "a release made during the last check was dropped");
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static async Task<(WeakReference Program, int Released, bool Busy)> VerifyReleasingOnCompletion(
    ExecutionEngine engine, CommandLineOptions options)
  {
    var program = Parse(options, First, "first.bpl");
    var tasks = await engine.GetVerificationTasks(program);
    var released = 0;
    var busy = false;
    var completion = new TaskCompletionSource();
    tasks[0].TryRun()!.Subscribe(status => {
      if (status is Completed) {
        busy = engine.CheckerPool.CheckersNotBackInPool > 0;
        released = engine.CheckerPool.Release(program);
      }
    }, e => completion.TrySetException(e), () => completion.TrySetResult());
    await completion.Task;
    return (new WeakReference(program), released, busy);
  }

  /// <summary>
  /// Negative control: a release of another program, or of one no checker was set up for, releases
  /// nothing, and the checkers keep their program.
  /// </summary>
  [Test]
  public async Task OnlyCheckersOfTheProgramAreReleased()
  {
    var options = Options("/vcsCores:1 /prune:1");
    options.ProverOptions.Add("SOLVER=noop");
    using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
    var first = Parse(options, First, "first.bpl");
    var second = Parse(options, Second, "second.bpl");
    Assert.AreEqual(0, engine.CheckerPool.Release(first), "no checker exists yet");
    await Run(engine, first);
    Assert.AreEqual(0, engine.CheckerPool.Release(second));
    Assert.AreEqual(1, engine.CheckerPool.Release(first));
    Assert.AreEqual(0, engine.CheckerPool.Release(first), "already released");
  }
}
