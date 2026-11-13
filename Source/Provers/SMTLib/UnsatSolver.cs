using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Boogie.SMTLib;

public class UnsatSolver : NoopSolver {
  private readonly SemaphoreSlim semaphore;

  public UnsatSolver() : this(new SemaphoreSlim(int.MaxValue)) {
  }

  public UnsatSolver(SemaphoreSlim semaphore) {
    this.semaphore = semaphore;
  }

  public override void Send(string request)
  {
    if (request == "(check-sat)")
    {
      responses.Enqueue(new SExpr("unsat"));
      return;
    }
    base.Send(request);
  }


  protected override async Task<SExpr> GetProverResponse() {
    // Reserve this reader's response before suspending: a cancelled check may
    // still have a pending reader when the checker starts its next request.
    var response = await base.GetProverResponse();
    if (response?.Name == "unsat") {
      await semaphore.WaitAsync();
    }
    return response;
  }
}
