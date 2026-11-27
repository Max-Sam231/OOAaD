# Спецификация `04`: Deadlock-Free `GameProxy`, 2-процессная миграция, `ServerSnapshot` и полные 2PC-транзакции

* **Статус:** Zero-Decision Delta Spec (поверх сведённого `origin/*` после Spec `01`)
* **Трек / Владелец:** Track C — Teammate 3 (Максим)
* **Связанные решения (`docs/DECISIONS.md`):** `D-010`, `D-014`, `D-015`, `D-017`, `D-018`, `D-019`, `D-020`, `D-021` *(расширяет `origin/*` `D-004`, `D-005`, `D-006`)*
* **Зависимости (DAG):** `01-reconciliation-and-value-types.md` (`CommandDescription`, `IGameRouter`, `IGameReceiverRegistry`); стыкуется по контрактам с `02-rules-and-scope-hot-reload.md` (`ProxyScope`, `Game.Rules.{Parse,Apply,Version,Config}`) и `03-server-endpoint-routing-and-step.md` (`DeliverMessageCommand`, `Server.Ingress.Paused`, `Server.Thread.TryEnqueue`, `ServerBootstrap`).
* **Целевое решение:** `SpaceBattle/SpaceBattle.sln` (`SpaceBattle.Lib`, `SpaceBattle.Server`, `SpaceBattle.Tests`)
* **Источники требований:** `ПЗ_3:249–273, 409–736`, `ПЗ_4:469–678`, `ПЗ_5 00:00–01:36, 06:40–10:15`, `ПЗ_6 50:00–64:20`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`.

---

## 1. Контекст, проблема, цели и границы (Goals & Non-Goals)

### 1.1. Проблема (аудит унаследованного кода `origin/*`)
Унаследованные ветки Fork B (`origin/feature/server-thread-queues`, `origin/feature/game-transfer`, `origin/feature/game-transactions`) содержат базовую реализацию `ServerThread`, `GameSnapshot`, `RedirectingGameCommandSending` и `GameTransactions`, но не удовлетворяют обязательным требованиям защиты (`ПЗ_3`–`ПЗ_6`):
1. **Deadlock и блокирующий I/O при миграции (`ПЗ_6 50:00–64:20`):**
   * `ServerThread.Receive` вызывает блокирующий `_inbox.Add(command)`, а `TransferGameCommand` внутри `ServerThread` вызывает `Game.Restore(..., _target)` с поочерёдной отправкой `snapshot.Commands` в `_target._inbox.Add(...)` → гарантированный **mutual deadlock** при одновременной взаимной миграции на ограниченных очередях (`boundedCapacity`).
   * `RedirectingGameCommandSending` не реализует `GameProxy : ICommand, ICommandReceiver` с собственной очередью (`ПЗ_6 50:00–64:20`).
   * `HardStop` доставляется через `_inbox.Add(...)` и зависает на переполненной ограниченной очереди; `SoftStop` преждевременно завершает поток, пока `GameProxy` ждёт окно карантина `2*T`.
2. **Неполный `GameSnapshot` и отсутствие 2-процессной миграции (`ПЗ_3:409–736`):**
   * Старая `Game` остаётся в планировщике Thread A после переноса.
   * `GameSnapshot` v1 теряет длительные действия (`CommandInjectableCommand` / `BridgeCommand` + `Repeater` — движущиеся корабли замирают), `next_entity_id` (новые сущности перезаписывают живые корабли), `current_step`, `rules_version` и `rules_config`.
   * Отсутствует простой HTTP-перенос JSON между двумя ОС-процессами и окно карантина `2*T` (`ПЗ_3:644–648`).
3. **Отсутствие атомарного `ServerSnapshot` (`shutdown & restore`, `ПЗ_5 09:30–10:15`):**
   * Невозможно консистентно остановить сервер со всеми потоками, сохранить полный JSON-снимок (`thread_count`, `game_to_thread_map`, массив `GameSnapshot` v2) и восстановить сервер с продолжением всех партий.
4. **Дыры целостности и производительности в 2PC-транзакциях (`D-015`):**
   * `Game.Restore` создаёт обычные `Dictionary<string, object>`, теряя `TransactionalGameItem`.
   * `TransactionalGameItem.Remove`/`Clear` бросают `NotSupportedException` внутри открытой транзакции.
   * `GameObjectRepository` и `Game._queue` не журналируются — при откате выстрела боезапас возвращается, а снаряд и `MoveTorpedoCommand` остаются в игре.
   * `TransactionalGameItem.View()` аллоцирует новый `Dictionary` на каждый `TryGetValue`/`ContainsKey`, деградируя бенчмарк $\ge 20\text{–}50\text{k}$ msg/s, а ключи журнала `(item, key)` не используют `ReferenceEqualityComparer`.

### 1.2. Измеримые цели (Goals)
1. **Deadlock-free `GameProxy : ICommand, ICommandReceiver`:** неблокирующий `ITryCommandReceiver.TryReceive` на `ServerThread`, `CancellationToken`-основанный `HardStop`, `ActiveProxies`-защита в `SoftStop`, квантованный `TryPeek -> TryReceive -> TryDequeue` (при `TryReceive == false` прокси не блокируется и завершает текущий квант, уступая планировщик другим играм — 0 deadlock, 0 overhead на немигрирующие игры).
2. **Простая 2-процессная Stop-and-Serialize миграция:** сериализация в `GameSnapshot` v2 (включая `BridgeCommand` / `ActiveActionDescriptor`, `next_entity_id`, `rules_version`, `rules_config`) → `POST /migrate/import` на 2-й процесс → переключение роутера → пересылка in-flight через `AsyncChannelCommandReceiver` (`POST /migrate/forward`) + единый IoC-ключ карантина `"Game.Transfer.Quarantine"` (`2*T`).
3. **Атомарный `ServerSnapshot` (`shutdown & restore`):** quiesce-барьер по всем `ServerThread` (флаг `"Server.Ingress.Paused"` → дренаж активных прокси → остановка после текущего кванта без сброса очередей игр → атомарная запись JSON) и полное восстановление `RestoreServerFromSnapshotCommand`.
4. **Полные in-memory 2PC-транзакции игры:** фабрика `"Game.Item.Create"` в `GameScope` и `Game.Restore`, tombstones для `Remove`/`Clear`, `TransactionalGameObjectRepository`, `TransactionalCommandReceiver` outbox, zero-allocation `TryGetValue`/`ContainsKey` и `ReferenceEqualityComparer` для ключей журнала.
5. **Полное соответствие гейтам (`D-017`, `INV-1..INV-9`):** **100.00% line & branch** Coverlet, 0 `record` в продакшн-коде, 0 `switch`, 0 `-able`-интерфейсов, 0 `IDictionary<string, object>` в `*Command.cs`, 0 комментариев, строгая изоляция `new` в `INFRA_FILE_MARKERS`.

### 1.3. Явные Non-Goals (границы)
* Распределённый 2PC поверх HTTP (`Prepare`/`Commit`/`Abort`, `PreparedGamesRegistry`) — избыточно для требования `ПЗ_3:409–413` («сериализовали в JSON → переслали на 2-й процесс → запустили»).
* `migrationEpoch`, `MigrationDrainGate`, `A → B → A` anti-ping-pong, 1-hop router fallback и `ExponentialGameProxyBackoff` — избыточная распределённая магия сверх критериев `ПЗ_6 63:05–64:20`.
* Zero-downtime миграция, `Saga`, `Nginx` Reverse Proxy *(Deferred Backlog, `docs/ROADMAP.md`)*.
* Изменение `.agents/hooks/*` или порогов Coverlet *(запрещено `D-017`)*.

### 1.4. Ключевые сценарии (`Given → When → Then`)
1. **Взаимная миграция на ограниченных очередях (Zero Deadlock / Zero Overhead):**
   * *Given* два `ServerThread` (A и B) с `boundedCapacity = 1` и переполненными inbox, Game 1 на A, Game 2 на B, плюс здоровая Game 3 на A.
   * *When* одновременно запускаются `Game 1: A → B` и `Game 2: B → A`, и в оба потока поступают in-flight команды.
   * *Then* оба `GameProxy` при `TryReceive == false` не блокируются и завершают квант, отдавая CPU здоровой Game 3; после освобождения слотов обе миграции завершаются без потери и перестановки команд.
2. **2-процессная миграция (JSON → 2-й процесс → Quarantine `2*T`):**
   * *Given* Process A и Process B на разных localhost-портах; на Process A в Game G летит корабль (`ActiveActionDescriptor` + `CommandInjectableCommand`) и стоят команды в очереди.
   * *When* Process A отсоединяет G, ставит `GameProxy`, отправляет `POST /migrate/import` с `GameSnapshot` v2 на Process B → при `200 OK` переключает роутер, привязывает `AsyncChannelCommandReceiver` (`POST /migrate/forward`) и запускает `"Game.Transfer.Quarantine"` (`2*T`).
   * *Then* корабль продолжает движение на Process B с сохранённым `next_entity_id`, `current_step` и `TransactionalGameItem`, а in-flight команда пересылается через `GameProxy`; при `409 VersionMismatch` или сетевой ошибке на `import` Process A возвращает G локально (`Reattach`) без потери команд.
3. **Полный `ServerSnapshot` (`shutdown & restore`):**
   * *Given* сервер с 2 потоками и 3 активными играми (движущийся корабль, отложенные команды, `TransactionalGameItem`).
   * *When* выполняется `CaptureServerSnapshotCommand(path)`, сервер останавливается, затем новый экземпляр выполняет `RestoreServerFromSnapshotCommand(path)`.
   * *Then* восстанавливаются 2 потока, `game_to_thread_map`, координаты, `ActiveActionDescriptor`, очереди команд и `TransactionalGameItem`s; партии продолжаются с того же шага.
4. **Откат 2PC с `Remove`, спавном и очередью (`Phantom Entity Prevention`):**
   * *Given* восстановленная после миграции игра с кораблём (`Ammo = 1`, `Shield = "on"`).
   * *When* внутри `InTransactionCommand` выполняются: `ship["Ammo"] = 0`, `ship.Remove("Shield")`, `repo.Set(entityId, entity)`, `receiver.Receive(moveCmd)`, после чего следующая команда бросает исключение.
   * *Then* после `Rollback()` `ship["Ammo"] == 1`, `ship["Shield"] == "on"`, `entityId` отсутствует в репозитории, а `moveCmd` никогда не попадает в очередь `Game`.

---

## 2. Архитектура, контракты и альтернативы

### 2.1. Неблокирующий `ITryCommandReceiver`, `CancellationToken` `HardStop` и Deadlock-Free `GameProxy` (`D-014`)

#### 2.1.1. Контракты `ServerThread`
* **Новый интерфейс `ITryCommandReceiver : ICommandReceiver`** (`SpaceBattle.Lib/Server/ITryCommandReceiver.cs`):
  * `bool TryReceive(ICommand command);` — неблокирующая вставка (`_inbox.TryAdd(command)`). Межпоточные пересылки и `GameProxy` используют только `TryReceive`.
* **Расширение `ServerThread : ITryCommandReceiver`:**
  * Реализует `TryReceive(ICommand command)` (`ArgumentNullException.ThrowIfNull(command); return _inbox.TryAdd(command);`).
  * `RegisterIoCDependencyServerThread` принимает опциональный `int boundedCapacity` (`<= 0` → `ArgumentOutOfRangeException`).
* **`CancellationToken`-основанный `HardStop`, `Quiesce` и счётчик `ActiveProxies` в `ServerThreadState`:**
  * Хранит `CancellationTokenSource _hardStopCts`, `bool _softStopRequested`, `bool _quiesceRequested`, `int _activeProxies`.
  * `HardStopToken`, `ActiveProxies` (`Volatile.Read`), `RequestHardStop()` (`_hardStopCts.Cancel()` — неблокирующий, работает при полном inbox), `RequestSoftStop()`, `RequestQuiesce()`, `RegisterActiveProxy()` / `UnregisterActiveProxy()` (`Interlocked`).
  * `CanContinue` = `!_hardStopCts.IsCancellationRequested && !_quiesceRequested && !(_softStopRequested && _inbox.Count == 0 && !_scheduler.HasReady && ActiveProxies == 0)`.
  * **`ServerThreadLoopCommand`:** вызывает `_inbox.Take(_state.HardStopToken)` при `!_scheduler.HasReady`; `OperationCanceledException` чисто завершает цикл (`break`).
  * **`GameQuantumState` / `GameQuantumCommand`:** `bool IsDetached { get; }`, `void Detach()`, `void Reattach()`. В `GameQuantumCommand.Execute()` ранний `if (_state.IsDetached) return;`, в `finally` `if (!_state.IsIdle && !_state.IsDetached) _scheduler.MakeReady(this);`.
  * **Регистрации в L2A `serverScope`:** `"Server.Thread.Stop.Hard"` (напрямую `HardStopServerThreadCommand`, без `_inbox.Add`), `"Server.Thread.TryEnqueue"` (override на `ITryCommandReceiver.TryReceive`), `"Server.Thread.Quiesce"`.

#### 2.1.2. Простой Deadlock-Free `GameProxy : ICommand, ICommandReceiver` (`ПЗ_6 50:00–64:20`)
`GameProxy` реализует `ICommand, ICommandReceiver` (100% LSP с `Game`), владеет очередью `ConcurrentQueue<ICommand>` и планируется тем же `RoundRobinGameScheduler` через `GameQuantumCommand` + `SchedulingCommandReceiver` (0 накладных расходов для немигрирующих игр).

* **Протокол одного кванта `GameProxy.Execute()` (строго на Thread A):**
  1. Если `_target == null` (игра ещё отправляется на 2-й процесс): вызывает `_quantumState.MarkIdle()` и выходит (команды безопасно буферизуются в `_queue`).
  2. Цикл `while (_quantumState.CanContinue())`:
     * Если `!_queue.TryPeek(out var head)`:
       * `_quantumState.MarkIdle()`.
       * Если `_quarantineElapsed && !_retired`: ставит `_retired = true`, вызывает `_onRetired()` (удаляет себя из `IGameReceiverRegistry` через `RemoveIfSame`, вызывает `_threadState.UnregisterActiveProxy()`).
       * Выход из кванта.
     * Внутри `try`: `var accepted = _target.TryReceive(head);`.
     * Если `TryReceive` бросил исключение: `_queue.TryDequeue(out _);` (извлечение poison-сообщения), маршрутизация в `Ioc.Resolve<ICommand>("Exception.Handle", head, exception).Execute()`, переход к следующей итерации.
     * Если `!accepted` (целевая очередь полна): **не извлекаем `head` из `_queue` и не ждём** — немедленный `return` из кванта. `RoundRobinGameScheduler` передаёт управление остальным играм на Thread A и вернётся к `GameProxy` на следующем кванте.
     * Если `accepted`: `_queue.TryDequeue(out _);`.
* **Замена `RedirectingGameCommandSending` (`ProxyGameCommandSending : IGameCommandSending`):**
  * Для внутрипроцессной миграции (Thread A → Thread B): `envelopeFactory` создаёт `DeliverMessageCommand(targetTable, description)`; `_target` = целевой `ServerThread` (`ITryCommandReceiver`).
  * Для межпроцессной миграции (Process A → Process B): `envelopeFactory` создаёт `ForwardRemoteMessageCommand(transport, targetUrl, description)`; `_target` = `AsyncChannelCommandReceiver : ITryCommandReceiver` (ограниченный `Channel<ICommand>`, `TryReceive` = lock-free `Writer.TryWrite`, фоновый ThreadPool-воркер — **ноль сетевого I/O на `ServerThread`**).

---

### 2.2. Stop-and-Serialize 2-процессная миграция (`ПЗ_3:409–736`)

#### 2.2.1. Карантин `"Game.Transfer.Quarantine"` (`2*T`)
```csharp
public interface IQuarantineDelay
{
    void Schedule(TimeSpan duration, Action onElapsed);
}
```
* Регистрация в **L2B `GameBaseScope_vN`**:
  * `"Game.Transfer.Quarantine.Duration"` → `TimeSpan` (по умолчанию `200 ms`, конфигурируется как `2 * T`).
  * `"Game.Transfer.Quarantine.Delay"` → `IQuarantineDelay` (`TimerQuarantineDelay` на одноразовом `System.Threading.Timer` в продакшне; `FakeQuarantineDelay` в unit-тестах).
  * `"Game.Transfer.Quarantine"` `(Action onElapsed)` или `(TimeSpan duration, Action onElapsed)` → `QuarantineCommand : ICommand`.
* Запускается после успешного переноса и переключения роутера; по истечении `2*T` вызывает `MarkQuarantineElapsed()` и будит `GameProxy` на Thread A для отставки (`ActiveProxies--`).

#### 2.2.2. Простой HTTP-транспорт (2 метода вместо 5)
```csharp
public interface IGameTransferTransport
{
    TransferResponse ImportGame(string targetBaseUrl, string gameId, string snapshotJson);
    TransferResponse ForwardMessage(string targetBaseUrl, CommandDescription description);
}
```
* **Plain-class DTO (0 `record`):** `TransferResponse` (`IsSuccess`, `StatusCode`, `Error`), `MigrateStartRequest` (`game_id`, `target_url`), `MigrateImportRequest` (`game_id`, `snapshot_json`).
* **3 прямых маршрута в `ServerBootstrap.cs`** (без `PreparedGamesRegistry`, `MigrationEndpointHandler`, `MigrationResponseBuilder`, `AdminEndpointRegistry`):

| Метод и маршрут | Тело запроса | Коды ответа и поведение |
| :--- | :--- | :--- |
| `POST /migrate/start` | `MigrateStartRequest` | Запускает `"Game.Transfer.Remote"` на Process A: отсоединяет игру → ставит `GameProxy` → `ImportGame` на `target_url` → при `200 OK` переключает роутер, привязывает `AsyncChannelCommandReceiver`, запускает `"Game.Transfer.Quarantine"`; при ошибке — откатывает игру локально (`Reattach`). `200 OK` / `404` / `409` / `502`. |
| `POST /migrate/import` | `MigrateImportRequest` | Десериализует `GameSnapshot` v2 (`400` при битом JSON/неверной `schema_version`), валидирует `rules_config` через `"Game.Rules.Parse"` и при заданном серверном allowlist поддерживаемых версий сверяет `rules_version` (`409` при несовпадении, поддерживая мульти-правила из Spec `02`, `D-020`), восстанавливает игру через `"Game.Restore"` на целевой `ServerThread`, регистрирует в `IGameRouter`. `200 OK`. |
| `POST /migrate/forward` | `CommandDescription` | Маршрутизирует пересланную команду через `IGameRouter.Route(description)`: `Accepted` → `200 OK`, `GameNotFound` → `404`, `QueueFull` → `503`. |

* **Шов для 100% покрытия `HttpGameTransferTransport`:** `TransferTransportFactory.Create(HttpMessageHandler handler, TimeSpan timeout)` + `TransferHttpRequestBuilder` (все `new HttpRequestMessage`/`StringContent`/`TransferResponse` внутри `*Builder`, **0 `new`**, **0 `switch`** в `HttpGameTransferTransport.cs`).

#### 2.2.3. JSON-схема `GameSnapshot` v2 и единый `IActiveActionTracking`
```json
{
  "schema_version": 2,
  "game_id": "game-42",
  "rules_version": "1.0.0",
  "rules": "standard",
  "rules_config": "{\"version\":\"1.0.0\",\"entities\":[],\"actions\":{}}",
  "current_step": 12,
  "next_entity_id": 105,
  "command_limit": 10,
  "players": ["player-1", "player-2"],
  "is_game_over": false,
  "winner_player_id": null,
  "items": {
    "1": {
      "Position": { "type": "SpaceBattle.Lib.Vector", "value": [12, 5] },
      "Angle": { "type": "SpaceBattle.Lib.Angle", "value": [2, 8] },
      "Fuel": { "type": "System.Int32", "value": 40 }
    }
  },
  "active_actions": [
    {
      "bridge_id": "b-01",
      "game_item_id": 1,
      "action_type": "Macro.Move",
      "property_key": "MoveCommand",
      "args": {}
    }
  ],
  "commands": [
    {
      "id": "c-99",
      "game_id": "game-42",
      "player_id": "player-2",
      "type": "Commands.Rotate",
      "game_item_id": 1,
      "step": 12
    }
  ]
}
```
```csharp
public interface IActiveActionTracking
{
    void Register(ActiveActionDescriptor descriptor, ICommandInjecting injectable);
    bool Unregister(string bridgeId);
    IReadOnlyList<ICommandInjecting> Drain(int gameItemId);
    IReadOnlyList<ICommandInjecting> Drain(int gameItemId, string propertyKey);
    IReadOnlyList<ICommandInjecting> DrainAll();
    IReadOnlyList<ActiveActionDescriptor> Active { get; }
}
```
* **Обязательные инварианты `GameSnapshotBuilder` и `Game.Restore` (`D-020`):**
  1. Запрет снимка внутри открытой транзакции (`if (game.Transactions.IsActive) throw new InvalidOperationException(...)`).
  2. **Резолв `"Snapshot.Codecs"` строго из L2B/L4 (никогда из L2A `serverScope`, `D-020`):** т.к. `L2A serverScope` и `L2B GameBaseScope_vN` — независимые дети `RootScope`, `Game.Create.Transferable` и `Game.Restore` обязаны резолвить `"Snapshot.Codecs"` **внутри `InSeparateScopeCommand(gameScope, ...)`**, а `CaptureServerSnapshotCommand` / `TransferGameCommand` / `MigrateGameToRemoteCommand` — внутри `holder.Current` (`GameBaseScope_vN`) или `game.Scope` (обеспечивает видимость кодеков плагинов после hot-reload). Пропуск несериализуемых значений (`ICommand`, `ICommandInjecting`) в `SnapshotValueCodecs.TryWrite`.
  3. **Реконструкция `GameScope` (`INV-7`, `depth = 4`):** `Game.Create.Transferable` и `Game.Restore` всегда создают `GameScope` с явным родителем `Ioc.Resolve<object>("Server.Scope.Proxy")` (без 0-аргументного `IoC.Scope.Create`), регистрируют игровые состояния (`RegisterIoCDependencyTransactions`, `RegisterIoCDependencyGameStep`, `RegisterIoCDependencyGameState`), затем при непустом `rules_config` вызывают `"Game.Rules.Parse"` → `"Game.Rules.Apply"`, иначе `"Game.Rules." + rules`.
  4. Обёртка объектов через `"Game.Item.Create"` (`TransactionalGameItem`), синхронизация `NextEntityId = Math.Max(snapshot.NextEntityId, maxItemId + 1)` (чтение `IEntityIdGenerating.PeekNextId()` при наличии), сохранение и восстановление `Players`, `IsGameOver` и `WinnerPlayerId` в `MatchState` (если `"Game.Match.State"` зарегистрирован, без потери уже выбывших игроков). После удаления отсутствующих в `snapshot.Items` сущностей и записи восстановленных объектов `Game.Restore` вызывает `"Game.State.Reindex"` (который очищает `SectorGrid` и переиндексирует строго живые сущности снапшота — 0 призраков).
  5. Восстановление `active_actions`: для каждого `ActiveActionDescriptor` в `snapshot.ActiveActions` создаётся `ICommandInjecting` (`"Commands.CommandInjectable"`), внутрь инжектируется повторяемый тиковый делегат, который при наличии объекта `descriptor.GameItemId` в репозитории декодирует `descriptor.Args` (если непусты — 2-аргументный `Ioc.Resolve<ICommand>(descriptor.ActionType, item, decodedArgs)`, иначе 1-аргументный `Ioc.Resolve<ICommand>(descriptor.ActionType, item)`), при исключении снимает `descriptor.BridgeId` из `ActiveActionRegistry` перед пробросом, а при успехе ставит себя повторно в `"Game.Command.Receiver"`; регистрируется в `ActiveActionRegistry` (`"Game.Actions.Registry"`) и добавляется вместе с `snapshot.Commands` во внутреннюю очередь `Game` **до** однократной постановки игры на целевой `ServerThread` (1 слот inbox вместо $N$).
  6. `GameSnapshotSerializer` и `ServerSnapshotSerializer` используют `SnapshotSerializerOptionsBuilder.Default` (**0 `new JsonSerializerOptions`** в `*Serializer.cs`).

---

### 2.3. Полный JSON `ServerSnapshot` (`shutdown & restore`, `ПЗ_5 09:30–10:15`)
```json
{
  "schema_version": 1,
  "rules_version": "1.0.0",
  "thread_count": 2,
  "game_to_thread_map": { "game-1": 0, "game-2": 1 },
  "games": [
    { "thread_index": 0, "snapshot": { "...GameSnapshot v2..." } },
    { "thread_index": 1, "snapshot": { "...GameSnapshot v2..." } }
  ]
}
```
* `ISnapshotFileStore` (`WriteAtomic(filePath, content)`, `ReadAll(filePath)`) — реализация `AtomicSnapshotFileStore` (`.tmp` + `File.Move(..., overwrite: true)`).
* **`CaptureServerSnapshotCommand`:** включает флаг паузы приёма `"Server.Ingress.Paused"` (`true`) → ждёт `All(t => t.State.ActiveProxies == 0)` (при таймауте снимает паузу и бросает `TimeoutException`) → отправляет `QuiesceServerThreadCommand` + `Join` всем потокам → `ServerSnapshotBuilder.Build` → `ServerSnapshotSerializer.Serialize` → `ISnapshotFileStore.WriteAtomic`.
* **`RestoreServerFromSnapshotCommand`:** читает и валидирует `ServerSnapshot` (`schema_version == 1`, валидация каждого `GameSnapshot.RulesConfig` и опционального allowlist `RulesVersion`, поддерживая разные `game-rules.json` на одном сервере, `D-020`) → создаёт `ThreadCount` потоков → восстанавливает игры через `"Game.Restore"` и заполняет `IGameRouter` → стартует потоки → снимает паузу `"Server.Ingress.Paused"` (`false`).
* Подключается к `POST /admin/shutdown?snapshotPath=...` из Spec `03` через переопределение `"Server.Admin.Shutdown"`.

---

### 2.4. Полные in-memory 2PC-транзакции игры (`D-015`, `ПЗ_4:469–678`)
```csharp
public interface ITransactionParticipant
{
    void Begin();
    void Commit();
    void Rollback();
}
```
1. **`GameTransactions`:** `GameItemKeyComparer` (`ReferenceEquals` + `StringComparer.Ordinal` из `GameTransactionsKeyFactory`), маркер `DeletedSentinel = new object()`, `RegisterParticipant`, `TryRemove`, обновлённый `TryRead(..., out bool isDeleted)`, координация `Begin`/`Commit`/`Rollback` (порядок внешнего `Commit`: свойства → `Repository` → `Outbox`).
2. **`TransactionalGameItem`:** zero-allocation `TryGetValue`/`ContainsKey` (без `View()`), поддержка tombstones в `this[key]`, `Remove(string)`, `Remove(KeyValuePair)`, `Clear()`, `View()` (удалён `NotSupportedException`).
3. **`TransactionalGameObjectRepository`:** декоратор `EnumerableGameObjectRepository` со стеком фреймов и `null`-tombstone для `Remove`.
4. **`TransactionalCommandReceiver`:** staged outbox под ключом `"Game.Command.Receiver"` — буферизует `Receive(cmd)` пока активна транзакция, сбрасывает в `_inner` на внешнем `Commit`, отбрасывает на `Rollback`.
5. **Единственная регистрация (`scopes.dll` Trap #1):** `RegisterIoCDependencyTransactions` регистрирует `"Game.Transactions"`, `"Game.Item.Transactional"`, `"Game.Item.Create"`, `"Commands.InTransaction"`, `"Game.Repository.ReadOnly"` / `".Writable"` и `"Game.Command.Receiver"` в транзакционном `GameScope`.

---

### 2.5. Сводная матрица `IoC`-ключей Spec `04` (`INV-1`, `INV-7`)

| `IoC`-ключ | Уровень скоупа | Аргументы | Возврат | Назначение |
| :--- | :---: | :--- | :--- | :--- |
| `"Server.Thread.Create"` | L2A (`serverScope`) | `()` или `(int boundedCapacity)` | `ServerThread` | Создаёт поток с `ITryCommandReceiver` и `HardStopToken` |
| `"Server.Thread.TryEnqueue"` | L2A (`serverScope`) | `(ServerThread thread)` | `Func<ICommand, bool>` | Override Spec `03` на `ITryCommandReceiver.TryReceive` |
| `"Server.Thread.Stop.Hard"` | L2A (`serverScope`) | `(ServerThread thread)` | `ICommand` | Прямой `HardStopServerThreadCommand` (без `_inbox.Add`) |
| `"Server.Thread.Stop.Soft"` | L2A (`serverScope`) | `(ServerThread thread)` | `ICommand` | `SoftStop`, ждущий `ActiveProxies == 0` |
| `"Server.Thread.Quiesce"` | L2A (`serverScope`) | `(ServerThread thread)` | `ICommand` | Остановка потока после текущего кванта без сброса очередей игр |
| `"Game.Transfer.Transport"` | L2A (`serverScope`) | `()` | `IGameTransferTransport` | Простой HTTP-транспорт (`ImportGame`, `ForwardMessage`) |
| `"Game.Transfer"` | L2A (`serverScope`) | `(TransferableGame, ServerThread)` | `ICommand` | Внутрипроцессная миграция через `GameProxy` |
| `"Game.Transfer.Remote"` | L2A (`serverScope`) | `(string gameId, string targetBaseUrl)` | `ICommand` | 2-процессная миграция (`ImportGame` → `GameProxy` + `Quarantine`) |
| `"Game.Create.Transferable"` | L2A (`serverScope`) | `(ServerThread, int limit, string rules[, string gameId, string rulesConfig])` | `TransferableGame` | Создаёт игру в `GameScope` (`parent = ProxyScope`, `depth = 4`) |
| `"Game.Restore"` | L2A (`serverScope`) | `(string json, ServerThread)` | `TransferableGame` | Восстанавливает `GameSnapshot` v2 (`Game.Rules.Parse` → `Apply`, `Game.Item.Create`) |
| `"Server.Snapshot.Store"` | L2A (`serverScope`) | `()` | `ISnapshotFileStore` | Атомарная запись/чтение `ServerSnapshot` JSON |
| `"Server.Snapshot.Capture"` | L2A (`serverScope`) | `(string filePath)` | `ICommand` | Quiesce-барьер + сохранение `ServerSnapshot` |
| `"Server.Snapshot.Restore"` | L2A (`serverScope`) | `(string filePath)` | `ICommand` | Восстановление потоков, игр и `IGameRouter` из `ServerSnapshot` |
| `"Snapshot.Codecs"` | L2B (`GameBaseScope_vN`) | `()` | `SnapshotValueCodecs` | Канонические кодеки `Vector`, `Angle`, `Int32`, `String` |
| `"Game.Transfer.Quarantine.Duration"` | L2B (`GameBaseScope_vN`) | `()` | `TimeSpan` | Окно карантина `2*T` |
| `"Game.Transfer.Quarantine.Delay"` | L2B (`GameBaseScope_vN`) | `()` | `IQuarantineDelay` | Неблокирующий планировщик задержки (шов для тестов) |
| `"Game.Transfer.Quarantine"` | L2B (`GameBaseScope_vN`) | `(Action onElapsed)` или `(TimeSpan, Action)` | `ICommand` | Команда ожидания карантина `2*T` |
| `"Game.Item.Create"` | L2B / L4 | `()` или `(IDictionary<string, object>)` | `IDictionary<string, object>` | Фабрика объектов (`TransactionalGameItem` в транзакционной игре) |
| `"Game.Actions.Registry"` | L4 (`GameScope`) | `()` | `IActiveActionTracking` | Единственный реестр активных `BridgeCommand` / `Repeater` |
| `"Game.Transactions"` | L4 (`GameScope`) | `()` | `GameTransactions` | Координатор 2PC (свойства + `Repository` + `Outbox`) |
| `"Game.Item.Transactional"` | L4 (`GameScope`) | `(IDictionary<string, object>)` | `TransactionalGameItem` | Транзакционная обёртка с tombstones и zero-alloc `TryGetValue` |
| `"Game.Repository.ReadOnly"` / `".Writable"` | L4 (`GameScope`) | `()` | `TransactionalGameObjectRepository` | Единственная регистрация в транзакционном `GameScope` |
| `"Game.Command.Receiver"` | L4 (`GameScope`) | `()` | `TransactionalCommandReceiver` | Транзакционный staged outbox |
| `"Commands.InTransaction"` | L4 (`GameScope`) | `(ICommand)` | `InTransactionCommand` | Атомарное выполнение команды с `Begin`/`Commit`/`Rollback` |

---

## Detailed Implementation Plan

### 1. Список файлов (`Modify` / `Create` / `Delete`)

#### 1.1. `SpaceBattle/SpaceBattle.Lib/Server/`
| Действие | Файл | Контракт / Изменения |
| :-: | :--- | :--- |
| `Create` | `Server/ITryCommandReceiver.cs` | `public interface ITryCommandReceiver : ICommandReceiver { bool TryReceive(ICommand command); }` |
| `Modify` | `Server/ServerThread.cs` | Реализует `ITryCommandReceiver` (`_inbox.TryAdd`). |
| `Modify` | `Server/ServerThreadState.cs` | `CancellationTokenSource _hardStopCts` (`HardStopToken`), `_quiesceRequested`, `_activeProxies` (`RegisterActiveProxy`/`UnregisterActiveProxy`), `CanContinue`. |
| `Modify` | `Server/ServerThreadLoopCommand.cs` | `_inbox.Take(_state.HardStopToken)` с чистым выходом при `OperationCanceledException`. |
| `Modify` | `Server/GameQuantumState.cs` | `bool IsDetached { get; }`, `void Detach()`, `void Reattach()`. |
| `Modify` | `Server/GameQuantumCommand.cs` | Ранний `if (_state.IsDetached) return;` и проверка `!_state.IsDetached` в `finally`. |
| `Create` | `Server/QuiesceServerThreadCommand.cs` | `ICommand`: вызывает `_state.RequestQuiesce()`. |
| `Modify` | `Server/RegisterIoCDependencyServerThread.cs` | Опциональный `boundedCapacity`, `"Server.Thread.TryEnqueue"`, прямой `"Server.Thread.Stop.Hard"`, `"Server.Thread.Quiesce"`. |

#### 1.2. `SpaceBattle/SpaceBattle.Lib/Transfer/`
| Действие | Файл | Контракт / Изменения |
| :-: | :--- | :--- |
| `Create` | `Transfer/IQuarantineDelay.cs` | `void Schedule(TimeSpan duration, Action onElapsed);` |
| `Create` | `Transfer/QuarantineCommand.cs` | `ICommand`: вызывает `_delay.Schedule(_duration, _onElapsed)`. |
| `Create` | `Transfer/QuarantineDelayFactory.cs` | *(Infra `Factory`)* Создаёт `TimerQuarantineDelay : IQuarantineDelay` (одноразовый `System.Threading.Timer`). |
| `Create` | `Transfer/GameProxy.cs` | `ICommand, ICommandReceiver`: `ConcurrentQueue<ICommand>`, `AttachTarget`, `MarkQuarantineElapsed`, квантованный `TryPeek -> TryReceive -> TryDequeue` (при `false` — выход из кванта без блокировки), отставка после карантина. |
| `Create` | `Transfer/ProxyGameCommandSending.cs` | `IGameCommandSending`: пересылает `CommandDescription` в `SchedulingCommandReceiver(gameProxy)`. |
| `Create` | `Transfer/GameProxyFactory.cs` | *(Infra `Factory`)* Собирает `GameProxy`, `ProxyGameCommandSending`, `GameQuantumState`, `GameQuantumCommand`, регистрирует `ActiveProxies`. |
| `Create` | `Transfer/ForwardRemoteMessageCommand.cs` | `ICommand`: исполняется фоновым воркером `AsyncChannelCommandReceiver`, вызывает `transport.ForwardMessage(...)`, при ошибке/отказе маршрутизирует в `"Exception.Handle"`. |
| `Create` | `Transfer/AsyncChannelCommandReceiverFactory.cs` | *(Infra `Factory`)* Создаёт `AsyncChannelCommandReceiver : ITryCommandReceiver, IAsyncDisposable` над `Channel<ICommand>`. |
| `Create` | `Transfer/ActiveActionDescriptor.cs` | Plain `class` DTO (`BridgeId`, `GameItemId`, `ActionType`, `PropertyKey`, `Args`). |
| `Create` | `Transfer/IActiveActionTracking.cs` | Контракт `"Game.Actions.Registry"` (`Register`, `Unregister`, `Drain(int)`, `DrainAll()`, `Active`). |
| `Create` | `Transfer/ActiveActionRegistry.cs` | Единственная реализация `IActiveActionTracking` (используется в Spec `04` и Spec `05`). |
| `Create` | `Transfer/SnapshotSerializerOptionsBuilder.cs` | *(Infra `Builder`)* Переиспользуемый `JsonSerializerOptions` (`Default`) для сериализаторов. |
| `Modify` | `Transfer/GameSnapshot.cs` | Схема v2 (plain `class`): `SchemaVersion = 2`, `GameId`, `RulesVersion`, `Rules`, `RulesConfig`, `CurrentStep`, `NextEntityId`, `CommandLimit`, `Players`, `IsGameOver`, `WinnerPlayerId`, `Items`, `ActiveActions`, `Commands`. |
| `Modify` | `Transfer/GameSnapshotBuilder.cs` | Проверка `!game.Transactions.IsActive`, чтение `IEntityIdGenerating.PeekNextId()` и `MatchState` (`"Game.Match.State"`) при наличии, сборка полей v2. |
| `Modify` | `Transfer/GameSnapshotSerializer.cs` | Использует `SnapshotSerializerOptionsBuilder.Default` (0 `new JsonSerializerOptions`), валидация полей v2. |
| `Modify` | `Transfer/TransferableGame.cs` | `GameId`, `RulesVersion`, `RulesConfig`, `CurrentStep`, `NextEntityId`, `ActiveActions`, `Transactions`, `QuantumState`, `Detach()`, `Reattach()`. |
| `Modify` | `Transfer/TransferGameCommand.cs` | Без рекурсии и блокирующего `_inbox.Add`: отсоединяет `Game`, ставит `GameProxy`, строит `GameSnapshot` v2, восстанавливает на целевом потоке через `TryReceive`, переключает роутер, запускает `"Game.Transfer.Quarantine"`. |
| `Create` | `Transfer/IGameTransferTransport.cs` | `ImportGame(targetBaseUrl, gameId, snapshotJson)`, `ForwardMessage(targetBaseUrl, description)`. |
| `Create` | `Transfer/TransferDtos.cs` | Plain `class` DTO: `TransferResponse`, `MigrateStartRequest`, `MigrateImportRequest`. |
| `Create` | `Transfer/TransferHttpRequestBuilder.cs` | *(Infra `Builder`)* Строит `HttpRequestMessage`, `StringContent`, `TransferResponse` (0 `switch`). |
| `Create` | `Transfer/HttpGameTransferTransport.cs` | Реализация `IGameTransferTransport` поверх `HttpClient` и `TransferHttpRequestBuilder` (0 `new`, 0 `switch`). |
| `Create` | `Transfer/TransferTransportFactory.cs` | *(Infra `Factory`)* Создаёт `HttpGameTransferTransport` с инжектируемым `HttpMessageHandler`. |
| `Create` | `Transfer/MigrateGameToRemoteCommand.cs` | `ICommand`: отсоединяет игру → ставит `GameProxy` → `transport.ImportGame(...)` → при успехе привязывает `AsyncChannelCommandReceiver`, переключает роутер, запускает карантин `2*T`; при ошибке откатывает локально (`Reattach`). |
| `Create` | `Transfer/ISnapshotFileStore.cs` | `WriteAtomic(filePath, content)`, `ReadAll(filePath)`. |
| `Create` | `Transfer/SnapshotFileStoreFactory.cs` | *(Infra `Factory`)* `AtomicSnapshotFileStore` (`.tmp` + `File.Move(..., overwrite: true)`). |
| `Create` | `Transfer/ServerSnapshot.cs` | Plain `class` DTO: `ServerSnapshot`, `ServerGameSnapshotEntry`. |
| `Create` | `Transfer/ServerSnapshotBuilder.cs` | *(Infra `Builder`)* Собирает `ServerSnapshot` из quiesced потоков и игр. |
| `Create` | `Transfer/ServerSnapshotSerializer.cs` | Валидация `ServerSnapshot` (0 `new JsonSerializerOptions`). |
| `Create` | `Transfer/CaptureServerSnapshotCommand.cs` | `ICommand`: пауза `"Server.Ingress.Paused"` → ожидание `ActiveProxies == 0` → `Quiesce` + `Join` → запись JSON. |
| `Create` | `Transfer/RestoreServerFromSnapshotCommand.cs` | `ICommand`: чтение `ServerSnapshot` → сверка `RulesVersion` → создание потоков, `"Game.Restore"`, `IGameRouter` → старт потоков → снятие паузы. |
| `Modify` | `Transfer/RegisterIoCDependencyTransferableGame.cs` | Регистрация ключей L2A (`serverScope`) и L2B (`GameBaseScope_vN`); явный `ProxyScope` (`depth = 4`), `"Game.Rules.Parse"` → `"Game.Rules.Apply"`, `"Game.Item.Create"`, восстановление `ActiveActionDescriptor`. |
| `Delete` | `Transfer/RedirectingGameCommandSending.cs` | Поглощён `ProxyGameCommandSending` + `GameProxy` (`INV-8`, 0 dead code). |

#### 1.3. `SpaceBattle/SpaceBattle.Lib/Transactions/`
| Действие | Файл | Контракт / Изменения |
| :-: | :--- | :--- |
| `Create` | `Transactions/ITransactionParticipant.cs` | `Begin()`, `Commit()`, `Rollback()`. |
| `Create` | `Transactions/GameTransactionsKeyFactory.cs` | *(Infra `Factory`)* `ReferenceEquals` + `StringComparer.Ordinal` компаратор для `(IDictionary<string, object>, string)`. |
| `Modify` | `Transactions/GameTransactions.cs` | `DeletedSentinel`, `RegisterParticipant`, `TryRemove`, `TryRead(..., out bool isDeleted)`, координация `Begin`/`Commit`/`Rollback`. |
| `Modify` | `Transactions/TransactionalGameItem.cs` | Zero-alloc `TryGetValue`/`ContainsKey`, tombstones в `this[key]`, `Remove`, `Clear`, `View`. |
| `Create` | `Transactions/TransactionalGameObjectRepository.cs` | Декоратор `EnumerableGameObjectRepository` + `ITransactionParticipant` со стеком фреймов и `null`-tombstone. |
| `Create` | `Transactions/TransactionalCommandReceiver.cs` | Staged outbox `ICommandReceiver, ITransactionParticipant` под ключом `"Game.Command.Receiver"`. |
| `Modify` | `Transactions/RegisterIoCDependencyTransactions.cs` | Единственная регистрация `Game.Transactions`, `Game.Item.*`, `Game.Repository.*`, `Game.Command.Receiver`, `Commands.InTransaction`. |

#### 1.4. `SpaceBattle/SpaceBattle.Server/`
| Действие | Файл | Контракт / Изменения |
| :-: | :--- | :--- |
| `Modify` | `SpaceBattle.Server/ServerBootstrap.cs` | Добавляет 3 прямых маршрута миграции (`POST /migrate/start`, `POST /migrate/import`, `POST /migrate/forward`) и подключает `CaptureServerSnapshotCommand` в `POST /admin/shutdown?snapshotPath=...` (0 `switch`, 0 `IoC.Scope.Current` после `await`). |

### 2. План xUnit-тестов для 100.00% Line & Branch Coverage (`INV-8`)

| Файл теста | Обязательные кейсы (100% Line & Branch) |
| :--- | :--- |
| `ServerTests/ServerThreadTests.cs` *(Modify)* | `Server.Thread.Create` (0 и 1 арг, `<= 0` → `ArgumentOutOfRangeException`); `TryReceive` (`null`, `true`, `false` на `boundedCapacity = 1`); `HardStop` на пустом и на полном inbox; `SoftStop` ждёт `ActiveProxies == 0`; `QuiesceServerThreadCommand` оставляет очереди игр целыми; `GameQuantumState.Detach`/`Reattach`. |
| `TransferTests/GameProxyTests.cs` *(Create)* | Взаимная миграция `Game 1: A → B` и `Game 2: B → A` на `boundedCapacity = 1` + здоровая `Game 3` (0 deadlock); fake `ITryCommandReceiver` (`false`, `true`) с сохранением FIFO и `commandLimit`; исключение в `_target.TryReceive` → извлечение poison-команды + `"Exception.Handle"`; буферизация при `_target == null`; отставка после `MarkQuarantineElapsed()`; `"Game.Transfer.Quarantine"` (`FakeQuarantineDelay` и 1 ms smoke `TimerQuarantineDelay`). |
| `TransferTests/GameSnapshotV2Tests.cs` *(Create)* | Round-trip `GameSnapshot` v2 (`Vector`, `Angle`, `Int32`, `String`, `rules_config`, `players`, `is_game_over`, `winner_player_id`, `active_actions`, `commands`); `ActiveActionRegistry` (`Register`, `Unregister`, `Drain`, `DrainAll`, `Active`); синхронизация `next_entity_id`; сохранение выбывших игроков в `MatchState`; пересоздание повторяемых `ActiveActionDescriptor` (`Bridge`) при `Game.Restore`; запрет снимка при `Transactions.IsActive == true`; граничные ошибки `GameSnapshotSerializer.Deserialize`; `Game.Create.Transferable` и `Game.Restore` (`depth = 4`, `Game.Rules.Parse` → `Apply`, `TransactionalGameItem`). |
| `TransferTests/HttpTransferAndTwoProcessTests.cs` *(Create)* | Unit-тесты `HttpGameTransferTransport` через `FakeHttpMessageHandler` (`ImportGame`, `ForwardMessage`: `200`, `400`, `404`, `409`, `500`, `503`, `HttpRequestException`, `TaskCanceledException`); `ForwardRemoteMessageCommand` (успех и ошибка в `"Exception.Handle"`); `AsyncChannelCommandReceiver` FIFO + backpressure; живая 2-процессная миграция (`Process A → Process B` на разных localhost-портах) с движущимся кораблём, in-flight командой в окне `2*T`, проверкой 2PC-отката на Process B, и откатом (`Reattach`) при `409 VersionMismatch`. |
| `TransferTests/ServerSnapshotTests.cs` *(Create)* | Round-trip `CaptureServerSnapshotCommand` → остановка → `RestoreServerFromSnapshotCommand` (2 потока, 3 игры, движущийся корабль, транзакции); таймаут ожидания `ActiveProxies` со снятием паузы ingress; несовпадение `rules_version`; ошибки `ServerSnapshotSerializer.Deserialize`; `AtomicSnapshotFileStore`. |
| `TransactionsTests/Full2PcTransactionsTests.cs` *(Create)* | Tombstones (`Remove`, `Remove(KVP)`, `Clear`) в одиночной и вложенной транзакции; zero-alloc `TryGetValue`/`ContainsKey` + `ReferenceEqualityComparer`; `TransactionalGameObjectRepository`; `TransactionalCommandReceiver` под `"Game.Command.Receiver"` (откат спавна + очереди); восстановление `TransactionalGameItem` в `Game.Restore`. |

### 3. Команды верификации
```bash
set -o pipefail
python3 .agents/hooks/ooand-lint-gate.py --check-all .
dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes
dotnet build SpaceBattle/SpaceBattle.sln --no-incremental
dotnet test SpaceBattle/SpaceBattle.sln --collect:"XPlat Code Coverage"
```
