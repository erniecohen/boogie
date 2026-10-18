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
/// A program that a front end builds in memory, printed and parsed again, must send the solver
/// the same queries as the program itself.  These tests cover what the printer used to drop (the
/// attributes of if commands and of assignments), the command line's own verification pipeline
/// (/verifyAsTasks), and decisions that depended on which expression object a literal is
/// (/structuralLiterals).
/// </summary>
[TestFixture]
public class PrintFidelityTest
{
  private static CommandLineOptions Options(params string[] arguments)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, arguments);
    options.Prune = true;
    options.NormalizeDeclarationOrder = false;
    options.NormalizeNames = true;
    options.EmitDebugInformation = false;
    return options;
  }

  private static Program Parse(string text)
  {
    Assert.AreEqual(0, Parser.Parse(text, "program.bpl", out var program), text);
    return program;
  }

  private static string Print(Program program, CommandLineOptions options)
  {
    var text = new StringWriter();
    using (var writer = new TokenTextWriter("<print>", text, false, false, options))
    {
      program.Emit(writer);
    }
    return text.ToString();
  }

  private static string NewLogDirectory(CommandLineOptions options)
  {
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    options.ProverLogFilePath = Path.Combine(directory, "@PROC@.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    return directory;
  }

  private static List<(string Name, string Text)> Logs(string directory)
  {
    var logs = Directory.GetFiles(directory).OrderBy(f => f, StringComparer.Ordinal)
      .Select(f => (Path.GetFileName(f), File.ReadAllText(f))).ToList();
    Assert.IsNotEmpty(logs);
    return logs;
  }

  /// <summary>The solver queries of the command line's verification of a program.</summary>
  private static async Task<List<(string Name, string Text)>> CommandLineLogs(Program program, CommandLineOptions options)
  {
    var directory = NewLogDirectory(options);
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options))
    {
      await engine.ProcessProgram(TextWriter.Null, program, "1");
    }
    return Logs(directory);
  }

  /// <summary>The solver queries of the verification tasks that a front end runs.</summary>
  private static async Task<List<(string Name, string Text)>> TaskLogs(Program program, CommandLineOptions options)
  {
    var directory = NewLogDirectory(options);
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options))
    {
      foreach (var task in await engine.GetVerificationTasks(program))
      {
        Assert.IsInstanceOf<Completed>(await task.TryRun()!.ToTask());
      }
    }
    return Logs(directory);
  }

  private static List<string> Texts(List<(string Name, string Text)> logs) => logs.Select(log => log.Text).ToList();

  private const string Attributed = @"
procedure P(x: int) returns (r: int)
{
  r := {:id ""a0""} 0;
  if {:allow_path_isolation} (x < 0) {
    r := {:id ""a1""} 0 - x;
  } else if {:allow_path_isolation} (x == 0) {
    r := 1;
  } else {
    r := x;
  }
  assert {:isolate ""paths""} r >= 0;
}
";

  [Test]
  public void IfAndAssignmentAttributesArePrinted()
  {
    var options = Options();
    var text = Print(Parse(Attributed), options);
    StringAssert.Contains("if {:allow_path_isolation} (x < 0)", text);
    StringAssert.Contains("else if {:allow_path_isolation} (x == 0)", text);
    StringAssert.Contains("r := {:id \"a0\"} 0;", text);
    StringAssert.Contains("r := {:id \"a1\"} 0 - x;", text);
    Assert.AreEqual(text, Print(Parse(text), options));
  }

  [Test]
  public void CommandsWithoutAttributesPrintAsBefore()
  {
    var text = Print(Parse(@"
procedure P(x: int) returns (r: int)
{
  r := 0;
  if (x < 0) { r := 1; } else if (*) { r := 2; }
}
"), Options());
    StringAssert.Contains("    if (x < 0)", text);
    StringAssert.Contains("    else if (*)", text);
    StringAssert.Contains("    r := 0;", text);
  }

  [Test]
  public async Task PrintedIfAttributesKeepThePathSplits()
  {
    // {:isolate "paths"} splits at the jumps that allow path isolation: three tasks.  When the
    // printer dropped the attribute, the printed program had one.
    var options = Options();
    var original = await TaskLogs(Parse(Attributed), Options());
    Assert.Greater(original.Count, 1);
    var printed = await TaskLogs(Parse(Print(Parse(Attributed), options)), Options());
    CollectionAssert.AreEqual(original, printed);
  }

  /// <summary>
  /// A join reached from a branch that ends in "assume false", after a branch whose commands
  /// survive passification.  The command line's type checking prunes the edge from the first
  /// branch before blocks are coalesced, and so merges the join into the second branch; the
  /// verification tasks keep the join a block of its own.
  /// </summary>
  private const string JoinAfterAssumeFalse = @"
procedure N(xs: int, ys: int)
{
  var i: int;
  if (*) {
    havoc i;
    if (*) {
      assert i == i;
      assume false;
    }
    assert i >= 0 || i < 0;
    assume false;
  } else {
    assume xs == ys;
  }
  assume true;
  assert xs == ys;
}
";

  [Test]
  public void VerifyAsTasksIsParsed()
  {
    Assert.IsFalse(CommandLineOptions.FromArguments(TextWriter.Null).VerifyAsTasks);
    Assert.IsTrue(CommandLineOptions.FromArguments(TextWriter.Null, "/verifyAsTasks").VerifyAsTasks);
  }

  [Test]
  public async Task VerifyAsTasksSendsTheQueriesOfTheTasks()
  {
    var tasks = await TaskLogs(Parse(JoinAfterAssumeFalse), Options());
    CollectionAssert.AreEqual(tasks, await CommandLineLogs(Parse(JoinAfterAssumeFalse), Options("/verifyAsTasks")));

    // The control: without the option the command line runs its own pipeline, as before, and
    // its query differs from the task's in more than the log's name.
    var commandLine = await CommandLineLogs(Parse(JoinAfterAssumeFalse), Options());
    CollectionAssert.AreNotEqual(Texts(tasks), Texts(commandLine));
  }

  private static async Task<string> Trailer(string text, CommandLineOptions options)
  {
    var output = new StringWriter();
    options.ProverOptions.Add("SOLVER=noop");
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options))
    {
      Assert.IsTrue(await engine.ProcessProgram(output, Parse(text), "1"));
    }
    return output.ToString().Split('\n').Single(line => line.Contains("Boogie program verifier finished with"));
  }

  [Test]
  public async Task VerifyAsTasksCountsImplementationsLikeTheCommandLine()
  {
    // Three tasks, for two implementations, one of which has two splits.
    var text = JoinAfterAssumeFalse + @"
procedure R() { assert {:split_here} true; assert true; }
";
    Assert.AreEqual(await Trailer(text, Options()), await Trailer(text, Options("/verifyAsTasks")));
  }

  [Test]
  public async Task VerifyAsTasksRefusesCivl()
  {
    var output = new StringWriter();
    using var engine = ExecutionEngine.CreateWithoutSharedCache(Options("/verifyAsTasks"));
    Assert.IsFalse(await engine.ProcessProgram(output, Parse(@"
yield procedure {:layer 1} Y() { }
"), "1"));
    StringAssert.Contains("/verifyAsTasks does not support CIVL declarations", output.ToString());
  }

  [Test]
  public async Task VerifyAsTasksRefusesToLeaveAPolymorphicProgramUnmonomorphized()
  {
    var text = @"
function F<T>(x: T): T;
procedure P() { assert F(1) == F(1); }
";
    var output = new StringWriter();
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(Options("/verifyAsTasks")))
    {
      Assert.IsFalse(await engine.ProcessProgram(output, Parse(text), "1"));
    }
    StringAssert.Contains("/verifyAsTasks does not monomorphize a polymorphic program", output.ToString());
    Assert.AreEqual(await Trailer(text, Options("/typeEncoding:a")),
      await Trailer(text, Options("/typeEncoding:a", "/verifyAsTasks")));
  }

  /// <summary>
  /// The program with its if guard, or the value assigned on both branches, replaced by the
  /// shared object Expr.True, as a front end builds it.  Its printed text is the same program's.
  /// </summary>
  private static Program WithSharedTrue(string text, bool guard)
  {
    var program = Parse(text);
    var implementation = program.Implementations.Single();
    if (guard)
    {
      // Desugaring the guard Expr.True gives "assume true" on the then branch and
      // Expr.Not(Expr.True), which is Expr.False, on the else branch.  Rewrite the blocks that
      // parsing desugared to match, rather than desugaring the statements again.
      var ifCmd = (IfCmd)implementation.StructuredStmts.BigBlocks.Single(b => b.ec is IfCmd).ec;
      ifCmd.Guard = Expr.True;
      var partitions = implementation.Blocks.SelectMany(b => b.Cmds).OfType<AssumeCmd>()
        .Where(assume => assume.Attributes.FindBoolAttribute("partition")).ToList();
      Assert.AreEqual(2, partitions.Count);
      foreach (var assume in partitions)
      {
        assume.Expr = assume.Expr is LiteralExpr ? Expr.True : Expr.False;
      }
    }
    else
    {
      foreach (var assign in implementation.Blocks.SelectMany(b => b.Cmds).OfType<AssignCmd>())
      {
        assign.Rhss = new List<Expr> { Expr.True };
      }
    }
    return program;
  }

  private const string LiteralGuard = @"
procedure G(x: int) returns (y: int)
{
  y := x;
  if (true) {
    y := 0;
  }
  assert y == 0;
}
";

  private const string SameLiteralOnBothBranches = @"
procedure S(c: bool) returns (b: bool)
{
  if (c) {
    b := true;
  } else {
    b := true;
  }
  assert b;
}
";

  [TestCase(LiteralGuard, true)]
  [TestCase(SameLiteralOnBothBranches, false)]
  public async Task StructuralLiteralsMakeThePrintedProgramSendTheSameQueries(string text, bool guard)
  {
    var printed = Print(WithSharedTrue(text, guard), Options());
    Assert.AreEqual(Print(Parse(text), Options()), printed);

    var inMemory = await TaskLogs(WithSharedTrue(text, guard), Options("/structuralLiterals"));
    CollectionAssert.AreEqual(inMemory, await TaskLogs(Parse(printed), Options("/structuralLiterals")));

    // The control: without the option Boogie still tells the shared object from a parsed literal,
    // so the program built in memory and the printed one get different queries, as before.
    var inMemoryByDefault = await TaskLogs(WithSharedTrue(text, guard), Options());
    CollectionAssert.AreNotEqual(Texts(inMemoryByDefault), Texts(await TaskLogs(Parse(printed), Options())));
  }

  [Test]
  public void StructuralLiteralsIsParsed()
  {
    Assert.IsFalse(CommandLineOptions.FromArguments(TextWriter.Null).StructuralLiterals);
    Assert.IsTrue(CommandLineOptions.FromArguments(TextWriter.Null, "/structuralLiterals").StructuralLiterals);
  }
}
