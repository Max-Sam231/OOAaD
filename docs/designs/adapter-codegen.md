# Spec: Adapter Code Generation (`adapter-codegen`)

* **Decisions:** `D-003` in `docs/DECISIONS.md`
* **Target Repository:** `SpaceBattle/SpaceBattle.sln` (`SpaceBattle.Lib`, `SpaceBattle.Tests`)
* **Sources:** practice session 2 transcript (`контекст/транскрипт ПЗ/ПЗ_2.md`), wiki lab 5 (`https://wiki.pmifi.ru/disciplines/ooaad/lab-di-and-adapter`), `origin/feature/5lab`.

## 1. Context, Problem, Goals & Non-Goals

### Problem
1. `RegisterIoCDependencyMoveCommand`, `RegisterIoCDependencyRotateCommand`, `RegisterIoCDependencyShootCommand` resolve `"Adapters.IMovingObject"`, `"Adapters.IRotatingObject"`, `"Adapters.IShootingObject"`. Nothing in `SpaceBattle.Lib` registers them; only tests stub them.
2. Writing an adapter and its registration by hand for every new interface is the step the course wants removed.

### Goals
* For any property-only interface, one command registers a working adapter: `Ioc.Resolve<TInterface>("Adapters.<InterfaceName>", gameItem)`.
* Generation and compilation happen once per interface type per factory cache (`INV-6`). No reflection on the resolve path.
* Standard properties (read or write the dictionary entry named like the property) need no code from the developer.
* A non-standard property is declared with one assembly-level `[Dependency]` attribute pointing to a static strategy method.
* A command with a single interface-typed constructor parameter is registered by one generic command, without a hand-written `RegisterIoCDependency*` class.
* 100% line and branch coverage, lint gate clean, no existing `.cs` file modified.

### Non-Goals
* Interfaces with methods or events (only properties are generated; others are rejected with `ArgumentException`).
* Interfaces that inherit other interfaces: rejected with `ArgumentException`. Reflection returns only the members an interface declares itself, and the course prefers composing commands over nesting interfaces; a combined object gets one adapter per interface.
* Generic interfaces, indexers, properties named with C# keywords, arrays of generic types: not handled specially; they fail at compile time with `InvalidOperationException` carrying the compiler diagnostics.
* Caching compiled assemblies on disk between process runs.
* Per-game override of a property strategy beyond what scopes already give.
* The fuel demo command and the runnable server (separate tasks).
* Replacing the existing hand-written `RegisterIoCDependencyMoveCommand` and siblings.

### Core Scenarios
* `Given` `"Adapters.Factory"` is registered `When` `new RegisterIoCDependencyAdapter(typeof(IMovingObject)).Execute()` runs `Then` `Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", item)` returns an object whose `Position` getter returns `item["Position"]` and whose `Position` setter writes `item["Position"]`.
* `Given` `[assembly: Dependency(typeof(ITestMovingObject), "Velocity", typeof(TestVelocityUtility), "Get")]` `When` the adapter's `Velocity` is read `Then` the value comes from `TestVelocityUtility.Get(new object[] { item })`, and `item` needs no `"Velocity"` entry.
* `Given` the adapter for an interface was registered in two different scopes `Then` the source was built and compiled once.
* `Given` a type that is not an interface, or an interface with a method `When` its factory is requested `Then` `ArgumentException`.
* `Given` generated source that fails to compile `Then` `InvalidOperationException` whose message contains the compiler diagnostics.
* `Given` `new RegisterIoCDependencyCommandWithAdapter("Commands.TestMove", typeof(TestMoveCommand)).Execute()` and a registered `"Adapters.ITestMovingObject"` `When` `Ioc.Resolve<ICommand>("Commands.TestMove", item)` `Then` the result is a `TestMoveCommand` holding the adapter for `item`.
* `Given` a command type with two constructors, or a constructor parameter that is not an interface `Then` `ArgumentException` at registration.

## 2. Architecture & Alternatives

### Components (all new, folder `SpaceBattle/SpaceBattle.Lib/Adapters/`)

| Component | Responsibility |
| --- | --- |
| `DependencyAttribute` | Assembly-level, `AllowMultiple = true`. Constructor `(Type interfaceType, string propertyName, Type strategyType, string strategyMethodName)`. C# attribute arguments must be constants, so the method is given as type + name. The method signature is `static object Name(object[] args)`. |
| `IAdapterFactory` | `object Create(IDictionary<string, object> gameItem)`. Hand-written so calling code knows the type at compile time. |
| `AdapterDependencyNaming` | Single place for key rules: `Adapter(Type)` → `"Adapters." + type.Name`; `Getter(Type, string property)` → `type.FullName + "." + property`; `Setter(...)` → getter + `".Set"`. |
| `AdapterSourceBuilder` | `string Build(Type interfaceType)`. Emits namespace `SpaceBattle.Generated` with class `<Name>Adapter : <interface>` and class `<Name>AdapterFactory : IAdapterFactory`. Getter body: `Ioc.Resolve<TProp>("<getter key>", _gameItem)`. Setter body: `Ioc.Resolve<ICommand>("<setter key>", _gameItem, value).Execute()`. Type names are emitted with `global::` and full generic arguments. |
| `AdapterFactoryCompiler` | `IAdapterFactory Compile(string source, string factoryTypeName)`. Roslyn `CSharpCompilation`; references are all loaded non-dynamic assemblies with a non-empty `Location`. Loads the result with `AssemblyLoadContext.Default.LoadFromStream` and creates the factory with `Activator.CreateInstance` — the only reflection-based instantiation, once per interface. |
| `RegisterIoCDependencyAdapterFactory` | Registers `"Adapters.Factory"`: `args[0]` is the interface `Type`, result is `IAdapterFactory`. The strategy closes over a `ConcurrentDictionary<Type, IAdapterFactory>` and uses `GetOrAdd`. Intended for the root or game-layer scope. |
| `GameItemPropertySetting` | `ICommand` returned by the standard setter strategy: writes `gameItem[key] = value`. |
| `RegisterIoCDependencyAdapter` | Constructor `(Type interfaceType)`. Registers in the current scope: the adapter key, and for every property a getter key and, if writable, a setter key. Getter strategy is the `[Dependency]` method when one matches, otherwise the standard dictionary read. |
| `RegisterIoCDependencyCommandWithAdapter` | Constructor `(string dependencyName, Type commandType)`. Registers `dependencyName` → command built from `Ioc.Resolve<object>("Adapters.<InterfaceName>", args)`. The constructor call is a delegate compiled once from an expression tree, so there is no reflection per resolve. Does not register the adapter itself, so two commands can share one interface. |

### Rules fixed by this spec
* `[Dependency]` attributes are collected from all loaded assemblies, so a plugin can supply a strategy for an interface it does not own. Two attributes for the same interface and property → `InvalidOperationException`.
* `[Dependency]` overrides the getter only. A writable non-standard property is out of scope until a case appears.
* The standard getter does not catch a missing dictionary key: `KeyNotFoundException` propagates.
* A `[Dependency]` naming a method that does not exist → `InvalidOperationException` at registration. A method with a wrong signature fails in `CreateDelegate` with `ArgumentException`.
* Write-only properties are supported: only the setter key is registered.
* Registering the same interface twice in one scope fails with the container's `ArgumentException` (verified behavior of `scopes.dll`); it is not hidden.
* Two threads requesting the same uncached interface may both compile; one result is kept. This is harmless and avoids a lock.

### Alternatives Considered
* **Merge `feature/5lab`:** rejected, see `D-003` option 1.
* **`Reflection.Emit` / `DispatchProxy`:** rejected, see `D-003` option 2.
* **Attribute on the interface (wiki lab 5 wording):** rejected. Changing the strategy would require editing the interface file; the assembly-level form keeps the interface untouched (practice session 2, third approach).
* **Explicit `IoC.Register` of the property key by the developer:** rejected. It exposes the key naming rule; if the rule changes, every such registration breaks.
* **Static cache field:** rejected in favor of a cache owned by the `"Adapters.Factory"` strategy, so tests and scopes stay isolated.

### Risks & Constraints
* **New NuGet package** `Microsoft.CodeAnalysis.CSharp` 4.12.0 — requires explicit team approval (`AGENTS.md` §2). Adds roughly 20 MB of assemblies to the build output.
* **Lint rule `INV-2`** flags `IDictionary<string, object>` in files named `*Command.cs`. `GameItemPropertySetting` is adapter infrastructure, the boundary where the dictionary is legitimately touched, so it is deliberately not named `*Command`. This must be visible to the reviewer.
* **First resolve cost:** the first compilation for an interface takes hundreds of milliseconds. It happens at registration time, not during the game loop.
* **Verified:** Roslyn 4.12.0 compiles and loads generated adapters on `net10.0`; 131 tests pass with 100% line and branch coverage.

## Detailed Implementation Plan

### Files (`Create` / `Modify`)
* `Modify`: `SpaceBattle/SpaceBattle.Lib/SpaceBattle.Lib.csproj` — add `<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />`.
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/DependencyAttribute.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/IAdapterFactory.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/AdapterDependencyNaming.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/AdapterSourceBuilder.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/AdapterFactoryCompiler.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/GameItemPropertySetting.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/RegisterIoCDependencyAdapterFactory.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/RegisterIoCDependencyAdapter.cs`
* `Create`: `SpaceBattle/SpaceBattle.Lib/Adapters/RegisterIoCDependencyCommandWithAdapter.cs`
* `Create`: `SpaceBattle/SpaceBattle.Tests/AdaptersTests/AdapterTestTypes.cs` — `ITestMovingObject` (`Vector Position { get; set; }`, `Vector Velocity { get; }`), `ITestWithMethod`, `TestVelocityUtility`, `TestMoveCommand`, `TestTwoConstructorsCommand`, `TestNonInterfaceParameterCommand`, and the assembly-level `[Dependency]` for `ITestMovingObject.Velocity`.
* `Create`: `SpaceBattle/SpaceBattle.Tests/AdaptersTests/AdapterSourceAndCompilerTests.cs` (naming, builder, compiler, `GameItemPropertySetting`), `RegisterIoCDependencyAdapterTests.cs` (factory cache, adapter registration, integration with `MoveCommand`), `RegisterIoCDependencyCommandWithAdapterTests.cs`.
* `Modify` (after implementation, via `memory-bank` skill): `.memory-bank/context.md` — new module and keys.

### Verification & 100% Test Plan
* `AdapterDependencyNaming`: adapter, getter, setter keys for `ITestMovingObject`.
* `AdapterSourceBuilder`: not an interface → `ArgumentException`; interface with a method → `ArgumentException`; read-only property emits a getter and no setter; read-write property emits both; generic property type is emitted with its arguments.
* `AdapterFactoryCompiler`: valid source → factory whose `Create` returns an object implementing the interface; invalid source → `InvalidOperationException` containing diagnostics.
* `RegisterIoCDependencyAdapterFactory`: returns a working factory; second request for the same type returns the same instance (compile-once).
* `GameItemPropertySetting`: writes the value under the key.
* `RegisterIoCDependencyAdapter`: standard getter reads the dictionary; standard setter writes it; `[Dependency]` getter calls the static method and ignores the dictionary; missing dictionary key → `KeyNotFoundException`; duplicate `[Dependency]` for one property → `InvalidOperationException`; null type → `ArgumentNullException`.
* `RegisterIoCDependencyCommandWithAdapter`: happy path end to end with a real generated adapter; type not implementing `ICommand` → `ArgumentException`; two constructors → `ArgumentException`; non-interface parameter → `ArgumentException`; null arguments → `ArgumentNullException`.
* Integration: `MoveCommand` resolved through the existing `RegisterIoCDependencyMoveCommand` plus a generated `"Adapters.IMovingObject"` moves a dictionary-backed object.
* Commands:
  * `python3 .agents/hooks/ooand-lint-gate.py --check-all .`
  * `dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes`
  * `dotnet build SpaceBattle/SpaceBattle.sln --no-incremental`
  * `dotnet test SpaceBattle/SpaceBattle.sln --collect:"XPlat Code Coverage"` (`line-rate="1"` and `branch-rate="1"`)
