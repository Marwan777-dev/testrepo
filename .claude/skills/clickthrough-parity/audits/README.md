# Recorded whole-module parity audits

One `<feature>.json` per module that has passed a **full-module** (bare-feature, no phase)
click-through parity audit. Written by `../record-audit.py`; read by
`.claude/hooks/parity-gate.py`, which blocks `git push` to `main`/`master` until the finished
module has a current stamp.

A stamp pins the audited commit. It goes **stale** — and the gate asks for a re-run — as soon as
anything under `frontend/src` changes; a backend or docs commit does not invalidate it.

**Do not hand-write or hand-edit these files.** The stamp exists to prove an audit happened; a
forged one turns the release gate into decoration. Per-phase runs never record here either — a
phase saw a slice of the routes, so it cannot clear the module.
