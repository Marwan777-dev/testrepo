#!/usr/bin/env python3
"""Record that a WHOLE-MODULE click-through parity audit was completed.

Run at the very end of a bare-feature (no phase) parity run:

    python3 .claude/skills/clickthrough-parity/record-audit.py <feature> \
        --routes 4 --defects 26 --discussion 7 --provenance blind

The stamp is what `.claude/hooks/parity-gate.py` checks before letting a push reach main/master.
It pins the audited commit, so a later change under `frontend/src` marks the audit stale and the
gate asks for a re-run — a docs or backend commit does not.

Only a genuine whole-module run may record a stamp:
  * a PHASE run must not — it saw a slice of the routes, so it cannot clear the module;
  * a run whose implementation was PORTED from the click-through must not — that run is VOID, and
    `--provenance ported` is refused outright rather than written as a passing gate.
"""

import argparse
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone

SKILL_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(SKILL_DIR)))
AUDIT_DIR = os.path.join(SKILL_DIR, "audits")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("feature", help="Spec feature folder, e.g. 006-integration-hub")
    parser.add_argument("--routes", type=int, required=True, help="Page-bearing routes compared")
    parser.add_argument("--defects", type=int, required=True, help="Defects reported")
    parser.add_argument("--discussion", type=int, default=0, help="Needs-discussion items")
    parser.add_argument(
        "--provenance", choices=["blind", "ported", "unknown"], required=True,
        help="'blind' = the implementation never read the click-through. Anything else is refused.",
    )
    parser.add_argument("--notes", default="", help="One line for the record")
    args = parser.parse_args()

    if not re.match(r"^\d{3}-[a-z0-9-]+$", args.feature):
        sys.exit(f"error: '{args.feature}' is not a spec feature folder name (NNN-slug).")

    if args.provenance != "blind":
        sys.exit(
            "refused: a parity run whose implementation was ported from the click-through (or whose\n"
            "provenance is unknown) is VOID, not clean — it compared the reference against itself.\n"
            "Recording it would make the push gate pass on an audit that measured nothing. Rebuild\n"
            "the pages click-through-blind, re-audit, then record."
        )

    head = subprocess.run(
        ["git", "-C", REPO, "rev-parse", "HEAD"], capture_output=True, text=True,
    )
    if head.returncode != 0:
        sys.exit(
            "error: cannot read HEAD — this working copy is not a git repository, so there is no\n"
            "commit to pin the audit to. Record from the checkout you actually push from."
        )

    os.makedirs(AUDIT_DIR, exist_ok=True)
    stamp = {
        "feature": args.feature,
        "scope": "whole-module",
        "provenance": "click-through-blind",
        "head_sha": head.stdout.strip(),
        "recorded_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "routes_compared": args.routes,
        "defects": args.defects,
        "needs_discussion": args.discussion,
        "notes": args.notes,
    }
    path = os.path.join(AUDIT_DIR, f"{args.feature}.json")
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(stamp, handle, indent=2)
        handle.write("\n")

    print(f"recorded {path}")
    print(f"  {args.feature} @ {stamp['head_sha'][:12]} — {args.routes} routes, "
          f"{args.defects} defects, {args.discussion} needs-discussion")
    print("  pushes to main/master for this module are now unblocked until frontend/src changes.")


if __name__ == "__main__":
    main()
