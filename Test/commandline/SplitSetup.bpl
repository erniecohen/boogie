// RUN: %boogie /typeEncoding:a /proverOpt:SOLVER=noop /proverLog:"%t.base.smt2" "%s" > "%t.base"
// RUN: %boogie /typeEncoding:a /proverOpt:SOLVER=noop /proverLog:"%t.opt.smt2" /cacheUsedTypes /lockFreeExpansion "%s" > "%t.opt"
// RUN: cmp "%t.base.smt2" "%t.opt.smt2"
// RUN: %boogie /typeEncoding:p /prune:0 /vcsSplitOnEveryAssert /proverOpt:SOLVER=noop /proverLog:"%t.base.smt2" "%s" > "%t.base"
// RUN: %boogie /typeEncoding:p /prune:0 /vcsSplitOnEveryAssert /proverOpt:SOLVER=noop /proverLog:"%t.opt.smt2" /cacheUsedTypes /lockFreeExpansion "%s" > "%t.opt"
// RUN: cmp "%t.base.smt2" "%t.opt.smt2"
// RUN: %boogie /typeEncoding:a /vcsSplitOnEveryAssert "%s" > "%t"
// RUN: %boogie /typeEncoding:a /vcsSplitOnEveryAssert /cacheUsedTypes /lockFreeExpansion "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// /cacheUsedTypes and /lockFreeExpansion change how the prover's context is set up for each
// split, and not a byte of what the solver gets: cmp compares the whole command stream of one
// prover, every (reset) and background included.  Verified with one prover, split on every
// assertion, the procedures are three contexts, each reset between its splits: the background
// sent after such a reset was the first context's, which does not declare h, and Z3 stopped
// with "unknown constant".
type T;
type Box a;
function f(x: int): real;
function g<a>(x: a): Box a;
function {:inline} twice(x: int): int { x + x }
function {:inline} pick(b: bool, x: real, y: real): real { if b then x else y }
function h(x: int): int uses {
  hideable axiom (forall x: int :: {h(x)} h(x) == twice(x) + 1);
}
axiom (forall x: int :: {f(x)} f(x) == real(x));
axiom (forall<a> x: a :: {g(x)} g(x) == g(x));
const c: T;
var m: [int]bool;

procedure P(x: int) returns (r: int)
  ensures r == twice(x);
{
  r := twice(x);
  assert r == 2 * x;
  assert twice(r) == 4 * x;
}

procedure {:random_seed 3} Q(b: bool) returns (y: real)
  ensures y == pick(b, 1.0, 2.0);
{
  hide *;
  if (b) { y := 1.0; } else { y := 2.0; }
  assert h(1) == 3;
  reveal h;
  assert h(1) == 3;
}

procedure {:rlimit 100000} R(s: string) modifies m;
{
  var k: int;
  m[twice(k)] := true;
  assert m[2 * k];
  assert g(c) == g(c);
  assert s == s;
}
