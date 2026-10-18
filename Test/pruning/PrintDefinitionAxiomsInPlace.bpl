// RUN: %boogie -prune:1 -errorTrace:0 "%s" > "%t"
// RUN: %boogie -noVerify -env:0 -printDefinitionAxiomsInPlace -print:"%t.print.bpl" "%s" > "%t.noVerify"
// RUN: %boogie -noVerify -env:0 -printDefinitionAxiomsInPlace -print:"%t.reprint.bpl" "%t.print.bpl" >> "%t.noVerify"
// RUN: %diff "%t.print.bpl" "%t.reprint.bpl"
// RUN: cat "%t.print.bpl" >> "%t"
// RUN: %boogie -prune:1 -errorTrace:0 "%t.print.bpl" >> "%t"
// RUN: %boogie -prune:1 -errorTrace:0 -printDefinitionAxiomsInPlace -printSplit:"%t" -printSplitDeclarations "%s" > "%t.split"
// RUN: cat "%t-ConstantOnly--1.spl" >> "%t"
// RUN: %OutputCheck "%s" --file-to-check="%t"

// With /printDefinitionAxiomsInPlace, each axiom is printed once, at its own position, and an
// axiom that belongs to functions or constants names them in attributes, which resolution
// attaches it to.  The printed program has the same declarations, in the same order, with the
// same pruning edges, as the program printed.  The attributes also express what a uses clause
// cannot: one axiom with two owners, and a definition axiom that is not next to its owner.

type Ty;
function CtorId(d: Ty): int;
function A(): Ty;
const idA: int;
function B(n: int): Ty;
function F(x: int): int;

// A uses clause, and a function body, which defines the function by an axiom.
function G(x: int): int uses {
  axiom (forall x: int :: {G(x)} G(x) == 2 * x);
}
function H(x: int): int { x - 1 }

// One axiom that belongs to both the nullary constructor A and its identifier idA, as a front
// end builds it.  Printed with uses clauses, it would be printed, and parsed, twice.
axiom {:used_by_function "A"} {:used_by_constant "idA"} CtorId(A()) == idA;
axiom {:used_by_constant "idA"} idA == 0;
axiom (forall n: int :: {B(n)} CtorId(B(n)) == 1);
hideable axiom {:used_by_function "F"} (forall x: int :: {F(x)} F(x) == x + 1);

procedure Distinct()
{
  assert A() != B(0);
}

// The shared axiom is needed, and only idA leads to it.
procedure ConstantOnly()
{
  assert (exists d: Ty :: CtorId(d) == idA);
}

procedure UsesAndBody(y: int)
{
  assert G(y) == 2 * y && H(y) == y - 1;
}

procedure Hidden(y: int)
{
  hide *;
  assert F(y) == y + 1;  // error: the definition of F is hidden
}

procedure Revealed(y: int)
{
  hide *;
  reveal F;
  assert F(y) == y + 1;
}

// CHECK-L: Boogie program verifier finished with 4 verified, 1 error
// CHECK-L: const idA: int;
// CHECK-L: axiom {:used_by_function "G"} (forall x: int :: { G(x) } G(x) == 2 * x);
// CHECK-L: function G(x: int) : int;
// CHECK-L: function H(x: int) : int;
// CHECK-L: axiom {:definition_of_function "H"} (forall x: int :: { H(x): int } H(x): int == x - 1);
// CHECK-L: axiom {:used_by_function "A"} {:used_by_constant "idA"} CtorId(A()) == idA;
// CHECK-L: axiom {:used_by_constant "idA"} idA == 0;
// CHECK-L: hideable axiom {:used_by_function "F"} (forall x: int :: { F(x) } F(x) == x + 1);
// CHECK-L: Boogie program verifier finished with 4 verified, 1 error
// CHECK-L: axiom {:used_by_function "A"} {:used_by_constant "idA"} CtorId(A()) == idA;
// CHECK-L: axiom {:used_by_constant "idA"} idA == 0;
