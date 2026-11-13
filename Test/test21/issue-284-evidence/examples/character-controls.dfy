// Proposed-repair controls; not run in this research task.
// Four positive lemmas should verify; charNegative must fail at assert false.
type AnyChar = c: char | true witness '\0'
datatype Cell = Cell(c: char)

lemma charRange(c: char)
  ensures 0 <= c as int < 0x110000
{}

lemma subsetCharRange(c: AnyChar)
  ensures 0 <= c as int < 0x110000
{}

lemma forallCharRange()
  ensures forall c: char :: 0 <= c as int < 0x110000
{}

lemma fieldCharRange(x: Cell)
  ensures 0 <= x.c as int < 0x110000
{}

lemma charNegative(c: char) {
  assert 0 <= c as int < 0x110000;
  assert false;
}
