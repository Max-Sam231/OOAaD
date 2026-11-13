# SpaceBattle (OOAnD) — Memory Bank Context

## 1. Project Overview & Team
Educational server-side space-combat game engine for the Object-Oriented Analysis and Design course (2nd semester). Game rules, entity types, composite actions, and atomic operation ordering (`BurnFuel before Move`) are constructed dynamically via registrations inside `IoC` scopes. 1st-semester state is preserved on branch `archive`.

* **Team:** Maxim Maryutin, Ivan Chvalyuk, Dmitry Zyuzin.
* **Stack:** C# / `.NET 10` (`SpaceBattle/SpaceBattle.sln`: `SpaceBattle.Lib`, `SpaceBattle.Tests`), xUnit, NSubstitute, Coverlet (**mandatory threshold: `100%` line and `100%` branch**).

## 2. Closed Binary Contracts (`SpaceBattle/Common/`)
Binaries from the base course delivery (not decompiled or modified):

* **`SpaceBattle/Common/core.dll` (`namespace App`):**
  * `public interface ICommand { void Execute(); }`
  * `public static class Ioc { public static T Resolve<T>(string dependency, params object[] args); }`
  * Base container strategy update key: `"Update Ioc Resolve Dependency Strategy"`.
* **`SpaceBattle/Common/scopes.dll` (`namespace App.Scopes`):**
  * `public class InitCommand : ICommand` — one-time initialization of the root scope tree (`new InitCommand().Execute()`).
  * Scope & registration keys: `"IoC.Register"`, `"IoC.Scope.Create"`, `"IoC.Scope.Create.Empty"`, `"IoC.Scope.Current"`, `"IoC.Scope.Current.Set"`, `"IoC.Scope.Current.Clear"`, `"IoC.Scope.Parent"`.

## 3. Current Modules & IoC Keys (`SpaceBattle/`)
* **Value types:** `Vector`, `Angle` (`SpaceBattle.Lib/Struct/`).
* **Operation interfaces (`-ing`):** `IMovingObject`, `IRotatingObject`, `ICollidingObject`, `ICommandInjecting`, `IShootingObject`, `ICommandReceiver`, `ITorpedoMovementProvider`, `IReadOnlyGameObjectRepository`, `IWritableGameObjectRepository`.
* **Commands & Game loop:** `MoveCommand`, `RotateCommand`, `ShootCommand`, `MacroCommand`, `AuthorizeCommand`, `CheckCollisionCommand`, `CommandInjectableCommand`, `SendCommand`, `Game`, `InSeparateScopeCommand`, `GameObjectRepository`, `RegisterIoCDependency*`.
* **IoC keys registered by `SpaceBattle.Lib` (`RegisterIoCDependency*` commands):**
  * `"Commands.Move"` (obj → `MoveCommand` via `"Adapters.IMovingObject"`), `"Commands.Rotate"` (via `"Adapters.IRotatingObject"`), `"Commands.Shoot"` (via `"Adapters.IShootingObject"`).
  * `"Commands.Torpedo.Move"` (`ITorpedoMovementProvider` → `MoveTorpedoCommand`; looks the torpedo up in `"Game.Repository.ReadOnly"`, then resolves `"Commands.Move"`).
  * `"Commands.Macro"` (`ICommand[]` → `MacroCommand`), `"Macro.Move"` / `"Macro.Rotate"` (`CreateMacroCommandStrategy`: reads command names from `"Specs.Move"` / `"Specs.Rotate"`, resolves each, wraps in `"Commands.Macro"`).
  * `"Commands.Send"` (→ `SendCommand`; args converted via `"Adapters.ICommand"` and `"Adapters.ICommandReceiver"`), `"Commands.CommandInjectable"` (→ `CommandInjectableCommand`).
  * `"Commands.Authorize"` (`(ICommand, subject, objectOwner)` → `AuthorizeCommand`; throws `InvalidOperationException` when subject ≠ owner).
  * `"Actions.Start"` (order dict with `"Receiver"`, `"Target"` → injectable sent to receiver), `"Actions.Stop"` (order dict with `"Injectable"` → injects `"Commands.Empty"`).
  * `"Game.Repository.ReadOnly"` / `"Game.Repository.Writable"` (one shared `GameObjectRepository`, objects are `IDictionary<string, object>` keyed by `int` id).
* **IoC keys resolved by `SpaceBattle.Lib` but registered nowhere in it (tests stub them; a real composition root does not exist yet):** `"Adapters.IMovingObject"`, `"Adapters.IRotatingObject"`, `"Adapters.IShootingObject"` (now obtainable via `RegisterIoCDependencyAdapter`, but nothing calls it outside tests), `"Adapters.ICommand"`, `"Adapters.ICommandReceiver"`, `"Commands.Empty"`, `"Specs.<Name>"`, `"Game.Collision.Check"` (`dx, dy, ddx, ddy` → `bool`), `"Game.Collision.Event"`, `"Game.Loop.CanContinue"`, `"Game.Loop.Idle"`, `"Exception.Handle"` (`(command, exception)` → `ICommand`).
* **Game loop:** `Game : ICommand, ICommandReceiver` owns a private `ConcurrentQueue<ICommand>`; `Execute()` loops while `"Game.Loop.CanContinue"`, runs one dequeued command per iteration, routes exceptions to `"Exception.Handle"`, runs `"Game.Loop.Idle"` on an empty queue. Scope switching is external: `InSeparateScopeCommand(targetScope, innerCommand)` sets the scope and restores the previous one in `finally`.
* **Adapter code generation (`SpaceBattle.Lib/Adapters/`, `D-003`, spec `docs/designs/adapter-codegen.md`):**
  * `new RegisterIoCDependencyAdapterFactory().Execute()` registers `"Adapters.Factory"` (`Type` → `IAdapterFactory`, built by `AdapterSourceBuilder` + Roslyn `AdapterFactoryCompiler`, cached per interface type inside the strategy).
  * `new RegisterIoCDependencyAdapter(typeof(I)).Execute()` registers `"Adapters.<I.Name>"` (game item → adapter) plus per-property keys `"<I.FullName>.<Property>"` (getter) and `"<I.FullName>.<Property>.Set"` (setter, returns `ICommand`). Key rules live only in `AdapterDependencyNaming`.
  * Standard property = read/write the dictionary entry named like the property. Non-standard getter = `[assembly: Dependency(typeof(I), "Property", typeof(Utility), "StaticMethod")]`, method signature `static object M(object[] args)`.
  * `new RegisterIoCDependencyCommandWithAdapter("Commands.X", typeof(XCommand)).Execute()` registers a command whose single constructor parameter is an interface; it does not register the adapter itself.
  * Only property-only interfaces are supported. NuGet: `Microsoft.CodeAnalysis.CSharp` 4.12.0.
* **Server thread and game scheduling (`SpaceBattle.Lib/Server/`, `D-004`, spec `docs/designs/server-thread-queues.md`):**
  * `new RegisterIoCDependencyServerThread().Execute()` registers `"Server.Thread.Create"` (→ `ServerThread`: `Receive` is the only thread-safe entry, `Start()`, `Join(timeout)`; its body runs in the scope current at creation), `"Server.Thread.Stop.Hard"` / `"Server.Thread.Stop.Soft"` (`ServerThread` → `ICommand` that posts the stop into that thread's inbox).
  * `new RegisterIoCDependencyGameCreate().Execute()` registers `"Game.Create"` (`ServerThread`, `int` command limit → `ICommandReceiver`). Each game gets its own scope (child of the scope current at creation) holding `"Game.Loop.CanContinue"` and `"Game.Loop.Idle"` backed by `GameQuantumState`; `Game` itself is unchanged.
  * Loop (`ServerThreadLoopCommand`): no ready game → block on the inbox; otherwise alternate one inbox command and one game turn. A turn runs at most the limit of commands; a game with an empty queue leaves the ready set until `SchedulingCommandReceiver.Receive` is called.
  * Threading rule: the receiver returned by `"Game.Create"` is safe to call from any thread (it posts to the thread's inbox). The scheduler, game queues and game scopes are used only on the server thread. To register rules in a game scope, deliver the registering command to the game.
  * `"Exception.Handle"` must be registered in the scope the thread is created in before `Start()`; if it is missing or throws, the process terminates.
* **Game snapshot and transfer (`SpaceBattle.Lib/Transfer/`, `D-005`, spec `docs/designs/game-transfer.md`):**
  * `new RegisterIoCDependencySnapshotCodecs().Execute()` registers `"Snapshot.Codecs"` (`SnapshotValueCodecs` for `Int32`, `String`, `Vector`, `Angle`, keyed by the full type name; a `null` or a value of any other type is skipped when saving).
  * `new RegisterIoCDependencyTransferableGame().Execute()` registers `"Game.Create.Transferable"` (`ServerThread`, command limit, rule set name → `TransferableGame`), `"Game.Command.Send"` (`TransferableGame`, IoC key of the command, game item id → `ICommand`), `"Game.Transfer"` (`TransferableGame`, target `ServerThread` → `ICommand`), `"Game.Restore"` (snapshot JSON, `ServerThread` → `TransferableGame`).
  * A rule set is an IoC key `"Game.Rules.<name>"` returning an `ICommand` that registers the game's rules and creates its items in the current (game) scope. It runs again on restore; restored values are then merged into the items it created, so non-serializable values come from the rules; items the rules created that are absent from the snapshot are removed.
  * Constraints: every thread a game has lived on must stay running, because old handles forward through it; server threads involved in a transfer must be created from the same scope.
  * A transferable game has its own `EnumerableGameObjectRepository` under `"Game.Repository.ReadOnly"` / `"Game.Repository.Writable"` in the game scope. Only commands sent through `"Game.Command.Send"` are journaled and survive a transfer; each is resolved again as `Ioc.Resolve<ICommand>(type, item)` when it executes.
  * Snapshot JSON: `{"rules", "command_limit", "items": {id: {property: {"type", "value"}}}, "commands": [{"id", "type", "game_item_id"}]}`. Transfer currently works between server threads of one process; the JSON string is the intended payload for a future cross-process channel.
  * `Vector.Dimension` was added for the codec.
* **Not in `SpaceBattle.Lib` yet:** network endpoint, two-process transfer, waiting for all agents and step numbers, transactions, a composition root that calls the registrations.

## 4. Harness & Status
* Architectural invariants (`INV-1..INV-9`): [`.agents/rules/ooand-invariants.md`](../.agents/rules/ooand-invariants.md) (`always_on`).
* Workflow: `docs/DECISIONS.md` (`egm-brainstorm`) → `docs/designs/<slug>.md` (`egm-draft`/`egm-review`/`egm-resolve`) → TDD (`csharp-developer`) → `adversarial-skeptic` + `spec-review` (`code-reviewer-qa`).
* Unmerged legacy feature branches preserved on remote for manual triage (all predate the `-ing` renames of `D-002`):
  * `origin/feature/5lab` (PR #65) — Roslyn adapter codegen (`AdapterCodeGenerator`, `AdapterCompiler`, `AutoRegisterCommandDependency`, `AdapterAttribute`). Violates `INV-6`: `Activator.CreateInstance` runs on every resolve, no compile-once cache, uses `[Adapter]` instead of `[Dependency]`. Generated property keys: `"<Interface>:<Property>.Get"` / `".Set"`.
  * `origin/feature/4lab_3` (PR #64) — `CheckSectorCollisionsCommand`, `UpdateSectorCommand`, `"Commands.MoveWithCollision"` registration.
  * `origin/feature/IsCollision`, `origin/feature/collision-data-processor` — `QuadrantCollisionDetector` + 1000-object dataset; no open PR.
* Harness merged into `main` on 2026-11-13 (PR #67). `.agents/hooks.json` uses tool names of another agent runtime (`run_command`, `replace_file_content`); under Claude Code the gates do not fire automatically — run them by hand (commands in `README.md`).
* `main` is protected by ruleset `auto merge`: changes only via PR, 0 required approvals, required check `build-test-coverage` (strict).
* Verified 2026-11-13 on `chore/add-harness`: 104 tests pass, 100% line / 100% branch, `ooand-lint-gate --check-all` clean.
* [x] Archived 1st-semester `main` to `archive` and deployed team harness in `main` (`D-001`).
* [x] Remediated `ooand-lint-gate` & `dotnet format` violations in `SpaceBattle/` (`D-002`, `docs/designs/linter-compliance-cleanup.md`).
