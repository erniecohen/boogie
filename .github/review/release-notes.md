Boogie 3.5.5 with a fix for [#1168](https://github.com/boogie-org/boogie/issues/1168), a
soundness issue: under `/typeEncoding:a`, the arguments type encoding, Boogie emitted a reverse
cast `forall x: U :: B_2_U(U_2_B(x)) == x` for each built-in type `B`.  For `bool` it says that
the sort `U` has at most two elements, while the left inverse for `int` makes `U` infinite, so
the axioms had no model, and a solver could prove `false` from them.

The fix changes `/typeEncoding:a` only.  It emits no reverse casts; it no longer retypes a
quantifier's `int` or `bool` variable to `U` for the sake of its triggers, which was sound only
under the reverse casts; and it passes a value of a built-in type to a `U` parameter as a cast,
also when the value is the result of a polymorphic function or a map select.  It adds no axiom.
The predicates and monomorphic encodings are unchanged.

A program that relied on the reverse casts may now fail to verify, or take a different amount
of solver work.  Dafny 4.11.0 selects this encoding.  Built against this fix and run over 1,069
programs of Dafny's own test suite at a resource limit of 16,000,000, it changed the outcome of
10: in 5 a proof now finishes within the limit that did not, and in 5 one stops finishing.  No
program gained an error.
