---
name: "speckit-implement"
description: "Execute the implementation plan by processing and executing all tasks defined in tasks.md"
argument-hint: "Optional implementation guidance or task filter"
compatibility: "Requires spec-kit project structure with .specify/ directory"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/implement.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

**Check for extension hooks (before implementation)**:
- Check if `.specify/extensions.yml` exists in the project root.
- If it exists, read it and look for entries under the `hooks.before_implement` key
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- When constructing slash commands from hook command names, replace dots (`.`) with hyphens (`-`). For example, `speckit.git.commit` → `/speckit-git-commit`.
- For each executable hook, output the following based on its `optional` flag:
  - **Optional hook** (`optional: true`):
    ```
    ## Extension Hooks

    **Optional Pre-Hook**: {extension}
    Command: `/{command}`
    Description: {description}

    Prompt: {prompt}
    To execute: `/{command}`
    ```
  - **Mandatory hook** (`optional: false`) — **ACTUALLY RUN IT YOURSELF with the Skill tool**
    (`Skill(skill: "{command-with-dots-replaced-by-hyphens}", args: "…")`) and wait for its result
    before proceeding to the Outline. Announce it first:
    ```
    ## Extension Hooks

    **Automatic Pre-Hook**: {extension}
    Running: `/{command}`
    ```
    ⚠️ **Never print `EXECUTE_COMMAND: {command}` and treat the hook as dispatched** — no
    host-side hook runner exists in this repo, so that line runs nothing (see the same warning
    under Mandatory Post-Execution Hooks). If the command maps to no invocable skill, say so
    plainly instead of emitting a marker.
- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently

## Outline

> **Testing flow (BINDING — see CLAUDE.md "Unit Test Policy" + "E2E Test Policy").** Execute each backend story's **Unit Tests** subsection before its implementation tasks; run the **Red Checkpoint** in between — verify a valid red state (compile error if the type doesn't exist yet, else assertion failure), or a **green baseline** on a retrofit where the code already exists — and commit the baseline before any implementation task. If a `test(USx): red baseline` commit already exists, do not re-author the tests. Run **Integration/Scenario** at the per-story checkpoint (Docker up). For page-bearing frontend stories, run the **E2E** filter at the checkpoint against the running stack (start DB + backend + SPA dev server; set `E2E_BASE_URL` to match the dev-server scheme; ensure Playwright browsers installed). E2E credentials are inputs — if a required key is missing, STOP and ASK; never seed an account. Frontend build gate = the SPA build script AND the E2E filter green. **Click-through parity is a TASK, not a step of this skill (changed 2026-09-03).** A page-bearing frontend story's tasks.md carries a **`Click-through Parity for User Story X 🎨`** subsection after its E2E subsection, plus one full-module task in the Polish phase. **Do NOT run `/clickthrough-parity` from this skill, and do not treat it as part of the build gate.** It is owned by the frontend developer and run by hand once they are ready to triage the defect list — firing it automatically delivered reports nobody was sitting down to act on. What this skill owes instead: leave the parity task **unchecked**, and say so in the Completion Report (e.g. *"T0xx — click-through parity for US2's routes — is pending; run `/clickthrough-parity <feature> phase <N>` when ready to triage"*). If the user explicitly asks for the audit in the same session, run it then — an explicit request is not the automatic firing this rule removes.

> **Timing (BINDING).** Report the start time, end time, and per-task duration for this run.
> Timestamps MUST be read from the shell — never estimated or hallucinated. Get one with
> `date '+%Y-%m-%d %H:%M:%S'` (Bash) or `Get-Date -Format 'yyyy-MM-dd HH:mm:ss'` (PowerShell).
> - **Run start = prompt start.** Capture the run-start timestamp as the very FIRST action of
>   this skill, BEFORE the prerequisite check and any context-gathering / file reads — the clock
>   baselines at prompt start, not at first-task work. Record it verbatim.
> - **Per task:** capture a timestamp immediately before starting each task `T-N` and immediately
>   after it is verified complete; the task's duration is the difference. Tasks run in parallel
>   (`[P]`) share wall-clock — note that rather than double-counting their durations.
> - **Run end:** capture a final timestamp right before writing the Completion Report.
> - Surface all of it in the Completion Report's timing table (see that section). If a run is
>   resumed or interrupted, report the timing for the tasks actually executed in this pass.

1. Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute. For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

2. **Check checklists status** (if FEATURE_DIR/checklists/ exists):
   - Scan all checklist files in the checklists/ directory
   - For each checklist, count:
     - Total items: All lines matching `- [ ]` or `- [X]` or `- [x]`
     - Completed items: Lines matching `- [X]` or `- [x]`
     - Incomplete items: Lines matching `- [ ]`
   - Create a status table:

     ```text
     | Checklist | Total | Completed | Incomplete | Status |
     |-----------|-------|-----------|------------|--------|
     | ux.md     | 12    | 12        | 0          | ✓ PASS |
     | test.md   | 8     | 5         | 3          | ✗ FAIL |
     | security.md | 6   | 6         | 0          | ✓ PASS |
     ```

   - Calculate overall status:
     - **PASS**: All checklists have 0 incomplete items
     - **FAIL**: One or more checklists have incomplete items

   - **If any checklist is incomplete**:
     - Display the table with incomplete item counts
     - **STOP** and ask: "Some checklists are incomplete. Do you want to proceed with implementation anyway? (yes/no)"
     - Wait for user response before continuing
     - If user says "no" or "wait" or "stop", halt execution
     - If user says "yes" or "proceed" or "continue", proceed to step 3

   - **If all checklists are complete**:
     - Display the table showing all checklists passed
     - Automatically proceed to step 3

3. Load and analyze the implementation context:
   - **REQUIRED**: Read tasks.md for the complete task list and execution plan
   - **REQUIRED**: Read plan.md for tech stack, architecture, and file structure
   - **IF EXISTS**: Read data-model.md for entities and relationships
   - **IF EXISTS**: Read contracts/ for API specifications and test requirements
   - **IF EXISTS**: Read research.md for technical decisions and constraints
   - **IF EXISTS**: Read .specify/memory/constitution.md for governance constraints
   - **IF EXISTS**: Read quickstart.md for integration scenarios
   - **REQUIRED FOR FRONTEND TASKS**: Before writing any TSX / UI code under `frontend/`, re-read and follow the **Nabadat Design System** in the project-root `CLAUDE.md` (color tokens, Two-Palette Rule, RTL logical properties, Component Sourcing Rule, button hierarchy). It is auto-loaded into context but MUST be treated as binding for every frontend task — do not rely on ambient recall during long runs.

4. **Project Setup Verification**:
   - **REQUIRED**: Create/verify ignore files based on actual project setup:

   **Detection & Creation Logic**:
   - Check if the following command succeeds to determine if the repository is a git repo (create/verify .gitignore if so):

     ```sh
     git rev-parse --git-dir 2>/dev/null
     ```

   - Check if Dockerfile* exists or Docker in plan.md → create/verify .dockerignore
   - Check if .eslintrc* exists → create/verify .eslintignore
   - Check if eslint.config.* exists → ensure the config's `ignores` entries cover required patterns
   - Check if .prettierrc* exists → create/verify .prettierignore
   - Check if .npmrc or package.json exists → create/verify .npmignore (if publishing)
   - Check if terraform files (*.tf) exist → create/verify .terraformignore
   - Check if .helmignore needed (helm charts present) → create/verify .helmignore

   **If ignore file already exists**: Verify it contains essential patterns, append missing critical patterns only
   **If ignore file missing**: Create with full pattern set for detected technology

   **Common Patterns by Technology** (from plan.md tech stack):
   - **Node.js/JavaScript/TypeScript**: `node_modules/`, `dist/`, `build/`, `*.log`, `.env*`
   - **Python**: `__pycache__/`, `*.pyc`, `.venv/`, `venv/`, `dist/`, `*.egg-info/`
   - **Java**: `target/`, `*.class`, `*.jar`, `.gradle/`, `build/`
   - **C#/.NET**: `bin/`, `obj/`, `*.user`, `*.suo`, `packages/`
   - **Go**: `*.exe`, `*.test`, `vendor/`, `*.out`
   - **Ruby**: `.bundle/`, `log/`, `tmp/`, `*.gem`, `vendor/bundle/`
   - **PHP**: `vendor/`, `*.log`, `*.cache`, `*.env`
   - **Rust**: `target/`, `debug/`, `release/`, `*.rs.bk`, `*.rlib`, `*.prof*`, `.idea/`, `*.log`, `.env*`
   - **Kotlin**: `build/`, `out/`, `.gradle/`, `.idea/`, `*.class`, `*.jar`, `*.iml`, `*.log`, `.env*`
   - **C++**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.so`, `*.a`, `*.exe`, `*.dll`, `.idea/`, `*.log`, `.env*`
   - **C**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.a`, `*.so`, `*.exe`, `*.dll`, `autom4te.cache/`, `config.status`, `config.log`, `.idea/`, `*.log`, `.env*`
   - **Swift**: `.build/`, `DerivedData/`, `*.swiftpm/`, `Packages/`
   - **R**: `.Rproj.user/`, `.Rhistory`, `.RData`, `.Ruserdata`, `*.Rproj`, `packrat/`, `renv/`
   - **Universal**: `.DS_Store`, `Thumbs.db`, `*.tmp`, `*.swp`, `.vscode/`, `.idea/`

   **Tool-Specific Patterns**:
   - **Docker**: `node_modules/`, `.git/`, `Dockerfile*`, `.dockerignore`, `*.log*`, `.env*`, `coverage/`
   - **ESLint**: `node_modules/`, `dist/`, `build/`, `coverage/`, `*.min.js`
   - **Prettier**: `node_modules/`, `dist/`, `build/`, `coverage/`, `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`
   - **Terraform**: `.terraform/`, `*.tfstate*`, `*.tfvars`, `.terraform.lock.hcl`
   - **Kubernetes/k8s**: `*.secret.yaml`, `secrets/`, `.kube/`, `kubeconfig*`, `*.key`, `*.crt`

5. Parse tasks.md structure and extract:
   - **Task phases**: Setup, Tests, Core, Integration, Polish
   - **Task dependencies**: Sequential vs parallel execution rules
   - **Task details**: ID, description, file paths, parallel markers [P]
   - **Execution flow**: Order and dependency requirements

6. Execute implementation following the task plan:
   - **Phase-by-phase execution**: Complete each phase before moving to the next
   - **Respect dependencies**: Run sequential tasks in order, parallel tasks [P] can run together  
   - **Follow TDD approach**: Execute test tasks before their corresponding implementation tasks
   - **File-based coordination**: Tasks affecting the same files must run sequentially
   - **Validation checkpoints**: Verify each phase completion before proceeding

7. Implementation execution rules:
   - **Setup first**: Initialize project structure, dependencies, configuration
   - **Tests before code**: If you need to write tests for contracts, entities, and integration scenarios
   - **Core development**: Implement models, services, CLI commands, endpoints
   - **Integration work**: Database connections, middleware, logging, external services
   - **Polish and validation**: Unit tests, performance optimization, documentation

8. Progress tracking and error handling:
   - Report progress after each completed task, **including its start time, end time, and
     duration** (read from the shell per the Timing note above — do not estimate)
   - Halt execution if any non-parallel task fails
   - For parallel tasks [P], continue with successful tasks, report failed ones
   - Provide clear error messages with context for debugging
   - Suggest next steps if implementation cannot proceed
   - **IMPORTANT** For completed tasks, make sure to mark the task off as [X] in the tasks file.

9. Completion validation:
   - Verify all required tasks are completed
   - Check that implemented features match the original specification
   - Validate that tests pass and coverage meets requirements
   - Confirm the implementation follows the technical plan

Note: This command assumes a complete task breakdown exists in tasks.md. If tasks are incomplete or missing, suggest running `/speckit-tasks` first to regenerate the task list.

## Mandatory Post-Execution Hooks

**You MUST complete this section before reporting completion to the user.**

Check if `.specify/extensions.yml` exists in the project root.
- If it does not exist, or no hooks are registered under `hooks.after_implement`, skip to the Completion Report.
- If it exists, read it and look for entries under the `hooks.after_implement` key.
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue to the Completion Report.
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- When constructing slash commands from hook command names, replace dots (`.`) with hyphens (`-`). For example, `speckit.git.commit` → `/speckit-git-commit`.
- For each executable hook, output the following based on its `optional` flag:
  - **Mandatory hook** (`optional: false`) — **ACTUALLY RUN IT YOURSELF. Invoke the skill with the
    Skill tool** (`Skill(skill: "{command-with-dots-replaced-by-hyphens}", args: "…")`), then report
    its result inline before the Completion Report. Announce it first:
    ```
    ## Extension Hooks

    **Automatic Hook**: {extension}
    Running: `/{command}`
    ```
    ⚠️ **Do NOT print `EXECUTE_COMMAND: {command}` and stop there.** That line is a marker for a
    host-side hook runner, and **this repo has no such runner** — nothing consumes it, so the hook
    is announced and silently never runs. A mandatory hook is binding on *you*: invoke the skill
    with the Skill tool and report its result. If the command maps to no invocable skill, say so
    plainly instead of emitting a marker.
    (Historical note, so the rule above is not mistaken for the parity case: on 2026-09-02 the
    then-mandatory `clickthrough-parity` after-hook was printed as `EXECUTE_COMMAND`, nothing ran
    it, and the user had to ask for the report by hand — which is how this rule was found. That
    hook is **now `optional: true` by design**, because the audit moved into tasks.md as assigned
    frontend work; the general rule stands for any hook that is still mandatory.)
  - **Optional hook** (`optional: true`):
    ```
    ## Extension Hooks

    **Optional Hook**: {extension}
    Command: `/{command}`
    Description: {description}

    Prompt: {prompt}
    To execute: `/{command}`
    ```

## Completion Report

Report final status with summary of completed work.

**Timing summary (REQUIRED).** Include a table of the tasks executed in this pass, one row per
task, plus a totals line. All values come from the shell timestamps captured during the run (per
the Timing note in the Outline) — never estimated. Format:

```text
Run start: 2026-07-16 09:12:03   (captured at prompt start, before context-gathering)
Run end:   2026-07-16 09:41:57
Total:     29m 54s

| Task | Description                     | Start    | End      | Duration |
|------|---------------------------------|----------|----------|----------|
| T001 | SurveyBuilder.csproj            | 09:14:20 | 09:18:05 | 3m 45s   |
| T002 | UnitTests.csproj                | 09:18:05 | 09:19:40 | 1m 35s   |
| …    | …                               | …        | …        | …        |
```

- Report the **Run start** as the prompt-start timestamp (before the prerequisite check), and note
  that context-gathering time is included in the total.
- For parallel `[P]` tasks that overlapped, say so (e.g. "T002–T005 ran concurrently") instead of
  summing their individual durations into the total — the total is wall-clock (Run end − Run start).

## Done When

- [ ] All tasks in tasks.md completed and marked `[X]`
- [ ] Implementation validated against specification, plan, and test coverage
- [ ] Extension hooks dispatched or skipped according to the rules in Mandatory Post-Execution Hooks above
- [ ] Completion reported to user with summary of completed work, **including the timing summary
      table (run start, run end, total, and per-task durations) read from shell timestamps**


## Deferred & Gap Tracking (TODO.md)

Before implementing any task `T-N` from the current spec's `tasks.md`:

1. Identify the current module code from the `**Module**` line in `tasks.md`'s header.
2. Search `TODO.md` for any `OPEN` or `READY` entry whose `Blocked by` field names a task that
   `T-N` depends on, or whose `Created by task` is `T-N` itself from a prior partial pass.
   - If found and still unresolved, tell the user before writing code:
     "T-N has an open dependency ({ID}): {one-line summary}. I'll implement with the same stub
     pattern unless you want the blocker landed first."
3. Search `TODO.md` for any entry whose `Blocked by` field names `T-N`. If found, finishing
   `T-N` should also resolve that earlier stub — tell the user:
     "Finishing T-N will also unblock {ID}, created back in {task}. I'll remove that stub and
     wire the real behavior as part of this task."

While implementing `T-N`:

4. Implement the task normally.
5. If part of `T-N`'s own implementation needs something that doesn't exist yet (an
   unimplemented port, an unshipped module, an unratified spec change), do NOT silently guess.
   **Before logging anything, check whether an existing entry already covers this same stub or
   deferred feature** (including one surfaced by step 2's `Created by task: T-N` check, or any
   other `OPEN`/`READY` entry describing the same gap for the same module). If one exists, do
   NOT create a new entry — update the existing one in place (refreshed stub behavior, resume
   instructions, or status) instead of duplicating it. Only if no matching entry exists, ship a
   narrow, explicit stub (a clear error code, a documented no-op, a TODO comment tied to an ID)
   and add a new `DEFERRED` entry to `TODO.md` using the module-prefixed ID scheme and the
   template at the bottom of that file. Resume instructions must be literal — the actual code
   change to make later, not "revisit this."
6. Separately — even if nothing is technically blocked — check whether `T-N`'s scope leaves
   something incomplete that the current spec/tasks.md never assigned to any task at all (a
   field in the spec with no corresponding task, a requirement with no implementation path).
   If so, **first check `TODO.md` for an existing `GAP` entry describing the same missing
   piece** — if found, leave it as-is (or update its description if this pass sharpened it)
   rather than adding a second entry for the same gap. Only if none exists, add a new `GAP`
   entry — these have no resolver task, just a description of what's missing and a suggested
   next step (new task needed? spec change needed?).
7. If step 3 found a resolvable entry, do the unblock now: open the file(s) named in that
   entry's `Files affected`, remove the stub, wire the real behavior, verify it (run the test
   the entry names, if any).

After implementing `T-N`:

8. Update `TODO.md`:
   - Entries created in step 5/6 → `OPEN`.
   - Entries resolved in step 7 → move to `RESOLVED`, note what was verified.
   - A landed blocker whose stub wasn't removed in this pass → `READY`, not `RESOLVED`.
9. Report a one-line summary of any `TODO.md` changes to the user — new entries, resolved
   entries — without requiring them to open the file.

Rules:
- Never mark `RESOLVED` without confirming the real code path executes — a landed dependency
  alone does not resolve an entry.
- Never write a vague entry. If literal resume instructions (DEFERRED) or a concrete
  description (GAP) can't be written, the blocker isn't understood well enough yet to log.
- Never create a duplicate entry for a stub or gap that's already tracked — always search
  `TODO.md` for an existing match first (same module, same underlying deferred feature/gap,
  regardless of which task originally created it) and update that entry instead of adding a
  new ID. This matters most when resuming a partially-implemented task, since step 2 may
  already have surfaced the pre-existing entry.
- `TODO.md` is a deliverable — commit it alongside the code changes in the same pass.