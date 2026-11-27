# Spec: Transactional Game Items (`game-transactions`)

* **Decisions:** `D-006` in `docs/DECISIONS.md`
* **Target Repository:** `SpaceBattle/SpaceBattle.sln` (`SpaceBattle.Lib`, `SpaceBattle.Tests`)
* **Sources:** practice session 4 transcript, second half (`контекст/транскрипт ПЗ/ПЗ_4.md`).

## 1. Context, Problem, Goals & Non-Goals

### Problem
`MacroCommand` executes its commands in order and stops at the first exception. Whatever the earlier commands wrote stays written. Two examples from practice session 4:
1. Burn fuel, then move; the move fails → fuel is gone, position unchanged.
2. Move, then check fuel; the check throws → the ship moved without fuel, and repeating the command moves it again.

### Goals
* A command wrapped in a transaction either applies all its writes to game items or none.
* Commands and adapters do not know about transactions: they read and write the game item as before.
* Inside a transaction a command sees the values written earlier in the same transaction.
* Transactions nest: an inner failure caught by the outer command undoes only the inner writes; an outer failure undoes everything, including inner transactions that already committed.
* 100% line and branch coverage, lint gate clean, no existing `.cs` file modified.

### Non-Goals
* Concurrent transactions, locks, dirty-read and dirty-write policies, conflict merging.
* Saga and compensating commands.
* Transactions that span several games or threads.
* Undoing effects other than writes to transactional game items: commands sent to queues, items added to or removed from the repository, keys removed from an item.
* The "second way" announced in practice session 4 (the recording ends before it).

### Core Scenarios
* `Given` a transactional item with `Fuel = 10`, `Position = (1, 2)` `When` a transaction runs [burn fuel, failing move] `Then` the exception propagates and `Fuel = 10`, `Position = (1, 2)`.
* `Given` the same item `When` a transaction runs [move, burn fuel] successfully `Then` both changes are visible after it.
* `Given` a transaction wrote `Fuel = 9` `When` a later command in the same transaction reads `Fuel` `Then` it gets 9, while the underlying dictionary still holds 10 until commit.
* `Given` an outer transaction [write A, inner transaction [write B, throw] caught by the outer command, write C] `Then` after commit A and C are applied and B is not.
* `Given` an outer transaction [inner transaction [write B] commits, then throw] `Then` B is not applied.
* `Given` no open transaction `When` an item is written `Then` the value goes straight to the underlying dictionary.
* `Given` no open transaction `When` commit or rollback is called `Then` `InvalidOperationException`.

## 2. Architecture & Alternatives

### Components (all new, folder `SpaceBattle/SpaceBattle.Lib/Transactions/`)

| Component | Responsibility |
| --- | --- |
| `GameTransactions` | Per-game transaction manager and journal. `Begin()`, `Commit()`, `Rollback()`, `IsActive`. A stack of journals; a journal maps (item, key) to the written value. `TryWrite(item, key, value)` writes to the innermost journal and returns `false` when no transaction is open. `TryRead(item, key, out value)` searches journals from innermost to outermost. `Pending(item)` lists journaled writes for an item, outermost first. Not thread-safe; used on the game's server thread. |
| `TransactionalGameItem` | Implements `IDictionary<string, object>` over an underlying dictionary and a `GameTransactions`. Indexer get: journal first, then the dictionary. Indexer set and `Add`: journal if a transaction is open, otherwise the dictionary. Enumeration, `Keys`, `Values`, `Count`, `ContainsKey`, `TryGetValue`, `Contains`, `CopyTo` work on the merged view. `Remove` and `Clear` throw `NotSupportedException` while a transaction is open and act on the dictionary otherwise. |
| `InTransactionCommand` | Constructor `(ICommand command, GameTransactions transactions)`. `Execute()`: begin; run the command; on exception roll back and rethrow; otherwise commit. |
| `RegisterIoCDependencyTransactions` | Executed in a game scope. Creates one `GameTransactions` and registers `"Game.Transactions"` (→ it), `"Game.Item.Transactional"` (dictionary → `TransactionalGameItem`), `"Commands.InTransaction"` (`ICommand` → `InTransactionCommand`). |

### How it is used
A rule set that wants transactional movement registers, in the game scope, for example `"Movement"` → `Ioc.Resolve<ICommand>("Commands.InTransaction", Ioc.Resolve<ICommand>("Macro.Move", item))`, and stores items as `Ioc.Resolve<IDictionary<string, object>>("Game.Item.Transactional", dictionary)`. Nothing else changes: generated adapters and their standard property strategies index the item, which is now the transactional object.

### Rules fixed by this spec
* Commit of the outermost transaction applies the last journaled value of every key to the underlying dictionaries. The order between different keys is not defined.
* The journal identifies an item by reference to its underlying dictionary, so two transactional wrappers over the same dictionary share writes. Wrapping a `TransactionalGameItem` in another one is not supported: the outer wrapper would journal against the inner wrapper, not the dictionary.
* An exception from the wrapped command is rethrown unchanged after rollback.
* A snapshot for game transfer (`D-005`) is taken between turns, when no transaction is open, so it sees committed values only.

### Alternatives Considered
* **Whole-item copy / saga:** see `D-006`.
* **A journal table with transaction ids and stored statuses, flushed lazily on the next write (as drawn in practice session 4):** it exists to serve concurrent transactions. With one thread per game it gives the same observable behavior as the stack of journals but needs status lookups on every read and a separate clean-up step. The deviation is recorded in `D-006` because the teacher's drawing differs.
* **Hooking the adapter property strategies instead of the item:** commands that touch the dictionary without an adapter would bypass the transaction.

### Risks & Constraints
* **`IDictionary<string, object>` has about fifteen members**; all must be implemented and tested for 100% coverage. Most are one-line delegations to the merged view.
* **The merged view is rebuilt on each enumeration-style call** (`Keys`, `Count`, …). The hot path — indexer get and set — does not build it.
* **Only items created through `"Game.Item.Transactional"` are protected.** A plain dictionary in the same macro command is written immediately.
* **Items restored by a game transfer lose the wrapper.** An item created during play and restored by `"Game.Restore"` (`D-005`) is a plain dictionary, so it is no longer transactional on the target; items recreated by the rule set keep whatever the rule set gives them. Closing this needs an item-factory key used by restore and is left for a later decision.
* **The underlying dictionary must not throw on write.** Commit applies writes one by one; if one throws, earlier ones stay applied. This cannot happen with `Dictionary<string, object>`.
* **Wrapped commands must not call `Begin`, `Commit` or `Rollback` on `"Game.Transactions"` themselves.** `InTransactionCommand` assumes the stack depth is the same before and after the wrapped command.
* **Stacked branch:** fourth in the chain after PR #68, #69, #70.

## Detailed Implementation Plan

### Files (`Create` / `Modify`)
* `Create` in `SpaceBattle/SpaceBattle.Lib/Transactions/`: `GameTransactions.cs`, `TransactionalGameItem.cs`, `InTransactionCommand.cs`, `RegisterIoCDependencyTransactions.cs`.
* `Create` in `SpaceBattle/SpaceBattle.Tests/TransactionsTests/`: `GameTransactionsTests.cs` (manager and item), `InTransactionCommandTests.cs` (scenarios above, including one with a generated adapter, the existing `MoveCommand` and `MacroCommand`).
* `Modify` (after implementation): `.memory-bank/context.md`.
* No existing `.cs` file is modified. No new NuGet package.

### Verification & 100% Test Plan
* `GameTransactions`: commit/rollback without a transaction → `InvalidOperationException`; `TryWrite` without a transaction → `false`; read-your-writes; inner commit merges into outer; inner rollback keeps outer writes; the merged view (`TryGetValue`, `Values`) shows the innermost value when a key is written at two levels; outermost commit applies to the dictionary; rollback applies nothing.
* `TransactionalGameItem`: every `IDictionary` member with and without an open transaction; `Add` of an existing key → `ArgumentException`; `Remove`/`Clear` inside a transaction → `NotSupportedException`; null constructor arguments.
* `InTransactionCommand`: commit on success; rollback and rethrow on failure; nested scenarios; null constructor arguments.
* Integration: transactional item + generated `IMovingObject` adapter + `MacroCommand` of [`MoveCommand`, failing command] inside `"Commands.InTransaction"` → `Position` unchanged; without the transaction the same macro leaves `Position` changed (shows the problem the feature solves).
* Commands: lint gate, `dotnet format --verify-no-changes`, `dotnet build --no-incremental`, `dotnet test --collect:"XPlat Code Coverage"` (`line-rate="1"`, `branch-rate="1"`).
