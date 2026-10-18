using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Boogie;
using NUnit.Framework;

namespace ExecutionEngineTests;

/// <summary>
/// A printed program must parse into the program that was printed.  A front end builds programs
/// in memory that uses clauses cannot express: an axiom owned by two declarations, definition
/// axioms at positions of their own, and hideable definition axioms.  These tests build such a
/// program, print it, parse the printed text, and compare the two.
/// </summary>
[TestFixture]
public class PrintRoundTripTest
{
  private const string Source = @"
type Ty;
function CtorId(d: Ty): int;
function A(): Ty;
const idA: int;
function B(n: int): Ty;
function F(x: int): int;
function G(x: int): int { 2 * x }
axiom {:comment ""constructor identifier""} CtorId(A()) == idA;
axiom idA == 0;
axiom (forall n: int :: {B(n)} CtorId(B(n)) == 1);
axiom (forall x: int :: {F(x)} F(x) == x + 1);

procedure Distinct() { assert A() != B(0); }
procedure ConstantOnly() { assert (exists d: Ty :: CtorId(d) == idA); }
procedure Hidden(y: int) { hide *; assert F(y) == y + 1; }
procedure Revealed(y: int) { hide *; reveal F; reveal G; assert F(y) == y + 1 && G(y) == 2 * y; }
";

  /// <summary>
  /// The program as a front end such as Dafny builds it: the nullary constructor A and the
  /// constant idA share one axiom, the definition axioms of F and G are hideable, and each
  /// definition axiom stands after its owners.
  /// </summary>
  private static Program InMemoryProgram()
  {
    Assert.AreEqual(0, Parser.Parse(Source, "source.bpl", out var program));
    var functions = program.TopLevelDeclarations.OfType<Function>().ToDictionary(f => f.Name);
    var idA = program.TopLevelDeclarations.OfType<Constant>().Single();
    var axioms = program.TopLevelDeclarations.OfType<Axiom>().ToList();
    Assert.AreEqual(5, axioms.Count);
    var definitionOfG = axioms[0];
    var shared = axioms[1];
    var idAIsZero = axioms[2];
    var definitionOfF = axioms[4];
    Assert.AreSame(functions["G"].DefinitionAxiom, definitionOfG);
    definitionOfG.CanHide = true;
    functions["A"].OtherDefinitionAxioms.Add(shared);
    idA.DefinitionAxioms.Add(shared);
    idA.DefinitionAxioms.Add(idAIsZero);
    definitionOfF.CanHide = true;
    functions["F"].OtherDefinitionAxioms.Add(definitionOfF);
    return program;
  }

  private static CommandLineOptions Options(bool inPlace)
  {
    var options = CommandLineOptions.FromArguments(TextWriter.Null);
    options.PrintDefinitionAxiomsInPlace = inPlace;
    options.Prune = true;
    options.NormalizeDeclarationOrder = false;
    options.NormalizeNames = true;
    options.EmitDebugInformation = false;
    return options;
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

  private static Program Parse(string text, CommandLineOptions options)
  {
    Assert.AreEqual(0, Parser.Parse(text, "print.bpl", out var program), text);
    Assert.AreEqual(0, program.Resolve(options), text);
    return program;
  }

  private static string Attributes(QKeyValue kv)
  {
    var attributes = new List<string>();
    for (; kv != null; kv = kv.Next)
    {
      attributes.Add(kv.Key + "(" + string.Join(", ", kv.Params.Select(p => p.ToString())) + ")");
    }
    return string.Join(" ", attributes);
  }

  /// <summary>
  /// What a declaration is, apart from its owners: its kind, its name, and for an axiom its
  /// expression and whether it is hideable.  Each with its attributes.
  /// </summary>
  private static string Describe(Declaration declaration)
  {
    var description = declaration.GetType().Name + " " + Attributes(declaration.Attributes);
    return declaration switch
    {
      Axiom axiom => description + (axiom.CanHide ? " hideable " : " ") + axiom.Expr,
      NamedDeclaration named => description + " " + named.Name,
      _ => description
    };
  }

  /// <summary>
  /// The definition axioms of each function and constant, as positions in the declarations.
  /// </summary>
  private static List<string> Owners(Program program)
  {
    var declarations = program.TopLevelDeclarations.ToList();
    string Positions(IEnumerable<Axiom> axioms) => string.Join(",", axioms.Select(a => declarations.IndexOf(a)));
    var owners = new List<string>();
    foreach (var declaration in declarations)
    {
      if (declaration is Function function)
      {
        var definition = function.DefinitionAxiom == null ? -1 : declarations.IndexOf(function.DefinitionAxiom);
        owners.Add(function.Name + ": definition " + definition + ", uses " + Positions(function.OtherDefinitionAxioms));
      }
      else if (declaration is Constant constant)
      {
        owners.Add(constant.Name + ": uses " + Positions(constant.DefinitionAxioms));
      }
    }
    return owners;
  }

  private static async Task<string> ProverLogs(Program program, CommandLineOptions options)
  {
    var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(directory);
    options.ProverLogFilePath = Path.Combine(directory, "@PROC@.smt2");
    options.ProverOptions.Add("SOLVER=noop");
    using (var engine = ExecutionEngine.CreateWithoutSharedCache(options))
    {
      await engine.ProcessProgram(TextWriter.Null, program, "1");
    }
    var logs = Directory.GetFiles(directory).OrderBy(f => f, StringComparer.Ordinal).ToList();
    Assert.IsNotEmpty(logs);
    return string.Join("\n", logs.Select(f => Path.GetFileName(f) + "\n" + File.ReadAllText(f)));
  }

  [Test]
  public void OptionIsParsed()
  {
    Assert.IsFalse(CommandLineOptions.FromArguments(TextWriter.Null).PrintDefinitionAxiomsInPlace);
    Assert.IsTrue(CommandLineOptions.FromArguments(TextWriter.Null, "/printDefinitionAxiomsInPlace").PrintDefinitionAxiomsInPlace);
  }

  [Test]
  public void HideableAxiomsArePrintedHideable()
  {
    var options = Options(false);
    var text = Print(InMemoryProgram(), options);
    Assert.AreEqual(2, Regex.Matches(text, "hideable axiom").Count, text);
    var reparsed = Parse(text, options);
    var hideable = reparsed.TopLevelDeclarations.OfType<Axiom>().Where(a => a.CanHide).Select(a => a.Expr.ToString());
    CollectionAssert.AreEquivalent(new[] {
      "(forall x: int :: { G(x): int } G(x): int == 2 * x)",
      "(forall x: int :: { F(x) } F(x) == x + 1)"
    }, hideable, text);
  }

  [Test]
  public void InPlacePrintingParsesIntoTheSameDeclarations()
  {
    var options = Options(true);
    var original = InMemoryProgram();
    var text = Print(original, options);
    Assert.IsFalse(text.Contains("uses"), text);
    var reparsed = Parse(text, options);
    CollectionAssert.AreEqual(original.TopLevelDeclarations.Select(Describe).ToList(),
      reparsed.TopLevelDeclarations.Select(Describe).ToList(), text);
    CollectionAssert.AreEqual(Owners(original), Owners(reparsed), text);
    Assert.AreEqual(text, Print(reparsed, options));
  }

  [Test]
  public void DefaultPrintingCopiesASharedAxiom()
  {
    var options = Options(false);
    var original = InMemoryProgram();
    var reparsed = Parse(Print(original, options), options);
    Assert.AreEqual(original.TopLevelDeclarations.OfType<Axiom>().Count() + 1,
      reparsed.TopLevelDeclarations.OfType<Axiom>().Count());
  }

  [Test]
  public async Task InPlacePrintingSendsTheSolverTheSameQueries()
  {
    var inMemory = await ProverLogs(InMemoryProgram(), Options(true));
    var printed = Print(InMemoryProgram(), Options(true));
    Assert.AreEqual(0, Parser.Parse(printed, "print.bpl", out var reparsed));
    Assert.AreEqual(inMemory, await ProverLogs(reparsed, Options(true)), printed);

    // The control: printed with uses clauses, the shared axiom comes back twice, and the
    // definition axioms move.
    var printedWithUses = Print(InMemoryProgram(), Options(false));
    Assert.AreEqual(0, Parser.Parse(printedWithUses, "print.bpl", out var reparsedWithUses));
    Assert.AreNotEqual(inMemory, await ProverLogs(reparsedWithUses, Options(false)), printedWithUses);
  }

  [Test]
  public void UndeclaredOwnerIsAnError()
  {
    var text = @"
function f(x: int): int;
axiom {:used_by_function ""g""} f(0) == 0;
axiom {:used_by_constant ""f""} f(1) == 1;
axiom {:definition_of_function ""f""} f(2) == 2;
axiom {:definition_of_function ""f""} f(3) == 3;
";
    Assert.AreEqual(0, Parser.Parse(text, "owners.bpl", out var program));
    Assert.AreEqual(3, program.Resolve(Options(true)));
  }
}
