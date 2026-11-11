// UNSUPPORTED: batch_mode
// RUN: %boogie /typeEncoding:a /prune:1 /vcsSplitOnEveryAssert /proverOpt:SOLVER=noop /proverLog:"%t.base.smt2" "%s" > "%t.base"
// RUN: %boogie /typeEncoding:a /prune:1 /vcsSplitOnEveryAssert /proverOpt:SOLVER=noop /proverLog:"%t.cached.smt2" /cacheBackground "%s" > "%t.cached"
// RUN: cmp "%t.base.smt2" "%t.cached.smt2"
// RUN: %boogie /typeEncoding:a /prune:1 /vcsSplitOnEveryAssert /trackVerificationCoverage /proverLog:"%t.base.smt2" "%s" > "%t"
// RUN: %boogie /typeEncoding:a /prune:1 /vcsSplitOnEveryAssert /trackVerificationCoverage /proverLog:"%t.cached.smt2" /cacheBackground "%s" > "%t.cached"
// RUN: cmp "%t.base.smt2" "%t.cached.smt2"
// RUN: %diff "%t" "%t.cached"
// RUN: %diff "%s.expect" "%t"

// /cacheBackground restarts a prover's context for a split with the same declarations as the one
// it holds, instead of setting it up again, and the solver gets the same commands: cmp compares
// the whole command stream of one prover, without and with Z3's answers, with the named assumes and
// unsat cores of /trackVerificationCoverage.  The assertions of each procedure use f and g, so its
// splits have the same declarations; Q has P's and an SMT option of its own, which its contexts
// must send.  The batch mode prover does not restart contexts and sets every one up, as without the
// option.
function {:inline} twice(x: int): int { x + x }
function g(x: int): int;
axiom {:id "gAx"} (forall x: int :: {g(x)} g(x) > x);
function f(x: int): real;
axiom {:id "fAx"} (forall x: int :: {f(x)} f(x) == real(x));

procedure P(x: int) returns (r: int)
{
  r := twice(x);
  assert {:id "p1"} r == 2 * x && g(r) > r - 1 && f(r) == f(r);
  assert {:id "p2"} g(r) > r && f(r) == f(r);
  assert {:id "p3"} f(r) == real(r) && g(r) == g(r);
}

procedure {:smt_option "smt.case_split", 2} Q(x: int) returns (r: int)
{
  r := twice(x);
  assert {:id "q1"} r == 2 * x && g(r) > r - 1 && f(r) == f(r);
  assert {:id "q2"} g(r) > r && f(r) == f(r);
  assert {:id "q3"} f(r) == real(r) && g(r) == g(r);
}

procedure T(x: int)
{
  assert {:id "t1"} g(x) > x && f(x) == f(x);
  assert {:id "t2"} x > 0 && f(x) == f(x) && g(x) == g(x);
}
