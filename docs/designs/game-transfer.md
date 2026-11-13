# Spec: Game Snapshot and Transfer (`game-transfer`)

* **Decisions:** `D-005` in `docs/DECISIONS.md`
* **Target Repository:** `SpaceBattle/SpaceBattle.sln` (`SpaceBattle.Lib`, `SpaceBattle.Tests`)
* **Sources:** practice session 3 transcript, second half (`контекст/транскрипт ПЗ/ПЗ_3.md`); `контекст/docs/ROADMAP.md` stage 5.

## 1. Context, Problem, Goals & Non-Goals

### Problem
1. A game cannot leave the thread it was created on; its state exists only as live objects.
2. Three things block serialization today:
   * `Game` keeps its queue private, and queued commands are opaque `ICommand` objects with no type name or game item id.
   * `GameObjectRepository` has `Get`/`Set`/`Remove` by id but no way to list ids.
   * `Vector` has an indexer but no way to learn how many coordinates it has.

### Goals
* A game created as transferable can be moved to another `ServerThread` and continues: its items have the same serializable values, its pending commands execute on the target in the same order, and it runs under the same rules.
* The only data passed from the old location to the new one is a JSON string plus the target thread.
* Values without a codec are skipped when saving and are expected to be recreated by the rule set (practice session 3: non-serializable data is ignored on save and restored on load).
* No command sent to the game is lost or executed twice, whether it was sent before, during or after the transfer.
* 100% line and branch coverage, lint gate clean.

### Non-Goals
* Two OS processes, sockets, files on disk. The JSON string is the hand-over point for that later work.
* The waiting window of two message lifetimes from practice session 3. It is needed when commands travel over a network; inside one process forwarding gives the same guarantee without waiting.
* Commands that were not sent through `"Game.Command.Send"` (for example ad-hoc lambdas): they still execute, but a transfer does not carry them.
* Commands with arguments other than one game item. Long-running operations built with `"Actions.Start"` are out of scope until they are expressed as a key taking a game item.
* Version differences between servers (a rule set or command that exists only on one side fails with the container's "not found" error).
* Hot update of scopes.

### Core Scenarios
* `Given` a transferable game on thread 1 with two items and three pending commands `When` `"Game.Transfer"` to thread 2 executes `Then` the three commands execute on thread 2, in order, against items with the same values.
* `Given` an item holds a value with no codec (a command reference) `When` transferred `Then` the snapshot does not contain it and the transfer succeeds.
* `Given` the rule set registers a rule in the game scope `When` the game is transferred `Then` a command on the target resolves that rule.
* `Given` a command is sent through the old handle after the transfer `Then` it executes once, on thread 2.
* `Given` a command was already in thread 1's inbox when the transfer started `Then` it executes once, on thread 2, after the commands from the snapshot.
* `Given` JSON that is not a valid snapshot `When` restored `Then` `InvalidOperationException`.
* `Given` an item created by the rules was removed during play `When` the game is transferred `Then` the item does not exist on the target.

## 2. Architecture & Alternatives

### Snapshot format
```json
{
  "rules": "Standard",
  "command_limit": 2,
  "items": { "1": { "Position": { "type": "SpaceBattle.Lib.Vector", "value": [1, 2] }, "Fuel": { "type": "System.Int32", "value": 10 } } },
  "commands": [ { "id": "…", "type": "Commands.Move", "game_item_id": 1 } ]
}
```
`commands` is ordered; it is both the list of commands and the queue.

### Components (new, folder `SpaceBattle/SpaceBattle.Lib/Transfer/`)

| Component | Responsibility |
| --- | --- |
| `CommandDescription` | `Id`, `Type` (IoC key), `GameItemId`. |
| `PendingCommandJournal` | Ordered descriptions of commands that were sent and not yet executed: `Add`, `Remove(id)` → `bool`, `Clear`, `Pending`. Used only on the game's server thread. |
| `JournaledCommand` | Removes its description from the journal and executes the wrapped command only if the description was still there. A command left in the queue of a game that has moved away finds an empty journal and does nothing. |
| `IGameObjectIdSource`, `EnumerableGameObjectRepository` | Repository that also lists ids. Registered in the game scope under the existing `"Game.Repository.*"` keys; the existing `GameObjectRepository` is not modified. |
| `SnapshotValueCodecs` | Registry full type name → write/read functions. `TryWrite` returns `false` for `null` and for a type without a codec. `Read` throws `InvalidOperationException` for an unknown type or a malformed entry. |
| `RegisterIoCDependencySnapshotCodecs` | Registers `"Snapshot.Codecs"` with codecs for `int`, `string`, `Vector`, `Angle`. |
| `GameSnapshot` | Data shape above. |
| `GameSnapshotBuilder` | `Build(game, codecs)` → `GameSnapshot`; `ReadItem(written, codecs)` → item dictionary. |
| `GameSnapshotSerializer` | To and from a string with `System.Text.Json`. Invalid JSON or an incomplete snapshot (missing rules, non-positive command limit, `null` items, values or commands) → `InvalidOperationException`. |
| `IGameCommandSending` | `void Send(CommandDescription description)`. |
| `LocalGameCommandSending` | Server-thread side of a game: journals the description and delivers the command built from it to the game. |
| `RedirectingGameCommandSending` | Passes to its current target; `RedirectTo(target)` switches it. Used only on the old server thread. |
| `DelegatingGameCommandSending`, `DelegatingCommand` | Adapters from a delegate to `IGameCommandSending` and `ICommand`. They exist because the lint gate allows object creation only in registration files, so the wiring lives there as lambdas. |
| `TransferableGame` | Handle: `Sending` (safe from any thread: posts to the game's server thread), and the state the transfer needs: `Thread`, `Location`, `Journal`, `Repository`, `Rules`, `CommandLimit`, `Successor`. |
| `TransferGameCommand` | Runs on the game's server thread. If the game already moved, forwards the request to its successor. Otherwise: build and serialize the snapshot, resolve `"Game.Restore"` for the target, clear the journal, remember the successor, redirect the old location to it. |
| `RegisterIoCDependencyTransferableGame` | Registers `"Game.Create.Transferable"` (`ServerThread`, command limit, rule set name), `"Game.Restore"` (snapshot JSON, `ServerThread`), `"Game.Command.Send"` (`TransferableGame`, command key, game item id → `ICommand`), `"Game.Transfer"` (`TransferableGame`, target `ServerThread` → `ICommand`). |

### Modified
* `SpaceBattle/SpaceBattle.Lib/Struct/Vector.cs` — added `public int Dimension`.

### How a game is created or restored
1. A game scope is created as a child of the current scope.
2. Inside it: the quantum keys and the repository keys are registered, then the rule set `"Game.Rules.<name>"` is executed. This happens synchronously on the calling thread, before the game is reachable from any other thread.
3. On restore only: items the rules created that are absent from the snapshot are removed; snapshot values are merged into the remaining items; items the rules did not create are added.
4. The game, its turn command and its scheduling receiver are built for the target thread.
5. On restore only: snapshot commands are sent through the new handle, in order.

### Rules fixed by this spec
* The snapshot is taken by a command in the source thread's inbox, so it sees a game between turns.
* A described command is resolved as `Ioc.Resolve<ICommand>(type, item)` when it executes, not when it is sent.
* Order on the target: rule set, items, snapshot commands, then forwarded late commands. The old location is redirected only after the snapshot commands were posted, and inboxes are FIFO.
* Everything in `TransferGameCommand` that can throw runs before any state changes, so a failed transfer leaves the game working at its old location.
* The old handle stays valid, and a transfer requested through it is applied to the game's current location.

### Known limitations
* Every server thread a game has lived on must keep running: old handles forward through it. A command sent through a handle whose thread has stopped, or a transfer to a stopped thread, is lost without an error.
* Server threads taking part in a transfer must have been created from the same scope: the new game scope is created on the source thread and its parent is the scope current there.
* A property removed from an item during play reappears after a transfer if the rule set creates it; removal of whole items is preserved.
* Codec lookups fail with exceptions other than `InvalidOperationException` for malformed values inside a well-formed snapshot (for example an angle stored as a one-element array). Reading an `Angle` assigns the static `Angle.Denominator`, as every `Angle` construction does.
* Two end-to-end tests rely on the exact alternation of inbox commands and game turns in `ServerThreadLoopCommand`; a change of that loop requires revisiting them.

### Alternatives Considered
* **Reading the queue of `Game`:** requires modifying `Game` or reflection on a private field. Rejected (`INV-3`).
* **Describing a macro as a list of nested command ids (as drawn in practice session 3):** unnecessary here; `"Macro.Move"` is rebuilt from `"Specs.Move"` by its key. The deviation is recorded in `D-005`.
* **Waiting two message lifetimes before finishing:** deferred with the network work, see Non-Goals.
* **A type check `command is IDescribed` in the receiver:** rejected (`INV-2`); describable commands use their own entry `"Game.Command.Send"`.
* **Replacing restored items wholesale:** rejected; values without a codec would be lost. Merging into what the rules created keeps them.

## Detailed Implementation Plan

### Files (`Create` / `Modify`)
* `Create` in `SpaceBattle/SpaceBattle.Lib/Transfer/`: the 18 files named in the component table.
* `Modify`: `SpaceBattle/SpaceBattle.Lib/Struct/Vector.cs` (add `Dimension`).
* `Create` in `SpaceBattle/SpaceBattle.Tests/TransferTests/`: `SnapshotTests.cs` (journal, journaled command, repository, codecs, builder, serializer, sending classes, `Vector.Dimension` — no threads), `GameTransferTests.cs` (two real server threads).
* `Modify`: `.memory-bank/context.md`.

### Verification & 100% Test Plan
* Journal: order kept; removal by id reports whether it removed; clear.
* `JournaledCommand`: executes once; a second execution does nothing.
* Repository: `Ids` reflects `Set` and `Remove`; `Get`/`Remove` of a missing id → `KeyNotFoundException`.
* Codecs: round trip for `int`, `string`, `Vector`, `Angle`; `null` and a type without a codec are not written; missing `type`, missing `value`, unknown type → `InvalidOperationException`.
* Builder and serializer: a value without a codec is absent from the snapshot; full round trip; each kind of invalid or incomplete snapshot → `InvalidOperationException`.
* Sending classes: local journals and delivers; redirecting switches target.
* End to end on two threads: items, rules and pending commands move, in order; a value without a codec is recreated by the rules; an item removed during play stays removed; a command already in the old inbox and a command sent through the old handle after the transfer execute once on the target; the full log is compared after both threads stopped, so duplicates fail the test. A second transfer through the old handle, with a state change in between, moves the game from its current location back to the first thread.
* Commands: lint gate, `dotnet format --verify-no-changes`, `dotnet build --no-incremental`, `dotnet test --collect:"XPlat Code Coverage"` (`line-rate="1"`, `branch-rate="1"`).
