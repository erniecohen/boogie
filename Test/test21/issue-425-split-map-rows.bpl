// https://github.com/erniecohen/dafny/issues/425
// RUN: %boogie /typeEncoding:a /prune:0 /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %boogie /typeEncoding:a /legacyNoncanonicalMapPayloads:1 /prune:0 /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.legacy.expect" "%t"

// The same-index row of a map whose result type is a skeleton parameter is emitted as one
// row per native sort and one row for the other result types.  Stores through such a map
// keep their read-over-write law at every native sort and at a non-native one.  A payload of
// a procedure's type parameter is not known to be canonical, so Generic is proved only with
// the legacy row, before and after the split.
type Key;
type Value;
function EncodingAnchor<T>(x: T): T;

procedure Boolean(m: <A>[Key]A, k: Key, v: bool) {
  assert m[k := v][k] == v;
}
procedure Integer(m: <A>[Key]A, k: Key, v: int) {
  assert m[k := v][k] == v;
}
procedure Real(m: <A>[Key]A, k: Key, v: real) {
  assert m[k := v][k] == v;
}
procedure Bits(m: <A>[Key]A, k: Key, v: bv8) {
  assert m[k := v][k] == v;
}
procedure NonNative(m: <A>[Key]A, k: Key, v: Value) {
  assert m[k := v][k] == v;
}
procedure Monomorphic(m: [Key]bool, n: [Key]Value, k: Key, v: bool, w: Value) {
  assert m[k := v][k] == v;
  assert n[k := w][k] == w;
}
procedure OtherIndex(m: <A>[Key]A, k: Key, j: Key, v: bool) {
  var w: bool;
  w := m[j];
  assert k != j ==> m[k := v][j] == w;
}
procedure Generic<T>(m: [Key]T, k: Key, v: T) {
  assert m[k := v][k] == v;
}
procedure Stores(m: <A>[Key]A, k: Key, j: Key, v: bool, w: int) {
  assert m[k := v][j := w][k] == v || k == j;
}
procedure Negative(m: <A>[Key]A, k: Key) {
  assert m[k := true][k];
  assert false;
}
