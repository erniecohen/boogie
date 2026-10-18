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

The new opt-in `/deterministicLiteralHashes:1` makes numeric and rounding-mode literal content hashes
independent of per-process runtime hash seeds when normalizing declaration order.
The default remains unchanged. Enabling the option may change solver resource counts
and outcomes at a resource limit. See `REVIEW.md` for scope and regression coverage.

The printer now keeps the parentheses that the parser needs to rebuild the same tree.  It
printed `a && (b && c)` as `a && b && c`, which parses as `(a && b) && c`, and
`2 * (3 div 2)` as `2 * 3 div 2`, which means `(2 * 3) div 2`: the right operand of `+`, `*`,
`&&`, `||` and `<==>`, and the left operand of `**`, lost its parentheses when it had the same
binding power.  Printed programs (`/print`) change; a program verified without being printed is
sent to the solver unchanged.

The printer now writes `hideable` before a hideable axiom.  It dropped the keyword, so in a printed
program `hide` pruned nothing, and a printed program could verify an assertion that the original
does not.  Programs without hideable axioms print as before.

The new opt-in `/printDefinitionAxiomsInPlace` prints each axiom once, at its own position, instead
of in the uses clause of each function or constant that owns it, and names the owners in the
attributes `{:definition_of_function "f"}`, `{:used_by_function "f", ...}` and
`{:used_by_constant "c", ...}`, which resolution attaches the axiom to.  A printed program then
parses into the declarations that were printed, in the same order, with the same owners, where
uses clauses copied an axiom with two owners and moved definition axioms.  Without the option,
printing is unchanged apart from `hideable`.  See `REVIEW.md`.

The printer now writes the attributes of an if command, after `if` and after `else if`, and of an
assignment, after `:=`, where the parser reads them.  It dropped them, so a printed program lost
marks such as `{:allow_path_isolation}`, at which `{:isolate "paths"}` splits, and an assignment's
`{:id}`.  Commands without attributes print as before.

The new opt-in `/verifyAsTasks` makes the command line verify through
`ExecutionEngine.GetVerificationTasks`, the interface through which a front end such as Dafny
verifies a program, so that a program gets the same preparation, splits and solver queries as
there.  The command line's own pipeline prunes implementations during type checking, before blocks
are coalesced, and so can merge blocks that the task pipeline keeps apart.  Programs with CIVL
declarations are not supported.

The new opt-in `/structuralLiterals` folds the negated literal guards that desugaring assumes, and
lets passification keep an incarnation at a join when the predecessors hold equal literals or the
same variable, by value rather than by object.  With it, a program built in memory and the same
program printed and parsed get the same queries.  See `REVIEW.md`.
