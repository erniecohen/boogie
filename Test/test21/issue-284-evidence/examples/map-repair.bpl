// Standalone TARGET schema with fixed map type arguments suppressed.
// This replaces a Boogie-generated row; it is not a prelude-only edit.
// It has an infinite-carrier local model. The final assertion is false.
type U;
function e(bool): U;
function p(U): bool;
function s(U,U): U;
function w(U,U,U): U;
const unique a, b, c: U;
axiom (forall b: bool :: p(e(b)) == b);
axiom (forall m, x, v: U ::
  e(p(v)) == v ==> s(w(m,x,v),x) == v);
axiom (forall m, x, y, v: U ::
  x != y ==> s(w(m,x,v),y) == s(m,y));
axiom (forall m, n: U ::
  (forall x: U :: p(s(m,x)) == p(s(n,x))) ==> m == n);
procedure Negative() { assert false; }
