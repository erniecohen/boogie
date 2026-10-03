using System.Globalization;
using Microsoft.BaseTypes;
using Microsoft.Boogie;
using NUnit.Framework;

namespace CoreTests;

[TestFixture]
public class DeterministicLiteralHashTests
{
  [TestCase("0", 84696351)]
  [TestCase("-1", 2047574606)]
  [TestCase("2147483648", 282303375)]
  [TestCase("-2147483649", -889659574)]
  [TestCase("18446744073709551616", -941260692)]
  public void IntegerHashIsProcessIndependent(string text, int expected)
  {
    var value = BigNum.FromString(text);
    var literal = new LiteralExpr(Token.NoToken, value);
    Assert.AreEqual(expected, literal.GetContentHash(true));
    Assert.AreEqual(value.GetHashCode(), literal.ContentHash);
    Assert.AreEqual(literal.ContentHash, literal.GetContentHash(false));
  }

  [TestCase("RNE", "roundNearestTiesToEven")]
  [TestCase("RNA", "roundNearestTiesToAway")]
  [TestCase("RTP", "roundTowardPositive")]
  [TestCase("RTN", "roundTowardNegative")]
  [TestCase("RTZ", "roundTowardZero")]
  public void RoundingModeAliasesHaveDeterministicHashes(string shortName, string longName)
  {
    var literal = new LiteralExpr(Token.NoToken, RoundingMode.FromString(shortName));
    var alias = new LiteralExpr(Token.NoToken, RoundingMode.FromString(longName));
    Assert.AreEqual(shortName.GetDeterministicHashCode(), literal.GetContentHash(true));
    Assert.AreEqual(literal.GetContentHash(true), alias.GetContentHash(true));
    Assert.AreEqual(literal.Val.GetHashCode(), literal.ContentHash);
    Assert.AreEqual(literal.ContentHash, literal.GetContentHash(false));
  }

  [Test]
  public void NumericRepresentationsAndCulture()
  {
    var a = new LiteralExpr(Token.NoToken, BigDec.FromString("12345678901234567890.1250"));
    var b = new LiteralExpr(Token.NoToken, BigDec.FromString("12345678901234567890.125"));
    Assert.AreEqual(a.GetContentHash(true), b.GetContentHash(true));
    var integer = BigNum.FromString("18446744073709551616");
    var wide = new LiteralExpr(Token.NoToken, integer, 128);
    Assert.AreNotEqual(wide.GetContentHash(true), new LiteralExpr(Token.NoToken, integer, 256).GetContentHash(true));
    var positiveZero = new LiteralExpr(Token.NoToken, BigFloat.FromString("0x0.0e0f53e11"));
    var negativeZero = new LiteralExpr(Token.NoToken, BigFloat.FromString("-0x0.0e0f53e11"));
    Assert.AreNotEqual(positiveZero.GetContentHash(true), negativeZero.GetContentHash(true));
    var values = new[] {a, wide, positiveZero, negativeZero,
      new LiteralExpr(Token.NoToken, BigFloat.FromString("0x1.123456789abcde0f53e11")),
      new LiteralExpr(Token.NoToken, BigFloat.FromString("0+oo53e11")),
      new LiteralExpr(Token.NoToken, BigFloat.FromString("0NaN53e11"))};
    var culture = CultureInfo.CurrentCulture;
    try {
      foreach (var value in values) {
        var hash = value.GetContentHash(true);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
        Assert.AreEqual(hash, value.GetContentHash(true));
        Assert.AreEqual(value.ContentHash, value.GetContentHash(false));
        CultureInfo.CurrentCulture = culture;
      }
    } finally {
      CultureInfo.CurrentCulture = culture;
    }
  }
}
