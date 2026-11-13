# Architectural Decision Log (`DECISIONS.md`)

> **Rule (`INV-9`):** This log is strictly **append-only**. Every design fork is recorded before writing a spec in `docs/designs/<slug>.md`. Accepted entries are never deleted or edited retroactively — when revisiting a decision, append a new entry `D-NNN` citing `Supersedes: D-MMM` and the reason for revision.

---

## Entry Template

```markdown
## D-NNN — Short Decision Title (YYYY-MM-DD)
* **Status:** Accepted | Superseded by D-XXX
* **Context / Task Prompt:** What problem or ambiguity is being resolved.
* **Options Considered:**
  1. Option A — pros / cons.
  2. Option B — pros / cons.
* **Decision:** Which option was chosen and why.
* **Consequences & Invariants:** Affected `IoC` contracts / `docs/designs/` specs.
```

---

## D-001 — Preserve 1st-Semester Snapshot in `archive` and Deploy Unified Team Harness on `main` (2026-11-13)
* **Status:** Accepted
* **Context / Task Prompt:** Before building 2nd-semester OOAnD features without fixed TZs, `main` holds the 1st-semester `SpaceBattle/` solution (`dfb5143`), CI checks only 90% coverage without invariant linting, and 5 legacy feature branches remain unmerged (`feature/4lab_3`, `feature/5lab`, `feature/collision-data-processor`, `feature/IsCollision`, `feature/13_icommand_define`).
* **Options Considered:**
  1. Keep `main` unchanged and start a separate repository.
  2. Snapshot 1st-semester `main` to branch `archive`, deploy the unified `.agents/` harness + `AGENTS.md` + `scripts/githooks/pre-push`, raise CI thresholds to 100% + `ooand-lint-gate`, and keep existing `SpaceBattle/` on `main` while fixing its linter violations.
* **Decision:** Chose Option 2 — preserve 1st-semester `main` cleanly in `archive`, install the full EGM + `Actor → Critic → Evaluator` harness in `main`, and fix existing invariant violations on `SpaceBattle/` via spec-driven TDD.
* **Consequences & Invariants:** All `.cs` changes are governed by `DECISIONS.md → docs/designs/<slug>.md → TDD → spec-review`. Coverage gate in local hooks and CI is 100% line and 100% branch (`INV-8`).

## D-002 — Linter & Formatting Compliance Remediation on `SpaceBattle/` (`INV-5`, `INV-7`, `INV-8`) (2026-11-13)
* **Status:** Accepted
* **Context / Task Prompt:** Running `ooand-lint-gate.py --check-all .` and `dotnet format --verify-no-changes` on `SpaceBattle/` reveals 10 invariant errors (`Game.cs` referencing `IoC.Scope.*` and `Console.WriteLine`, five `-able` interfaces `ICollidable`, `ICommandInjectable`, `IMovable`, `IRotatable`/`IRotateble.cs`, `IShootable`, and `//` comments in `RegisterIoCDependencyActionsStartTests.cs`) plus 2 whitespace formatting diffs.
* **Options Considered:**
  1. Add `SpaceBattle/` to linter ignore lists indefinitely.
  2. Extract scope switching from `Game` into `InSeparateScopeCommand`, route `Game` loop exceptions through `Ioc.Resolve<ICommand>("Exception.Handle", command, ex)`, rename all 5 `-able` interfaces and their `"Adapters.*"` keys to `-ing` names, delete comments, and normalize formatting while keeping 100% line & branch coverage.
* **Decision:** Chose Option 2 — clean compliance with `INV-5`, `INV-7`, and `INV-8` directly on `main`. `Game` uses parameterless construction and `"Exception.Handle"`; `InSeparateScopeCommand` takes `(object targetScope, ICommand innerCommand)`.
* **Consequences & Invariants:** Operation interfaces become `ICollidingObject`, `ICommandInjecting`, `IMovingObject`, `IRotatingObject`, `IShootingObject` (and keys `"Adapters.IMovingObject"`, `"Adapters.IRotatingObject"`, `"Adapters.IShootingObject"`). `Game` is scope-blind (`INV-7`), zero `.cs` comments exist (`INV-8`), and CI/pre-push pass with 100% line & branch coverage.
