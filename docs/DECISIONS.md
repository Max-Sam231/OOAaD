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

## D-004 — Server Thread with Inbound Queue, Round-Robin Game Scheduler, and Command-Count Quanta (2026-11-13)
* **Status:** Accepted
* **Context / Task Prompt:** `Game.Execute()` loops while `"Game.Loop.CanContinue"` and sleeps 1 ms on an empty queue; nothing runs it in a thread, and nothing can run several games. Practice session 3 requires: a thread with a thread-safe queue facing outward, a scheduler that gives games quanta, a thread that does not sleep while any game can run, and a receiver that tells the scheduler when a game has work. The same session requires starting with the simplest working form before multi-server features.
* **Options Considered:**
  1. One OS thread per game with a blocking queue — simplest, but the number of games is bounded by threads and it contradicts the scheduler requirement.
  2. `Task`/`async` with the thread pool — the runtime schedules, so there is no scheduler of our own to show, and a game scope could hop between threads (`INV-7`).
  3. An explicit server thread: one `BlockingCollection<ICommand>` inbox, a round-robin scheduler of ready games, and a quantum expressed as a maximum number of commands per turn. The quantum and the idle signal are supplied through the existing keys `"Game.Loop.CanContinue"` and `"Game.Loop.Idle"` in the game scope, so `Game` is not modified.
* **Decision:** Option 3. The quantum is counted in commands, not milliseconds, so behavior is deterministic and testable. A game that finds its queue empty leaves the ready set and returns to it when its receiver gets a command.
* **Consequences & Invariants:** New keys `"Server.Thread.Create"`, `"Server.Thread.Stop.Hard"`, `"Server.Thread.Stop.Soft"`, `"Game.Create"`. `Game.cs` and all existing commands stay unchanged (`INV-3`). A game scope is touched only by the server thread that runs the game (`INV-7`). Out of scope and left for later decisions: network endpoint, waiting for all agents, step generations, moving a game between threads, load testing.

## D-005 — Game Transfer as JSON Snapshot Restored by Re-Initialization, First Between Server Threads (2026-11-13)
* **Status:** Accepted
* **Context / Task Prompt:** Practice session 3 requires moving a running game to another server so that it keeps playing: serialize game items and the command queue, skip non-serializable data and recreate it on load, re-initialize the game's IoC scope on the target, and do not lose commands sent while the game is moving. The code has no network endpoint and no server process yet, `Game` hides its queue, `GameObjectRepository` cannot be enumerated, and `Vector` exposes no dimension.
* **Options Considered:**
  1. Binary serialization of the object graph — cannot carry delegates, scopes or compiled adapters, and is removed from modern .NET.
  2. Serialize the IoC scope of the game — scopes hold delegates; not serializable (practice session 3 says the same).
  3. A JSON snapshot with three parts: the name of the rule set that initializes the game scope, game items with only the values that have a registered codec, and the pending commands as `{id, type, game_item_id}`. The target creates a new game, runs the rule set again, loads the items, and resolves each command again from IoC by its type. The snapshot is produced and consumed as a string, so the same payload can later cross a process boundary.
* **Decision:** Option 3. The first deliverable transfers a game between two server threads of one process, passing only the JSON string. Two operating-system processes and a network channel are a later decision, because they need the endpoint. Pending commands are tracked in a journal next to the game instead of reading the private queue of `Game` (`INV-3`). After the snapshot, the old game location forwards every late command to the new one.
* **Consequences & Invariants:** New keys `"Game.Create.Transferable"`, `"Game.Command.Send"`, `"Game.Transfer"`, `"Game.Restore"`, `"Game.Rules.<name>"`, `"Snapshot.Codecs"`. Only commands sent through `"Game.Command.Send"` survive a transfer. `Vector` gets a read-only `Dimension` property (additive change to a value type). A macro command is described by its IoC key and game item, not by a list of nested command ids, because macros are already built from `"Specs.*"`. JSON uses `System.Text.Json`; no new NuGet package.

## D-006 — Transactions as a Per-Game Change Journal Hidden Behind the Game Item, with Nested Transactions (2026-11-13)
* **Status:** Accepted
* **Context / Task Prompt:** A macro command changes several values of a game item in several steps. If a later step throws, earlier changes stay: fuel is burned though the ship did not move, or the ship moved though the fuel check failed (practice session 4). The session asks for a transaction: either every step takes effect or the item is as it was before the macro command, implemented through a change journal and a transaction manager, invisible to commands.
* **Options Considered:**
  1. Copy the whole game item before a macro command and restore it on failure — simple, but copies every value on every macro command and cannot nest.
  2. Compensating commands (saga) — each command needs a hand-written undo; mentioned in practice session 4 as a later topic.
  3. The journal from practice session 4: writes made inside a transaction go to a journal kept by a per-game transaction manager; reads look into the journal first; commit applies the journal to the item, rollback discards it. The game item object implements `IDictionary<string, object>` and does this internally.
* **Decision:** Option 3, with one simplification that follows from the threading model. A game runs on one thread, so transactions never run concurrently; they can only be nested (a macro command inside a macro command). The manager therefore keeps a stack of journals, one per open transaction, instead of a single table with transaction ids and stored success/fail statuses: committing an inner transaction merges its journal into the outer one, rolling back drops it, committing the outermost applies it to the items. Locks, dirty-read rules and conflict merging from the session are not needed and are not implemented.
* **Consequences & Invariants:** New keys `"Game.Transactions"`, `"Game.Item.Transactional"`, `"Commands.InTransaction"`, registered per game scope. Existing commands, adapters and `MacroCommand` are unchanged (`INV-3`): they keep using the item as a dictionary. Removing keys from an item is not transactional. An item created during play and restored by a game transfer (`D-005`) comes back as a plain dictionary and is not transactional on the target.
