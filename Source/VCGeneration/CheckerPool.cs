#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;

namespace VC
{
  public class CheckerPool
  {
    private readonly ConcurrentStack<Checker> availableCheckers = new();
    private readonly SemaphoreSlim checkersSemaphore;
    private bool disposed;

    public VCGenOptions Options { get; }

    public CheckerPool(VCGenOptions options)
    {
      Options = options;
      checkersSemaphore = new(options.VcsCores);
      free = options.VcsCores;
    }

    public async Task<Checker> FindCheckerFor(Program program, Split? split, CancellationToken cancellationToken)
    {
      if (Options.CacheBackground)
      {
        return await FindCheckerByImplementation(program, split, cancellationToken);
      }

      await checkersSemaphore.WaitAsync(cancellationToken);
      try {
        if (!availableCheckers.TryPop(out var checker)) {
          checker ??= CreateNewChecker();
        }
        PrepareChecker(program, split, checker);
        return checker;
      } catch (Exception) {
        checkersSemaphore.Release();
        throw;
      }
    }

    // Under /cacheBackground the pool hands out checkers itself instead of through the semaphore.  A
    // prover restarts a context with the same declarations instead of setting it up again, and the
    // splits of an implementation often have the same, so a checker that comes back goes to a split
    // of the implementation it last verified, if one waits, and else to the first split whose
    // implementation no checker is verifying.  The splits of an implementation then tend to run one
    // after the other on one checker.  Which checker verifies a split, and when, does not change
    // what its solver gets.
    private readonly object gate = new();
    private readonly List<(Split? Split, TaskCompletionSource<Checker?> Ready)> waiters = new();
    private readonly Dictionary<Implementation, int> running = new();
    private readonly Dictionary<Checker, Implementation> runningOn = new();
    // The checkers that can be handed out without waiting: available ones and ones not created yet.
    private int free;

    private async Task<Checker> FindCheckerByImplementation(Program program, Split? split, CancellationToken cancellationToken)
    {
      Checker? checker;
      TaskCompletionSource<Checker?>? ready = null;
      lock (gate)
      {
        if (0 < free && waiters.Count == 0)
        {
          free--;
          checker = TakeAvailableChecker(split?.Implementation);
          MarkRunning(split?.Implementation);
        }
        else
        {
          checker = null;
          ready = new TaskCompletionSource<Checker?>(TaskCreationOptions.RunContinuationsAsynchronously);
          waiters.Add((split, ready));
        }
      }

      if (ready != null)
      {
        await using (cancellationToken.Register(() =>
                     {
                       lock (gate)
                       {
                         if (0 < waiters.RemoveAll(waiter => waiter.Ready == ready))
                         {
                           ready.TrySetCanceled(cancellationToken);
                         }
                       }
                     }))
        {
          // null: a checker may be created
          checker = await ready.Task;
        }
      }

      try
      {
        checker ??= CreateNewChecker();
        lock (gate)
        {
          if (split != null)
          {
            runningOn[checker] = split.Implementation;
          }
        }
        PrepareChecker(program, split, checker);
        return checker;
      }
      catch (Exception)
      {
        Returned(checker, checker is { IsClosed: false } ? checker : null);
        throw;
      }
    }

    private void MarkRunning(Implementation? implementation)
    {
      if (implementation != null)
      {
        running[implementation] = running.GetValueOrDefault(implementation) + 1;
      }
    }

    /// <summary>
    /// A checker handed out comes back: available again, or null when it died or could not be made.
    /// It goes to a waiting split, or becomes free.
    /// </summary>
    private void Returned(Checker? handedOut, Checker? available)
    {
      lock (gate)
      {
        if (handedOut != null && runningOn.Remove(handedOut, out var implementation))
        {
          if (--running[implementation] == 0)
          {
            running.Remove(implementation);
          }
        }

        var index = -1;
        if (available?.LastImplementation != null)
        {
          index = waiters.FindIndex(waiter => waiter.Split?.Implementation == available.LastImplementation);
        }
        if (index < 0)
        {
          index = waiters.FindIndex(waiter => waiter.Split == null || !running.ContainsKey(waiter.Split.Implementation));
        }
        if (index < 0 && 0 < waiters.Count)
        {
          index = 0;
        }

        if (0 <= index)
        {
          var (split, ready) = waiters[index];
          waiters.RemoveAt(index);
          MarkRunning(split?.Implementation);
          ready.SetResult(available);
          return;
        }

        if (available != null)
        {
          availableCheckers.Push(available);
        }
        free++;
      }
    }

    /// <summary>
    /// An available checker whose last context was for the implementation, if there is one, and else
    /// the one made available last; null if none is available.
    /// </summary>
    private Checker? TakeAvailableChecker(Implementation? implementation)
    {
      if (implementation != null)
      {
        var others = new Stack<Checker>();
        Checker? found = null;
        while (availableCheckers.TryPop(out var candidate))
        {
          if (candidate.LastImplementation == implementation)
          {
            found = candidate;
            break;
          }
          others.Push(candidate);
        }
        while (others.TryPop(out var other))
        {
          availableCheckers.Push(other);
        }
        if (found != null)
        {
          return found;
        }
      }
      return availableCheckers.TryPop(out var checker) ? checker : null;
    }

    private int createdCheckers;
    private Checker CreateNewChecker()
    {
      if (disposed)
      {
        throw new ObjectDisposedException(nameof(CheckerPool));
      }

      var log = Options.ProverLogFilePath;
      var index = Interlocked.Increment(ref createdCheckers) - 1;
      if (log != null && !log.Contains("@PROC@") && index > 0) {
        log = log + "." + index;
      }

      return new Checker(this, log, Options.ProverLogFileAppend);
    }

    public void Dispose()
    {
      lock(availableCheckers)
      {
        disposed = true;
        if (Options.Trace && Options.CacheBackground)
        {
          Options.OutputWriter.WriteLine("Background cache: {0} contexts restarted, {1} set up",
            Checker.ContextRestarts, Checker.ContextSetups);
        }
        checkersSemaphore.Dispose();
        while (availableCheckers.TryPop(out var checker)) {
          checker.Close();
        }
      }
    }

    void PrepareChecker(Program program, Split? split, Checker checker)
    {
      if (checker.WillingToHandle(program) &&
          (split == null || checker.SolverOptions.RandomSeed == split.RandomSeed && !Options.Prune))
      {
        checker.GetReady();
        return;
      }

      checker.Target(program, checker.TheoremProver.Context, split);
      checker.GetReady();
    }

    public void AddChecker(Checker checker)
    {
      if (checker.IsClosed) {
        throw new Exception();
      }

      lock (availableCheckers) {
        if (disposed) {
          checker.Close();
          return;
        }

        if (Options.CacheBackground)
        {
          Returned(checker, checker);
          return;
        }
        availableCheckers.Push(checker);
        checkersSemaphore.Release();
      }
    }

    public void CheckerDied(Checker checker)
    {
      if (Options.CacheBackground)
      {
        Returned(checker, null);
        return;
      }
      checkersSemaphore.Release();
    }
  }
}
