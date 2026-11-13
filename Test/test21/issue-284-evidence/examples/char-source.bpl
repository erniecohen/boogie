// Source: satisfiable; the assertion must fail.
// Pinned :a target theory: logically unsatisfiable.
// No observed default Boogie/Dafny verdict is claimed.
type Char;
type Ty;
function valid(n: int): bool { 0 <= n && n < 65536 }
function fromInt(int): Char;
function toInt(Char): int;
axiom (forall n: int :: valid(n) ==> toInt(fromInt(n)) == n);
axiom (forall c: Char :: fromInt(toInt(c)) == c && valid(toInt(c)));
function ty(int): Ty;
function inv(Ty): int;
axiom (forall n: int :: inv(ty(n)) == n);
procedure Main() { assert false; }
