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
/// /cacheUsedTypes and /lockFreeExpansion change how a prover's context is set up for a split,
/// and must not change a byte of what the solver gets.  The tests compare the whole command
/// stream, the background sent after every FullReset and Reset included, with and without them.
/// </summary>
[TestFixture]
public class SplitSetupTest
{
  private const string Optimizations = "/cacheUsedTypes /lockFreeExpansion";

  // Basic types first met in different declarations and implementations, a polymorphic function
  // and map, an inline function called from several implementations, hideable axioms with hide and
  // reveal, and implementations with their own seed and resource limit.
  private const string Text = @"
type T;
type Box a;
function f(x: int): real;
function g<a>(x: a): Box a;
function {:inline} twice(x: int): int { x + x }
function {:inline} pick(b: bool, x: real, y: real): real { if b then x else y }
function h(x: int): int uses {
  hideable axiom (forall x: int :: {h(x)} h(x) == twice(x) + 1);
}
axiom (forall x: int :: {f(x)} f(x) == real(x));
axiom (forall<a> x: a :: {g(x)} g(x) == g(x));
const c: T;
var m: [int]bool;

procedure P(x: int) returns (r: int)
  ensures r == twice(x);
{
  r := twice(x);
  assert r == 2 * x;
  assert twice(r) == 4 * x;
}

procedure {:random_seed 3} Q(b: bool) returns (y: real)
  ensures y == pick(b, 1.0, 2.0);
{
  hide *;
  if (b) { y := 1.0; } else { y := 2.0; }
  assert h(1) == 3;
  reveal h;
  assert h(1) == 3;
}

procedure {:rlimit 100000} R(s: string) modifies m;
{
  var k: int;
  m[twice(k)] := true;
  assert m[2 * k];
  assert g(c) == g(c);
  assert s == s;
}
";

  private static CommandLineOptions Options(string arguments)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null,
      arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    options.VcsCores = 1;
    return options;
  }

  private static Program Parse(CommandLineOptions options)
  {
    Assert.AreEqual(0, Parser.Parse(Text, "program.bpl", out var program));
    Assert.AreEqual(0, program.Resolve(options));
    Assert.AreEqual(0, program.Typecheck(options));
    return program;
  }

  /// <summary>
  /// Everything sent to the solver while a front end runs the verification tasks of the program,
  /// in one log: each task's (reset), background and query.
  /// </summary>
  private static async Task<string> TaskLog(string arguments)
  {
    var options = Options(arguments);
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    options.ProverLogFilePath = Path.Combine(directory, "solver.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options)) {
      foreach (var task in await engine.GetVerificationTasks(Parse(options))) {
        await task.TryRun()!.ToTask();
      }
    }
    var log = await File.ReadAllTextAsync(Path.Combine(directory, "solver.smt2"));
    Directory.Delete(directory, true);
    return FromLeastSkolemId(log);
  }

  /// <summary>Everything the command line sends to the solver, in one log.</summary>
  private static async Task<string> CommandLineLog(string arguments)
  {
    var options = Options(arguments);
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    options.ProverLogFilePath = Path.Combine(directory, "solver.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options)) {
      Assert.AreEqual(0, Parser.Parse(Text, "program.bpl", out var program));
      await engine.ProcessProgram(TextWriter.Null, program, "program.bpl");
    }
    var log = await File.ReadAllTextAsync(Path.Combine(directory, "solver.smt2"));
    Directory.Delete(directory, true);
    return FromLeastSkolemId(log);
  }

  /// <summary>
  /// Each parse numbers its quantifiers' skolem ids on from a counter of the process, so two
  /// parses of the program in one process differ by a constant in every id.  Subtracting each
  /// log's least id makes two logs equal exactly when they are equal up to that constant.
  /// </summary>
  private static string FromLeastSkolemId(string log)
  {
    var ids = Regex.Matches(log, @":skolemid \|(\d+)\|").Select(m => int.Parse(m.Groups[1].Value)).ToList();
    if (ids.Count == 0) {
      return log;
    }
    var least = ids.Min();
    return Regex.Replace(log, @":skolemid \|(\d+)\|", m => $":skolemid |{int.Parse(m.Groups[1].Value) - least}|");
  }

  private static IEnumerable<string> Configurations()
  {
    foreach (var encoding in new[] { "/typeEncoding:a", "/typeEncoding:p" }) {
      foreach (var prune in new[] { "/prune:1", "/prune:0" }) {
        foreach (var order in new[] { "", "/normalizeDeclarationOrder:0", "/randomSeed:7" }) {
          foreach (var split in new[] { "", "/vcsSplitOnEveryAssert" }) {
            yield return $"{encoding} {prune} {order} {split}";
          }
        }
      }
    }
  }

  /// <summary>
  /// The background that a prover sends after each Reset is the one its context, set up by the
  /// last FullReset, sent first.  The command line verifies the splits of an implementation with
  /// one prover, reset between them, so three procedures split on every assertion make three
  /// contexts with several Resets each.
  /// </summary>
  [Test]
  public async Task ResetSendsTheBackgroundOfItsOwnContext()
  {
    // The log has a Reset's background with the line ends of the cached text, \r\n.
    var log = (await CommandLineLog("/typeEncoding:a /vcsSplitOnEveryAssert")).Replace("\r\n", "\n");
    // The log starts with the first context; each later one starts after a FullReset.
    var contexts = log.Split("; did a full reset\n").ToList();
    Assert.AreEqual(3, contexts.Count);
    foreach (var context in contexts) {
      // What is sent before a check, after the context starts or after a Reset.
      var backgrounds = context.Split("(reset)\n")
        .Where(sent => sent.Contains("(push 1)"))
        .Select(sent => sent.Substring(0, sent.IndexOf("(push 1)", StringComparison.Ordinal)))
        // without the log's comments, such as the outcome of the previous check
        .Select(sent => string.Join("\n", sent.Split('\n').Where(line => line != "" && !line.StartsWith(";"))))
        .ToList();
      Assert.Greater(backgrounds.Count, 1);
      foreach (var background in backgrounds) {
        Assert.AreEqual(backgrounds[0], background);
      }
    }
  }

  [Test]
  public void OptionsAreOffByDefault()
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null);
    Assert.IsFalse(options.CacheUsedTypes);
    Assert.IsFalse(options.LockFreeExpansion);
    options = Options(Optimizations);
    Assert.IsTrue(options.CacheUsedTypes);
    Assert.IsTrue(options.LockFreeExpansion);
  }

  [Test]
  public void CachedUsedTypesAreTheVisitorsTypesInTheSameOrder()
  {
    var options = Options("/typeEncoding:a");
    var program = Parse(options);
    foreach (var implementation in program.Implementations) {
      var visited = UsedTypes.Collect(program, implementation, false);
      Assert.IsNotEmpty(visited);
      for (var i = 0; i < 2; i++) { // the second time from the cache
        var cached = UsedTypes.Collect(program, implementation, true);
        Assert.AreEqual(visited.Count, cached.Count, implementation.Name);
        for (var j = 0; j < visited.Count; j++) {
          Assert.AreSame(visited[j], cached[j], $"{implementation.Name}, type {j}");
        }
      }
    }
  }

  [Test]
  public async Task TheSolverGetsTheSameCommandsFromTheTasks()
  {
    var configurations = Configurations().ToList();
    foreach (var configuration in configurations) {
      var expected = await TaskLog(configuration);
      StringAssert.Contains("(reset)", expected, configuration);
      Assert.AreEqual(expected, await TaskLog($"{configuration} {Optimizations}"), configuration);
    }
  }

  [Test]
  public async Task TheSolverGetsTheSameCommandsFromTheCommandLine()
  {
    foreach (var configuration in Configurations()) {
      var expected = await CommandLineLog(configuration);
      StringAssert.Contains("(check-sat)", expected, configuration);
      Assert.AreEqual(expected, await CommandLineLog($"{configuration} {Optimizations}"), configuration);
    }
  }
}
