// RUN: %boogie -noVerify -env:0 -print:"%t.print.bpl" "%s" > "%t"
// RUN: %boogie -noVerify -env:0 -print:"%t.reprint.bpl" "%t.print.bpl" >> "%t"
// RUN: %diff "%s.expect" "%t.print.bpl"
// RUN: %diff "%t.print.bpl" "%t.reprint.bpl"

// Printing a program and parsing the printed text must give back the same trees.  The parser
// builds a chain of +, -, *, div, mod, /, ++, &&, || or <==> to the left, and a chain of ==> or
// ** to the right, so the printer must keep the parentheses of an operand of the same binding
// power on the other side.  Each operator appears below in both nestings.

const a: int;
const b: int;
const c: int;
const d: int;
const r: real;
const s: real;
const t: real;
const x: real;
const u: bv8;
const v: bv8;
const w: bv8;
const P: bool;
const Q: bool;
const R: bool;

axiom a + (b + c) == d;
axiom (a + b) + c == d;
axiom a + (b - c) == d;
axiom (a + b) - c == d;
axiom a - (b + c) == d;
axiom (a - b) + c == d;
axiom a - (b - c) == d;
axiom (a - b) - c == d;

axiom a * (b * c) == d;
axiom (a * b) * c == d;
axiom a * (b div c) == d;
axiom (a * b) div c == d;
axiom a * (b mod c) == d;
axiom (a * b) mod c == d;
axiom a div (b * c) == d;
axiom (a div b) * c == d;
axiom a div (b div c) == d;
axiom (a div b) div c == d;
axiom a mod (b mod c) == d;
axiom (a mod b) mod c == d;
axiom r * (s / t) == x;
axiom (r * s) / t == x;
axiom r / (s * t) == x;
axiom (r / s) * t == x;
axiom r / (s / t) == x;
axiom (r / s) / t == x;

axiom (r ** s) ** t == x;
axiom r ** (s ** t) == x;

axiom u ++ (v ++ w) == (u ++ v) ++ w;

axiom P && (Q && R);
axiom (P && Q) && R;
axiom P || (Q || R);
axiom (P || Q) || R;
axiom P <==> (Q <==> R);
axiom (P <==> Q) <==> R;
axiom P ==> (Q ==> R);
axiom (P ==> Q) ==> R;
axiom (P <== Q) <== R;

procedure Assoc(p: bool, q: bool, r: bool)
{
  assert p && (q && r);
}
