// RUN: %boogie -noVerify -env:0 -print:"%t.print.bpl" "%s" > "%t"
// RUN: %boogie -noVerify -env:0 -print:"%t.reprint.bpl" "%t.print.bpl" >> "%t"
// RUN: %OutputCheck "%s" --file-to-check="%t.print.bpl"
// RUN: %diff "%t.print.bpl" "%t.reprint.bpl"

// The printer writes the attributes of an if command, after "if" and after "else if", and those
// of an assignment, after ":=", where the parser reads them.  It used to drop them.  A front end
// marks the jumps at which it allows path isolation with {:allow_path_isolation}, which
// {:isolate "paths"} splits at, and gives assignments an {:id}.

procedure P(x: int) returns (r: int)
{
  r := {:id "a0"} 0;
  if {:allow_path_isolation} (x < 0) {
    r := {:id "a1"} 0 - x;
  } else if {:allow_path_isolation} (x == 0) {
    r := 1;
  } else {
    r := x;
  }
  assert {:isolate "paths"} r >= 0;
}

// CHECK-L: r := {:id "a0"} 0;
// CHECK-L: if {:allow_path_isolation} (x < 0)
// CHECK-L: r := {:id "a1"} 0 - x;
// CHECK-L: else if {:allow_path_isolation} (x == 0)
// CHECK-L: r := 1;
