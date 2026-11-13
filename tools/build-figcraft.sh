#!/usr/bin/env bash
# Compiles FigCraft from the external/figcraft submodule (our fork Kazo-ca/figcraft, branch
# kazo/v0.18.1; work item 3 of doc/en/plan.md) the way its release workflow builds the web zip, then
# vendors the result in src/Kazo.Fig/wwwroot/figcraft/ with tools/vendor-figcraft.sh --dir. The
# .github/workflows/build-figcraft.yml workflow runs it; it also runs on a developer machine.
#
# Usage:
#   tools/build-figcraft.sh --craft-fonts <folder>
#
# <folder> is a checkout of storytold/craft-fonts at the revision pinned below: FigCraft's web
# release embeds Inter and Noto Sans Georgian from it (the browser has no system fonts) and refuses
# to build without them (CRAFT_FONTS_REQUIRED). Needs: Rust stable with the wasm32-unknown-unknown
# target, Trunk at the version below, git, zip and unzip.
set -euo pipefail

# The Trunk version of FigCraft's release workflow (craft-common-actions, install-trunk).
trunk_version="0.21.14"
# The craft-fonts revision of FigCraft's release workflow (.github/workflows/release.yml of the
# v0.18.1 commit, craft-fonts-ref).
craft_fonts_ref="37827adc41fd165f9a9234ac40f94dbad353aa69"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
figcraft="$repo_root/external/figcraft"

fail() { echo "error: $*" >&2; exit 1; }

fonts_dir=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --craft-fonts) [[ $# -ge 2 ]] || fail "--craft-fonts needs a folder"; fonts_dir="$2"; shift 2 ;;
    -h|--help) sed -n '2,14p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$fonts_dir" ]] || fail "--craft-fonts <folder> is required (a checkout of storytold/craft-fonts at $craft_fonts_ref)"
[[ -d "$fonts_dir" ]] || fail "$fonts_dir is not a folder"
fonts_dir="$(cd "$fonts_dir" && pwd)"
# The fonts embedded in the .wasm must be the release's: the provenance records this revision.
actual_fonts="$(git -C "$fonts_dir" rev-parse HEAD 2>/dev/null)" || fail "$fonts_dir is not a git checkout of craft-fonts"
[[ "$actual_fonts" == "$craft_fonts_ref" ]] || fail "craft-fonts is at $actual_fonts; FigCraft 0.18.1's release uses $craft_fonts_ref"
[[ -z "$(git -C "$fonts_dir" status --porcelain)" ]] || fail "$fonts_dir has local changes"

[[ -f "$figcraft/Cargo.toml" ]] || fail "external/figcraft is empty. Run: git submodule update --init external/figcraft"
# A local change of FigCraft must go through a commit of the fork, so the provenance names it.
[[ -z "$(git -C "$figcraft" status --porcelain)" ]] || fail "external/figcraft has local changes; commit them in the fork first"

command -v trunk >/dev/null || fail "trunk not found; install Trunk $trunk_version"
actual_trunk="$(trunk --version | awk '{print $2}')"
[[ "$actual_trunk" == "$trunk_version" ]] || fail "trunk $actual_trunk found; FigCraft's release uses $trunk_version"
rustup target list --installed 2>/dev/null | grep -qx wasm32-unknown-unknown \
  || fail "the wasm32-unknown-unknown target is missing (rustup target add wasm32-unknown-unknown)"
command -v zip >/dev/null && command -v unzip >/dev/null || fail "zip and unzip are needed"

commit="$(git -C "$figcraft" rev-parse HEAD)"
rustc_version="$(rustc --version)"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# FigCraft's own packaging script: trunk build --release (wasm size gate included), then the
# licences, NOTICE and font licences next to the site. The build date is the commit's (UTC), so
# building the same commit twice bakes the same date into the About window.
build_date="$(TZ=UTC git -C "$figcraft" log -1 --format=%cd --date=format-local:%Y-%m-%d)"
echo "Building FigCraft $commit (rustc: $rustc_version, trunk $actual_trunk, craft-fonts $craft_fonts_ref)"
(
  unset FIGCRAFT_EMBED_CJK FIGCRAFT_EMBED_SCRIPTS FIGCRAFT_WEB_FONTS FIGCRAFT_VERSION
  export CRAFT_FONTS_DIR="$fonts_dir" CRAFT_FONTS_REQUIRED=1
  export FIGCRAFT_BUILD_SHA="$commit" FIGCRAFT_BUILD_DATE="$build_date"
  export DIST="$work/dist"
  export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-$figcraft/target}"
  cd "$figcraft"
  packaging/web/package.sh
)

shopt -s nullglob
zips=("$work"/dist/figcraft-web-*.zip)
shopt -u nullglob
[[ ${#zips[@]} -eq 1 ]] || fail "packaging/web/package.sh made no single figcraft-web-*.zip"
unzip -q "${zips[0]}" -d "$work/site"
site=("$work"/site/figcraft-web-*)
[[ ${#site[@]} -eq 1 && -d "${site[0]}" ]] || fail "the zip has no single figcraft-web-*/ folder"

"$repo_root/tools/vendor-figcraft.sh" --dir "${site[0]}" \
  --craft-fonts "$craft_fonts_ref" --rustc "$rustc_version" --trunk "$actual_trunk"
