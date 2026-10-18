// RUN: %boogie -prune:1 -errorTrace:0 "%s" > "%t"
// RUN: %boogie -noVerify -env:0 -print:"%t.print.bpl" "%s" > "%t.noVerify"
// RUN: cat "%t.print.bpl" >> "%t"
// RUN: %boogie -prune:1 -errorTrace:0 "%t.print.bpl" >> "%t"
// RUN: %OutputCheck "%s" --file-to-check="%t"

// The printer writes "hideable" before a hideable axiom, at the top level and in a uses
// clause, so that "hide" prunes the axiom in the printed program as it does in the original.
// It used to drop "hideable": the printed program then verified all three procedures, and the
// original only one.

function F(x: int): int;
hideable axiom (forall x: int :: {F(x)} F(x) == x + 1);

function G(x: int): int uses {
  hideable axiom (forall x: int :: {G(x)} G(x) == x - 1);
}

procedure HiddenF(y: int)
{
  hide *;
  assert F(y) == y + 1;  // error: the definition of F is hidden
}

procedure HiddenG(y: int)
{
  hide *;
  assert G(y) == y - 1;  // error: the definition of G is hidden
}

procedure Revealed(y: int)
{
  hide *;
  reveal F;
  reveal G;
  assert F(y) == y + 1 && G(y) == y - 1;
}

// CHECK-L: Boogie program verifier finished with 1 verified, 2 errors
// CHECK-L: hideable axiom (forall x: int :: { F(x) } F(x) == x + 1);
// CHECK-L: uses {
// CHECK-L: hideable axiom (forall x: int :: { G(x) } G(x) == x - 1);
// CHECK-L: Boogie program verifier finished with 1 verified, 2 errors
