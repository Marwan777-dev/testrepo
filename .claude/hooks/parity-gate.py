#!/usr/bin/env python3
"""Release gate: block `git push` to main/master until the finished module's full-module
click-through parity audit has been recorded.

Why a push gate and not CI: the parity audit needs the private click-through checkout, two live
authenticated servers, AND a model reading screenshots to judge layout/text/controls/behaviour.
There is no headless assertion for it, so it cannot run in GitHub Actions — the enforceable point
is the moment someone promotes a finished module.

Scope, deliberately narrow:
  * ONLY pushes that target main/master are gated. Feature-branch pushes are never touched —
    pushing to main is what "the module is finished" means in this team's flow.
  * ONLY modules that actually ship pages. A backend-only module has nothing to audit.
  * The stamp is invalidated by later changes under frontend/src, not by any commit, so a docs or
    backend commit after the audit does not force a re-run.

Recorded by `.claude/skills/clickthrough-parity/record-audit.py`, which the parity skill runs at
the end of a whole-module (bare-feature) audit.

Fail-open vs fail-closed: if we cannot even tell that the push targets main/master, allow (a hook
that misreads an unusual refspec must not wedge every push). Once we know it IS main/master, an
unverifiable stamp denies — that is the whole point of the gate.
"""

import json
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AUDIT_DIR = os.path.join(REPO, ".claude", "skills", "clickthrough-parity", "audits")
RELEASE_BRANCHES = ("main", "master")


def allow():
    """Say nothing and get out of the way — the overwhelmingly common path."""
    sys.exit(0)


def deny(reason):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": reason,
        }
    }))
    sys.exit(0)


def git(*args):
    try:
        return subprocess.run(
            ["git", "-C", REPO, *args],
            capture_output=True, text=True, timeout=15,
        )
    except Exception:
        return None


def push_segments(command):
    """Every `git … push …` segment of the command, as token lists.

    Split on shell separators first, so `npm test && git push origin main` is still gated. Within a
    segment the test is positional — `git` appears before a `push` token — rather than a regex on
    the flags between them: `git -C /path push origin main` slipped a flag-matching regex entirely
    (caught in the pipe test), and so would `git --git-dir=… push`. Over-matching here is the safe
    direction; the branch check below is what actually narrows it.
    """
    segments = []
    for raw in re.split(r"&&|\|\||[;|\n]", command):
        tokens = raw.split()
        if "git" not in tokens or "push" not in tokens:
            continue
        if tokens.index("push") > tokens.index("git"):
            segments.append(tokens)
    return segments


def targets_release_branch(tokens):
    """True when this push lands on main/master.

    Covers the explicit forms (`git push origin main`, `git push origin HEAD:main`), the bare
    `git push` while main/master is checked out, and the blunderbuss flags that necessarily
    include it (`--all`, `--mirror`).
    """
    if "--all" in tokens or "--mirror" in tokens:
        return True

    for token in tokens[tokens.index("push") + 1:]:
        if token.startswith("-"):
            continue
        ref = token.split(":")[-1]                      # HEAD:main -> main
        ref = re.sub(r"^refs/heads/", "", ref)
        if ref in RELEASE_BRANCHES:
            return True

    # No refspec named a branch — fall back to what is checked out.
    head = git("rev-parse", "--abbrev-ref", "HEAD")
    if head and head.returncode == 0:
        return head.stdout.strip() in RELEASE_BRANCHES
    return False


def page_bearing(feature_dir):
    """A module needs an audit only if its tasks.md ships frontend pages."""
    tasks = os.path.join(REPO, "specs", feature_dir, "tasks.md")
    try:
        with open(tasks, encoding="utf-8") as handle:
            return "frontend/src" in handle.read()
    except OSError:
        return False


def resolve_feature():
    """The module being promoted: the spec-kit feature branch, else the newest page-bearing spec."""
    head = git("rev-parse", "--abbrev-ref", "HEAD")
    branch = head.stdout.strip() if head and head.returncode == 0 else ""
    slug = branch.split("/")[-1]
    specs = os.path.join(REPO, "specs")
    if not os.path.isdir(specs):
        return None
    features = sorted(d for d in os.listdir(specs) if re.match(r"^\d{3}-", d))
    if slug in features:
        return slug
    # On main itself the branch name says nothing — take the newest page-bearing spec folder.
    for candidate in reversed(features):
        if page_bearing(candidate):
            return candidate
    return None


def main():
    try:
        payload = json.load(sys.stdin)
    except Exception:
        allow()

    command = (payload.get("tool_input") or {}).get("command") or ""
    segments = push_segments(command)
    if not segments:
        allow()
    if not any(targets_release_branch(tokens) for tokens in segments):
        allow()

    # Delegate to the shared verifier so this hook, the .githooks/pre-push hook, and CI all apply
    # the SAME rules: the module is resolved from the changed files (not "newest spec folder"),
    # and staleness is scoped to that module's own pages (not all of frontend/src).
    verifier = os.path.join(
        REPO, ".claude", "skills", "clickthrough-parity", "verify-stamps.py"
    )
    if os.path.isfile(verifier):
        base = None
        for candidate in ("@{upstream}", "origin/HEAD"):
            probe = git("rev-parse", "--verify", "--quiet", candidate)
            if probe and probe.returncode == 0 and probe.stdout.strip():
                base = probe.stdout.strip()
                break
        if base is None:
            empty = git("hash-object", "-t", "tree", os.devnull)
            base = empty.stdout.strip() if empty and empty.returncode == 0 else None
        if base:
            result = subprocess.run(
                [sys.executable, verifier, "--base", base, "--head", "HEAD"],
                capture_output=True, text=True, timeout=60,
            )
            if result.returncode == 0:
                allow()
            deny(
                "Push to main/master blocked — click-through parity.\n\n"
                + (result.stdout or result.stderr).strip()
                + "\n\nRun the audit first, in Claude Code:\n"
                  "    /clickthrough-parity <module>      (bare feature, NO phase)\n\n"
                "Then commit the stamp it writes under\n"
                "    .claude/skills/clickthrough-parity/audits/\n"
                "and push again."
            )

    # Fallback: the verifier is absent (older checkout) — use the original resolution.
    feature = resolve_feature()
    if feature is None or not page_bearing(feature):
        allow()   # backend-only module (or no spec resolvable) — nothing to compare

    stamp_path = os.path.join(AUDIT_DIR, f"{feature}.json")
    run_cmd = f"/clickthrough-parity {feature}"

    if not os.path.isfile(stamp_path):
        deny(
            f"Push to main/master blocked: no full-module click-through parity audit is recorded "
            f"for {feature}.\n\n"
            f"Pushing to main means the module is finished, so it must be compared against the "
            f"click-through as a whole before it ships.\n\n"
            f"Run:  {run_cmd}\n"
            f"      (bare feature, NO phase — whole-module scope is the only scope that sees "
            f"cross-page placement differences)\n\n"
            f"Triage the report, apply what the frontend lead accepts with --fix, then the audit "
            f"records itself and this push proceeds. Backend-only modules are never gated."
        )

    try:
        with open(stamp_path, encoding="utf-8") as handle:
            stamp = json.load(handle)
    except Exception:
        deny(f"Push blocked: {stamp_path} is unreadable or not valid JSON. Re-run {run_cmd}.")

    audited_sha = stamp.get("head_sha")
    if not audited_sha:
        deny(f"Push blocked: the recorded audit for {feature} has no head_sha. Re-run {run_cmd}.")

    changed = git("diff", "--quiet", audited_sha, "HEAD", "--", "frontend/src")
    if changed is None:
        deny(
            f"Push blocked: cannot verify the {feature} parity audit is still current (git "
            f"unavailable). Re-run {run_cmd}."
        )
    if changed.returncode == 1:
        deny(
            f"Push to main/master blocked: {feature}'s parity audit is STALE.\n\n"
            f"It was recorded at {audited_sha[:12]} ({stamp.get('recorded_at', 'unknown time')}), "
            f"and frontend/src has changed since — so the report no longer describes what you are "
            f"about to ship.\n\n"
            f"Re-run:  {run_cmd}"
        )
    if changed.returncode != 0:
        deny(
            f"Push blocked: could not diff frontend/src against the audited commit "
            f"{audited_sha[:12]} ({changed.stderr.strip()[:200]}). Re-run {run_cmd}."
        )

    allow()


if __name__ == "__main__":
    main()
