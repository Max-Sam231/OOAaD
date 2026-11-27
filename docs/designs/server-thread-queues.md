# Spec: Server Thread Queues and Game Scheduling (`server-thread-queues`)

* **Decisions:** `D-004` in `docs/DECISIONS.md`
* **Target Repository:** `SpaceBattle/SpaceBattle.sln` (`SpaceBattle.Lib`, `SpaceBattle.Tests`)
* **Sources:** practice session 3 transcript (`контекст/транскрипт ПЗ/ПЗ_3.md`), practice session 4 (scope per game), `контекст/docs/ROADMAP.md` stages 1 and 4.

## 1. Context, Problem, Goals & Non-Goals

### Problem
1. `Game` owns a queue but nothing drives it: there is no thread, and its loop either spins or sleeps (`ServerIdleCommand` sleeps 1 ms).
2. One thread cannot serve several games: there is no notion of a turn, and no component decides which game runs next.
3. Commands arriving from outside have no thread-safe entry point.

### Goals
* A server thread that executes commands from a thread-safe inbox and gives ready games turns in round-robin order.
* The thread blocks only when no game is ready. While at least one game is ready it reads the inbox without blocking.
* A game turn executes at most N of its queued commands, then the next ready game runs.
* A game with an empty queue costs nothing: it is not scheduled until a command is delivered to it.
* A game is created with its own IoC scope and runs inside `InSeparateScopeCommand`.
* Hard stop (after the current command) and soft stop (when the inbox is empty and no game is ready).
* An exception in an inbox command or a game turn is routed to `"Exception.Handle"` and does not stop the thread.
* 100% line and branch coverage, lint gate clean, no existing `.cs` file modified.

### Non-Goals
* Network endpoint and message format (`контекст/docs/DECISIONS.md` D-006, D-007).
* Waiting until all agents answered; step numbers and discarding stale messages.
* Moving a game between threads or servers; several threads with load balancing.
* Bounded inbox and back-pressure; load test.
* Time-based quanta.

### Core Scenarios
* `Given` a started thread with no games `When` a command is sent to it `Then` the command is executed in that thread.
* `Given` two games with 5 queued commands each and a quantum of 2 `Then` commands execute in the order A,A,B,B,A,A,B,B,A,B.
* `Given` a game whose queue became empty `Then` it is not given turns `When` a command is delivered through its receiver `Then` it is scheduled again and the command executes.
* `Given` two games created with different registrations under the same key in their scopes `When` each executes a command resolving that key `Then` each sees its own registration.
* `Given` an inbox command that throws `Then` `"Exception.Handle"` receives that command and the exception, and the next command still executes.
* `Given` hard stop is sent while commands remain in the inbox `Then` the thread ends without executing them.
* `Given` soft stop is sent while commands remain and a game is ready `Then` the thread ends only after the inbox is empty and no game is ready.

## 2. Architecture & Alternatives

### Components (all new, folder `SpaceBattle/SpaceBattle.Lib/Server/`)

| Component | Responsibility |
| --- | --- |
| `IGameScheduler` | `void MakeReady(ICommand game)`, `bool HasReady { get; }`, `bool TryTakeReady(out ICommand game)`. |
| `RoundRobinGameScheduler` | FIFO of ready games plus a set that prevents a game from being queued twice. Not thread-safe; used only from its server thread. |
| `GameQuantumState` | Constructor `(int commandLimit)`, must be positive. `Start()` resets the counter and the idle mark. `CanContinue()` returns `false` when idle or when the limit is reached, otherwise counts one command and returns `true`. `MarkIdle()`, `IsIdle`. |
| `GameQuantumCommand` | Constructor `(ICommand game, GameQuantumState state, IGameScheduler scheduler)`. `Execute()`: `state.Start()`, run the game, and in `finally` call `scheduler.MakeReady(this)` unless the state is idle. |
| `SchedulingCommandReceiver` | `ICommandReceiver` wrapping the game's receiver: forwards the command, then calls `scheduler.MakeReady(gameTurn)`. This is the receiver from practice session 3 that signals the scheduler. |
| `ServerThreadState` | Holds the inbox and the scheduler. `RequestHardStop()`, `RequestSoftStop()`, `CanContinue` = not hard-stopped and not (soft-stopped and inbox empty and no ready game). |
| `ServerThreadLoopCommand` | Constructor `(BlockingCollection<ICommand> inbox, IGameScheduler scheduler, ServerThreadState state)`. While `state.CanContinue`, each iteration runs exactly one command: with no ready game it blocks on the inbox; with a ready game it alternates between one inbox command (taken without blocking, skipped if the inbox is empty) and one game turn. Every run is wrapped: an exception goes to `Ioc.Resolve<ICommand>("Exception.Handle", command, exception).Execute()`. |
| `HardStopServerThreadCommand`, `SoftStopServerThreadCommand` | Constructor `(ServerThreadState state)`; set the corresponding request. They run inside the thread, delivered through the inbox. |
| `ServerThread` | `ICommandReceiver`. Constructor `(Thread thread, BlockingCollection<ICommand> inbox, IGameScheduler scheduler, ServerThreadState state)`. `Receive` adds to the inbox (safe from any thread). `Start()`, `Join(TimeSpan)`. Exposes `Scheduler` and `State` for the registrations below. |
| `MarkGameIdleCommand` | Constructor `(GameQuantumState state)`; calls `MarkIdle()`. Returned by `"Game.Loop.Idle"`. |
| `RegisterIoCDependencyGameQuantum` | Constructor `(GameQuantumState state)`; registers `"Game.Loop.CanContinue"` and `"Game.Loop.Idle"` in the current scope. Executed inside the game scope. |
| `DelegatingCommandReceiver` | `ICommandReceiver` built from an `Action<ICommand>`. |
| `RegisterIoCDependencyServerThread` | Registers `"Server.Thread.Create"` → `ServerThread` (background thread) whose body is `InSeparateScopeCommand(scope current at creation, loop)`; `"Server.Thread.Stop.Hard"` and `"Server.Thread.Stop.Soft"` with `args[0]` = `ServerThread` → `ICommand` that sends the stop command to that thread's inbox. |
| `RegisterIoCDependencyGameCreate` | Registers `"Game.Create"` with `args[0]` = `ServerThread`, `args[1]` = command limit → `ICommandReceiver` of the new game. Creates a game scope (child of the current scope), a `Game`, registers `"Game.Loop.CanContinue"` and `"Game.Loop.Idle"` in the game scope from a new `GameQuantumState`, wraps the game in `InSeparateScopeCommand` and `GameQuantumCommand`. Returns a thread-safe receiver: its `Receive` posts `SendCommand(command, schedulingReceiver)` to the thread's inbox, so the `SchedulingCommandReceiver` itself is only ever called on the server thread. |

### How a command reaches a game
A caller on any thread calls `Receive` on the receiver returned by `"Game.Create"`. That posts a `SendCommand` to the server thread's inbox, which also wakes the thread if it is blocked. The server thread executes it; `SchedulingCommandReceiver` puts the command into the game queue and marks the game ready. `ServerThread.Receive` is the only cross-thread entry point; scheduler, game queue and game scope are touched only by the server thread. Rules are registered in a game scope the same way: by delivering the registering command to the game.

### Rules fixed by this spec
* While a game is ready, inbox commands and game turns alternate, one per iteration. If the inbox is empty on its turn, a game turn runs instead, so games may get consecutive turns. Neither side can starve the other, and a hard stop takes effect before the next command.
* The scope captured by `"Server.Thread.Create"` is read from the new thread. It must not be written after `Start()`; game-specific registrations go to game scopes.
* A game that throws from its own loop is handled by `"Exception.Handle"` inside `Game`; `GameQuantumCommand` still reschedules it in `finally` if it is not idle.
* `"Game.Create"` does not start anything. The game gets its first turn when its receiver gets its first command.

### Alternatives Considered
* **Thread per game / `Task`-based:** rejected, see `D-004`.
* **Time-based quantum:** rejected for now; non-deterministic in tests. The limit is one constructor argument, a time-based state can be added as a new class.
* **Changing `Game` to take a quantum or to report "idle":** rejected (`INV-3`); the two existing loop keys are enough.
* **A thread-local IoC scope holding `"Server.Thread.CanContinue"`:** rejected; it would put a thread scope between the game layer and game scopes and break the depth limit once the hot-update mediator scope is added (`INV-7`).

### Risks & Constraints
* **Real threads in tests:** every threaded test ends with a stop command and `Join` with a timeout, and server threads are background threads, so a bug fails the test instead of hanging the run.
* **Unbounded inbox:** a producer faster than the thread grows memory. Accepted until the endpoint and load test exist.
* **`"Exception.Handle"` has no registration in `SpaceBattle.Lib`:** as before, the composition root or the test must register it. It must be registered in the scope the thread is created in before `Start()`. If it is missing, or the handler itself throws, the exception is unhandled on the server thread and .NET terminates the whole process. This is deliberate: swallowing it would violate `INV-7`.
* **Stacked branch:** this work is cut from `feature/adapter-codegen`. If PR #68 changes after review, this branch is rebased.

## Detailed Implementation Plan

### Files (`Create` / `Modify`)
* `Create` in `SpaceBattle/SpaceBattle.Lib/Server/`: `IGameScheduler.cs`, `RoundRobinGameScheduler.cs`, `GameQuantumState.cs`, `GameQuantumCommand.cs`, `SchedulingCommandReceiver.cs`, `ServerThreadState.cs`, `ServerThreadLoopCommand.cs`, `MarkGameIdleCommand.cs`, `RegisterIoCDependencyGameQuantum.cs`, `DelegatingCommandReceiver.cs`, `HardStopServerThreadCommand.cs`, `SoftStopServerThreadCommand.cs`, `ServerThread.cs`, `RegisterIoCDependencyServerThread.cs`, `RegisterIoCDependencyGameCreate.cs`.
* `Create` in `SpaceBattle/SpaceBattle.Tests/ServerTests/`: `GameSchedulingTests.cs` (scheduler, quantum state, quantum command, scheduling receiver — no threads), `ServerThreadTests.cs` (loop, stops, exception routing — real thread), `GameCreateTests.cs` (round-robin order through the loop without a thread; on a real thread: scope isolation, delivery from another thread while the server waits, an exception inside a game turn).
* `Modify` (after implementation, via `memory-bank` skill): `.memory-bank/context.md`.
* No existing `.cs` file is modified. No new NuGet package.

### Verification & 100% Test Plan
* `RoundRobinGameScheduler`: empty → `HasReady` false and `TryTakeReady` false; FIFO order; the same game made ready twice is taken once; can be made ready again after being taken.
* `GameQuantumState`: non-positive limit → `ArgumentOutOfRangeException`; `CanContinue` true exactly `limit` times; false after `MarkIdle`; `Start` resets both.
* `GameQuantumCommand`: null arguments; reschedules when the limit ended the turn; does not reschedule when idle; reschedules when the game throws, and the exception propagates.
* `SchedulingCommandReceiver`: null arguments; forwards and marks ready.
* `ServerThreadState`: `CanContinue` for each combination that decides a branch (hard; soft with inbox non-empty; soft with a ready game; soft with nothing left).
* `ServerThreadLoopCommand` and `ServerThread`: command executes on the server thread (thread id differs from the test thread); exception routed to `"Exception.Handle"` and the loop continues; hard stop skips remaining commands; soft stop drains inbox and ready games; null arguments.
* `RegisterIoCDependencyGameCreate`: two games, quantum 2, five commands each → exact interleaving from the scenarios; two games resolve different registrations of the same key; an idle game is rescheduled by a later command.
* Commands:
  * `python3 .agents/hooks/ooand-lint-gate.py --check-all .`
  * `dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes`
  * `dotnet build SpaceBattle/SpaceBattle.sln --no-incremental`
  * `dotnet test SpaceBattle/SpaceBattle.sln --collect:"XPlat Code Coverage"` (`line-rate="1"` and `branch-rate="1"`)
