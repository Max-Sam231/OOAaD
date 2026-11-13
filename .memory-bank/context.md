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
* **Core IoC keys:** `"Actions.Start"`, `"Actions.Stop"`, `"Commands.Macro"`, `"Commands.Send"`, `"Commands.CommandInjectable"`, `"Adapters.IMovingObject"`, `"Adapters.IRotatingObject"`, `"Adapters.IShootingObject"`, `"Game.Repository.ReadOnly"`, `"Game.Repository.Writable"`, `"Game.Loop.CanContinue"`, `"Game.Loop.Idle"`, `"Exception.Handle"`.

## 4. Harness & Status
* Architectural invariants (`INV-1..INV-9`): [`.agents/rules/ooand-invariants.md`](../.agents/rules/ooand-invariants.md) (`always_on`).
* Workflow: `docs/DECISIONS.md` (`egm-brainstorm`) → `docs/designs/<slug>.md` (`egm-draft`/`egm-review`/`egm-resolve`) → TDD (`csharp-developer`) → `adversarial-skeptic` + `spec-review` (`code-reviewer-qa`).
* Unmerged legacy feature branches preserved on remote for manual triage: `origin/feature/4lab_3`, `origin/feature/5lab`, `origin/feature/collision-data-processor`, `origin/feature/IsCollision`, `origin/feature/13_icommand_define`.
* [x] Archived 1st-semester `main` to `archive` and deployed team harness in `main` (`D-001`).
* [x] Remediated `ooand-lint-gate` & `dotnet format` violations in `SpaceBattle/` (`D-002`, `docs/designs/linter-compliance-cleanup.md`).
