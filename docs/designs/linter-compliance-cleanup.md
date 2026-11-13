# Spec: Linter & Formatting Compliance Cleanup (`linter-compliance-cleanup`)

* **Decisions:** `D-001`, `D-002` in `docs/DECISIONS.md`
* **Target Repository:** `SpaceBattle/SpaceBattle.sln` (`SpaceBattle.Lib`, `SpaceBattle.Tests`)

## 1. Context, Problem, Goals & Non-Goals

### Problem
Running `python3 .agents/hooks/ooand-lint-gate.py --check-all .` and `dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes` on `main` fails with:
1. `INV-7` (`Game.cs:24,25,50`): `Game` reads/mutates `IoC.Scope.Current` internally and catches exceptions with `Console.WriteLine`.
2. `INV-5`: Five `-able` interfaces violate `-ing` naming (`ICollidable`, `ICommandInjectable`, `IMovable`, `IRotatable` in `IRotateble.cs`, `IShootable`).
3. `INV-8`: `RegisterIoCDependencyActionsStartTests.cs:23,42` contains `// Arrange` and `// Act` comments.
4. `dotnet format`: Trailing whitespace lines in `RegisterIoCDependencyActionsStartTests.cs:48` and `RegisterIoCDependencyActionsStopTests.cs:36`.

### Goals & Non-Goals
* **Goals:** Achieve exit code `0` for `ooand-lint-gate.py --check-all .`, `dotnet format --verify-no-changes`, `dotnet build --no-incremental`, and `dotnet test --collect:"XPlat Code Coverage"` with `line-rate="1"` and `branch-rate="1"`.
* **Non-Goals:** Do not refactor `Angle.Denominator` or `Vector` allocations or merge legacy branches in this cleanup task.

### Core Scenarios
* `Given` `InSeparateScopeCommand(targetScope, innerCommand)` `When` executed `Then` it saves `IoC.Scope.Current`, switches to `targetScope`, executes `innerCommand`, and restores previous scope in `finally` (even if `innerCommand` throws).
* `Given` `Game` `When` dequeued `command.Execute()` throws `ex` `Then` `Game` resolves and executes `Ioc.Resolve<ICommand>("Exception.Handle", command, ex)`.

## 2. Architecture & Alternatives

* **`INV-7` (`Game` & `InSeparateScopeCommand`):**
  * Rejected keeping scope switching inside `Game` or ignoring `Game.cs` in linter (violates `INV-7`).
  * `Game` becomes scope-blind: parameterless constructor `public Game()`, `Receive(ICommand command)`, `Execute()`. Inside `Execute()`, caught exceptions invoke `Ioc.Resolve<ICommand>("Exception.Handle", command, ex).Execute()`.
  * New command `InSeparateScopeCommand(object targetScope, ICommand innerCommand)` validates both parameters for `null` (`ArgumentNullException`) and manages `IoC.Scope.Current` / `IoC.Scope.Current.Set` around `_innerCommand.Execute()` using `try { ... } finally { ... }`.
* **`INV-5` (`-ing` interface renames + `"Adapters.*"` IoC keys):**
  * `ICollidable.cs` (`ICollidable`) -> `ICollidingObject.cs` (`ICollidingObject`)
  * `ICommandInjectable.cs` (`ICommandInjectable`) -> `ICommandInjecting.cs` (`ICommandInjecting`)
  * `IMovable.cs` (`IMovable`) -> `IMovingObject.cs` (`IMovingObject`), `"Adapters.IMovable"` -> `"Adapters.IMovingObject"`
  * `IRotateble.cs` (`IRotatable`) -> `IRotatingObject.cs` (`IRotatingObject`), `"Adapters.IRotatable"` -> `"Adapters.IRotatingObject"`
  * `IShootable.cs` (`IShootable`) -> `IShootingObject.cs` (`IShootingObject`), `"Adapters.IShootable"` -> `"Adapters.IShootingObject"`

## Detailed Implementation Plan

### Files (`Create` / `Modify` / Rename)
* `Create`: `SpaceBattle/SpaceBattle.Lib/Game/InSeparateScopeCommand.cs` — `InSeparateScopeCommand(object targetScope, ICommand innerCommand)` with null guards and `try/finally` scope switch/restore.
* `Create`: `SpaceBattle/SpaceBattle.Tests/GameTests/InSeparateScopeCommandTests.cs` — tests for constructor null guards (`targetScope`, `innerCommand`), normal execution scope switch & restore, and exception propagation with `finally` restore.
* `Modify`: `SpaceBattle/SpaceBattle.Lib/Game/Game.cs` — parameterless `Game`, remove `_gameScope` and `IoC.Scope.*`, replace `Console.WriteLine` with `Ioc.Resolve<ICommand>("Exception.Handle", command, ex).Execute()`.
* `Modify`: `SpaceBattle/SpaceBattle.Tests/GameTests/GameTests.cs` — run `Game` wrapped in `InSeparateScopeCommand(_testGameScope, game)` (or within active scope), register `"Exception.Handle"` verifying command + exception routing, remove obsolete null-scope constructor test (moved to `InSeparateScopeCommandTests`).
* `Modify` (git mv / rename + symbol updates):
  * `SpaceBattle/SpaceBattle.Lib/Interfaces/ICollidable.cs` -> `ICollidingObject.cs` + `CheckCollisionCommand.cs`, `CheckCollisionCommandTests.cs`
  * `SpaceBattle/SpaceBattle.Lib/Interfaces/ICommandInjectable.cs` -> `ICommandInjecting.cs` + `CommandInjectableCommand.cs`, `RegisterIoCDependencyActionsStart.cs`, `RegisterIoCDependencyActionsStop.cs`, `RegisterIoCDependencyActionsStartTests.cs`, `RegisterIoCDependencyActionsStopTests.cs`, `RegisterIoCDependencyCommandInjectableTest.cs`
  * `SpaceBattle/SpaceBattle.Lib/Interfaces/IMovable.cs` -> `IMovingObject.cs` + `MoveCommand.cs`, `RegisterIoCDependencyMoveCommand.cs`, `MoveCommandTests.cs`, `RegisterIoCDependencyMoveCommandTest.cs`
  * `SpaceBattle/SpaceBattle.Lib/Interfaces/IRotateble.cs` -> `IRotatingObject.cs` + `RotateCommand.cs`, `RegisterIoCDependencyRotateCommand.cs`, `RotateCommandTests.cs`, `RegisterIoCDependencyRotateCommandTest.cs`
  * `SpaceBattle/SpaceBattle.Lib/Interfaces/IShootable.cs` -> `IShootingObject.cs` + `ShootCommand.cs`, `RegisterIoCDependencyShootCommand.cs`, `ShootCommandTests.cs`, `RegisterIoCDependencyShootCommandTests.cs`
* `Modify`: `SpaceBattle/SpaceBattle.Tests/CommandsTests/RegisterIoCDependencyActionsStartTests.cs` — remove `// Arrange`, `// Act` and trailing whitespace.
* `Modify`: `SpaceBattle/SpaceBattle.Tests/CommandsTests/RegisterIoCDependencyActionsStopTests.cs` — remove trailing whitespace on line 36.

### Verification & 100% Test Plan
* Unit tests cover all branches of `InSeparateScopeCommand` (both null args, happy path, exception path) and `Game` (happy path, dequeued command exception routed to `"Exception.Handle"`, empty queue idle, null `Receive` ignored).
* Commands:
  * `python3 .agents/hooks/ooand-lint-gate.py --check-all .`
  * `dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes`
  * `dotnet build SpaceBattle/SpaceBattle.sln --no-incremental`
  * `dotnet test SpaceBattle/SpaceBattle.sln --collect:"XPlat Code Coverage"` (`line-rate="1"` and `branch-rate="1"`)
