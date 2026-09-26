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
without batch mode, and the Lean backend's tests.  The job `packages` builds and packs the
14 packages of an upstream release at this line's version.  On a tag, `release` checks that
the tag names the version and attaches the packages, with their sha256 sums in
`SHA256SUMS`, to a GitHub release.
