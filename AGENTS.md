# SpaceBattle (OOAnD) — Agent Working Rules

> Before starting work, read this file, [`.agents/rules/ooand-invariants.md`](.agents/rules/ooand-invariants.md), [`.memory-bank/context.md`](.memory-bank/context.md), and the latest entries in [`docs/DECISIONS.md`](docs/DECISIONS.md).

EGM artifact directory: .dev/egm

---

## 1. Workflow & Orchestration

### 1.1. Standard Feature Lifecycle
Every new feature and behavior change follows a strict sequence:
1. **Decisions (`docs/DECISIONS.md`):** Analyze the task with `egm-brainstorm`, surface architectural forks, and record the chosen option as `D-NNN`. `DECISIONS.md` is strictly **append-only**: deleting or rewriting old entries is forbidden; revisiting a decision adds a new entry referencing the superseded one.
2. **Spec (`docs/designs/<slug>.md`):** Draft the spec from recorded decisions via `egm-draft` (for non-trivial or concurrent work, run `egm-review` → `egm-resolve`). Editing `.cs` files without a `DECISIONS.md` entry and an approved spec is mechanically blocked by `guard-gate`.
3. **TDD Implementation (`tdd-workflow`):** Write a failing xUnit test first (Red), verify it fails for the expected reason, write minimal implementation code in `src/` (Green), and refactor cleanly (Refactor) while preserving **100% coverage**.
4. **Independent Review (`spec-review`) & Memory (`memory-bank`):** Run the evaluator subagent in a fresh isolated context to obtain `verdict: CLEAN` in `.dev/.last-review.md`, then update `.memory-bank/` whenever modules or public `IoC` keys change.

### 1.2. Mandatory Execution Pipeline (Actor — Critic — Evaluator)
There is no fast-path bypass — **every code change goes through the full pipeline**:
1. **Actor (`csharp-developer`):** Implements C# (`.NET 10`) code and xUnit tests strictly following the spec and `.agents/rules/ooand-invariants.md`, achieving 100% line and branch coverage.
2. **Critic (`adversarial-skeptic`):** Independently reviews architectural decisions, specs, and diffs for scope races, command-graph cycles, hidden edge cases, and over-engineering.
3. **Evaluator (`code-reviewer-qa`):** Final inspector in a clean context (invoked via `spec-review`). Audits `git diff` line by line against `ooand-invariants.md` (`INV-*`), `docs/DECISIONS.md` (`D-*`), and `docs/designs/<slug>.md`, verifies test honesty, runs `dotnet build` and `dotnet test` (Coverlet: line `100%`, branch `100%`), and records `.dev/.last-review.md`.
* **Circuit Breaker:** Maximum 2 rework rounds between Actor and Critic/Evaluator. If consensus is not reached after 2 rounds, the Lead halts the loop, preserves disputed changes via `git stash push -m "circuit-breaker-deadlock"` (or asks the user for explicit confirmation to revert specific files), and presents a decision table: *Trade-off / Risk / Recommendation*. Unauthorized `git checkout` or `git restore` is forbidden.
* **Delegation Rules:** Never spawn subagents just to read a couple of files or run a single `grep`. Never launch parallel subagents touching overlapping files. Always pass exact file paths, the active spec link, and done criteria when delegating; do not duplicate research already completed by a subagent.

---

## 2. Engineering Standards & Culture

* **Challenge with Evidence (No Sycophancy):** If a user proposal or spec violates `ooand-invariants.md`, contradicts an earlier entry in `DECISIONS.md` without justification, or introduces unnecessary complexity, say so directly **before** writing code and propose 1 concrete alternative. One argument, one alternative — the human decides.
* **Minimal Diffs & Read-Before-Write:** Always read target file lines before editing. Apply surgical edits with minimal context; full-file rewrites without clear need and placeholder stubs (`TODO`, ellipses) are forbidden.
* **Anti-Sprawl with OCP:** Do not create speculative helpers, "future-proof" utilities, one-off scripts, or redundant markdown reports. *OOAnD domain exception:* extending game rules and actions under Open-Closed (`INV-3`) means adding new command/strategy classes rather than bloating existing commands with `if`/`switch`.
* **YAGNI & Boundary Validation:** Three similar lines are better than a premature abstraction. Validate data strictly at external system boundaries (parsing rule descriptions, external inputs), not inside every private helper call.
* **Honest Tests, 100% Coverage & Package Hygiene:** Test coverage (`line` and `branch`) MUST be **100%**. Empty `catch` blocks that swallow exceptions and deleted or disabled failing tests (`Skip`) are forbidden. Every bugfix must start with a regression test reproducing the bug. Restoring locked dependencies (`dotnet restore`) is allowed; adding **new** NuGet packages (`dotnet add package`) requires checking the `.NET 10` standard library first and explicit user approval.
* **Code, Log & Tone Hygiene:**
  * **Zero code comments:** All comments (`//`, `/* */`, `///`) in source code are forbidden.
  * No filler, routine apologies, or flattery. Start directly with the substance, conclusion, or next concrete action.
  * Never dump raw log walls into context; preserve non-zero exit codes across pipes (`set -o pipefail`). Cite files via exact paths `path/to/File.cs:42`, and avoid ASCII art.

---

## 3. Git, Safety & Hard Stop Rules

* **STRICT NO AUTO-COMMITS:** Agents are STRICTLY FORBIDDEN from running `git commit` or `git push` unless the user explicitly and directly instructs them to do so in the current message.
* Inspect repository state freely with read-only git commands (`git status`, `git diff`, `git log`). When an explicit commit is requested: 1) never use blanket staging (`git add .`, `git add -A`) — stage files explicitly; 2) inspect `git status` and `git diff --staged` for secrets or stray artifacts; 3) verify `dotnet build` and `dotnet test` (100% coverage) exit with `0`.
* **Working Tree Protection:** Check `git status` before starting edits. Never overwrite unrelated dirty/uncommitted files without user consent.
* **Harness Protection:** Agents MUST NOT modify `AGENTS.md`, `.agents/**`, `.github/workflows/ci.yaml`, or `scripts/githooks/**` without explicit human instruction (enforced by `guard-gate`).
* **Destructive Commands (require explicit confirmation and caught by hooks):** `rm`, `git push --force`, `git reset --hard`, `git clean -f*`, `git branch -D`, `git checkout .`, `git restore .`, `--no-verify`, `sudo`, `docker rm`. Bypassing these bans via `find -delete`, `find -exec`, `xargs rm`, `curl | sh`, or inline deletion one-liners (`python -c`, `node -e`) is strictly forbidden.

---

## 4. Stack, Pre-Approved Commands & Project Memory

### 4.1. Safe Commands (run without manual confirmation)
* **Read & Search:** `ls`, `pwd`, `which`, `find` (without `-delete`/`-exec`/`-ok`), `cat`, `head`, `tail`, `grep`, `file`, `du`
* **Git (read-only):** `git status`, `git diff`, `git diff --staged`, `git log`, `git branch`, `git tag`, `git show`, `git remote -v`
* **Build, Format & Tests (`.NET 10`):**
  * `dotnet --info`, `dotnet restore`
  * `dotnet build --no-incremental`
  * `dotnet test` / `dotnet test --collect:"XPlat Code Coverage"` *(mandatory local & CI gate: line `100%`, branch `100%`)*
  * `dotnet format --verify-no-changes`

### 4.2. Long-Term Memory & Artifacts
* `.agents/rules/ooand-invariants.md` — hard OOAnD architectural invariants (`INV-1..INV-9`). Violating any item is a bug, not a compromise.
* `.memory-bank/context.md` — compact project index (when exceeding 100 lines, the `memory-bank` skill supports splitting sections into `.memory-bank/{product-overview,steerings,tech-details,tasks}/` linked from the index): stack, `Common/core.dll` / `scopes.dll` contracts, status, and backlog. Update only when modules or public `IoC` keys change, preserving contracts and avoiding secrets.
* `docs/DECISIONS.md` — append-only architectural decision log (`D-NNN`).
* `docs/designs/` — approved feature specs.
* `.dev/` *(in `.gitignore`)* — EGM working drafts `.dev/egm/` and review verdict artifact `.dev/.last-review.md`.
