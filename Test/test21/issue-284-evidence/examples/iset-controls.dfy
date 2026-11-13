// Negative control, not an observed old-version exploit; not run here.
// Expected: equality verifies, assert false fails.
lemma isetNegative() {
  var s: iset<int> := iset { 0, 1, 2 };
  var t: iset<int> := iset { 2, 1, 0 };
  assert s == t;
  assert false;
}
