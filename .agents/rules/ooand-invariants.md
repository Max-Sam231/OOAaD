---
trigger: always_on
description: Hard architectural invariants for OOAnD (SpaceBattle). Read by all agents and enforced by hooks.
---

# OOAnD Architectural Invariants (`ooand-invariants`)

> Violating any rule below is a blocking defect, not a trade-off. If an idea, spec, or diff violates an invariant, the agent must stop and cite the exact `INV-*` ID.

* **`INV-1` (Dependency resolution via `Ioc.Resolve` only):** Application and domain code (`src/`) MUST NOT use `new` to instantiate commands, adapters, strategies, or services. Object creation is allowed only inside `IoC.Register` registration delegates, compiled factories, startup scope initialization, immutable value-type structs (`Vector`, `Angle`), and exception throws (`throw new ...`).
* **`INV-2` (No type branching or raw dictionaries):** Domain commands MUST NOT use `switch`, type-check cascades (`is` / `as`), or direct access to `IDictionary<string, object>`. Commands operate exclusively through narrow typed interfaces backed by adapters.
* **`INV-3` (Strict Open-Closed Principle):** New entities, rules, checks, and actions are introduced by adding new command/strategy classes and new `IoC.Register` entries (`git A`), without modifying existing battle-tested command classes (`git M`).
* **`INV-4` (Composition via `MacroCommand` over inheritance):** Composite actions (e.g., `MoveStraight` combining `BurnFuel` and `Move`) are assembled dynamically via `MacroCommand` and topological sorting of an ordering graph (`A before B`), rather than through game-object class inheritance.
* **`INV-5` (`-ing` naming for operation interfaces):** Operation interfaces describe an ongoing action on an object and use present-participle names (`IMovingObject`, `IRotatingObject`, `IFuelBurningObject`). The `-able` suffix (`IMovable`, `IRotatable`) is forbidden.
* **`INV-6` (Compile-once adapter factories and `[Dependency]`):** Adapter C# code generation and Roslyn compilation run once per interface type. `Activator.CreateInstance` is permitted once to instantiate a compiled factory and is strictly forbidden on the hot path of every `Ioc.Resolve`. Derived properties (such as computing a velocity vector from scalar speed and angle) are wired via external strategies using the assembly-level `[Dependency]` attribute rather than hardcoding them into the generator.
* **`INV-7` (`Game` scope blindness and threading model):** The `Game` class has no knowledge of `IoC.Scope.*` and never switches scopes internally — setting and restoring the game scope is handled by an external wrapper command (`InSeparateScopeCommand`). Exceptions inside the game loop are never swallowed into `Console.WriteLine`; they are routed through an `ExceptionHandler` resolved from `IoC`. `RootScope` is thread-safe, game scopes are single-threaded, and scope hierarchy depth MUST NOT exceed 4.
* **`INV-8` (100% test coverage and zero code comments):** Coverlet test coverage MUST be **100% line coverage (`line = 100%`) and 100% branch coverage (`branch = 100%`)**. Any comments in source code (`//`, `/* */`, `///`) are forbidden. Immutable structs (`Angle`, `Vector`) MUST NOT hold mutable global `static` state.
* **`INV-9` (Append-only decision log):** Every architectural fork is recorded in `docs/DECISIONS.md` (`D-NNN`) before writing a spec. Existing entries in `docs/DECISIONS.md` are never deleted or edited retroactively.
