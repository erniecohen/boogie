using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Boogie;

/// <summary>
/// A definition axiom belongs to the functions and constants that hold it: as a function's
/// DefinitionAxiom, in a function's OtherDefinitionAxioms, or in a constant's DefinitionAxioms.
/// Pruning follows an edge from each owner to the axiom.  One axiom may have several owners, and
/// it is a top-level declaration at its own position, which need not be next to an owner.
///
/// The default printer prints an owned axiom in the "uses" clause of each owner, and not at its
/// own position.  The parser makes a new axiom for each "uses" clause and puts it just before
/// the owner, so an axiom with two owners comes back as two axioms, and the order of the
/// declarations changes.
///
/// With /printDefinitionAxiomsInPlace, the printer prints each declaration once, in order, with
/// no "uses" clauses, and an owned axiom names its owners in attributes:
/// {:definition_of_function "f"} if the axiom is f's DefinitionAxiom;
/// {:used_by_function "f", "g"} if it is in the OtherDefinitionAxioms of f and g;
/// {:used_by_constant "c", "d"} if it is in the DefinitionAxioms of c and d.
/// Resolving the axiom attaches it to each owner that these attributes name, and removes the
/// attributes, so the printed program parses and resolves into the same declarations, in the
/// same order, with the same attributes and the same owners.
/// </summary>
public static class DefinitionAxiomOwners
{
  public const string DefinitionOfFunction = "definition_of_function";
  public const string UsedByFunction = "used_by_function";
  public const string UsedByConstant = "used_by_constant";

  private static bool IsOwnerAttribute(QKeyValue kv) =>
    kv.Key is DefinitionOfFunction or UsedByFunction or UsedByConstant;

  /// <summary>
  /// Prints the declarations in their order, each axiom at its own position, with attributes
  /// that name its owners among the declarations.  An owned axiom that is not one of the
  /// declarations is not printed: only the declarations reach the solver.
  /// </summary>
  public static void EmitInPlace(TokenTextWriter stream, IEnumerable<Declaration> declarations)
  {
    var decls = declarations.Where(d => d != null).ToList();
    var axioms = decls.OfType<Axiom>().ToHashSet();
    var definitionOf = new Dictionary<Axiom, List<object>>();
    var usedByFunction = new Dictionary<Axiom, List<object>>();
    var usedByConstant = new Dictionary<Axiom, List<object>>();

    void Add(Dictionary<Axiom, List<object>> owners, Axiom axiom, NamedDeclaration owner)
    {
      if (!axioms.Contains(axiom))
      {
        return;
      }
      var names = owners.GetOrCreate(axiom, () => new List<object>());
      var name = PrintedName(stream, owner);
      if (!names.Contains(name))
      {
        names.Add(name);
      }
    }

    foreach (var declaration in decls)
    {
      switch (declaration)
      {
        case Function function:
          if (function.DefinitionAxiom != null)
          {
            Add(definitionOf, function.DefinitionAxiom, function);
          }
          foreach (var axiom in function.OtherDefinitionAxioms)
          {
            Add(usedByFunction, axiom, function);
          }
          break;
        case Constant constant:
          foreach (var axiom in constant.DefinitionAxioms)
          {
            Add(usedByConstant, axiom, constant);
          }
          break;
      }
    }

    var first = true;
    foreach (var declaration in decls)
    {
      if (!first)
      {
        stream.WriteLine();
      }
      first = false;

      if (declaration is Axiom axiom)
      {
        var owners = Prepend(axiom, UsedByConstant, usedByConstant, null);
        owners = Prepend(axiom, UsedByFunction, usedByFunction, owners);
        owners = Prepend(axiom, DefinitionOfFunction, definitionOf, owners);
        axiom.Emit(stream, 0, owners);
      }
      else
      {
        declaration.Emit(stream, 0);
      }
    }
  }

  private static QKeyValue Prepend(Axiom axiom, string key, Dictionary<Axiom, List<object>> owners, QKeyValue next)
  {
    return owners.TryGetValue(axiom, out var names) ? new QKeyValue(axiom.tok, key, names, next) : next;
  }

  /// <summary>
  /// The name that the parser gives the owner when it reads the owner's printed declaration.
  /// </summary>
  private static string PrintedName(TokenTextWriter stream, NamedDeclaration owner)
  {
    var name = TokenTextWriter.SanitizeIdentifier(owner.Name);
    if (name.StartsWith("\\"))
    {
      // The parser drops the backslash that the printer puts before a keyword.
      name = name.Substring(1);
    }
    return stream.Options.PrintWithUniqueASTIds ? $"h{owner.GetHashCode()}^^{name}" : name;
  }

  /// <summary>
  /// Attaches the axiom to each owner that its owner attributes name, and removes those
  /// attributes.  Reports an owner that is not declared.
  /// </summary>
  public static void Attach(Axiom axiom, ResolutionContext rc)
  {
    if (QKeyValue.FindAttribute(axiom.Attributes, IsOwnerAttribute) == null)
    {
      return;
    }

    var remaining = new List<QKeyValue>();
    for (var kv = axiom.Attributes; kv != null; kv = kv.Next)
    {
      if (!IsOwnerAttribute(kv))
      {
        remaining.Add(kv);
        continue;
      }

      foreach (var parameter in kv.Params)
      {
        if (parameter is not string name)
        {
          rc.Error(kv, "attribute :{0} expects the names of its owners as strings", kv.Key);
          continue;
        }

        if (kv.Key == UsedByConstant)
        {
          if (rc.LookUpVariable(name) is not Constant constant)
          {
            rc.Error(kv, "undeclared constant in attribute :{0}: {1}", kv.Key, name);
          }
          else if (!constant.DefinitionAxioms.Contains(axiom))
          {
            // A new list: the parser gives the constants of one declaration the same list.
            constant.DefinitionAxioms = constant.DefinitionAxioms.Append(axiom).ToList();
          }
          continue;
        }

        var function = rc.LookUpFunction(name);
        if (function == null)
        {
          rc.Error(kv, "undeclared function in attribute :{0}: {1}", kv.Key, name);
        }
        else if (kv.Key == UsedByFunction)
        {
          if (!function.OtherDefinitionAxioms.Contains(axiom))
          {
            function.OtherDefinitionAxioms.Add(axiom);
          }
        }
        else if (function.DefinitionAxiom != null && function.DefinitionAxiom != axiom)
        {
          rc.Error(kv, "function {0} already has a definition axiom", name);
        }
        else
        {
          function.DefinitionAxiom = axiom;
        }
      }
    }

    QKeyValue attributes = null;
    for (var i = remaining.Count - 1; i >= 0; i--)
    {
      attributes = new QKeyValue(remaining[i].tok, remaining[i].Key, remaining[i].Params, attributes);
    }
    axiom.Attributes = attributes;
  }
}
