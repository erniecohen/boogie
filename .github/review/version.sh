#!/bin/sh
# Print the version of this line's packages, and the tag of its release, as GitHub step
# outputs:
#
#   version=<Version>-review.<hash>    the version of the NuGet packages
#   tag=v<Version>+review.<hash>       the tag of the release that carries them
#
# <Version> is Source/Directory.Build.props's, the upstream release this line is based on.
# .github/review/base holds two words: that release's tag, and the line's last product
# commit, the last commit that changes a file the packages are built from.  <hash> is the
# first 8 hex digits of the sha256 of
#
#   git diff --diff-filter=M --abbrev=7 <tag> <last product commit>
#
# and the script fails if any commit after the last product commit changes a product
# file: such a change needs a new product commit named here, and so a new version.  The
# product is everything outside Test/, .github/ and REVIEW.md.  REVIEW.md says why.
#
# The packages' version carries a prerelease label, so that NuGet never takes it for,
# or silently replaces it with, the upstream release of the same number.
set -eu
prefix=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Source/Directory.Build.props)
read -r base product < .github/review/base
# Shallow fetches keep the repository small, so that 7-digit object ids stay unambiguous.
git fetch --quiet --no-tags --depth=1 origin "+refs/tags/$base:refs/tags/$base"
git cat-file -e "$product^{commit}" 2>/dev/null || git fetch --quiet --no-tags --depth=1 origin "$product"
changed=$(git diff --name-only "$product" HEAD -- . \
  ':(exclude)Test' ':(exclude).github' ':(exclude)REVIEW.md')
if [ -n "$changed" ]; then
  echo "product files changed after $product, the last product commit in .github/review/base:" >&2
  echo "$changed" >&2
  exit 1
fi
hash=$(git diff --no-ext-diff --no-color --diff-filter=M --abbrev=7 "$base" "$product" | sha256sum | cut -c1-8)
# SemVer forbids a numeric identifier with a leading zero.
case "$hash" in
  0*[!0-9]*|[1-9a-f]*) ;;
  *) echo "the hash $hash is not a valid prerelease identifier" >&2; exit 1 ;;
esac
echo "version=$prefix-review.$hash"
echo "tag=v$prefix+review.$hash"
