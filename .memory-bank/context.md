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
* **Not in `SpaceBattle.Lib` yet:** pluggable queues, transactions, server-to-server transfer, threads/server runtime, a composition root that calls the adapter registrations.

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
