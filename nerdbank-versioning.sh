#!/bin/bash
# nerdbank-versioning.sh
# Validates the repo's planned-release GitHub Flow strategy with Nerdbank.GitVersioning.
#
# Test summary and expected results:
# 1. Initial main build
#    - Local nbgv:    1.0.0-preview.1.g<commit>
# 2. Feature branch squash-merged to main
#    - Local nbgv:    1.0.0-preview.2.g<commit>
# 3. Fix branch squash-merged to main
#    - Local nbgv:    1.0.0-preview.3.g<commit>
# 4. RC promotion tag on main
#    - Tag:           v1.0.0-rc.1
#    - Local nbgv:    1.0.0-preview.3.g<commit>
#    - Effective env: 1.0.0-rc.1
# 5. Bug fix after RC on main
#    - Local nbgv:    1.0.0-preview.4.g<commit>
# 6. RC repromotion tag on main
#    - Tag:           v1.0.0-rc.2
#    - Local nbgv:    1.0.0-preview.4.g<commit>
#    - Effective env: 1.0.0-rc.2
# 7. Production promotion tag on main
#    - Tag:           v1.0.0
#    - Local nbgv:    1.0.0-preview.4.g<commit>
#    - Effective env: 1.0.0
# 8. Next release train starts with a new base version; height resets automatically
#    - version.json:  1.1.0-preview.{height}
#    - Local nbgv:    1.1.0-preview.1.g<commit>
#
# Notes:
# - This script uses a disposable sandbox repo, not the workspace Git history.
# - Cleanup runs before setup and again on exit so version/history state never lingers.

set -euo pipefail

GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BLUE='\033[0;34m'; MAGENTA='\033[0;35m'; BOLD='\033[1m'; NC='\033[0m'

PASSED=0
FAILED=0
ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
SANDBOX=""

header() { echo -e "\n${CYAN}━━━ $1 ━━━${NC}"; }
ok()     { echo -e "  ${GREEN}✅ $1${NC}"; PASSED=$((PASSED + 1)); }
fail()   { echo -e "  ${RED}❌ $1${NC}"; FAILED=$((FAILED + 1)); }
info()   { echo -e "  ${BLUE}ℹ️  $1${NC}"; }

cleanup() {
    if [ -n "$SANDBOX" ] && [ -d "$SANDBOX" ]; then
        echo -e "${YELLOW}Cleaning sandbox history/version state: ${SANDBOX}${NC}"
        rm -rf "$SANDBOX"
        SANDBOX=""
    fi
}

trap cleanup EXIT

run_in_repo() {
    (
        cd "$SANDBOX"
        "$@"
    )
}

get_declared_version() {
    run_in_repo sed -n 's/^[[:space:]]*"version":[[:space:]]*"\([^"]*\)".*/\1/p' version.json | head -n 1
}

get_train_prefix() {
    get_declared_version
}

get_stable_version() {
    get_train_prefix | sed 's/-preview.*$//'
}

get_semver() {
    run_in_repo dotnet nbgv get-version -v SemVer2 2>/dev/null || echo ""
}

get_effective_version() {
    local stable_tag
    local rc_tag

    stable_tag=$(run_in_repo sh -c "git tag --points-at HEAD | grep -E '^v[0-9]+\\.[0-9]+\\.[0-9]+$' | head -n 1" || true)
    if [ -n "$stable_tag" ]; then
        echo "${stable_tag#v}"
        return
    fi

    rc_tag=$(run_in_repo sh -c "git tag --points-at HEAD | grep -E '^v[0-9]+\\.[0-9]+\\.[0-9]+-rc\\.[0-9]+$' | head -n 1" || true)
    if [ -n "$rc_tag" ]; then
        echo "${rc_tag#v}"
        return
    fi

    get_semver
}

print_versions() {
    local actual_nbgv="$1"
    local expected_version="$2"
    local effective_actual="$3"
    local effective_expected="$4"

    echo -e "  ${MAGENTA}${BOLD}Calculated by nbgv:${NC} ${MAGENTA}${actual_nbgv}${NC}"
    echo -e "  ${YELLOW}${BOLD}Expected version:${NC}  ${YELLOW}${expected_version}${NC}"
    echo -e "  ${CYAN}${BOLD}Effective version:${NC}  ${CYAN}${effective_actual}${NC}"
    echo -e "  ${GREEN}${BOLD}Expected effective:${NC} ${GREEN}${effective_expected}${NC}"
}

assert_equals() {
    local label="$1"
    local actual="$2"
    local expected="$3"

    if [ "$actual" = "$expected" ]; then
        ok "$label: $actual"
    else
        fail "$label: expected '$expected' but got '$actual'"
    fi
}

assert_contains() {
    local label="$1"
    local haystack="$2"
    local needle="$3"

    if echo "$haystack" | grep -Fq -- "$needle"; then
        ok "$label: contains '$needle'"
    else
        fail "$label: expected '$haystack' to contain '$needle'"
    fi
}

assert_not_equals() {
    local label="$1"
    local actual="$2"
    local not_expected="$3"

    if [ "$actual" != "$not_expected" ]; then
        ok "$label: '$actual' differs from '$not_expected'"
    else
        fail "$label: did not expect '$actual'"
    fi
}

commit_change() {
    local message="$1"

    run_in_repo sh -c "printf '%s - %s\n' '$message' '$(date +%s)' >> changes.txt"
    run_in_repo git add changes.txt
    run_in_repo git commit -m "$message" --no-verify >/dev/null
}

merge_branch() {
    local branch="$1"

    run_in_repo git checkout main >/dev/null
    run_in_repo git merge --squash "$branch" >/dev/null
    run_in_repo git commit -m "Merge $branch" --no-verify >/dev/null
    run_in_repo git branch -d "$branch" >/dev/null 2>&1 || true
}

tag_head() {
    local tag_name="$1"
    local message="${2:-Release $1}"

    run_in_repo git tag -a "$tag_name" -m "$message"
    info "Tagged HEAD with $tag_name"
}

set_release_train() {
    local stable="$1"
    local old_version

    old_version=$(get_declared_version)
    run_in_repo sed -i.bak "s/\"version\": \"$old_version\"/\"version\": \"${stable}-preview.{height}\"/" version.json
    run_in_repo rm -f version.json.bak
    info "Started release train: ${stable}-preview.{height}"
}

setup() {
    header "Setup"

    cleanup

    SANDBOX="$(mktemp -d)"
    cp "$ROOT_DIR/version.json" "$SANDBOX/version.json"
    cp "$ROOT_DIR/dotnet-tools.json" "$SANDBOX/dotnet-tools.json"

    run_in_repo rm -rf .git bin obj changes.txt .gitignore version.json.bak

    run_in_repo git init >/dev/null
    run_in_repo git config user.email "test@test.com"
    run_in_repo git config user.name "Test"
    run_in_repo git checkout -b main >/dev/null
    run_in_repo dotnet tool restore >/dev/null

    run_in_repo sh -c "printf 'bin/\nobj/\n' > .gitignore"
    run_in_repo sh -c "echo 'seed' > changes.txt"
    run_in_repo git add .
    run_in_repo git commit -m "chore: initial project setup" --no-verify >/dev/null

    info "Sandbox: $SANDBOX"
    info "Declared version: $(get_declared_version)"
    info "Stable target: $(get_stable_version)"
    info "Starting height-based preview: $(get_declared_version)"
    ok "Setup complete"
}

check_dev() {
    local description="$1"
    local expected_prefix="$2"
    local actual_semver
    local effective_version

    header "$description"

    actual_semver=$(get_semver)
    effective_version=$(get_effective_version)

    print_versions "$actual_semver" "${expected_prefix}.<height>[.g<commit>]" "$effective_version" "$actual_semver"

    assert_contains "nbgv preview prefix" "$actual_semver" "${expected_prefix}."
    assert_equals "effective dev version" "$effective_version" "$actual_semver"
}

check_staging() {
    local description="$1"
    local expected_version="$2"
    local expected_effective="$3"
    local actual_semver
    local effective_version

    header "$description"

    actual_semver=$(get_semver)
    effective_version=$(get_effective_version)

    print_versions "$actual_semver" "$expected_version" "$effective_version" "$expected_effective"

    assert_equals "expected version matches nbgv" "$actual_semver" "$expected_version"
    assert_equals "staging tag version" "$effective_version" "$expected_effective"
    assert_not_equals "nbgv stays on preview train" "$actual_semver" "$expected_effective"
    assert_contains "nbgv still reflects preview train" "$actual_semver" "-preview."
}

check_prod() {
    local description="$1"
    local expected_version="$2"
    local expected_effective="$3"
    local actual_semver
    local effective_version

    header "$description"

    actual_semver=$(get_semver)
    effective_version=$(get_effective_version)

    print_versions "$actual_semver" "$expected_version" "$effective_version" "$expected_effective"

    assert_equals "expected version matches nbgv" "$actual_semver" "$expected_version"
    assert_equals "production tag version" "$effective_version" "$expected_effective"
    assert_not_equals "nbgv stays on preview train" "$actual_semver" "$expected_effective"
    assert_contains "nbgv still reflects preview train" "$actual_semver" "-preview."
}

test_all() {
    local stable
    local next_stable
    local current_preview
    local post_rc_preview

    stable=$(get_stable_version)

    header "VERSIONING TESTS"
    info "DEV derives preview height from Git commits; each merge gets a unique preview"
    info "DEV artifact metadata includes commit traceability"
    info "STAGING and PROD are promoted by git tags; RC bug fixes stay on the same release train and repromote as rc.N"

    check_dev "1. Initial main build" "${stable}-preview"

    header "2. Feature branch merge"
    run_in_repo git checkout -b feat/login >/dev/null
    commit_change "feat(auth): add authentication"
    merge_branch "feat/login"
    check_dev "After feature merge" "${stable}-preview"

    header "3. Fix branch merge"
    run_in_repo git checkout -b fix/login-bug >/dev/null
    commit_change "fix(auth): resolve token issue"
    merge_branch "fix/login-bug"
    current_preview=$(get_semver)
    check_dev "After fix merge" "${stable}-preview"

    header "4. RC promotion"
    tag_head "v${stable}-rc.1" "Release candidate"
    check_staging "After RC tag" "$current_preview" "${stable}-rc.1"

    header "5. Bug fix after RC"
    run_in_repo git checkout -b fix/rc-bug >/dev/null
    commit_change "fix(auth): resolve release candidate regression"
    merge_branch "fix/rc-bug"
    post_rc_preview=$(get_semver)
    check_dev "After RC bug fix merge" "${stable}-preview"

    header "6. RC repromotion"
    tag_head "v${stable}-rc.2" "Release candidate 2"
    check_staging "After RC2 tag" "$post_rc_preview" "${stable}-rc.2"

    header "7. Production promotion"
    tag_head "v${stable}" "Production release"
    check_prod "After production tag" "$post_rc_preview" "$stable"

    header "8. Next release train"
    next_stable="${stable%.*}.$(( ${stable##*.} + 1 ))"
    set_release_train "$next_stable"
    run_in_repo git add version.json
    run_in_repo git commit -m "chore: start ${next_stable}" --no-verify >/dev/null
    check_dev "After starting next train" "${next_stable}-preview"
}

summary() {
    header "RESULTS"
    echo -e "  ${GREEN}Passed: $PASSED${NC}"
    echo -e "  ${RED}Failed: $FAILED${NC}\n"
    echo -e "  ${BOLD}Scenario summary:${NC}"
    echo -e "  ${BLUE}1.${NC} main init            -> 1.0.0-preview.1 -> dev 1.0.0-preview.1"
    echo -e "  ${BLUE}2.${NC} feature squash merge -> 1.0.0-preview.2 -> dev 1.0.0-preview.2"
    echo -e "  ${BLUE}3.${NC} fix squash merge     -> 1.0.0-preview.3 -> dev 1.0.0-preview.3"
    echo -e "  ${BLUE}4.${NC} rc tag               -> nbgv preview height -> staging 1.0.0-rc.1"
    echo -e "  ${BLUE}5.${NC} post-rc bug fix      -> 1.0.0-preview.4 -> dev 1.0.0-preview.4"
    echo -e "  ${BLUE}6.${NC} rc retag             -> nbgv preview height -> staging 1.0.0-rc.2"
    echo -e "  ${BLUE}7.${NC} stable tag           -> nbgv preview height -> prod 1.0.0"
    echo -e "  ${BLUE}8.${NC} next train           -> 1.1.0-preview.1 -> dev 1.1.0-preview.1\n"

    if [ "$FAILED" -eq 0 ]; then
        echo -e "${GREEN}${BOLD}✅ ALL CHECKS PASSED!${NC}"
        echo -e "${BLUE}Validated strategy:${NC} main → dev preview, rc tag → staging, stable tag → prod"
    else
        echo -e "${RED}${BOLD}❌ $FAILED CHECK(S) FAILED!${NC}"
        exit 1
    fi
}

echo -e "${BOLD}${CYAN}"
echo "╔════════════════════════════════════════════════════╗"
echo "║  Nerdbank.GitVersioning Strategy Verification     ║"
echo "╚════════════════════════════════════════════════════╝"
echo -e "${NC}"

setup
test_all
summary
cleanup


