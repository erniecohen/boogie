// Semantic expectation: Read verifies; Negative fails.
// Example for the proposed certificate condition; checker not implemented.
type Item;
const missing: Item;
function at(int): Item;
axiom (forall i: int :: at(i) != missing);

procedure Read(i: int) returns (v: Item)
  ensures v != missing;
{
  v := at(i);
}

procedure Negative(i: int) {
  assert at(i) == missing;
}
