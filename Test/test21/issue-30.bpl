// Numeric declaration hashes must be stable in fresh Boogie processes.
// Compare all commands through check-sat; the helper validates the response/cleanup suffix.
// RUN: %boogie /deterministicLiteralHashes:1 /normalizeNames:1 /emitDebugInformation:0 /prune:0 /proverLog:%t.0.smt2 %s > %t.0.out
// RUN: %diff %s.expect %t.0.out
// RUN: %python %S/issue-30-query.py %t.0.smt2 > %t.0.query
// RUN: %boogie /deterministicLiteralHashes:1 /normalizeNames:1 /emitDebugInformation:0 /prune:0 /proverLog:%t.1.smt2 %s > %t.1.out
// RUN: %diff %s.expect %t.1.out
// RUN: %python %S/issue-30-query.py %t.1.smt2 > %t.1.query
// RUN: %boogie /deterministicLiteralHashes:1 /normalizeNames:1 /emitDebugInformation:0 /prune:0 /proverLog:%t.2.smt2 %s > %t.2.out
// RUN: %diff %s.expect %t.2.out
// RUN: %python %S/issue-30-query.py %t.2.smt2 > %t.2.query
// RUN: %boogie /deterministicLiteralHashes:1 /normalizeNames:1 /emitDebugInformation:0 /prune:0 /proverLog:%t.3.smt2 %s > %t.3.out
// RUN: %diff %s.expect %t.3.out
// RUN: %python %S/issue-30-query.py %t.3.smt2 > %t.3.query
// RUN: %diff %t.0.query %t.1.query
// RUN: %diff %t.0.query %t.2.query
// RUN: %diff %t.0.query %t.3.query
// RUN: %boogie /normalizeDeclarationOrder:0 /deterministicLiteralHashes:0 /emitDebugInformation:0 /prune:0 /proverLog:%t.off0.smt2 %s > %t.off0.out
// RUN: %diff %s.expect %t.off0.out
// RUN: %python %S/issue-30-query.py %t.off0.smt2 > %t.off0.query
// RUN: %boogie /normalizeDeclarationOrder:0 /deterministicLiteralHashes:1 /emitDebugInformation:0 /prune:0 /proverLog:%t.off1.smt2 %s > %t.off1.out
// RUN: %diff %s.expect %t.off1.out
// RUN: %python %S/issue-30-query.py %t.off1.smt2 > %t.off1.query
// RUN: %diff %t.off0.query %t.off1.query
const K0: int;
axiom K0 == 4294967296;
const K1: int;
axiom K1 == 8589934592;
const K2: int;
axiom K2 == 12884901888;
const K3: int;
axiom K3 == 17179869184;
const K4: int;
axiom K4 == 21474836480;
const K5: int;
axiom K5 == 25769803776;
const K6: int;
axiom K6 == 30064771072;
const K7: int;
axiom K7 == 34359738368;
const Negative: int;
axiom Negative == -18446744073709551617;
const Decimal: real;
axiom Decimal == 12345678901234567890.125;
const Bits: bv128;
axiom Bits == 18446744073709551616bv128;
const Float: float53e11;
axiom Float == 0x1.123456789abcde0f53e11;
procedure Ordered() { assert K0 < K1 && K1 < K2 && K2 < K3 && K3 < K4 && K4 < K5 && K5 < K6 && K6 < K7; }
