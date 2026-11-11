using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Boogie;

/// <summary>
/// The basic types that a split's type encoding declares casts for.  <see cref="Checker.Target"/>
/// collects them with a <see cref="BasicTypeVisitor"/> over the whole program, entering no
/// implementation but the split's, once for every split.  Under /cacheUsedTypes they come
/// instead from a cache of each declaration's basic types, in the order the visitor meets them
/// first, and only the split's implementation is visited again.
///
/// The result is the same list.  The visitor visits the top-level declarations in order and
/// adds each basic type it meets to a set, whose list of types is in the order of their first
/// additions.  A type first met in declaration d is met there at its first position in d's own
/// visit, so adding each declaration's own list of types, in order, makes the same first
/// additions in the same order, with the same objects.
/// </summary>
public static class UsedTypes
{
  private static readonly ConditionalWeakTable<Declaration, List<Type>> cache = new();

  public static List<Type> Collect(Program program, Implementation implementation, bool cached)
  {
    bool EnterNode(Absy node) => node is not Implementation other || other == implementation;

    if (!cached) {
      return new BasicTypeVisitor(program, EnterNode).GetBasicTypes().ToList();
    }

    var types = new HashSet<Type>();
    foreach (var declaration in program.TopLevelDeclarations.ToList()) {
      if (declaration is Implementation) {
        if (declaration == implementation) {
          types.UnionWith(new BasicTypeVisitor(declaration, EnterNode).GetBasicTypes());
        }
        continue;
      }
      // A declaration other than an implementation contains no implementation, so the visitor
      // enters every node of it.  The callbacks are static, so a lookup allocates nothing.
      foreach (var type in cache.GetValue(declaration, visitDeclaration)) {
        types.Add(type);
      }
    }
    return types.ToList();
  }

  private static readonly Func<Absy, bool> enterAll = _ => true;

  private static readonly ConditionalWeakTable<Declaration, List<Type>>.CreateValueCallback visitDeclaration =
    declaration => new BasicTypeVisitor(declaration, enterAll).GetBasicTypes().ToList();
}
