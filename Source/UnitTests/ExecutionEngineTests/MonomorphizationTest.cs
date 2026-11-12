using System;
using System.IO;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Boogie;
using NUnit.Framework;

namespace ExecutionEngineTests;

[TestFixture]
public class MonomorphizationTest
{
  private static Program Parse(string text)
  {
    Assert.AreEqual(0, Parser.Parse(text, "monomorphization.bpl", out var program));
    return program;
  }

  [TestCase("m")]
  [TestCase("a")]
  [TestCase("p")]
  public async Task TaskApiSpecializesOnlyTheMonomorphicEncoding(string encoding)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, "/typeEncoding:" + encoding);
    options.VcsCores = 1;
    using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
    var program = Parse(@"
function Id<T>(x: T): T;
axiom (forall<T> x: T :: {Id(x)} Id(x) == x);
procedure Good() { assert Id(42) == 42; assert Id(true); }
procedure Bad() { assert Id(42) == 42; assert false; }
");
    var tasks = await engine.GetVerificationTasks(program);
    Assert.AreEqual(2, tasks.Count);
    Assert.AreEqual(encoding == "m", MonomorphismChecker.IsMonomorphic(program));
    var expected = options.TypeEncodingMethod;
    foreach (var task in tasks)
    {
      var result = ((Completed)await task.TryRun()!.ToTask()).Result;
      Assert.AreEqual(task.Split.Implementation.Name == "Good" ? SolverOutcome.Valid : SolverOutcome.Invalid,
        result.Outcome);
    }
    Assert.AreEqual(expected, options.TypeEncodingMethod);
  }

  [TestCase("m")]
  [TestCase("a")]
  public async Task TaskApiAcceptsPretypedBitvectorConcatenations(string encoding)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, "/typeEncoding:" + encoding);
    options.VcsCores = 1;
    using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
    var program = Parse(@"
function Id<T>(x: T): T;
axiom (forall<T> x: T :: {Id(x)} Id(x) == x);
procedure Good(x: bv16) { assert Id(0bv8 ++ x) == 0bv8 ++ x; }
procedure Bad(x: bv16) { assert Id(0bv8 ++ x) == 0bv8 ++ x; assert false; }
");
    // Front ends supply already typed expressions. The API typechecks again:
    // concat gets a fresh width proxy while its pretyped enclosing call keeps its type.
    Assert.AreEqual(0, program.Resolve(options));
    Assert.AreEqual(0, program.Typecheck(options));
    var tasks = await engine.GetVerificationTasks(program);
    Assert.AreEqual(2, tasks.Count);
    foreach (var task in tasks)
    {
      var result = ((Completed)await task.TryRun()!.ToTask()).Result;
      Assert.AreEqual(task.Split.Implementation.Name == "Good" ? SolverOutcome.Valid : SolverOutcome.Invalid,
        result.Outcome);
    }
  }

  [TestCase("a", CoreOptions.TypeEncoding.Arguments)]
  [TestCase("p", CoreOptions.TypeEncoding.Predicates)]
  [TestCase("m", CoreOptions.TypeEncoding.Monomorphic)]
  public async Task TaskApiPreservesExplicitEncodingForAnAlreadyMonomorphicProgram(
    string encoding, CoreOptions.TypeEncoding expected)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, "/typeEncoding:" + encoding);
    options.VcsCores = 1;
    using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
    Assert.AreEqual(1, (await engine.GetVerificationTasks(Parse("procedure P() { assert true; }"))).Count);
    Assert.AreEqual(expected, options.TypeEncodingMethod);
  }

  [TestCase(@"function F<T, U>(x: U): [T]U; procedure P() { assume {:add_to_pool ""A"", F(false)} true; }", "unhandled polymorphic features")]
  [TestCase(@"type C T; function G<T>(x: T): C T;
function F<T>(x: T): bool { F(G(x)) }
procedure P() { assert F(0); }", "expanding type cycle")]
  public void TaskApiRejectsProgramsThatCannotBeMonomorphized(string text, string diagnostic)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null, "/typeEncoding:m");
    options.VcsCores = 1;
    using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
    var program = Parse(text);
    var error = Assert.ThrowsAsync<ArgumentException>(() => engine.GetVerificationTasks(program));
    StringAssert.Contains(diagnostic, error!.Message);
  }
}
