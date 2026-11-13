// RUN: %boogie /vcsCores:1 /timeLimit:0 "%s" > "%t.default"
// RUN: %boogie /vcsCores:1 /timeLimit:0 /legacySolverIdleTimeout:1 "%s" > "%t.legacy"
// RUN: cmp "%t.default" "%t.legacy"
// RUN: %diff "%s.expect" "%t.default"

procedure Healthy(x: int) {
  assert x + 1 > x;
}

procedure NegativeControl() {
  assert false;
}
