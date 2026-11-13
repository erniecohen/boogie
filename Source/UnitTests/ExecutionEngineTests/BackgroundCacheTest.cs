using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Boogie;
using NUnit.Framework;

namespace ExecutionEngineTests;

/// <summary>
/// /cacheBackground restarts a prover's context, instead of setting it up again, for a split with the
/// same declarations, seed, type encoding types and SMT options, and sends the commands that set it
/// up as they were sent the first time.  The solver must get the same commands, byte for byte.
/// </summary>
[TestFixture]
public class BackgroundCacheTest
{
  // Splits on every assertion give implementations several splits with the same declarations.  P and
  // Q have the same declarations but Q has an SMT option of its own, which is part of the background,
  // so Q's splits must not restart P's context.  R hides and reveals, so its splits differ in their
  // declarations; S has a seed of its own; T fails, which reports a counterexample.
  private const string Text = @"
type T;
function f(x: int): real;
function {:inline} twice(x: int): int { x + x }
function h(x: int): int uses {
  hideable axiom (forall x: int :: {h(x)} h(x) == twice(x) + 1);
}
function g(x: int): int;
axiom (forall x: int :: {f(x)} f(x) == real(x));
axiom (forall x: int :: {g(x)} g(x) > x);
const c: T;
var m: [int]bool;

procedure P(x: int) returns (r: int)
{
  r := twice(x);
  assert r == 2 * x;
  assert g(r) > r;
  assert f(r) == real(r);
}

procedure {:smt_option ""smt.case_split"", 2} Q(x: int) returns (r: int)
{
  r := twice(x);
  assert r == 2 * x;
  assert g(r) > r;
  assert f(r) == real(r);
}

procedure R(x: int)
{
  hide *;
  assert x == x;
  reveal h;
  assert h(1) == 3;
  assert g(x) > x;
}

procedure {:random_seed 3} S(k: int) modifies m;
{
  m[twice(k)] := true;
  assert m[2 * k];
  assert g(k) > k;
}

procedure T(x: int)
{
  assert g(x) > x;
  assert x > 0;
  assert f(x) == real(x);
}
";

  private static CommandLineOptions Options(string arguments)
  {
    return CommandLineOptions.FromArguments(TextWriter.Null,
      arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
  }

  private static Program Parse(CommandLineOptions options)
  {
    Assert.AreEqual(0, Parser.Parse(Text, "program.bpl", out var program));
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

  /// <summary>
  /// The logs of a run, by file: a single log per prover with one core, else one per check.  A
  /// check's log can get the start of the next context's commands before it is closed, as much as the
  /// timing allows, so it is cut at the first (reset) after its last (check-sat).
  /// </summary>
  private static async Task<List<(string Name, string Text)>> Logs(string arguments, bool tasks)
  {
    var options = Options(arguments);
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    var perCheck = 1 < options.VcsCores;
    options.ProverLogFilePath = Path.Combine(directory, perCheck ? "@PROC@.smt2" : "solver.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options)) {
      if (tasks) {
        await Task.WhenAll((await engine.GetVerificationTasks(Parse(options))).Select(task => task.TryRun()!.ToTask()));
      } else {
        Assert.AreEqual(0, Parser.Parse(Text, "program.bpl", out var program));
        await engine.ProcessProgram(TextWriter.Null, program, "program.bpl");
      }
    }
    var logs = Directory.GetFiles(directory).OrderBy(f => f, StringComparer.Ordinal).Select(file => {
      var text = File.ReadAllText(file);
      if (perCheck) {
        var checkSat = text.LastIndexOf("(check-sat)", StringComparison.Ordinal);
        var reset = checkSat < 0 ? -1 : text.IndexOf("\n(reset)", checkSat, StringComparison.Ordinal);
        if (0 <= reset) {
          text = text.Substring(0, reset + 1);
        }
      }
      return (Path.GetFileName(file), FromLeastSkolemId(text));
    }).ToList();
    Directory.Delete(directory, true);
    Assert.IsNotEmpty(logs);
    return logs;
  }

  private static IEnumerable<string> Configurations()
  {
    foreach (var encoding in new[] { "/typeEncoding:a", "/typeEncoding:p" }) {
      foreach (var order in new[] { "", "/normalizeDeclarationOrder:0", "/randomSeed:7" }) {
        foreach (var more in new[] { "", "/trackVerificationCoverage", "/vcsCores:4", "/vcsCores:4 /scheduleByImplementation" }) {
          yield return $"{encoding} /prune:1 /vcsSplitOnEveryAssert {order} {more}";
        }
      }
    }
    yield return "/typeEncoding:a /prune:0 /vcsSplitOnEveryAssert";
    yield return "/typeEncoding:a /prune:1";
  }

  [Test]
  public void OptionIsOffByDefault()
  {
    Assert.IsFalse(CommandLineOptions.FromArguments(TextWriter.Null).CacheBackground);
    Assert.IsTrue(Options("/cacheBackground").CacheBackground);
    Assert.IsFalse(CommandLineOptions.FromArguments(TextWriter.Null).ScheduleByImplementation);
    Assert.IsTrue(Options("/scheduleByImplementation").ScheduleByImplementation);
  }

  private static async Task SameCommands(bool tasks)
  {
    foreach (var configuration in Configurations()) {
      var expected = await Logs(configuration, tasks);
      var restarts = Microsoft.Boogie.Checker.ContextRestarts;
      var actual = await Logs($"{configuration} /cacheBackground", tasks);
      // With one prover, the splits of an implementation run one after the other on it.
      if (configuration.Contains("/prune:1 /vcsSplitOnEveryAssert") && !configuration.Contains("/vcsCores")) {
        Assert.Greater(Microsoft.Boogie.Checker.ContextRestarts, restarts, $"no context was restarted: {configuration}");
      }
      CollectionAssert.AreEqual(expected.Select(log => log.Name), actual.Select(log => log.Name), configuration);
      foreach (var ((name, text), (_, actualText)) in expected.Zip(actual)) {
        Assert.AreEqual(text, actualText, $"{configuration}: {name}");
      }
    }
  }

  /// <summary>
  /// Everything one prover gets when a front end starts the program's tasks interleaved: the first
  /// split of each implementation, then the second of each, and so on.
  /// </summary>
  private static async Task<string> InterleavedLog(string arguments)
  {
    var options = Options(arguments);
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    options.ProverLogFilePath = Path.Combine(directory, "solver.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options)) {
      var tasks = (await engine.GetVerificationTasks(Parse(options)))
        .OrderBy(task => task.Split.SplitIndex)
        .ThenBy(task => task.Split.Implementation.Name, StringComparer.Ordinal)
        .ToList();
      await Task.WhenAll(tasks.Select(task => task.TryRun()!.ToTask()).ToList());
    }
    var log = await File.ReadAllTextAsync(Path.Combine(directory, "solver.smt2"));
    Directory.Delete(directory, true);
    return FromLeastSkolemId(log);
  }

  /// <summary>The implementations of the checks of a log, in the order they were sent.</summary>
  private static List<string> CheckedImplementations(string log)
  {
    return Regex.Matches(log, @"\(set-info :boogie-vc-id ([^\s)]+)\)")
      .Select(m => m.Groups[1].Value.Split('$')[0]).ToList();
  }

  /// <summary>
  /// /cacheBackground alone does not change which solver gets which check, or in what order: a
  /// solver keeps native state across (reset), so its resource counts depend on everything it got,
  /// not only on each check's commands.  One prover's whole log is the same without and with it,
  /// with the tasks started interleaved.  /scheduleByImplementation is what runs an implementation's
  /// splits one after the other; the test records that it does.
  /// </summary>
  [Test]
  public async Task OnlyTheSchedulingOptionChangesTheOrderOfChecks()
  {
    const string configuration = "/typeEncoding:a /prune:1 /vcsSplitOnEveryAssert /vcsCores:1";
    var off = await InterleavedLog(configuration);
    var order = CheckedImplementations(off);
    Assert.Greater(order.Count, 5);
    Assert.AreNotEqual(order[0], order[1], "the tasks did not run interleaved: " + string.Join(" ", order));

    var restarts = Microsoft.Boogie.Checker.ContextRestarts;
    Assert.AreEqual(off, await InterleavedLog($"{configuration} /cacheBackground"));

    var scheduled = CheckedImplementations(await InterleavedLog($"{configuration} /cacheBackground /scheduleByImplementation"));
    CollectionAssert.AreEquivalent(order, scheduled);
    // Each implementation's checks run one after the other, after its first.
    var runs = scheduled.Where((name, i) => i == 0 || scheduled[i - 1] != name).ToList();
    CollectionAssert.AllItemsAreUnique(runs, string.Join(" ", scheduled));
    Assert.Greater(Microsoft.Boogie.Checker.ContextRestarts, restarts);
  }

  [Test]
  public Task TheSolverGetsTheSameCommandsFromTheTasks() => SameCommands(true);

  [Test]
  public Task TheSolverGetsTheSameCommandsFromTheCommandLine() => SameCommands(false);
}
