// https://github.com/erniecohen/dafny/issues/284
// RUN: %boogie /typeEncoding:a /proverOpt:O:smt.mbqi=true /proverOpt:O:smt.mbqi.max_iterations=10 /prune:0 /normalizeDeclarationOrder:0 %s > %t
// RUN: %OutputCheck %s < %t
// RUN: %boogie /typeEncoding:a /proverOpt:O:smt.mbqi=true /proverOpt:O:smt.mbqi.max_iterations=10 /legacyNoncanonicalMapPayloads:1 /prune:0 /normalizeDeclarationOrder:0 %s > %t
// RUN: %OutputCheck %s < %t
// CHECK: Boogie program verifier finished with 6 verified, 1 error

type Key;
type Value;
procedure Boolean(m: [Key]bool, k: Key, v: bool) {
  assert m[k := v][k] == v;
}
procedure Integer(m: [Key]int, k: Key, v: int) {
  assert m[k := v][k] == v;
}
procedure Real(m: [Key]real, k: Key, v: real) {
  assert m[k := v][k] == v;
}
procedure Bits(m: [Key]bv8, k: Key, v: bv8) {
  assert m[k := v][k] == v;
}
procedure NonNative(m: [Key]Value, k: Key, v: Value) {
  assert m[k := v][k] == v;
}
procedure Polymorphic<T>(m: <A>[Key]A, k: Key, v: T) {
  assert m[k := v][k] == v;
}
procedure Negative(m: [Key]bool, k: Key) {
  assert m[k := true][k];
  assert false;
}
