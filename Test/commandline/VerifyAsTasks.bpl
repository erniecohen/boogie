// RUN: %boogie -errorTrace:0 "%s" > "%t"
// RUN: %boogie -errorTrace:0 -verifyAsTasks "%s" >> "%t"
// RUN: %OutputCheck "%s" --file-to-check="%t"

// /verifyAsTasks verifies through the verification tasks that a front end runs, one task per
// split, and reports per implementation, as the command line does.
procedure Ok(x: int) { assert x == x; }
procedure Fails(x: int) {
  assert {:split_here} x == x;
  assert x > 0;
}

// CHECK-L: VerifyAsTasks.bpl(10,3): Error: this assertion could not be proved
// CHECK-L: Boogie program verifier finished with 1 verified, 1 error
// CHECK-L: VerifyAsTasks.bpl(10,3): Error: this assertion could not be proved
// CHECK-L: Boogie program verifier finished with 1 verified, 1 error
