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

## D-007 — Adapter Source Templates Move to Text Files Registered in IoC (2026-11-14)
* **Status:** Accepted
* **Context / Task Prompt:** `AdapterSourceBuilder` (`D-003`) writes the adapter and factory source with C# string literals. Changing the generated code (another namespace, logging in accessors, a different base) means editing and recompiling the generator. The team asked for the template to live in `.txt` files so it can be changed without touching code.
* **Options Considered:**
  1. One template file with a template engine (Scriban, T4) — loops over properties are natural, but it is a new dependency for a handful of placeholders.
  2. Template files embedded into the assembly — cannot go missing, but any change needs a rebuild.
  3. Four plain text fragments (file with adapter and factory classes, property, getter, setter) with `{{Name}}` placeholders, copied next to the assembly and registered in IoC as strings. `AdapterSourceBuilder` resolves them from IoC, substitutes values and repeats the property fragment.
* **Decision:** Option 3. Editing a `.txt` file changes the generated code without recompiling `SpaceBattle.Lib`; a scope can also register a different template under the same key, which takes effect for interfaces whose factory has not yet been compiled in the process. Placeholders use `{{…}}` so that C# braces in a template need no escaping. `RegisterIoCDependencyAdapterFactory` registers the default templates, so existing callers keep working. `AdapterFactoryCompiler` finds the factory as the only type implementing `IAdapterFactory` in the compiled assembly instead of by a fixed name, so namespaces and class names in the template are free to change.
* **Consequences & Invariants:** New keys `"Adapters.Template.Class"`, `"Adapters.Template.Property"`, `"Adapters.Template.Getter"`, `"Adapters.Template.Setter"`, registered by `RegisterIoCDependencyAdapterTemplates`. A factory is compiled once per interface (`INV-6`), and the factory cache is shared by all scopes: the template used is the one visible in the scope where an interface is compiled first, and that factory then serves every scope. A template that produces invalid C#, or not exactly one concrete factory class, fails with `InvalidOperationException`. Key naming stays in `AdapterDependencyNaming`; templates receive only computed keys.

## D-008 — Command Factories Are Generated from a Text Template Like Adapter Factories (2026-11-14)
* **Status:** Accepted
* **Context / Task Prompt:** The teacher requires code generation of commands and factories. Adapters and their factories are generated (`D-003`, `D-007`), but a command such as `MoveCommand` is created by a delegate compiled from an expression tree inside `RegisterIoCDependencyCommandWithAdapter`; no source is generated for it, and its shape cannot be changed through a template.
* **Options Considered:**
  1. Keep the expression-tree delegate — fast, but it is not code generation and is invisible as code.
  2. Generate, per command type, a factory class from a text template (`CommandFactory.txt`): it resolves the adapter of the command's interface from IoC and calls the command's constructor. Compile it once per command type with the same compiler as adapter factories and register the command through it.
  3. Additionally generate the IoC registration class for every command — the registration is one generic line of C#, and a generated class would need the dependency name at run time, i.e. one more reflection call per registration.
* **Decision:** Option 2. The command's own logic stays hand-written: it is the game rule and cannot be generated. Generated per command type: `<Command>Factory : ICommandFactory` with `ICommand Create(IDictionary<string, object> gameItem)`. Registration stays in `RegisterIoCDependencyCommandWithAdapter`, which now delegates to the generated factory. The compiler becomes generic (`GeneratedCodeCompiler.Compile<T>`) and serves both adapter and command factories.
* **Consequences & Invariants:** New keys `"Commands.Factory"` (command `Type` → `ICommandFactory`, compiled once per type and cached by the registered strategy, `INV-6`) and `"Adapters.Template.CommandFactory"`. The command checks (implements `ICommand`, exactly one public constructor, exactly one interface parameter) move from `RegisterIoCDependencyCommandWithAdapter` to `CommandFactorySourceBuilder`; they still fail with `ArgumentException` when a command is registered. `RegisterIoCDependencyCommandFactory` relies on the templates registered by `RegisterIoCDependencyAdapterFactory`. `AdapterFactoryCompiler` is renamed to `GeneratedCodeCompiler`.

## D-009 — Keep Property and Getter Fragments in Code (2026-11-14)
* **Status:** Accepted; supersedes the file-based storage of `Adapters.Template.Property` and `Adapters.Template.Getter` from D-007.
* **Context / Task Prompt:** `AdapterProperty.txt` and `AdapterGetter.txt` are short, fixed fragments with only a few substitutions. Their separate files add file loading and packaging requirements without a need to customize those fragments independently.
* **Options Considered:**
  1. Keep both fragments in separate text files, as in D-007; this supports editing them without rebuilding the library.
  2. Keep their text as built-in templates registered in IoC by `RegisterIoCDependencyAdapterTemplates`; this removes the two files while preserving per-scope overrides and existing IoC keys, but built-in edits require rebuilding.
* **Decision:** Option 2. The built-in property and getter fragments live in `RegisterIoCDependencyAdapterTemplates`; the class, setter, and command factory remain external text files.
* **Consequences & Invariants:** `Adapters.Template.Property` and `Adapters.Template.Getter` remain available and overridable in IoC; the files are no longer copied or read from disk. Editing their built-in defaults requires recompiling `SpaceBattle.Lib`. Other D-007 templates are unchanged.

## D-010 — Team Repository Reconciliation (`origin/*` Fork A + B), `Angle`/`Vector` `INV-8` Remediation & 5-Spec Delta Plan (2026-11-26)
* **Status:** Accepted (`Supersedes: root D-001` greenfield reboot premise; preserves `origin/*` `D-001..D-009` verbatim)
* **Context / Task Prompt:** Root `D-001` (2026-10-31) assumed a greenfield rewrite in `src/`/`tests/`, whereas the team's canonical GitHub repository (`контекст/repo/.git`, `origin/main` `4efb0a0` + Fork A `origin/feature/adapter-codegen` `b0f65b0` + Fork B `origin/feature/game-transactions` `dd1fe75`) already contains **121 `.cs` files at 100%/100% coverage** and recorded decisions **`D-001..D-009`** (`origin/main` `D-001..D-002`, Fork A `D-003, D-007..D-009`, Fork B `D-004..D-006`). Note: `archive`/`dev` refs do not exist in this snapshot (Semester 1 is on local `main` `dfb5143`), and the "~180 files" figure is an external reference estimate only. However, `origin/*` still has two unresolved defects: mutable `public static int Denominator` in `Angle.cs` (`INV-8` cross-thread data race) and heap `class Vector` with LINQ allocations in `Vector.cs`, plus a CI `working-directory` mismatch (`SpaceBattle/` vs `.`).
* **Options Considered:**
  1. **Option A (Chosen):** Reconcile onto canonical `origin/main` + merge Fork A (`b0f65b0`) + rebase/merge Fork B (`dd1fe75`, zero overlapping `.cs` files) keeping `SpaceBattle/` layout matching `origin/main:.github/workflows/ci.yaml`, fix `Angle`/`Vector` + `RegisterIoCDependencySnapshotCodecs.cs:34` immediately in Day-0 Spec `01`, and execute **5 focused delta specs (`01..05`)**. All new decisions start at **`D-010`** so there is zero numbering collision with `origin/*`'s `D-001..D-009` (`INV-9`).
  2. **Option B (Rejected):** Greenfield rewrite in root `src/`/`tests/` discarding 121 tested files and team git history (high deadline risk under Fail-Fast defense and splits teammate Ivan's PRs #68–#71).
  3. **Option C (Rejected):** Copy files into `src/`/`tests/` without git history reconciliation (breaks commit attribution audited on defense per `ПЗ_3:277, 293`).
* **Decision:** Adopt **Option A**.
  * **Merge sequence:** `origin/main` (`4efb0a0`) → merge Fork A (`b0f65b0`) → merge/rebase Fork B (`dd1fe75`), preserving `D-001..D-009` verbatim and verifying 100%/100% coverage before delta work. All coding runs from the canonical git clone root (`SpaceBattle/SpaceBattle.sln`), never inside ignored `контекст/`.
  * **`Angle` (`INV-8` fix):** Immutable `readonly struct Angle : IEquatable<Angle>` with per-instance `Numerator` and `Denominator` (zero `static` mutable state; default value safely resolves denominator `8` so `default(Angle)` never divides by zero; `operator +` requires matching denominators and throws `ArgumentException` on mismatch; `Equals`/`GetHashCode` compare `(Numerator, Denominator)`). Update `RegisterIoCDependencySnapshotCodecs.cs` to serialize instance `((Angle)value).Denominator`.
  * **`Vector` remediation:** Zero-LINQ immutable value semantics with defensive coordinate array cloning and explicit `for`-loop arithmetic/equality (preserving `Dimension` and existing test contracts cleanly at 100%/100%).
  * **5-Spec Delta Plan:** Collapse 9 greenfield specs into `docs/designs/01..05-*.md` (reusing `CommandInjectableCommand` as `Bridge` and Fork A's `GeneratedCodeCompiler`/`IAdapterFactory`/`ICommandFactory` + adding only the derived `Velocity` `[assembly: Dependency]` strategy).
* **Consequences & Invariants:** Satisfies `INV-3`, `INV-6`, `INV-8`, `INV-9`; unlocks parallel implementation across 3 teammates after Spec `01`. Affects `docs/designs/01-reconciliation-and-value-types.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`.

---

## D-011 — Declarative JSON Game Rules & Deterministic Kahn `"A before B"` Topological Sort (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `ПЗ_1:13-21, 89` requires defining playable games via textual descriptions so multiple games with different rules run simultaneously on one server, and `INV-4` requires dynamic `MacroCommand` assembly via topological sorting of an ordering graph (`A before B`) with cycle detection. Standard Kahn's sort without deterministic tie-breaking can produce divergent orders across OS processes, and adding a free-form `bindings` mini-interpreter creates untestable branches under `INV-8`.
* **Options Considered:**
  1. **Option A (Chosen):** Structured `game-rules.json` parsed via built-in `System.Text.Json` (zero new NuGet packages) with 3 concrete sections: `version` (string), `entities` (initial items decoded via existing `SnapshotValueCodecs`), and `actions` (`steps: string[]` + `order: ["A before B", ...]`). Deterministic Kahn's topological sort breaking ties by declaration index in `steps`, deduplicating edges, checking unknown/duplicate/self-loop steps, and checking cross-action macro recursion cycles at load boundary. Translates purely into `IoC.Register` of `Specs.<action>` and `Macro.<action>` reusing existing `CreateMacroCommandStrategy` untouched (`INV-3`).
  2. **Option B (Rejected):** Custom line-based text DSL (extra lexer surface and harder 100% branch coverage; harder to embed verbatim inside `GameSnapshot`).
  3. **Option C (Rejected):** YAML via `YamlDotNet` (adds unvetted external NuGet dependency).
* **Decision:** Adopt **Option A** — JSON rules + index-tie-broken Kahn sort + cross-action DAG check + zero edits to `MacroCommand` / `CreateMacroCommandStrategy`.
* **Consequences & Invariants:** Satisfies `INV-1`, `INV-3`, `INV-4`, `INV-8`. Reuses `SnapshotValueCodecs` and `"Game.Rules.<name>"`; enables deterministic rule replay during 2-process migration. Affects `docs/designs/02-rules-and-scope-hot-reload.md`.

---

## D-012 — 4-Level Scope Tree, `ProxyScopeFactory` (`Volatile` + Per-Quantum Pinning) & Explicit Plugin Hot-Reload (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `ПЗ_4:289-389` and `INV-7` require a 4-level scope hierarchy (`RootScope` thread-safe → `GameBaseScope` plain `Dictionary` → `ProxyScope` → `GameScope` plain `Dictionary`, `depth <= 4`) with lock-free hot-reload of shared commands and `.dll` plugins. Goldfish audits exposed three blockers: (1) `scopes.dll` Catch-22 (`IoC.Register("IoC.Scope.Parent", ...)` throws `ArgumentException` on `IoC.Scope.Create` and `KeyNotFoundException` on `IoC.Scope.Create.Empty`), plus 0-arg `IoC.Scope.Create` breaks `depth <= 4`; (2) unpinned `Volatile.Read` on `ProxyScope` causes torn `v1`/`v2` reads mid-macro; (3) `GeneratedCodeCompiler` loading into `ALC.Default` and `AppDomain.CurrentDomain.GetAssemblies()` scanning breaks when `.dll` plugins load into custom `AssemblyLoadContext`s (`TypeLoadException`, `CS0433`, duplicate `[Dependency]`).
* **Options Considered:**
  1. **Option A (Chosen):**
     * **Explicit 4-level tree:** Capture `"Server.Scope.Root"` at startup. Create `GameBaseScope_vN` with explicit parent `rootScope`, `ProxyScope` via `ProxyScopeFactory` over `IoC.Scope.Create.Empty` setting `dict["IoC.Scope.Parent"] = _ => holder.Current` before publication, and `GameScope` always with explicit parent `proxyScope` (banning 0-arg `IoC.Scope.Create` for games/base scopes).
     * **Lock-free per-quantum pinning:** `ScopeParentHolder` combines `Volatile.Read/Write` on `_current` with a `[ThreadStatic]` `_pinnedBaseScope` set/cleared by `PinnedBaseScopeCommand` around each game quantum (`try/finally`). Scope depth remains strictly **4**, zero locks, atomic version view per quantum, and `GameBaseScope_vN` is strictly immutable after publication.
     * **Explicit plugin hot-reload:** Trigger via `HotReloadGameBaseScopeCommand(pluginsDir)` (no `FileSystemWatcher`). Load `.dll`s via `AssemblyLoadContext(name, isCollectible: true)` (`LoadFromStream`, without overriding `Load`), pass scoped `"Adapters.Assemblies"` per `GameBaseScope_vN`, compile generated factories into the target interface's `AssemblyLoadContext` with `MetadataReference.CreateFromImage`, and isolate plugin factory caches (`ConditionalWeakTable` or per-`GameBaseScope` cache).
  2. **Option B (Rejected):** Update `Parent` on every game sequentially (causes split-brain across live games, explicitly banned in `ПЗ_4:313-333`).
  3. **Option C (Rejected):** Guard `GameBaseScope` with `ReaderWriterLockSlim` (violates lock-free non-thread-safe game-scope rule `ПЗ_4:269-281, 349`).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-1`, `INV-6`, `INV-7`, `INV-8` and respects all verified `Common/scopes.dll` traps. Affects `docs/designs/02-rules-and-scope-hot-reload.md` and `docs/ARCHITECTURE.md`.

---

## D-013 — `SpaceBattle.Server` Thin Host, WebSocket Endpoint, $O(1)$ Routing, Unified Envelope & Non-Blocking Step Validation (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `ПЗ_3:49-241` and `ПЗ_5 03:32-06:40` require a bidirectional network endpoint (WebSocket recommended), $O(1)$ `gameId -> thread` hash routing, two-level dispatch (`endpoint → thread inbox → game queue`), stale `step` protection, and a synthetic local throughput benchmark of **tens of thousands ($\ge 20\text{–}50\text{k}$) msg/s**. Goldfish audits surfaced three traps: (1) interpreting messages on Kestrel threads violates single-threaded `GameScope` (`INV-7`) and degrades endpoint throughput; (2) branching `Game` vs `GameProxy` via `is`/`as` violates `INV-2`, while duplicating `IncomingMessage` alongside `CommandDescription` fractures snapshot serialization; (3) a strict barrier default hangs the mandatory E2E test (`ПЗ_6 69:05`) where Player 1 is silent.
* **Options Considered:**
  1. **Option A (Chosen):**
     * **Thin host & WebSocket endpoint:** `SpaceBattle.Server` (`ASP.NET Core` Kestrel WebSockets) resolves thread-safe endpoint services once synchronously in `ServerBootstrap` (zero `IoC.Scope.Current` reads across `await`). Outbound state pushes write non-blockingly (`TryWrite`) into a per-client bounded `Channel` drained asynchronously (zero blocking I/O on `ServerThread`). Clients properly execute `CloseAsync` (`TIME_WAIT` safety).
     * **$O(1)$ routing & polymorphic dispatch:** `ConcurrentDictionary<string, ICommandReceiver>` (`gameId -> thread` / routing entry) performs non-blocking `TryReceive` into the target `ServerThread` inbox (backpressure response on full queue). Unify wire, journal, proxy, and snapshot envelopes onto extended `CommandDescription` (`Id`, `GameId`, `Type`, `GameItemId`, `Step`, `Args`). Dispatch polymorphically via `ICommandReceiver` (zero `is`/`as`, zero edits to `Game.cs`), executing `InterpretMessageCommand` inside the game's own `InSeparateScopeCommand` quantum.
     * **Non-blocking default `"Game.Step.Validate"`:** Rejects stale (`step < currentStep`) and invalid future steps without deadlocking on a silent player (fully compatible with `ПЗ_6 69:05` E2E); strict barrier or `Lookahead(K)` remains a pure `git A` `IoC` override (`INV-3`).
     * **Dual benchmarks:** Both an in-process pipeline benchmark and a loopback WebSocket benchmark in `SpaceBattle.Tests/Benchmarks/` proving $\ge 20\text{–}50\text{k}$ msg/s locally.
  2. **Option B (Rejected):** Raw TCP with custom framing (incompatible with browser/standard HTTP tooling and adds framing boilerplate).
  3. **Option C (Rejected):** Strict barrier default + upfront `LookaheadStepPolicy(K)` (contradicts silent Player 1 E2E and adds speculative branch surface under `INV-8`).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-1`, `INV-2`, `INV-3`, `INV-7`, `INV-8`. Affects `docs/designs/03-server-endpoint-routing-and-step.md`.

---

## D-014 — Deadlock-Free `GameProxy : ICommand`, 2PC 2-Process Migration & Quiesced `ServerSnapshot` (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `ПЗ_3:249-273, 409-736`, `ПЗ_5 06:40-10:15`, and `ПЗ_6 50:00-64:20` mandate: (1) `GameProxy : ICommand, ICommandReceiver` left on the source thread/server during migration, strictly **deadlock-free** and with **zero overhead for non-migrating games**; (2) live 2-process migration with `2*T` in-flight quarantine and `GameScope` reconstruction; (3) full server JSON `shutdown & restore` (`ServerSnapshot`). Audits showed that `origin/feature/game-transfer`'s `RedirectingGameCommandSending` does not implement `GameProxy : ICommand`, risks deadlock on bounded `_inbox.Add()`, suffers CPU spin-livelock on naive `TryAdd` re-scheduling, risks split-stream reordering / `A -> B -> A` ping-pong / crash split-brain, and loses `BridgeCommand`/`Repeater` active actions + `next_entity_id`.
* **Options Considered:**
  1. **Option A (Chosen):**
     * **Deadlock-free `GameProxy : ICommand, ICommandReceiver` (subsumes `RedirectingGameCommandSending`):** Introduce `ITryCommandReceiver.TryReceive` on `ServerThread` and ban inter-thread blocking `_inbox.Add` (including inside `TransferGameCommand` and `Game.Restore`, which pre-populates the restored game's internal queue before 1-slot registration). `GameProxy.Execute()` respects `Game.Loop.CanContinue`, executes FIFO-safe `TryPeek → TryReceive → TryDequeue`, and on `TryReceive == false` breaks immediately into deferred backoff (eliminating 100% CPU livelock and starvation of healthy games). Cross-process forwarding writes non-blockingly into an async `Channel` drained on ThreadPool (zero blocking network I/O on `ServerThread`).
     * **Causal ordering & retirement:** Tag transfers with monotonic `migrationEpoch`, detach old `Game` from source scheduler before publication, buffer new target-side arrivals until `GameProxy` delivers `MigrationDrainComplete(epoch)`, fall back 1-hop to global O(1) router for ultra-late strays, and keep `SoftStop` waiting while `_activeProxies > 0` while making `HardStop` non-blocking even on a full inbox.
     * **2PC 2-process migration (`Prepare/Commit/Abort`) & `2*T` quarantine:** Inject quarantine via single IoC command `"Game.Transfer.Quarantine"` (`2*T` configurable: ms in tests, realistic in demo). Embed `schema_version`, `migration_epoch`, `rules_version`, `rules_config`, `current_step`, `next_entity_id`, `active_actions` (`BridgeCommand`/`Repeater` descriptors), `items`, `pending_commands`, and `buffered_messages` in `GameSnapshot`. Execute idempotent HTTP `Prepare` (reconstructs paused game & validates `rules_version`) → `Commit` (activates) or `Abort` (unfreezes locally) to eliminate crash split-brain.
     * **Quiesced `ServerSnapshot` (`shutdown & restore`):** Stop ingress → drain active `GameProxy` forwards → quiesce all `ServerThread`s → atomically write `{schema_version, rules_version, thread_count, games: [{threadIndex, gameSnapshot}]}` → restore threads, games, and O(1) router before re-opening ingress.
  2. **Option B (Rejected):** Dedicated `TransferDispatcherThread` (adds extra thread & bottleneck; teacher explicitly stated in `ПЗ_3:361` it is not required).
  3. **Option C (Rejected):** In-memory-only transfer without 2 OS processes or zero-downtime live delta migration (Option C1 violates `ПЗ_3:409`; Option C2 exceeds credit timeline).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-1`, `INV-3`, `INV-7`, `INV-8` and both `ПЗ_6 63:05–64:20` acceptance criteria. Affects `docs/designs/04-game-proxy-2proc-migration-and-snapshot.md`.

---

## D-015 — Complete 2PC Game Transactions (`Repository` + Outbox + Tombstones + Zero-Alloc Reads) & Deferred `Saga` (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `ПЗ_4:469-678` (confirmed in `ПЗ_5 00:00–01:36`) requires in-memory 2PC transactionality hidden behind game objects so failing `MacroCommand`s leave zero partial mutations. Auditing `origin/feature/game-transactions` (`D-006`) exposed four defects: (1) `Game.Restore` drops `TransactionalGameItem`; (2) `Remove`/`Clear` inside an open transaction throws `NotSupportedException`; (3) `GameObjectRepository` and `Game._queue` are unjournaled (enabling a "phantom torpedo" rollback exploit on shoot failure); (4) `TransactionalGameItem.View()` allocates a full `Dictionary` on every `TryGetValue`/`ContainsKey` (hurting the $\ge 20\text{–}50\text{k}$ msg/s benchmark), and tuple keys use default `IDictionary.Equals` instead of reference equality.
* **Options Considered:**
  1. **Option A (Chosen):** Include full 2PC completion in the mandatory scope while keeping `Saga` in the Deferred Backlog:
     * Wire `Game.Restore` and entity spawning through `"Game.Item.Create"` (wrapping restored items in `TransactionalGameItem`).
     * Add private `DeletedSentinel` tombstones in `GameTransactions` so `Remove`/`Clear` work cleanly inside open transactions.
     * Add `TransactionalGameObjectRepository` decorator and `TransactionalCommandReceiver` staged outbox (flushing enqueued commands only on outermost `Commit`, discarding on `Rollback`) — zero edits to existing commands (`INV-3`).
     * Use `ReferenceEqualityComparer` for `(item, key)` journal lookup and implement zero-allocation `TryGetValue`/`ContainsKey` directly against `_transactions.TryRead` + `_item`. Assert `!transactions.IsActive` before taking snapshots.
  2. **Option B (Rejected):** Implement `Saga` alongside 2PC now (speculative scope; `Saga` was only teased at `ПЗ_4:765` and never expanded in `ПЗ_5`–`ПЗ_6`).
  3. **Option C (Rejected):** Drop 2PC from mandatory scope (risks failing `ПЗ_4:489` defense check).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-3`, `INV-8` and preserves benchmark throughput. Affects `docs/designs/04-game-proxy-2proc-migration-and-snapshot.md` and `docs/designs/05-gameplay-collisions-and-e2e.md`.

---

## D-016 — Mandatory Gameplay Slice, Dual-Layer Proxy-Agent E2E & Deferred Event Storming Backlog (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `ПЗ_6 65:00–70:00` requires an end-to-end integration test using scripted Proxy-agents where Player 1 is silent and Player 2 destroys all of Player 1's ships and wins, while `event_storming.png` additionally includes refueling, meteorites, bonuses, and surrounding Contexts 1/2/4 (`Registration/Auth`, `Lobby`, `Match Archive`). `ПЗ_6 64:25` also warns that oral defense probes GoF patterns. Under tight deadline (`ПЗ_6 56:00`) and Fail-Fast grading, over-building non-mandatory contexts risks failing core requirements `A1–A11`.
* **Options Considered:**
  1. **Option A (Chosen):**
     * **Mandatory Gameplay Slice (pure OCP `git A`):** Load `Tools/collision_matrix.csv` (15,477 tuples) once into `GameBaseScope` for $O(1)$ lookup + sector collision wiring; add `IFuelBurningObject` + `CheckFuelCommand`/`BurnFuelCommand` (proving OCP `git A` and 2PC rollback); wire `Shoot`/`MoveTorpedo` + IoC pair-dispatched `Destroy` outcome (zero `switch`, `INV-2`) + Victory/Match-End Read Model.
     * **Dual-layer Proxy-agent E2E:** Deterministic in-process `ScriptedProxyAgent` in `SpaceBattle.Tests` (fast, zero flakes, 100% coverage) PLUS 1 real loopback WebSocket E2E test with `CloseAsync` cleanup.
     * **Honest GoF coverage & Deferred Backlog:** Apply zero artificial GoF classes (YAGNI); document the ~15 naturally occurring GoF patterns + why `Singleton` is banned in `docs/ARCHITECTURE.md`. Move `Refuel`, `Meteorites`, `Bonuses`, Contexts 1/2/4, `Saga`, zero-downtime migration, and `Nginx` Reverse Proxy to `docs/ROADMAP.md` Deferred Backlog.
  2. **Option B (Rejected):** Implement all 4 Event Storming contexts + artificial classes for all 23 GoF patterns (violates YAGNI and endangers Fail-Fast defense deadline).
  3. **Option C (Rejected):** In-process E2E only without a real WebSocket match test (leaves endpoint integration unverified end-to-end).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-2`, `INV-3`, `INV-5` (strictly `-ing` interfaces; never `IDestroyable`), `INV-8`. Affects `docs/designs/05-gameplay-collisions-and-e2e.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`.

---

## D-017 — Mechanical Gate Compliance & 100%/100% Coverage Discipline (2026-11-26)
* **Status:** Accepted
* **Context / Task Prompt:** `.agents/hooks/{guard-gate,ooand-lint-gate,stop-verify-gate}.py`, `scripts/githooks/pre-push`, and `.github/workflows/ci.yaml` strictly enforce zero comments, zero `switch` (statements AND expressions, even in infra files), zero `-able` interfaces, zero `IDictionary<string, object>` in non-infra `*Command.cs`, restricted `new` outside `INFRA_FILE_MARKERS` (`Register`/`Factory`/`Compiler`/`Generator`/`Builder`/`Init`/`Bootstrap`), `Activator.CreateInstance` only in `*Factory*`/`*Compiler*`, and exact **100.00% line & branch** Coverlet coverage across the entire solution (including `SpaceBattle.Server/Program.cs`). The user also decided against creating a separate `docs/AGENT_GUIDE.md`.
* **Options Considered:**
  1. **Option A (Chosen):** Keep `.agents/hooks/*` and `ci.yaml` coverage thresholds completely untouched unless the user explicitly authorizes a harness change. Design production code to comply natively:
     * **Ban C# `record` / `record struct` in production code** (eliminates compiler-generated `Equals`/`PrintMembers`/`<Clone>$` branch leaks).
     * **Isolate all BCL/network/host `new`** (`WebSocket`, `Channel`, `HttpClient`, `JsonSerializerOptions`, `AssemblyLoadContext`, `BlockingCollection`, `Thread`) inside `*Bootstrap.cs`, `*Factory.cs`, `*Builder.cs`, or `Register*.cs`.
     * **Expression-bodied `Program.Main`:** `public static Task Main(string[] args) => ServerBootstrap.RunAsync(args, CancellationToken.None);` (zero `async` state-machine branches, covered 100%/100% via graceful `/admin/shutdown` test, zero `[ExcludeFromCodeCoverage]`).
     * **Mandatory testable seams** documented directly in `docs/ARCHITECTURE.md` and `docs/designs/01..05-*.md`: `FakeWebSocket : WebSocket`, `HttpMessageHandler`, `"Game.Transfer.Quarantine"`, Roslyn-emitted temp plugin `.dll`, `TextReader` for CSV, diamond DAG for Kahn sort, and `dotnet format` strictly before `spec-review`.
  2. **Option B (Rejected):** Modify `.agents/hooks/ooand-lint-gate.py` or exclude `Program.cs` from Coverlet immediately (forbidden without explicit human instruction per `AGENTS.md §3` and weakens audit integrity).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Guarantees smooth green runs across `ooand-lint-gate`, `stop-verify-gate`, `pre-push`, and CI at 100.00%/100.00% (`INV-1`, `INV-2`, `INV-5`, `INV-6`, `INV-8`). Affects `docs/ARCHITECTURE.md` and all `docs/designs/01..05-*.md`.

---

## D-018 — Post-Review Cross-Spec Synchronization: `Vector` Class Semantics, Cross-Action DAG, Transaction & Reindex Hooks, Simplified Migration & `ScopeParentHolder` (2026-11-27)
* **Status:** Accepted (`Refines D-010, D-012, D-013, D-014`)
* **Context / Task Prompt:** Cross-spec consistency audit after Goldfish reviews uncovered six integration/documentation mismatches across `01..05`:
  1. `MoveCommand.cs` (frozen under `INV-3`) applies `??` to `IMovingObject.Position` and `Velocity`, causing `CS0019` if `Vector` is changed to `readonly struct` (`D-010`).
  2. `Macro.<action>` / WebSocket commands lacked an explicit `"Commands.InTransaction"` boundary hook connecting Spec `02`/`03` to Spec `04`/`05`.
  3. Initial entities from `game-rules.json` (`Spec 02`) were not hooked into `"Game.State.Reindex"` (`Spec 05`), leaving `SectorGrid` empty and `EntityIdGenerator` at `1` on new games.
  4. `EntitySpawnFactory.CreateLaunchCommand` (`Spec 05`) reused `MoveTorpedoCommand` directly (which resolves `"Commands.Move"` without `UpdateSector`/`CheckSectorCollisions`) instead of a composite projectile tick.
  5. `GameRulesParserBuilder` (`Spec 02`) omitted the cross-action `"Macro.<X>"` cycle check specified in `D-011`.
  6. `ARCHITECTURE.md`, `ROADMAP.md`, `.memory-bank/context.md`, and `Spec 01` (`IIngressGate`) still referenced superseded mechanisms (`PinnedBaseScopeCommand` from `D-012`, `Prepare/Commit/Abort`/`migrationEpoch` from `D-014`, `IIngressGate` from `D-013`).
* **Options Considered:**
  1. **Option A (Chosen):** Keep `Vector` as an immutable `sealed class : IEquatable<Vector>` (zero LINQ, defensive array cloning, `for`-loops) while keeping `Angle` as `readonly struct`; add L2B no-op defaults + L4 overrides for `"Commands.InTransaction"` and `"Game.State.Reindex"`; wrap `EntitySpawnFactory` projectile tick with `MoveTorpedoCommand` + `UpdateSector` + `CheckSectorCollisions`; restore cross-action DAG check in `GameRulesParserBuilder`; remove unused `IIngressGate` and align `ARCHITECTURE.md`/`ROADMAP.md`/`.memory-bank/context.md` with the Goldfish-reviewed `Spec 02`/`04` contracts (`Volatile`-only `ScopeParentHolder`, Stop-and-Serialize `/migrate/{start,import,forward}`, `"Server.Ingress.Paused"`).
  2. **Option B (Rejected):** Modify `MoveCommand.cs` to remove `??` (violates `INV-3` and breaks existing null-handling tests).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Eliminates `CS0019` without touching `MoveCommand.cs` (`INV-3`), restores `D-011` cross-action cycle safety, closes 2PC/SectorGrid integration gaps across `02..05`, and brings all docs into 1:1 alignment (`INV-9`).

---

## D-019 — Data-Driven Entities, Per-Game Collisions, Configurable Victory & Repeating Actions (2026-11-27)
* **Status:** Accepted (`Extends D-011, D-014, D-016`)
* **Context / Task Prompt:** Post-review audit of `Specs 02..05` exposed four gaps limiting the goal of describing arbitrary game rules and entities declaratively without editing core classes: (1) `MatchState` hardcoded `ObjectType == "Ship"` and `GameSnapshot` v2 omitted `players`/`is_game_over`/`winner_player_id` (losing eliminated players on migration); (2) `game-rules.json` lacked optional `templates`, `collisions`, and `victory_target` sections while `EntitySpawnFactory` hardcoded `[MoveTorpedoCommand, UpdateSector, CheckSectorCollisions]` instead of resolving a configurable tick action (preventing stationary mines or custom projectiles and breaking `ActiveActionDescriptor` restore); (3) reusable repeating `Bridge` actions (`Actions.Start`/`Actions.Stop`) had no 2-arg `(item, CommandArguments)` WebSocket entry point; (4) `ServerBootstrap` lacked startup `--plugins-dir` loading, breaking `RestoreServerFromSnapshotCommand` when games depend on plugin `.dll`s.
* **Options Considered:**
  1. **Option A (Chosen):** Extend `game-rules.json` with three optional root fields (`templates`, `collisions`, `victory_target`) while keeping C# plugins for new atomic formulas (`INV-3`, `INV-8`); resolve `MatchState` target type via `"Game.Victory.TargetType"` and preserve `Players`/`IsGameOver`/`WinnerPlayerId` in `GameSnapshot` v2; drive `EntitySpawnFactory` launch ticks via entity `"TickAction"` / `"Macro.ProjectileTick"` and add reusable pair collision commands (`"Commands.Collision.DestroyBoth"`, `"Commands.Collision.DestroyIfEnemy"`) plus WebSocket-callable `"Commands.Actions.Start"` / `"Commands.Actions.Stop"`; add optional `--plugins-dir` startup argument to `ServerBootstrap` and isolate `SnapshotValueCodecs` per `GameBaseScope_vN`.
  2. **Option B (Rejected):** Add a general-purpose expression/scripting language inside `game-rules.json` (rejected in `D-011`; explodes branch surface under `INV-8`).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-1..INV-9`. Preserves full backward compatibility with existing `game-rules.json` examples (all 3 new fields are optional) while enabling arbitrary entity types, templates, per-game collision pairings, repeating WebSocket actions, and lossless migration/restart with plugins. Affects `docs/designs/02..05-*.md`, `docs/ARCHITECTURE.md`, and `.memory-bank/context.md`.

---

## D-020 — End-to-End Integration Seam Fixes Across Specs `02..05` (2026-11-27)
* **Status:** Accepted (`Refines D-012, D-013, D-014, D-015, D-016, D-019`)
* **Context / Task Prompt:** Full end-to-end trace across `Specs 02..05` for arbitrary JSON rules + `.dll` plugins + WebSocket + 2PC + migration + hot-reload exposed 9 cross-spec integration defects:
  1. `GameBaseScopeBuilder` ran `baseInitializer` and plugin `registrationCommands` in the same `GameBaseScope_vN`, causing `scopes.dll` Trap #1 (`ArgumentException`) whenever a plugin overrode a built-in L2B key (e.g., `"Commands.CheckGameOver"`, `"Game.Collision.Check"`).
  2. `L2A serverScope` (`Game.Create.Transferable`, `Game.Restore`, `CaptureServerSnapshotCommand`) cannot see `L2B GameBaseScope_vN`, so resolving `"Snapshot.Codecs"` in `L2A` fails and misses plugin codecs.
  3. Ordering Catch-22 between `RegisterIoCDependencyGameState` (`MatchState` caching `"Game.Victory.TargetType"`) and `RegisterGameRulesCommand` (registering `"Game.Victory.TargetType"` then calling `"Game.State.Reindex"`).
  4. `ISectorGrid` lacked `Clear()`, leaving dead initial entities from `rules_config` as ghosts in `SectorGrid` after `Game.Restore`.
  5. `"Game.Step.Advance"` and `"Game.State.Broadcast"` were registered in `Spec 03` but never invoked in the live message pipeline.
  6. `StartActionCommand`/`StopActionCommand` (`Spec 05`) took `IDictionary<string, object>` (violating `ooand-lint-gate.py`), omitted repeater re-enqueue and failure cleanup in `ActiveActionRegistry`, lacked `Drain(int gameItemId, string propertyKey)` on `IActiveActionTracking`, and `Game.Restore` ignored `ActiveActionDescriptor.Args`.
  7. `SectorGrid` did not participate in 2PC (`ITransactionParticipant`), causing state drift on `Macro.Move` rollback.
  8. Unregistered collision pairs crashed movement instead of falling back to `"Game.Collision.Default"` (`"Commands.Empty"`), no built-in asymmetric handlers (`DestroyFirst`/`DestroySecond`) existed, `EntitySpawnFactory.Create` overwrote template `"Speed": 0` (phantom velocity on stationary mines), and missing `"OwnerId"` on neutral entities crashed `"Game.Item.Owner"`.
  9. `Spec 05 §2.7` omitted `"AngularVelocity"` (breaking `Macro.Rotate`), no WebSocket command existed to mutate properties (`Speed`/`AngularVelocity`) from `CommandArguments`, and `Spec 04` assumed one global server `rules_version` conflicting with multi-rules per server (`Spec 02`).
* **Options Considered:**
  1. **Option A (Chosen):** Fix all 9 cross-spec seams in one surgical pass without breaking `INV-1..INV-9` or `depth == 4`:
     * **`GameBaseScopeBuilder` (`Spec 02`):** Temporarily bind `"IoC.Register"` in the unpublished `GameBaseScope_vN` dictionary to allow upsert during plugin `registrationCommands`, then remove the temporary key before `holder.Swap(...)` (standard plugin `IoC.Register` overrides any built-in L2B key cleanly at `depth == 4`).
     * **L2B `"Snapshot.Codecs"` resolution (`Spec 04`):** Resolve `"Snapshot.Codecs"` inside `gameScope` (L4) or `holder.Current` (L2B), never directly in L2A `serverScope`.
     * **Lazy `TargetObjectType` & `ISectorGrid.Clear()` (`Spec 05`):** `MatchState` resolves `"Game.Victory.TargetType"` at evaluation time (eliminating the init Catch-22), `ISectorGrid` adds `Clear()` called at the start of `"Game.State.Reindex"`, and `SectorGrid` implements `ITransactionParticipant` (restoring touched object positions on 2PC rollback).
     * **Live step & broadcast wiring (`Spec 03`):** `InterpretMessageCommand` invokes `"Game.Step.Advance"` and `"Game.State.Broadcast"` after command execution; `BroadcastGameStateCommand` fast-paths (`return` with zero serialization) when `GetConnectionCount(gameId) == 0`.
     * **Lint-clean repeating `Actions.Start/Stop` & `Args` restore (`Spec 04`/`05`):** `StartActionCommand(int objectId, object targetItem, CommandArguments args)` and `StopActionCommand(int objectId, CommandArguments args)` contain zero `IDictionary`; `StartActionCommand` wraps in a repeater that unregisters `bridgeId` on exception; `IActiveActionTracking` adds `Drain(int gameItemId, string propertyKey)`; `Game.Restore` decodes `ActiveActionDescriptor.Args` when non-empty.
     * **Collision ergonomics & WebSocket property control (`Spec 03`/`05`):** `"Game.Item.Owner"` returns `""` when `"OwnerId"` is absent; `"Commands.Collision.DestroyIfEnemy"` treats `ownerA.Length > 0 && ownerA == ownerB` as friendly fire; `"Game.Collision.Event"` falls back to `"Game.Collision.Default"` (`"Commands.Empty"`) on missing pair key; add `"Commands.Collision.DestroyFirst"`, `"Commands.Collision.DestroySecond"`, and `"Commands.Properties.Set"` (via `CommandArguments`); preserve template `"Speed"` in `EntitySpawnFactory.Create` when explicitly set; add `"AngularVelocity"` to `Spec 05 §2.7`.
     * **Per-game rules validation on migration (`Spec 04`):** `POST /migrate/import` validates `schema_version == 2`, `rules_config` parseability, and an optional server allowlist of supported `rules_version`s rather than a single global version.
  2. **Option B (Rejected):** Defer fixes to implementation phase (risks mid-sprint spec churn across 3 parallel tracks).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-1..INV-9`, `scopes.dll` traps, `ooand-lint-gate.py`, and 100%/100% Coverlet. Specs `01..05` are now frozen for implementation starting with `Spec 01`. Affects `docs/designs/02..05-*.md`.

## D-021 — Rename `IGameReceiverTable` to `IGameReceiverRegistry` for `INV-5` Linter Compliance (2026-11-27)
* **Status:** Accepted
* **Context / Task Prompt:** `.agents/hooks/ooand-lint-gate.py:306` enforces `INV-5` via the regex `\binterface\s+I[A-Za-z0-9_]*(?:able|eble)\b`. The Day-0 contract `IGameReceiverTable` (introduced in Spec `01` and consumed in Specs `03` and `04`) ends in `Table` (`T` + `able`), triggering a hard lint error and blocking CI (`build-test-coverage`).
* **Options Considered:**
  1. Rename `IGameReceiverTable` → `IGameReceiverRegistry` (and `GameReceiverTable` → `GameReceiverRegistry` in Spec `03`) across `docs/ROADMAP.md` and `docs/designs/{01,03,04}-*.md`, leaving `.agents/hooks/ooand-lint-gate.py` untouched.
  2. Modify `.agents/hooks/ooand-lint-gate.py` to carve out an exception for `Table`.
* **Decision:** Option 1. Renaming preserves harness immutability (`AGENTS.md §3`), keeps CI green without hook drift, and clarifies the single-threaded per-thread receiver registry role.
* **Consequences & Invariants:** Day-0 file is `SpaceBattle/SpaceBattle.Lib/Endpoint/IGameReceiverRegistry.cs` (`IGameReceiverRegistry`); Spec `03` implements `GameReceiverRegistry : IGameReceiverRegistry`. Existing `DECISIONS.md` entries `D-001..D-020` remain untouched (`INV-9`).

## D-022 — Text-to-JSON Rules Translator (`GameRulesTextBuilder`) Over Canonical `game-rules.json` (2026-11-28)
* **Status:** Accepted (`Extends D-011`)
* **Context / Task Prompt:** The course instructor's concrete example specifies game rules as concise 4-clause Russian text lines (`"В игре есть Корабль, Ракета, Метеорит"`, `"Корабль может ДвигатьсяПоПрямой, Вращаться, Стрелять"`, `"ДвигатьсяПоПрямой включает Move, BurnFuel"`, `"BurnFuel до Move"`). Replacing `game-rules.json` directly would break `GameSnapshot` verbatim rule embedding (`D-011`, `D-014`) and `SnapshotValueCodecs` typed entity properties, whereas ignoring the text syntax leaves a gap against the instructor's rule-description format.
* **Options Considered:**
  1. **Option A (Chosen):** Add a single thin front-end translator file `GameRulesTextBuilder.cs` (in `SpaceBattle.Lib/Rules/`, exposing `"Game.Rules.TextToJson"` and `"Game.Rules.ParseText"`) that translates the 4-clause rule text into the canonical `game-rules.json` schema (`templates` with `"AllowedActions"` property via existing `System.String` codec, `entities: []`, `actions` with normalized `Commands.<Step>` names and `"<A> before <B>"` order constraints), keeping `GameRulesParserBuilder`, `GameRulesConfig`, and `GameSnapshot` untouched.
  2. **Option B (Rejected):** Replace `game-rules.json` with a standalone text parser inside `GameRulesParserBuilder` (breaks typed `SnapshotValueCodecs` entity definitions, `collisions`, `victory_target`, and `GameSnapshot` JSON transport).
* **Decision:** Adopt **Option A**.
* **Consequences & Invariants:** Satisfies `INV-1`, `INV-2` (zero `switch`), `INV-3`, `INV-4`, `INV-8` (100% line/branch coverage), and `INV-9`. Adds 1 translator file (`GameRulesTextBuilder.cs`) + 1 test file (`GameRulesTextBuilderTests.cs`) to `docs/designs/02-rules-and-scope-hot-reload.md` without modifying `GameRulesParserBuilder` or `GameRulesConfig`.
