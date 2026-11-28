# SpaceBattle (OOAnD) — Memory Bank Context

## 1. Project Overview & Team
Server-side space-combat game engine for the Object-Oriented Analysis and Design course (2nd semester). Game rules, entity types, composite actions, and atomic operation order (`CheckFuel before Move`) are declared in JSON rules and assembled dynamically inside `IoC` scopes (`INV-1..INV-9`).

* **Team:** Maxim Maryutin, Ivan Chvalyuk, Dmitry Zyuzin.
* **Canonical Repo & Baseline:** Team GitHub lineage `SpaceBattle/` (`origin/main` `4efb0a0` + Fork A `adapter-codegen` `b0f65b0` + Fork B `game-transactions` `dd1fe75` = **121 `.cs` files @ 100%/100%**; `SpaceBattle/SpaceBattle.sln`, CI `working-directory: SpaceBattle`). Always develop from the canonical git clone root (never inside ignored `контекст/`).
* **Stack:** C# / `.NET 10`, ASP.NET Core Kestrel (WebSockets), xUnit, NSubstitute, Coverlet (**mandatory threshold: `100%` line and `100%` branch**), Roslyn (`Microsoft.CodeAnalysis.CSharp 4.12.0`).

## 2. Closed Binary Contracts (`SpaceBattle/Common/`)
Binaries are carried over from the course base package without decompilation or modification:

* **`Common/core.dll` (`namespace App`):**
  * `public interface ICommand { void Execute(); }`
  * `public static class Ioc { public static T Resolve<T>(string dependency, params object[] args); }`
  * Base container strategy update key: `"Update Ioc Resolve Dependency Strategy"`.
* **`Common/scopes.dll` (`namespace App.Scopes`):**
  * `public class InitCommand : ICommand` — one-time initialization of the root scope tree (`new InitCommand().Execute()`).
  * Scope & registration keys:
    * `"IoC.Register"` — `(string key, Func<object[], object> strategy)` → `ICommand`
    * `"IoC.Scope.Create"` — `(object? parentScope)` → `object` (new scope)
    * `"IoC.Scope.Create.Empty"` → `object`
    * `"IoC.Scope.Current"` → `object` (current thread/context scope)
    * `"IoC.Scope.Current.Set"` — `(object scope)` → `ICommand`
    * `"IoC.Scope.Current.Clear"` → `ICommand`
    * `"IoC.Scope.Parent"` → `object`

### Verified `Common/scopes.dll` Traps
1. **Duplicate `IoC.Register` in the same scope** throws `ArgumentException` — never register per-server keys in L1 `RootScope` (breaks 2-server tests) and never call `IoC.Register("IoC.Scope.Parent", ...)` on a non-empty scope.
2. **Empty scope paralysis:** switching into `IoC.Scope.Create.Empty` breaks all `Ioc.Resolve` (`KeyNotFoundException: 'IoC.Scope.Parent'`). `InSeparateScopeCommand` resolves its restore command beforehand; `ProxyScopeFactory` assigns `dict["IoC.Scope.Parent"] = _ => holder.Current` before activation.
3. **Depth <= 4 (`INV-7`):** always pass explicit `parent` to `IoC.Scope.Create` (`L1 RootScope [primitives only]` → `L2A serverScope` [host] / `L2B GameBaseScope_vN` [game-visible shared] → `L3 ProxyScope` → `L4 GameScope`).
4. **Blind root error:** missing keys throw `The root scope has no a parent scope.` without naming the key.
5. **Storage:** `RootScope` is `ConcurrentDictionary`; child scopes are plain non-thread-safe `Dictionary`.

## 3. Architecture, Decisions & 5-Spec Plan
* **Invariants:** [`.agents/rules/ooand-invariants.md`](../.agents/rules/ooand-invariants.md) (`INV-1..INV-9`), enforced by `.agents/hooks/{guard-gate,ooand-lint-gate,stop-verify-gate}.py`, `scripts/githooks/pre-push`, and `.github/workflows/ci.yaml`.
* **Decision Log:** [`docs/DECISIONS.md`](../docs/DECISIONS.md) — `D-001..D-009` on `origin/*` preserved verbatim + **`D-010..D-020`** appended (`INV-9`).
* **Blueprint & DAG:** [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) (4-level scope diagram, 5-way Proxy/Bridge glossary, lint/100%-coverage seams, GoF & complexity oral-defense guide) and [`docs/ROADMAP.md`](../docs/ROADMAP.md).

### Status & 5 Delta Specs (`docs/designs/`)
* [x] Planning, Goldfish audits & 5 Goldfish-reviewed Zero-Decision specs (`D-010..D-019`).
* [ ] **Spec `01`** [`01-reconciliation-and-value-types.md`](../docs/designs/01-reconciliation-and-value-types.md) *(Day 0 Gate)* — Fork A+B merge, `Angle` `readonly struct` (`INV-8`) / `Vector` immutable `sealed class`, `Velocity` `[assembly: Dependency]`, shared `CommandDescription` + `IGameRouter`/`IGameReceiverTable`.
* [ ] **Spec `02`** [`02-rules-and-scope-hot-reload.md`](../docs/designs/02-rules-and-scope-hot-reload.md) *(Track A)* — `game-rules.json` + Kahn `"A before B"` sort + cross-action DAG, 4-level scope tree, `ProxyScopeFactory` + `Volatile` `ScopeParentHolder`, explicit `HotReloadGameBaseScopeCommand` + `ALC`.
* [ ] **Spec `03`** [`03-server-endpoint-routing-and-step.md`](../docs/designs/03-server-endpoint-routing-and-step.md) *(Track B)* — `SpaceBattle.Server` thin host (`Program.Main`), Kestrel WebSockets, $O(1)$ routing, non-blocking `"Game.Step.Validate"`, `/admin/*`, dual benchmarks ($\ge 20\text{–}50\text{k}$ msg/s).
* [ ] **Spec `04`** [`04-game-proxy-2proc-migration-and-snapshot.md`](../docs/designs/04-game-proxy-2proc-migration-and-snapshot.md) *(Track C)* — Deadlock-free `GameProxy : ICommand, ICommandReceiver`, Stop-and-Serialize HTTP 2-process migration (`/migrate/*`) + `2*T` quarantine, quiesced `ServerSnapshot`, complete 2PC transactions (`"Game.Command.Receiver"`).
* [ ] **Spec `05`** [`05-gameplay-collisions-and-e2e.md`](../docs/designs/05-gameplay-collisions-and-e2e.md) *(Integration)* — `collision_matrix.csv` + sectors, `CheckFuel`/`BurnFuel`, IoC `Destroy` + Victory Read Model, 3-layer Proxy-agent E2E.
