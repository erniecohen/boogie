// Many-sorted semantics: consistent. The assertion must fail.
// Pinned :a logical encoding: inconsistent (regardless of trigger scheduling).
// This file is a Boogie reproducer of the prelude family, not Dafny output.
type Box;
type ISet = [Box]bool;
const unique a, b, c: Box;
axiom (forall m, n: ISet ::
  (forall x: Box :: m[x] == n[x]) ==> m == n);
procedure Main() {
  assert false;
}
