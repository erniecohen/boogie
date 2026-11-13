// Source countermodel: S is a singleton; Other is the integers.
// The erased axioms remain consistent, but the erased assertion is valid.
type S;
type Other;
function embed(int): Other;
function project(Other): int;
axiom (forall n: int :: project(embed(n)) == n);
procedure LostCountermodel() {
  assert (exists x, y: S :: x != y);
}
