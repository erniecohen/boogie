# review/3.5.5: Boogie 3.5.5 with a soundness fix

This branch is Boogie 3.5.5 (tag `v3.5.5`, commit `d90c6c9`) with a fix for
[boogie-org/boogie#1168](https://github.com/boogie-org/boogie/issues/1168).  Its NuGet
packages have the version `3.5.5-review.37e4435d`, and the tag `v3.5.5+review.37e4435d`
marks the release that carries them.  It is the Boogie that the review line of the Dafny
fork [erniecohen/dafny](https://github.com/erniecohen/dafny) builds against.

The same fix, ported to upstream's `master`, is offered to boogie-org/boogie as a pull
request from the branch `fix/1168-guarded-casts`.

## The fix

| commit | test | issue | what it changes |
|---|---|---|---|
| `6a5b595` | `f755745` | [#1168](https://github.com/boogie-org/boogie/issues/1168) | Under `/typeEncoding:a`: no reverse cast axioms; no retyping of a quantifier's `int` (`bool`, ...) variables to `U` for the sake of its triggers; and a value of a built-in type is passed to a `U` parameter as a cast, also when its translation is already a `U` term. |

The fix commit's message says what each part changes and why the result is sound.  In
short: the reverse cast `forall x: U :: B_2_U(U_2_B(x)) == x` says that every value of `U`
is a `B`, which for `bool` bounds `U` to two elements while the left inverse for `int`
makes it infinite, so the axioms had no model.  The retyped quantifiers were equivalent to
the originals only under the reverse casts, so they go too, and the cast normal form keeps
the proofs that relied on the reverse casts to join a value with its cast.  Boogie's
remaining axioms under the encoding hold in a model where `U` is a tagged union of the
values of every type.  The predicates and monomorphic encodings are unchanged.

## Tests and release notes

`f755745` adds two tests to `Test/test21`, the type-encoding suite, in the style of
`issue-735.bpl`:

- `issue-1168.bpl`, the issue's program, ending in `assert false`;
- `issue-1168-redo.bpl`, which shows that dropping the reverse casts alone is not enough.

Each runs `/typeEncoding:a`, `:p` and `:m` with model-based quantifier instantiation.  Both
fail under v3.5.5, where `/typeEncoding:a` proves `assert false`, and the second also fails
with the reverse casts dropped and the retyping kept.  Both pass with the fix, under Z3
4.11.2 (the version the test suite assumes), 4.12.1, 4.16.0 and 5.1.0, in batch mode too.

`8f4eaef` is upstream's `5b67826` (#1160), cherry-picked: it replaces the `%T`
substitution, which the current `lit` has removed, in `inline/expansion2.bpl`.

Upstream keeps no change log in the repository; its releases are GitHub releases.  This
line's release notes are `.github/review/release-notes.md`, which the release carries.

## The version, and how to check it

`37e4435d` is the first 8 hex digits of the sha256 of the change that the line's product
commits make to files that v3.5.5 already has:

    git diff --diff-filter=M --abbrev=7 v3.5.5 6a5b595 | sha256sum

`6a5b595` is the last product commit, the last commit that changes a file the packages are
built from.  The product is everything outside `Test/`, `.github/` and this file.  The
commits after it add tests, CI and documents, and keep the version.  The rule on this
branch:

- a commit that changes or adds a product file is a new last product commit, and gets a
  new version and a new tag;
- any other commit keeps them.

`.github/review/base` names the base and the last product commit, and CI's
`.github/review/version.sh` computes the version from them and fails if a later commit
changes a product file.

The packages' version is `3.5.5-review.<hash>`, with a prerelease label, so that NuGet can
never take it for, or quietly replace it with, upstream's `3.5.5`.  The tag,
`v3.5.5+review.<hash>`, names the same build the way the Dafny fork names its builds of an
upstream release.

## CI

`.github/workflows/review.yml` replaces upstream's workflows on this branch, because on a
tag they publish to nuget.org.  Its jobs `test` and `lean-auto` are upstream's own test
jobs, without the deployment steps: the parser check, the build with warnings as errors,
the unit tests and the whole `lit` suite with Z3 4.11.2, in Debug and Release and with and
without batch mode, and the Lean backend's tests.  They take master's limits, and give
each test's Boogie process 300 s instead of `lit.site.cfg`'s 110 s: in batch mode on
GitHub's runners, v3.5.5's `civl/large-samples/GC.bpl` takes longer than 110 s and is
aborted, with the fix and without it (its 2,772 queries do not use the arguments encoding,
and the fix sends the solver the same queries).  The job `packages` builds and packs the
14 packages of an upstream release at this line's version.  On a tag, `release` checks that
the tag names the version and attaches the packages, with their sha256 sums in
`SHA256SUMS`, to a GitHub release.

## Returning pooled solvers to idle

After a proof, `Checker.GoBackToIdle` waits for the solver's existing ping before
returning its checker to the pool. Its former 100 ms deadline retired even healthy
solvers when output draining or scheduling took longer. This changed physical
solver histories independently of the proof time limit, including `/timeLimit:0`.
See https://github.com/erniecohen/dafny/issues/287 .

The default now uses `/solverIdleTimeout:<seconds>`, a separate hang safeguard
(default 300 seconds, accepted range 1..86400). A handshake taking, for example,
one second keeps its solver. A handshake that never replies is closed at the
safety bound and its pool slot is released for a replacement. Stdout EOF wakes
the response parser after buffered output drains; EOF, I/O failure and a disposed
transport also retire the checker. The safety bound is still a wall-clock limit:
this removes the short scheduling deadline, and does not promise retention under
arbitrarily long stalls. No extra request, assertion or solver option is sent.
Native solver history survives logical reset, so changes to reuse can change
resource counts of later tasks even when their own command bytes agree.

This determinism fix is on by default. The hidden `/legacySolverIdleTimeout:1`
restores the original 100 ms wait, timeout-only recovery and EOF handling; it
ignores `/solverIdleTimeout`. `/legacySolverIdleTimeout:0` selects the new policy.
The legacy option is for comparison and migration, not ordinary operation.

`IdleHandshakeTest` withholds a fake solver's pong and advances a manual clock:
the original/legacy policy closes it at exactly 100 ms, while the default keeps
it after one second and reuses it only after the pong. Failure and hang tests
check replacement and a usable pool slot. A real child process consumes a ping
and exits without replying, checking EOF propagation without a loaded machine.
Successful fake handshakes send identical complete command sequences in both
modes. `Test/commandline/IdleHandshake.bpl` checks the same 1 verified / 1 error
result in both modes, including an inhabited failing false assertion.

## Deterministic numeric declaration hashes

`/deterministicLiteralHashes:1` opts into process-independent hashes for numeric
literals when `/normalizeDeclarationOrder:1` sorts declarations. It covers integers,
normalized decimal reals, bitvectors and IEEE floating-point representations, including
signed zero and special values, and canonical floating-point rounding modes.
String literals already have this property; rounding-mode literals wrap strings and
now use the same deterministic string hash in the opt-in path. Rational is
not a literal-expression value; decimal and floating-point literals are hashed directly,
without using Rational or its ordinary hash code.

The option defaults to 0, preserving the existing ordering path and ordinary equality
hashes. Enabling it can change solver costs and outcomes at a resource limit. It has
no effect with declaration normalization disabled or a nonzero random seed. Hashes
remain ordering hints, not numeric equality tests; collisions remain possible, as with
the previous ordering. No axiom or verification condition is added or removed.

`Test/test21/issue-30.bpl` compares complete solver queries through check-sat from fresh processes and checks
that disabling declaration normalization ignores the option. Unit tests cover fixed
integer hash vectors, decimal normalization, floating-point representations, culture
independence, and solver-log invariance under declaration reordering and renaming.

The query comparison validates the trailing response/cleanup commands separately.
Batch mode does not send its final pop to the solver, and logging that pop can race
with disposal; its optional presence must not fail the ordering regression.

## Printing keeps the parentheses that parsing needs

The parser builds a chain of `+`, `-`, `*`, `div`, `mod`, `/`, `&&`, `||` or `<==>` to the
left, and a chain of `==>` or `**` to the right.  `BinaryOperator.Emit` printed an operand of
the same binding power without parentheses on the right of `+`, `*`, `&&`, `||` and `<==>`,
and on the left of `**`, so a printed program could parse to a different tree:
`a && (b && c)` printed as `a && b && c`, which parses as `(a && b) && c`, and
`2 * (3 div 2)` as `2 * 3 div 2`, which is `(2 * 3) div 2`.  It now parenthesizes those
operands, as it already did the right operand of `-`, `div`, `mod` and `/` and the left
operand of `==>`.

Only printed text changes, such as that of `/print` and `/printInstrumented`, and the
checksums that `/verifySnapshots:2` and above compute from printed commands.  A program
verified without being printed and parsed again sends the solver the same queries.

The unit test `PrintAssociativity` parses each binary operator nested both ways, prints it,
parses the printed text, and checks that the tree and the printed text come back the same.
`Test/test0/PrintAssociativity.bpl` prints a program with every operator nested both ways,
prints the printed file again, and compares the first print with its expected output and the
second with the first.  Two
expected outputs gain the parentheses: `test0/PrettyPrint.bpl.expect` in 5 lines and
`aitest0/constants.bpl.expect` in 8.

## Printing keeps hideable axioms hideable

`Axiom.Emit` printed `axiom`, the attributes and the expression, and dropped `Axiom.CanHide`,
which the parser sets from the keyword `hideable` and a front end sets on definition axioms
(Dafny does, on each function's).  Pruning refuses the edge from a hidden function to an axiom
only when the axiom is hideable, so in a printed program `hide` pruned nothing: the hidden
definitions, and everything they reach, came back into the solver's context, and the printed
program could prove what the original does not.  `Emit` now writes `hideable axiom`, at the top
level and in a uses clause, where the parser already reads it.

This changes a default, because without it a printed program means something other than the
program printed.  A program without hideable axioms prints as before, and a program verified
without being printed and parsed again sends the solver the same queries.

`Test/pruning/PrintHideable.bpl` verifies a program with a hideable axiom at the top level and
one in a uses clause, prints it, and verifies the printed program: both give 1 verified and 2
errors, where the printed program used to give 3 verified.

## Printing definition axioms in place

A definition axiom belongs to the functions and constants that hold it
(`Function.DefinitionAxiom` and `OtherDefinitionAxioms`, `Constant.DefinitionAxioms`), and
pruning follows an edge from each owner to it.  A front end builds programs that uses clauses
cannot express.  Dafny gives a nullary datatype constructor `#C` and its identifier constant
`##C` one axiom, `DatatypeCtorId(#C()) == ##C`, which both own; and it adds a definition axiom
to the top-level declarations when it makes it, often long after its owner.  The printer prints
an owned axiom in the uses clause of each owner, and not at its own position, and the parser
makes a new axiom for each uses clause, just before the owner.  So a printed program has two
copies of a shared axiom, one live with each owner (and if the axiom has a `{:name}`, the
printed program does not resolve); its definition axioms move, which changes the order of the
solver's input under `/normalizeDeclarationOrder:0`; and a function's own definition axiom comes
back as one of its uses-clause axioms.

The opt-in `/printDefinitionAxiomsInPlace` prints the top-level declarations once each, in
order, with no uses clauses.  An owned axiom names its owners in attributes, printed before its
own:

- `{:definition_of_function "f"}`: it is `f`'s `DefinitionAxiom`, as a function body makes it;
- `{:used_by_function "f", "g", ...}`: it is in their `OtherDefinitionAxioms`;
- `{:used_by_constant "c", "d", ...}`: it is in their `DefinitionAxioms`.

`Axiom.Resolve` attaches the axiom to each owner named, and removes the attributes, so the
printed program parses and resolves into the same declarations, in the same order, with the
same attributes and owners, and so with the same pruning edges.  An undeclared owner is a
resolution error.  `/printSplitDeclarations` prints a split's declarations the same way under
the option, naming the owners among them.  An owned axiom that is not a top-level declaration
is not printed, since pruning never sends it to the solver.  Dafny passes Boogie options through
`--boogie`, so `--boogie /printDefinitionAxiomsInPlace` reaches the printer of `--bprint`.

The option is off by default, and without it printing is unchanged apart from `hideable`.  It
changes only printed text.  Verification changes only for a program that writes the owner
attributes, which Boogie ignored before.

The unit tests `PrintRoundTripTest` build a program in the shape Dafny builds: a shared axiom,
definition axioms after their owners, hideable definition axioms and a function body.  They print
it, parse the printed text and resolve it.  With the option, the declarations, their order, their
attributes and their owners come back the same, printing the result gives the same text, and
the solver gets the same queries (`SOLVER=noop` logs, `/normalizeDeclarationOrder:0`).  Without
it, the shared axiom comes back twice and the queries differ.  `Test/pruning/PrintDefinitionAxiomsInPlace.bpl`
writes the attributes by hand, prints the program with the option, checks that each axiom is
printed once, in place, with its owners, prints the printed program again and compares, and
verifies both: each gives 4 verified and 1 error.  One of its procedures verifies only if the
constant still owns the shared axiom.  No expected output changes.

## Printing keeps the attributes of if commands and assignments

`IfCmd.Emit` printed `if (guard)` and `else if (guard)`, and `AssignCmd.Emit` printed
`x := e`, and both dropped the command's attributes, although the parser reads them, after `if`
and after `:=`.  A front end sets them: Dafny marks the if commands of its `if` statements and
`match` cases with `{:allow_path_isolation}`, which desugaring copies onto the jump to the
branches and which `{:isolate "paths"}` splits at, and gives assignments an `{:id}`.  So a printed
program could split differently: an assertion `{:isolate "paths"}` after an `if` statement was
one task in the printed program and one per path in the original.  The printer now writes
`if {:attr} (guard)`, `else if {:attr} (guard)` and `x := {:attr} e`.

This changes a default, as `hideable` did: without it a printed program means something other
than the program printed.  A command without attributes prints as before, and a program verified
without being printed and parsed again sends the solver the same queries.

`Test/test0/PrintCommandAttributes.bpl` prints a program with attributed if commands and
assignments, checks the printed attributes, prints the printed program again and compares.  The
unit test `PrintFidelityTest.PrintedIfAttributesKeepThePathSplits` checks that the printed
program has the original's path splits and queries.

## Verifying as tasks: `/verifyAsTasks`

A front end such as Dafny verifies a program it has built through
`ExecutionEngine.GetVerificationTasks`: it resolves and type checks the program, coalesces
blocks, prepares each implementation, and makes one task per split.  The command line verifies
through its own pipeline, `ProcessProgram`, whose `ResolveAndTypecheck` also runs the CIVL type
checker.  That checker prunes every implementation in place (`PruneUnreachableBlocks`, in
`LinearTypeChecker.VisitImplementation`): it cuts the edges out of blocks that assume `false`, and
reorders the blocks, before blocks are coalesced.  A join reached from a branch that ends in
`assume false` then has a single predecessor and is merged into the other branch, where the task
pipeline keeps it a block of its own.  So the command line sends the solver different queries for
the same program.

The opt-in `/verifyAsTasks` makes the command line verify through `GetVerificationTasks` and run
the tasks, and report the results as it reports its own: per implementation, with the same
outcome, error and trailer messages, and per split as an assertion batch in `/xml`.  A program then
gets the preparation, the splits and the queries it gets from a front end; a split's prover log is
named `<implementation>_split<n>`, as there.  It refuses a program with CIVL declarations, which
the task pipeline does not support, and a polymorphic program under the monomorphic encoding,
because `GetVerificationTasks` takes the encoding as given where `ResolveAndTypecheck` would
monomorphize the program.  Without the option the command line is unchanged.

`Test/commandline/VerifyAsTasks.bpl` verifies a program with and without the option and checks
that both report the same error and the same counts.  The unit tests
`PrintFidelityTest.VerifyAsTasks*` check that the option sends the queries of the tasks, that
without it the command line's queries differ as before, that it counts as the command line does,
and that it refuses CIVL and unmonomorphized polymorphic programs.

## Structural literals: `/structuralLiterals`

Two decisions depend on which expression object a literal is, and a printed program cannot say.
Desugaring a structured `if` or `while` assumes `Expr.Not(guard)` on the branch where the guard is
false, and `Expr.Not` folds only the shared objects `Expr.True` and `Expr.False`: a front end that
guards with `Expr.True` gets `assume false`, a dead branch, and the same program printed and
parsed gets `assume !true`, which is not pruned.  And passification keeps a variable's incarnation
at a join only if every predecessor holds the same expression object for it: a front end that
assigns the shared `Expr.True` on two branches keeps the variable `true`, and the printed program,
with two parsed literals, gets a new incarnation and an equation on each branch.

The opt-in `/structuralLiterals` makes both decisions by value.  Resolution folds the negation of a
boolean literal in the assumptions with the attribute `{:partition}` that desugaring makes, and the
join treats two literals of the same value, or two identifiers of the same variable, as the same
incarnation.  Both are sound: a folded assumption is equivalent to the original, and two
expressions that the join identifies denote the same value.  Passed to both, a front end's
in-memory program and its printed form get the same queries.  The option can change the queries,
and so the solver's work, of a program built in memory, and it changes nothing without it.

The unit tests `PrintFidelityTest.StructuralLiteralsMakeThePrintedProgramSendTheSameQueries` build
the two cases in memory with `Expr.True`, print them, and check that with the option the printed
program sends the same queries, and that without it the queries differ, as before.

## A Reset after a FullReset sends the current context's background

`SMTLibInteractiveTheoremProver` caches the background of a context, every declaration and
axiom sent before the first check, in `CachedCommon` when that check is prepared, and sends it
again after each `Reset`, which `(reset)`s the solver between the checks of one context.
`FullReset`, which starts a new context for a split (`Checker.Target`), cleared the background
but kept the cache, and `Check` set the cache only when it was empty (`??=`).  So the first
check of every later context put the first context's background back, and the next `Reset`
sent it: the solver got the first context's declarations and axioms instead of the current
one's.  A function that the first context pruned is undeclared, and Z3 stops with
`unknown constant`; an axiom that the current context hides is asserted.  `FullReset` now drops
the cache and `CachedAxBuilder`, which the first check of the new context sets again.  The cache
is now the background's length, `CachedCommonLength`, not a copy of it: between `FullReset`s the
background is only appended to, so cutting it back to that length restores the cached text,
where each context used to copy it twice, as a string and into a new builder.  On Dafny's Std,
with a context for each of 7,611 splits, those copies were about 2% of the allocation.

A prover reaches a `Reset` after a `FullReset` when one implementation has several splits and
the checks of a later split reuse the prover without targeting it again, as the command line's
verification of an implementation does (`/vcsSplitOnEveryAssert`, `{:split_here}`, ...).  The
task pipeline that Dafny uses targets the prover for every split, so its queries do not change.
Elsewhere, only the background sent after such a `Reset` changes, to the one the context sent
first.

`Test/commandline/SplitSetup.bpl` verifies three procedures under `/vcsSplitOnEveryAssert`
with one prover: Z3 stopped with `unknown constant` before, and all three verify now.  The unit
test `SplitSetupTest.ResetSendsTheBackgroundOfItsOwnContext` checks in the whole command stream
that every background sent after a `Reset` is the one its context sent first.

## Setting up a split: `/cacheUsedTypes` and `/lockFreeExpansion`

Two opt-in options take Dafny-process work out of setting up a prover for a split.  Neither
changes a byte of what the solver gets.

`Checker.Target` collects the basic types for which the arguments and predicates encodings
declare casts by visiting the whole program, entering no implementation but the split's, once
for every split.  On Dafny's `Test/dafny/Seq.bpl` under `/vcsSplitOnEveryAssert` (612 splits,
4 cores, `SOLVER=noop`) the visits took 6.2 s of the process's 12.7 s of CPU.  Under
`/cacheUsedTypes` the types come from a cache of each declaration's own types, in the order the
visitor meets them first, and only the split's implementation is visited again
(`VCGeneration/UsedTypes.cs` says why the list is the same, in the same order, with the same
objects).  The cache holds a declaration weakly and assumes that a declaration other than an
implementation does not change after verification starts.

`Boogie2VCExprTranslator` translates the body of an `{:inline}` function at each call while it
holds a lock on the function.  The translation reads the function and writes only its own
prover's translator and expressions, so the lock guards nothing that another prover shares, but
every prover that expands the same function waits for the others.  Profiles of `dafny verify` on
Dafny's standard library with 18 cores found 18.9 s of threads waiting there.  Under
`/lockFreeExpansion` the translation takes no lock.

The background itself is not cached.  The task proposed caching the erased and printed
background of a program once and sending it after every reset; but under pruning each split
gets its own background, the pruned declarations of its implementation, so the cache would key
on the split's declarations.  On `Seq.bpl`, 74 checks had 71 distinct backgrounds, and the
backgrounds are 62% of the bytes sent.  The text of one declaration cannot be cached either: the
namer gives a name the suffix `@@<n>` by the names it has given before in the context, so the
1,506 distinct axiom texts of the 74 backgrounds are 645 axioms.

The unit tests `SplitSetupTest` compare the whole command stream of a program with basic types
first met in different declarations, a polymorphic function and map, inline functions, hideable
axioms with `hide` and `reveal`, and implementations with their own `{:random_seed}` and
`{:rlimit}`, with and without both options: under both encodings, with and without pruning,
with declaration normalization, without it and with a random seed, and with and without
`/vcsSplitOnEveryAssert`, through the task pipeline and through the command line.  They fail if
the cached types come in another order.  `Test/commandline/SplitSetup.bpl` compares the command
streams of the command line with `cmp`.
