#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./release-version.sh prepare-preview <major.minor.patch>
  ./release-version.sh prepare-rc <major.minor.patch>
  ./release-version.sh prepare-stable <major.minor.patch>
  ./release-version.sh tag

Examples:
  ./release-version.sh prepare-preview 1.0.0
  ./release-version.sh prepare-rc 1.0.0
  ./release-version.sh prepare-stable 1.0.0
  ./release-version.sh tag
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

if ! command -v nbgv >/dev/null 2>&1; then
  echo "nbgv is required. Install it with: dotnet tool install --global nbgv" >&2
  exit 1
fi

next_prerelease_number() {
  local base_version="$1"
  local identifier="$2"
  local current_version
  local current_number

  current_version="$(sed -nE 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' version.json | head -n 1)"
  current_number="$(sed -nE "s/^${base_version}-${identifier}\.([0-9]+)$/\1/p" <<< "$current_version")"

  if [[ -n "$current_number" ]]; then
    echo $((current_number + 1))
  else
    echo 1
  fi
}

prepare_prerelease() {
  local base_version="$1"
  local identifier="$2"
  local number

  number="$(next_prerelease_number "$base_version" "$identifier")"
  nbgv set-version "${base_version}-${identifier}.${number}"
  echo "Updated version.json to ${base_version}-${identifier}.${number}. Open a pull request with this change."
}


case "$1" in
  prepare-preview)
    [[ $# -eq 2 ]] || { usage; exit 1; }
    prepare_prerelease "$2" "preview"
    ;;
  prepare-rc)
    [[ $# -eq 2 ]] || { usage; exit 1; }
    prepare_prerelease "$2" "rc"
    ;;
  prepare-stable)
    [[ $# -eq 2 ]] || { usage; exit 1; }
    nbgv set-version "$2"
    echo "Updated version.json to $2. Open a pull request with this change."
    ;;
  tag)
    nbgv get-version
    nbgv tag
    ;;
  *)
    usage
    exit 1
    ;;
esac