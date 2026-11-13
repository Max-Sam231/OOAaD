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

## D-003 — Runtime Adapter Code Generation with Compile-Once Factories and Assembly-Level `[Dependency]` (2026-11-13)
* **Status:** Accepted
* **Context / Task Prompt:** Commands work with game objects (`IDictionary<string, object>`) through `-ing` interfaces, but `"Adapters.*"` keys are resolved in `SpaceBattle.Lib` and registered nowhere. Legacy branch `feature/5lab` (PR #65) generates adapters with Roslyn but calls `Activator.CreateInstance` on every resolve, has no cache, does not register standard property dependencies, and predates the `-ing` renames (`D-002`). Practice session 2 requires: a developer adds a command by writing only the command, its interface, and strategies for non-standard properties.
* **Options Considered:**
  1. Rebase and merge `feature/5lab` as is — violates `INV-6`, fails the lint gate and the 100% coverage gate.
  2. `System.Reflection.Emit` or `DispatchProxy` — no new NuGet package, but is not "source text → compiled code" as required by the course, and `DispatchProxy` reflects on every call.
  3. Roslyn: generate adapter source plus an adapter factory per interface, compile once, instantiate the factory once via reflection, then call `Create` directly. Non-standard properties are declared with an assembly-level `[Dependency(typeof(I), "Property", typeof(Utility), "Method")]`.
* **Decision:** Option 3. New NuGet dependency `Microsoft.CodeAnalysis.CSharp` 4.12.0 in `SpaceBattle.Lib` (the version already proven by `feature/5lab` CI on `net10.0`). Property keys: getter `"<Interface.FullName>.<Property>"`, setter `"<Interface.FullName>.<Property>.Set"` (returns `ICommand`). The attribute is named `Dependency` (practice session 2, `INV-6`), not `Adapter` (wiki lab 5 text).
* **Consequences & Invariants:** New keys `"Adapters.Factory"`, `"Adapters.<InterfaceName>"`, and per-property keys. No existing `.cs` file is modified (`INV-3`). `Activator.CreateInstance` appears only in `AdapterFactoryCompiler` (`INV-6`). PR #65 is superseded by this work.
