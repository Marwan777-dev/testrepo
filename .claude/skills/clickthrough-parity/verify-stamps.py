#!/usr/bin/env python3
"""Verify every module touched by a change range carries a CURRENT whole-module parity stamp.

    python3 .claude/skills/clickthrough-parity/verify-stamps.py --base origin/main --head HEAD

This is the *verification* half of the parity gate, and it is the only half that can run headless.
It never performs an audit: the audit needs the private click-through checkout, two live
authenticated servers, and a model reading screenshots, so it stays a local `/clickthrough-parity`
run. This script only checks the receipt that run leaves behind — which is exactly what CI and a
`pre-push` hook need.

Two things it does better than `.claude/hooks/parity-gate.py`:

  * **It resolves the module from the changed files**, via the `frontend/src/features/<slug>`
    paths each `specs/<feature>/tasks.md` declares — instead of falling back to "the
    newest page-bearing spec", which names the wrong module whenever you push from `main`.
  * **Staleness is scoped to the module's own folders.** The push hook diffs all of
    `frontend/src`, so another module's commit invalidates your stamp for no reason. Here a
    stamp goes stale only when *its own* pages changed.

Exit 0 = every touched module is audited and current. Exit 1 = at least one is missing or stale.
"""

import argparse
import json
import os
import re
import subprocess
import sys

SKILL_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(SKILL_DIR)))
AUDIT_DIR = os.path.join(SKILL_DIR, "audits")
SPECS_DIR = os.path.join(REPO, "specs")
FEATURE_PATH = re.compile(r"frontend/src/features/[a-z0-9-]+")


def git(*args):
    return subprocess.run(
        ["git", "-C", REPO, *args], capture_output=True, text=True, timeout=60
    )


def owned_paths_by_feature():
    """feature-folder -> the frontend/src/features/* paths its tasks.md claims.

    tasks.md is the declaration of what a module ships, so it is also the honest answer to
    "which module owns this file". A folder claimed by two specs (kpi-management is claimed by
    both 003 and 005) puts a change in scope for BOTH — each has its own pages there.
    """
    owners = {}
    if not os.path.isdir(SPECS_DIR):
        return owners
    for feature in sorted(os.listdir(SPECS_DIR)):
        if not re.match(r"^\d{3}-[a-z0-9-]+$", feature):
            continue
        tasks = os.path.join(SPECS_DIR, feature, "tasks.md")
        try:
            with open(tasks, encoding="utf-8") as handle:
                text = handle.read()
        except OSError:
            # No tasks.md => we cannot tell what this module ships. Deliberately NOT treated as
            # "backend-only" (that is the push hook's fail-open bug): report it and let the
            # caller decide, rather than silently exempting an un-specced module.
            owners[feature] = None
            continue
        paths = sorted(set(FEATURE_PATH.findall(text)))
        owners[feature] = paths
    return owners


def changed_frontend_files(base, head):
    result = git("diff", "--name-only", "%s...%s" % (base, head), "--", "frontend/src")
    if result.returncode != 0:  # not an ancestor pair — fall back to a plain two-dot diff
        result = git("diff", "--name-only", base, head, "--", "frontend/src")
    if result.returncode != 0:
        sys.exit("error: cannot diff %s..%s (%s)" % (base, head, result.stderr.strip()[:200]))
    return [line for line in result.stdout.splitlines() if line.strip()]


def check_stamp(feature, owned, head):
    """Return a failure string, or None when the module's stamp is present and current."""
    run_cmd = "/clickthrough-parity %s" % feature
    path = os.path.join(AUDIT_DIR, "%s.json" % feature)

    if not os.path.isfile(path):
        return ("%s — NO AUDIT RECORDED.\n"
                "    This PR changes its pages, so it must be compared against the click-through\n"
                "    as a whole before it ships.  Run:  %s\n"
                "    (bare feature, NO phase), then commit "
                ".claude/skills/clickthrough-parity/audits/%s.json"
                % (feature, run_cmd, feature))

    try:
        with open(path, encoding="utf-8") as handle:
            stamp = json.load(handle)
    except Exception as exc:
        return "%s — stamp is unreadable (%s). Re-run %s." % (feature, exc, run_cmd)

    if stamp.get("scope") != "whole-module":
        return ("%s — stamp scope is %r, not 'whole-module'. A phase run sees a slice of the\n"
                "    routes and cannot clear the module. Re-run %s."
                % (feature, stamp.get("scope"), run_cmd))

    if stamp.get("provenance") != "click-through-blind":
        return ("%s — stamp provenance is %r. A run whose pages were ported from the\n"
                "    click-through compared the reference against itself and is VOID."
                % (feature, stamp.get("provenance")))

    sha = stamp.get("head_sha")
    if not sha:
        return "%s — stamp has no head_sha, so it pins nothing. Re-run %s." % (feature, run_cmd)

    if git("cat-file", "-e", "%s^{commit}" % sha).returncode != 0:
        return ("%s — the audited commit %s is not in this history.\n"
                "    In CI this usually means a shallow clone: set `fetch-depth: 0` on checkout."
                % (feature, sha[:12]))

    if not owned:
        return None  # module claims no frontend folders — nothing of its own can go stale

    changed = git("diff", "--quiet", sha, head, "--", *owned)
    if changed.returncode == 1:
        files = git("diff", "--name-only", sha, head, "--", *owned).stdout.split()
        return ("%s — audit is STALE.\n"
                "    Recorded at %s (%s); %d of its own file(s) changed since:\n%s\n"
                "    Re-run %s."
                % (feature, sha[:12], stamp.get("recorded_at", "unknown"), len(files),
                   "\n".join("      %s" % f for f in files[:10]), run_cmd))
    if changed.returncode != 0:
        return ("%s — could not diff its pages against %s (%s). Re-run %s."
                % (feature, sha[:12], changed.stderr.strip()[:120], run_cmd))
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True, help="Merge base / previous ref")
    parser.add_argument("--head", default="HEAD", help="Ref being promoted (default HEAD)")
    parser.add_argument(
        "--list-failing", action="store_true",
        help="Print only the feature folders needing an audit, one per line, and exit 0. "
             "For the pre-push hook, which then runs the audit for each.",
    )
    args = parser.parse_args()

    quiet = args.list_failing

    def say(*parts):
        if not quiet:
            print(*parts)

    changed = changed_frontend_files(args.base, args.head)
    if not changed:
        say("parity: no frontend/src changes in %s..%s — nothing to audit."
            % (args.base, args.head))
        return 0

    owners = owned_paths_by_feature()
    in_scope = {}
    attributed = set()
    for feature, owned in owners.items():
        if not owned:
            continue
        hit = [f for f in changed if any(f.startswith(p) for p in owned)]
        if hit:
            in_scope[feature] = owned
            attributed.update(hit)

    unspecced = [f for f, owned in owners.items() if owned is None]
    if unspecced:
        say("parity: WARNING — no tasks.md, so ownership is unknown for: %s"
            % ", ".join(unspecced))

    shared = sorted(set(changed) - attributed)
    if shared:
        say("parity: %d shared/unattributed frontend file(s) changed (not owned by any spec's "
            "tasks.md):" % len(shared))
        for f in shared[:10]:
            say("    %s" % f)
        say("    These are cross-cutting; no single module's audit covers them.")

    if not in_scope:
        say("parity: no module's own pages changed — nothing to verify.")
        return 0

    say("parity: modules in scope — %s" % ", ".join(sorted(in_scope)))
    results = [(f, check_stamp(f, owned, args.head)) for f, owned in sorted(in_scope.items())]
    failures = [(f, msg) for f, msg in results if msg]

    if args.list_failing:
        for feature, _ in failures:
            print(feature)
        return 0

    if failures:
        print("\nClick-through parity check FAILED:\n")
        for _, msg in failures:
            print("  * %s\n" % msg)
        print("The audit itself cannot run here — it needs the click-through checkout and a live\n"
              "signed-in stack. Run it locally, commit the stamp, and push again.")
        return 1

    print("parity: OK — every module in scope has a current whole-module audit.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
