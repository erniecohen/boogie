using System.Linq;
using Microsoft.Boogie;
using NUnit.Framework;

namespace CoreTests;

/// <summary>
/// Printing an expression and parsing the printed text must give back the same tree.  The
/// parser builds a chain of +, -, *, div, mod, /, ++, &amp;&amp;, || or &lt;==&gt; to the left, and a
/// chain of ==&gt; or ** to the right, so the printer must keep the parentheses of an operand of
/// the same binding power on the other side.
/// </summary>
[TestFixture]
public class PrintAssociativity
{
  private static Expr ParseExpr(string text)
  {
    var errors = Parser.Parse($"axiom {text};", "PrintAssociativity.bpl", out var program);
    Assert.AreEqual(0, errors, text);
    return program.TopLevelDeclarations.OfType<Axiom>().Single().Expr;
  }

  // Left-associative operators whose right operand of the same binding power lost its parentheses.
  [TestCase("a + (b + c)", "a + (b + c)")]
  [TestCase("(a + b) + c", "a + b + c")]
  [TestCase("a + (b - c)", "a + (b - c)")]
  [TestCase("a * (b * c)", "a * (b * c)")]
  [TestCase("(a * b) * c", "a * b * c")]
  [TestCase("a * (b div c)", "a * (b div c)")]
  [TestCase("a * (b mod c)", "a * (b mod c)")]
  [TestCase("a * (b / c)", "a * (b / c)")]
  [TestCase("p && (q && r)", "p && (q && r)")]
  [TestCase("(p && q) && r", "p && q && r")]
  [TestCase("p || (q || r)", "p || (q || r)")]
  [TestCase("(p || q) || r", "p || q || r")]
  [TestCase("p <==> (q <==> r)", "p <==> (q <==> r)")]
  [TestCase("(p <==> q) <==> r", "p <==> q <==> r")]
  // ** is right-associative: its left operand lost its parentheses.
  [TestCase("(a ** b) ** c", "(a ** b) ** c")]
  [TestCase("a ** (b ** c)", "a ** (b ** c)")]
  // Operators that were already printed correctly.
  [TestCase("a - (b - c)", "a - (b - c)")]
  [TestCase("(a - b) - c", "a - b - c")]
  [TestCase("a - (b + c)", "a - (b + c)")]
  [TestCase("(a - b) + c", "a - b + c")]
  [TestCase("a div (b div c)", "a div (b div c)")]
  [TestCase("(a div b) div c", "a div b div c")]
  [TestCase("a mod (b * c)", "a mod (b * c)")]
  [TestCase("(a mod b) * c", "a mod b * c")]
  [TestCase("a / (b / c)", "a / (b / c)")]
  [TestCase("(a / b) * c", "a / b * c")]
  [TestCase("u ++ (v ++ w)", "u ++ (v ++ w)")]
  [TestCase("(u ++ v) ++ w", "u ++ v ++ w")]
  [TestCase("p ==> (q ==> r)", "p ==> q ==> r")]
  [TestCase("(p ==> q) ==> r", "(p ==> q) ==> r")]
  [TestCase("p <== q <== r", "r ==> q ==> p")]
  [TestCase("(a == b) == c", "(a == b) == c")]
  [TestCase("p && (q || r)", "p && (q || r)")]
  [TestCase("(p || q) && r", "(p || q) && r")]
  public void PrintThenParseGivesTheSameTree(string source, string printed)
  {
    var expr = ParseExpr(source);
    Assert.AreEqual(printed, expr.ToString());
    var reparsed = ParseExpr(expr.ToString());
    Assert.IsTrue(expr.Equals(reparsed), $"{source} printed as {expr} parses to a different tree");
    Assert.AreEqual(printed, reparsed.ToString());
  }
}
