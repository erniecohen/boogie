using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;
using NUnit.Framework;

namespace ExecutionEngineTests;

[TestFixture]
public class UnsatSolverTest {
  private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(10);

  private sealed class HeldContext : SynchronizationContext {
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> callbacks = new();
    public readonly TaskCompletionSource Posted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Post(SendOrPostCallback callback, object state) {
      callbacks.Enqueue((callback, state));
      Posted.TrySetResult();
    }

    public void RunPending() {
      while (callbacks.TryDequeue(out var next)) {
        next.Callback(next.State);
      }
    }
  }

  [Test]
  public async Task CancelledReaderDoesNotGateTheNextResourceResponse() {
    using var semaphore = new SemaphoreSlim(0);
    using var cancellation = new CancellationTokenSource();
    var solver = new UnsatSolver(semaphore);
    var held = new HeldContext();
    var previousContext = SynchronizationContext.Current;
    Task<SExpr> oldRequest;
    try {
      SynchronizationContext.SetSynchronizationContext(held);
      oldRequest = solver.SendRequest("(check-sat)");
    } finally {
      SynchronizationContext.SetSynchronizationContext(previousContext);
    }

    Task<SExpr> nextRequest = null;
    Task<SExpr> resourceRequest = null;
    try {
      // As in CheckSatAndGetResponse, cancellation stops the wrapper while the
      // underlying fake response reader remains alive for the next run.
      var cancelledWrapper = oldRequest.WaitAsync(cancellation.Token);
      cancellation.Cancel();
      Assert.CatchAsync<OperationCanceledException>(async () => await cancelledWrapper);

      nextRequest = solver.SendRequest("(check-sat)");
      semaphore.Release(2);
      await held.Posted.Task.WaitAsync(SafetyTimeout);
      Assert.AreEqual("unsat", (await nextRequest.WaitAsync(SafetyTimeout)).Name);

      // The old reader has its permit, but its continuation is still held.
      // Peeking before the semaphore wait made this resource read wait for a
      // third permit even though only the two check-sat reads should be gated.
      resourceRequest = solver.SendRequest("(get-info :rlimit)");
      held.RunPending();
      Assert.AreEqual("unsat", (await oldRequest.WaitAsync(SafetyTimeout)).Name);
      Assert.IsTrue(resourceRequest.IsCompletedSuccessfully,
        "A resource response must not consume a third check-sat permit");
      var response = await resourceRequest.WaitAsync(SafetyTimeout);
      Assert.AreEqual(":rlimit", response.Name);
      Assert.AreEqual("0", response.Arguments[0].Name);
      Assert.AreEqual(0, semaphore.CurrentCount);
    } finally {
      // Also drain the deliberately blocked reads when the regression fails.
      semaphore.Release(3);
      await held.Posted.Task.WaitAsync(SafetyTimeout);
      held.RunPending();
      await oldRequest.WaitAsync(SafetyTimeout);
      if (nextRequest != null) { await nextRequest.WaitAsync(SafetyTimeout); }
      if (resourceRequest != null) { await resourceRequest.WaitAsync(SafetyTimeout); }
      solver.Close();
    }
  }
}
