// RUN: %parallel-boogie /typeEncoding:m /monomorphicSharedLiterals:0 /errorTrace:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %parallel-boogie /typeEncoding:m /monomorphicSharedLiterals:1 /errorTrace:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %parallel-boogie /typeEncoding:a /monomorphicSharedLiterals:0 /errorTrace:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %parallel-boogie /typeEncoding:a /monomorphicSharedLiterals:1 /errorTrace:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %parallel-boogie /typeEncoding:p /monomorphicSharedLiterals:0 /errorTrace:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %parallel-boogie /typeEncoding:p /monomorphicSharedLiterals:1 /errorTrace:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// CLI controls for https://github.com/erniecohen/dafny/issues/281 .
// Object sharing is checked separately through the programmatic interface:
// textual serialization does not preserve expression-object identity.
function Id<T>(x: T): T;
axiom (forall<T> x: T :: {Id(x)} Id(x) == x);

procedure Good(b: bool) {
  var x: bool;
  if (b) { x := true; } else { x := false; }
  assert Id(x) == x;
  assert Id(42) == 42;
}

procedure Bad(b: bool) {
  var x: bool;
  if (b) { x := true; } else { x := false; }
  assert Id(x) == x;
  assert false;
}
