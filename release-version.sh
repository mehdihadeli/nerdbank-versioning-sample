#!/bin/bash
# release-version.sh
# Helper for GitHub Flow + NBGV version transitions.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"

usage() {
  cat <<'EOF'
Usage:
  ./release-version.sh prepare-dev <major.minor.patch>
  ./release-version.sh prepare-staging <major.minor.patch> <rc-number>
  ./release-version.sh prepare-production <major.minor.patch>
  ./release-version.sh tag [--push]

Examples:
  ./release-version.sh prepare-dev 1.1.0
  ./release-version.sh prepare-staging 1.0.0 4
  ./release-version.sh prepare-production 1.0.0
  ./release-version.sh tag
  ./release-version.sh tag --push

Notes:
  - prepare-* commands only change and commit version.json.
  - Use these commands from a PR branch, then merge to main.
  - Run 'tag' on up-to-date main after merge so tags point to main.
EOF
}

die() {
  echo "Error: $1" >&2
  exit 1
}

require_clean_version_json() {
  local status
  status="$(git -C "$ROOT_DIR" status --porcelain -- version.json)"
  if [ -n "$status" ]; then
    die "version.json has uncommitted changes. Commit or discard them first."
  fi
}

ensure_in_git_repo() {
  git -C "$ROOT_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1 || die "Not inside a git repository."
}

set_declared_version() {
  local new_version="$1"
  sed -i.bak -E "s/^([[:space:]]*\"version\":[[:space:]]*\").*(\",[[:space:]]*)$/\1${new_version}\2/" "$ROOT_DIR/version.json"
  rm -f "$ROOT_DIR/version.json.bak"
}

assert_semver() {
  local expected="$1"
  dotnet tool restore >/dev/null
  local actual
  actual="$(dotnet nbgv get-version -v SemVer2)"
  if [ "$actual" = "$expected" ] || [[ "$actual" == "$expected".g* ]]; then
    echo "Verified local SemVer2: $actual"
    return
  fi
  die "Expected SemVer2 '$expected' (or '$expected.g<commit>' on non-public refs) but got '$actual'."
}

commit_version_change() {
  local message="$1"
  git -C "$ROOT_DIR" add version.json
  git -C "$ROOT_DIR" commit -m "$message" -- version.json
}

print_pr_instructions() {
  local branch
  branch="$(git -C "$ROOT_DIR" branch --show-current)"
  cat <<EOF

Next steps:
  1) Push this branch and open a PR to main:
     git push origin HEAD:refs/heads/${branch}
  2) Merge the PR.
  3) On your local main, sync:
     git checkout main
     git pull --ff-only origin main
  4) If this is a staging/prod release, create tag from merged main:
     ./release-version.sh tag --push
EOF
}

prepare_dev() {
  local base="$1"
  local target="${base}-preview.{height}"

  require_clean_version_json
  set_declared_version "$target"
  commit_version_change "chore(version): start ${base} preview train"
  assert_semver "${base}-preview.0"
  print_pr_instructions
}

prepare_staging() {
  local base="$1"
  local rc_number="$2"
  local target="${base}-rc.${rc_number}"

  case "$rc_number" in
    ''|*[!0-9]*) die "rc-number must be numeric." ;;
  esac

  require_clean_version_json
  set_declared_version "$target"
  commit_version_change "chore(version): set ${target}"
  assert_semver "$target"
  print_pr_instructions
}

prepare_production() {
  local base="$1"

  require_clean_version_json
  set_declared_version "$base"
  commit_version_change "chore(version): release ${base}"
  assert_semver "$base"
  print_pr_instructions
}

create_tag() {
  local push_tag="false"
  if [ "${1:-}" = "--push" ]; then
    push_tag="true"
  elif [ -n "${1:-}" ]; then
    die "Unknown option '$1' for tag command."
  fi

  local branch
  branch="$(git -C "$ROOT_DIR" branch --show-current)"
  if [ "$branch" != "main" ]; then
    die "Tagging should run on main after PR merge. Current branch: $branch"
  fi

  dotnet tool restore >/dev/null
  local semver
  semver="$(dotnet nbgv get-version -v SemVer2)"

  dotnet nbgv tag
  local tag_name="v${semver}"

  echo "Created tag: ${tag_name}"
  if [ "$push_tag" = "true" ]; then
    git -C "$ROOT_DIR" push origin "$tag_name"
    echo "Pushed tag: ${tag_name}"
  else
    echo "To push tag: git push origin ${tag_name}"
  fi
}

main() {
  ensure_in_git_repo

  local command="${1:-}"
  case "$command" in
    prepare-dev)
      [ "$#" -eq 2 ] || die "prepare-dev requires <major.minor.patch>."
      prepare_dev "$2"
      ;;
    prepare-staging)
      [ "$#" -eq 3 ] || die "prepare-staging requires <major.minor.patch> <rc-number>."
      prepare_staging "$2" "$3"
      ;;
    prepare-production)
      [ "$#" -eq 2 ] || die "prepare-production requires <major.minor.patch>."
      prepare_production "$2"
      ;;
    tag)
      shift
      create_tag "${1:-}"
      ;;
    -h|--help|help|"")
      usage
      ;;
    *)
      die "Unknown command '$command'. Use --help for usage."
      ;;
  esac
}

main "$@"

