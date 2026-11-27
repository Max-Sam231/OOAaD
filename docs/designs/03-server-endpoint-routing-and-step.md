# Spec 03: Тонкий хост `SpaceBattle.Server`, WebSocket-эндпоинт, $O(1)$ маршрутизация, валидация `step` и бенчмарки

> **Статус:** Zero-Decision дельта-спецификация (**Track B — Teammate 2**, параллельно с Spec `02` и `04`, 0 пересечений файлов)  
> **Связанные решения:** [`D-010`](../DECISIONS.md), [`D-013`](../DECISIONS.md), [`D-017`](../DECISIONS.md), [`D-018`](../DECISIONS.md), [`D-019`](../DECISIONS.md), [`D-020`](../DECISIONS.md), [`D-021`](../DECISIONS.md)  
> **Зависимости:** [`01-reconciliation-and-value-types.md`](01-reconciliation-and-value-types.md) (сведённый `origin/*` + `Angle`/`Vector`)  
> **Инварианты:** `INV-1`, `INV-2`, `INV-3`, `INV-5`, `INV-7`, `INV-8`, `INV-9`

---

## 1. Контекст, проблема, цели и границы

### Проблема
После сверки `origin/*` и общих Day-0 контрактов (Spec `01`: расширенный `CommandDescription`, `IGameRouter`, `IGameReceiverRegistry`) в репозитории есть многопоточный движок `ServerThread` + `RoundRobinGameScheduler`, `Game` (`InSeparateScopeCommand`) и сериализатор снапшотов (`SnapshotValueCodecs`), но **полностью отсутствуют** сетевой контур и точка входа сервера (`A6`, `A7`, `ПЗ_3:49-241`, `ПЗ_5 03:32-06:40`):
1. Нет исполняемого хоста `SpaceBattle.Server`, Composition Root и зарегистрированного обработчика `"Exception.Handle"` (`INV-7`).
2. Нет двунаправленного WebSocket-эндпоинта с push-рассылкой состояния после тика, событием `game_over`, паузой приёма на время снапшота и защитой от исчерпания сокетов (`TIME_WAIT`, `ПЗ_3:652-696`).
3. Нет реализации $O(1)$ маршрутизации `IGameRouter` (`gameId -> ServerThread`) и двухуровневой доставки (`эндпоинт → очередь потока → очередь игры или GameProxy`) без `is`/`as`/`switch` (`INV-2`) и без правок `Game.cs` (`INV-3`, `INV-7`).
4. Нет стратегии `"Game.Step.Validate"` для отброса устаревших (`step < currentStep`) и некорректных будущих команд без блокировки на молчащем игроке (`ПЗ_6 69:05`).
5. Нет локальных нагрузочных бенчмарков, подтверждающих пропускную способность десятки тысяч ($\ge 20\text{–}50\text{k}$) msg/s (`ПЗ_3:109-121`).

### Цели (Goals)
1. **Использование единого конверта `CommandDescription` и общих контрактов Spec `01`:** опираться на расширенный в Spec `01` `CommandDescription` (`GameId`, `PlayerId`, `Step`, nullable `Args?` в формате `SnapshotValueCodecs`), `IGameRouter` и `IGameReceiverRegistry` (0 пересечений файлов между Track B и Track C, 0 `IDictionary<string, object>` в `*Command.cs`).
2. **$O(1)$ роутинг и полиморфная диспетчеризация:** реализовать `GameRouter` (`ConcurrentDictionary<string, IGameEndpointRoute>`), `GameReceiverRegistry : IGameReceiverRegistry` и `DeliverMessageCommand` поверх единого существующего `-ing` интерфейса `IGameCommandSending` (0 `is`/`as`/`switch`, 0 правок `Game.cs` и `ServerThread.cs`), обеспечив прозрачную совместимость с `PendingCommandJournal` (Spec `04`).
3. **Интерпретация внутри `GameScope` и неблокирующий `step`:** реализовать `InterpretMessageCommand` (выполняется внутри `InSeparateScopeCommand`), неблокирующий `"Game.Step.Validate"` (отбрасывает `step < currentStep` и `step > currentStep + maxFutureDelta`) и хук авторизации `"Game.Message.Authorize"` → `"Commands.Authorize"` по полю `"OwnerId"`.
4. **Тонкий хост `SpaceBattle.Server` (100%/100% Coverlet & `ooand-lint-gate`):** expression-bodied `Program.Main`, единый `ServerBootstrap` со строгим разделением L2A `serverScope` (хост-сервисы) и L2B `GameBaseScope_vN` `baseInitializer` (все ключи, разрешаемые внутри `GameScope`, `depth == 4`), `WebSocketMessageLoop` над `abstract class WebSocket` (без чтения `IoC.Scope.Current` через `await`, с простым флагом паузы приёма `Func<bool> isIngressPaused`, буфером `maxMessageBytes + 1` и linked CTS для `DrainOutboundAsync`), неблокирующий outbound `Channel`, `EncodeGameOver` и прямые минимальные маршруты (`GET /health`, `GET /ws`, `POST /admin/shutdown`, `POST /admin/hot-reload`).
5. **Двойной бенчмарк (`ПЗ_3:49-121`):** in-process pipeline ($\ge 100\text{k}$ msg/s локально) и loopback WebSocket ($\ge 20\text{–}50\text{k}$ msg/s локально) с `TIME_WAIT`-безопасным `CloseAsync`.

### Границы (Non-Goals)
- Не модифицировать `CommandDescription.cs` (обновлён в Spec `01`), `Game.cs`, `ServerThread.cs`, `ServerThreadLoopCommand.cs`, `ServerThreadState.cs` и существующие доменные команды `Move`/`Rotate`/`Shoot`/`Macro`/`Authorize` (`INV-3`, гарантирует 0 merge-конфликтов с Track C — Spec `04`).
- Не реализовывать `GameProxy`, 2-процессную миграцию и `ServerSnapshot` (Spec `04`), парсер `game-rules.json` / `ProxyScope` hot-reload (Spec `02`), матрицу коллизий / E2E Proxy-агентов (Spec `05`). Спецификация `03` предоставляет им чистые `IoC`/HTTP-хуки.
- Не добавлять спекулятивные `StrictBarrierStepPolicy` / `LookaheadStepPolicy(K)` (явно отклонены в `D-013` Option C; остаются чистым `git A` расширением через `"Game.Step.Validate"`).
- Не добавлять внешние NuGet-пакеты (используется исключительно `Microsoft.NET.Sdk.Web` в `.NET 10`) и не использовать `record`/`record struct` в продакшн-коде (`D-017`).

### Ключевые сценарии (`Given → When → Then`)
1. **Штатная двухуровневая доставка:** `Given` игра `"g1"` зарегистрирована в `GameRouter` и `IGameReceiverRegistry` потока `T1` с объектом `id=1` `When` клиент присылает по `/ws?gameId=g1` валидный JSON `CommandDescription` (`step=0`) `Then` эндпоинт при активном приёме за $O(1)$ кладёт `DeliverMessageCommand` в inbox `T1` и немедленно освобождается, `T1` полиморфно вызывает `IGameCommandSending.Send` (фиксируя команду через `PendingCommandJournal`, когда он подключён) → `SchedulingCommandReceiver` активирует игру → `InterpretMessageCommand` в `GameScope` валидирует `step` и выполняет целевую команду.
2. **Полиморфизм `Game` vs `GameProxy` без `is`/`as`:** `Given` в `IGameReceiverRegistry["g1"]` привязан произвольный `IGameCommandSending` (локальная игра либо `GameProxy` из Spec `04`) `When` выполняется `DeliverMessageCommand` `Then` вызывается `target.Send(message)` без проверок типа и без правок `DeliverMessageCommand` или `Game.cs`.
3. **Устаревший и некорректный будущий `step`:** `Given` `GameStepState.CurrentStep == 5`, `MaxFutureDelta == 0` `When` приходят команды со `step = 4` и `step = 6` `Then` `"Game.Step.Validate"` возвращает `false`, команды бесшумно отбрасываются без исключения; команда со `step = 5` выполняется (даже если второй игрок молчит).
4. **Авторизация владельца:** `Given` сообщение с непустым `player_id = "p1"` и объект с `OwnerId = "p2"` `When` выполняется `InterpretMessageCommand` `Then` `"Game.Message.Authorize"` оборачивает команду в `"Commands.Authorize"`, выбрасывается `InvalidOperationException`, перехватывается `Game` и уходит в `"Exception.Handle"` (разрешённый из `GameBaseScope_vN`).
5. **Пауза приёма, Backpressure и отсутствие блокировки `ServerThread`:** `Given` включённый флаг паузы приёма (`isIngressPaused() == true`) либо переполненный inbox потока (`TryEnqueue == false`) `When` приходит WebSocket-фрейм `Then` без блокировки Kestrel клиенту в outbound `Channel` отправляется `{"kind":"error","code":"busy",...}`. При push состояния после тика (`state`) или завершения партии (`game_over`) `BroadcastGameStateCommand` вызывает `TryWrite` ($O(1)$, `DropOldest`) и никогда не ждёт сеть на `ServerThread`.
6. **Устойчивость протокола WebSocket и `TIME_WAIT`:** `Given` `FakeWebSocket` / реальный `ClientWebSocket` `When` поступают одиночный `Text`, фрагментированный `Text` (2 чанка), `Binary`, превышение `MaxWebSocketMessageBytes` (на одиночном и фрагментированном фреймах), битый JSON, `Close`, отмена или `WebSocketException` (как в receive, так и в `DrainOutboundAsync`) `Then` все ветки обрабатываются без необработанных исключений, клиент отписывается в `finally`, а `Close` отвечает `CloseOutputAsync` без утечки `TIME_WAIT`.
7. **Грациозный `/admin/shutdown` и `/admin/hot-reload`:** `Given` сервер запущен через `Program.Main` `When` приходит `POST /admin/hot-reload?pluginsDir=...` (выполняет `"Server.Admin.HotReload"` → `200`), а затем `POST /admin/shutdown` (или `POST /admin/shutdown?snapshotPath=...`) `Then` сервер вызывает `"Server.Admin.Shutdown"` с `snapshotPath`, возвращает `200`, останавливает рабочие потоки и завершает `Task` из `Program.Main`.

---

## 2. Архитектура, контракты и альтернативы

### 2.1. Единый конверт `CommandDescription` и JSON-схемы (`D-013`)

Spec `03` **переиспользует без модификации** расширенный в Spec `01` класс `SpaceBattle.Lib/Transfer/CommandDescription.cs` (обычный `class`, **не** `record`, 0 пересечений файлов между Track B и Track C):

| C# свойство | JSON ключ | Тип | Значение по умолчанию | Назначение |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `"id"` | `string` | `""` | Идентификатор команды (журнал / корреляция) |
| `GameId` | `"game_id"` | `string` | `""` | Ключ $O(1)$ маршрутизации `gameId -> route` |
| `PlayerId` | `"player_id"` | `string` | `""` | Субъект для `"Commands.Authorize"` (пусто = без проверки субъекта) |
| `Type` | `"type"` | `string` | `""` | `IoC`-ключ целевой команды (например, `"Macro.Move"`, `"Commands.Shoot"`) |
| `GameItemId` | `"game_item_id"` | `int` | `0` | Идентификатор объекта в `IReadOnlyGameObjectRepository` |
| `Step` | `"step"` | `long` | `0` | Номер шага (поколения), на котором агент принял решение |
| `Args` | `"args"` | `Dictionary<string, Dictionary<string, JsonElement>>?` | `null` *(`WhenWritingNull`)* | Опциональные типизированные аргументы в точном формате `SnapshotValueCodecs` (`{"type":"...","value":...}`) |

**Входящий WebSocket JSON (Client → Server):**
```json
{
  "id": "cmd-42",
  "game_id": "game-1",
  "player_id": "player-2",
  "type": "Macro.Move",
  "game_item_id": 2,
  "step": 5,
  "args": {
    "speed": { "type": "System.Int32", "value": 3 }
  }
}
```
Обязательные поля на границе WebSocket (`CommandMessageFactory.TryDecode`): валидный JSON-объект (не `"null"`), непустые `game_id` и `type`.

**Исходящий WebSocket JSON — push состояния после тика (`CommandMessageFactory.EncodeTickState`):**
```json
{
  "kind": "state",
  "game_id": "game-1",
  "step": 6,
  "status": "running",
  "winner": null,
  "objects": {
    "1": {
      "Position": { "type": "SpaceBattle.Lib.Vector", "value": [10, 5] },
      "Angle": { "type": "SpaceBattle.Lib.Angle", "value": [1, 8] }
    }
  }
}
```

**Исходящий WebSocket JSON — завершение партии (`CommandMessageFactory.EncodeGameOver`, потребляется Spec `05` через `"Server.Connections"`):**
```json
{
  "kind": "game_over",
  "game_id": "game-1",
  "step": 18,
  "status": "finished",
  "winner": "player-2",
  "alive_ships": {
    "player-1": 0,
    "player-2": 2
  }
}
```

**Исходящий WebSocket JSON — ошибка / backpressure (`CommandMessageFactory.EncodeError`):**
```json
{
  "kind": "error",
  "code": "busy",
  "game_id": "game-1",
  "message": "busy"
}
```
Коды ошибок (`code`): `"invalid_json"`, `"invalid_message"`, `"game_not_found"`, `"busy"`, `"unsupported_frame"`, `"message_too_large"`.

### 2.2. Интерфейсы и контракты компонентов (строго без `-able`, без `switch`, без `record`)

#### Общие контракты из Spec `01` (потребляются без дублирования, `design-3`)
- **`MessageRoutingStatus`** (`enum`: `Accepted`, `GameNotFound`, `QueueFull`), **`IGameEndpointRoute`** (`bool TryRoute(CommandDescription message)`), **`IGameRouter`** (`Route`, `Register`, `Unregister`, `TryGetRoute`) — определены в Spec `01`.
- **`IGameReceiverRegistry`** (`void Set(string gameId, IGameCommandSending target)`, `bool RemoveIfSame(string gameId, IGameCommandSending expected)`, `bool TryGet(string gameId, [MaybeNullWhen(false)] out IGameCommandSending target)`, `IReadOnlyCollection<string> GameIds`) — единая однопоточная таблица потока для Spec `03` и Spec `04`.

#### Контур маршрутизации и доставки (`SpaceBattle.Lib/Endpoint/`)
- **`GameRouter : IGameRouter`**: потокобезопасная $O(1)$ реализация на `ConcurrentDictionary<string, IGameEndpointRoute>`.
- **`GameReceiverRegistry : IGameReceiverRegistry`**: однопоточная реализация на обычном `Dictionary<string, IGameCommandSending>` (0 блокировок). `Set` перезаписывает привязку внутри потока (подмена `LocalEndpointGameSending` ↔ `GameProxy`), `RemoveIfSame` удаляет запись только при `ReferenceEquals(current, expected)`.
- **`ThreadGameEndpointRoute : IGameEndpointRoute`**:
  - Хранит `Func<ICommand, bool> _tryEnqueue` и `Func<CommandDescription, ICommand> _deliverFactory`.
  - `TryRoute(message)` проверяет `message != null` и вызывает `_tryEnqueue(_deliverFactory(message))`.
  - **Развязка с Track C (`ServerThread.cs`):** привязка `ServerThread` к `Func<ICommand, bool>` выполняется через `IoC`-ключ `"Server.Thread.TryEnqueue"` (в Spec `03` по умолчанию делегирует в `ICommandReceiver.Receive` + `true`; в Spec `04` переопределяется на `ITryCommandReceiver.TryReceive` без правки файлов Spec `03`).
- **`DeliverMessageCommand : ICommand`**:
  - Выполняется на целевом `ServerThread`.
  - Вызывает `_receiverTable.TryGet(_message.GameId, out var target)`.
  - При `true`: вызывает `target.Send(_message)` через **единый существующий `-ing` интерфейс `IGameCommandSending`** (`SpaceBattle.Lib/Transfer/IGameCommandSending.cs`) — **0 `is`/`as`/`switch` (`INV-2`)**, одинаково работает для `LocalEndpointGameSending` (`Game`) и `GameProxy` (Spec `04`).
  - При `false`: просто завершается (`return`, без лишних fallback-цепочек).
- **`LocalEndpointGameSending : IGameCommandSending`**:
  - Конструктор `(ICommandReceiver receiver, Func<CommandDescription, ICommand> commandFactory)` (0 `new`, 0 null-веток).
  - `Send(description)` вызывает `_receiver.Receive(_commandFactory(description))` (через `SchedulingCommandReceiver` кладёт команду в `Game._queue` и переводит игру в `Active` в `RoundRobinGameScheduler`, **0 правок `Game.cs`**).
  - **Шов сопряжения с `PendingCommandJournal` (Spec `04`):** в базовом режиме Spec `03` `"Endpoint.Target.Local.Create"` передаёт `desc => Ioc.Resolve<ICommand>("Commands.InterpretMessage", desc)`. Для переносимых игр (Spec `04`) передаётся фабрика с `PendingCommandJournal`, благодаря чему WebSocket-команды попадают в снапшот при миграции/`ServerSnapshot`.
- **`InterpretMessageCommand : ICommand`**:
  - Выполняется строго внутри `Game.Execute()` (внутри `InSeparateScopeCommand(gameScope, game)`, `INV-7`).
  - `Execute()` (`D-020`): (1) `if (!Ioc.Resolve<bool>("Game.Step.Validate", _description)) return;` (2) `Ioc.Resolve<ICommand>("Game.Message.Command", _description).Execute();` (3) `Ioc.Resolve<ICommand>("Game.Step.Advance").Execute();` (4) `Ioc.Resolve<ICommand>("Game.State.Broadcast", _description.GameId).Execute();`.
  - Не содержит ни одного `new`, ни одного `IDictionary<string, object>` (`INV-1`, `INV-2`, `ooand-lint-gate`).
- **`CommandArguments`** (`public sealed class`):
  - Типизированная иммутабельная обёртка над `IReadOnlyDictionary<string, object>`: `public T Get<T>(string key)`, `public bool Contains(string key)`, `public int Count { get; }`.
  - Устраняет упоминание `IDictionary<string, object>` в командах с дополнительными аргументами (`INV-2`).
- **`ICommandMessageDecoding`** (`-ing` интерфейс) и **`CommandMessageFactory : ICommandMessageDecoding`** *(infra `Factory`)*:
  - `bool TryDecode(ReadOnlySpan<byte> utf8Json, [MaybeNullWhen(false)] out CommandDescription message, out string errorCode);`
  - `ReadOnlyMemory<byte> EncodeError(string code, string gameId, string message);`
  - Статические фабричные методы:
    - `DecodeArguments(CommandDescription, SnapshotValueCodecs)` → `CommandArguments?` (возвращает `null` при `Args == null || Args.Count == 0` без аллокаций на горячем пути);
    - `EncodeTickState(string gameId, long step, string status, string? winner, IReadOnlyGameObjectRepository repository, IGameObjectIdSource idSource, SnapshotValueCodecs codecs)` → `ReadOnlyMemory<byte>`;
    - `EncodeGameOver(string gameId, long step, string winner, IReadOnlyDictionary<string, int> aliveShips)` → `ReadOnlyMemory<byte>` (формат `{"kind":"game_over","game_id":...,"step":...,"status":"finished","winner":...,"alive_ships":{...}}`).

#### Контур валидации шага и рассылки состояния (`SpaceBattle.Lib/Endpoint/`)
- **`GameStepState`** (живёт в `GameScope`):
  - `public long CurrentStep { get; private set; }` (старт `0`)
  - `public long MaxFutureDelta { get; }` (по умолчанию `0`, проверка `>= 0` в конструкторе)
  - `public void Advance()` (инкремент `CurrentStep`)
  - `public void Restore(long currentStep)` (установка при `Game.Restore` из Spec `04`, проверка `>= 0`)
- **`IStepValidating`** (`-ing` интерфейс) и **`NonBlockingStepValidating : IStepValidating`**:
  - `bool Validate(CommandDescription message)` — возвращает `message.Step >= _state.CurrentStep && message.Step <= _state.CurrentStep + _state.MaxFutureDelta`.
  - Не блокирует игру на молчащем игроке (100% совместимо с E2E `ПЗ_6 69:05`).
- **`IClientMessageSending`** (`-ing` интерфейс):
  - `bool TrySend(ReadOnlyMemory<byte> payload);`
  - `void Complete();`
- **`IGameConnectionRegistry`** и **`GameConnectionRegistry : IGameConnectionRegistry`**:
  - `void Subscribe(string gameId, string connectionId, IClientMessageSending sender);`
  - `void Unsubscribe(string gameId, string connectionId);` *(удаляет пустую внутреннюю коллекцию игры во избежание утечек)*
  - `void Publish(string gameId, ReadOnlyMemory<byte> payload);` *(неблокирующий вызов `TrySend` для каждого подписчика — $O(1)$ на клиента, 0 сетевого I/O на `ServerThread`)*
  - `int GetConnectionCount(string gameId);`
- **`BroadcastGameStateCommand : ICommand`**:
  - Выполняется в `GameScope` после исполнения команды (`D-020`); резолвит `"Server.Connections"` (из `GameBaseScope_vN`), делает fast-path `if (connections.GetConnectionCount(_gameId) == 0) return;` (0 аллокаций и 0 сериализации в headless-тестах и in-process бенчмарке!), иначе резолвит `"Game.Step.Current"`, `"Game.State.Serialize"` и публикует JSON в `IGameConnectionRegistry`.

#### Контур тонкого хоста (`SpaceBattle.Server/`)
- **`Program`**:
  - `public static Task Main(string[] args) => ServerBootstrap.RunAsync(args, CancellationToken.None);`
  - Ноль веток `async` state-machine, ноль `[ExcludeFromCodeCoverage]`, 100%/100% покрытие через `POST /admin/shutdown` (`D-017`).
- **`ServerOptions`** (`public sealed class`, не `record`): `WorkerThreadCount` (`2`), `OutboundChannelCapacity` (`64`), `MaxWebSocketMessageBytes` (`65536`), `GracefulStopTimeout` (`5s`).
- **`ChannelClientMessageSending : IClientMessageSending`**:
  - Тонкая обёртка над `Channel<ReadOnlyMemory<byte>>` (`SingleReader = true`, `SingleWriter = false`, `FullMode = BoundedChannelFullMode.DropOldest`), создаваемого напрямую в `ServerBootstrap` (в самом `ChannelClientMessageSending.cs` 0 вызовов `new`).
- **`WebSocketMessageLoop`**:
  - Принимает .NET `abstract class WebSocket` (шов для `FakeWebSocket : WebSocket`), `IGameRouter`, `Func<bool> isIngressPaused`, `ICommandMessageDecoding`, `ChannelClientMessageSending`, `int maxMessageBytes`.
  - **Никогда** не обращается к `IoC.Scope.Current` через `await` (`INV-7`).
  - Использует `WebSocket.ReceiveAsync(Memory<byte>, ct)` и `WebSocket.SendAsync(ReadOnlyMemory<byte>, ...)` (0 `new ArraySegment<byte>`, 0 `switch`!).
  - **Достижимая проверка размера (`impl-2`):** буфер приёма выделяется строго как **`new byte[_maxMessageBytes + 1]`**:
    - Fast-path одиночного `Text`-фрейма (`EndOfMessage == true`): если `result.Count > _maxMessageBytes` → ставит `"message_too_large"` в outbound `Channel`, вызывает `CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "message_too_large", loopToken)` и выходит; иначе декодирует напрямую из `buffer.AsSpan(0, result.Count)` без аллокации `MemoryStream`.
    - Фрагментированный `Text`-фрейм (`!EndOfMessage`): накапливает чанки в `MemoryStream`, проверяя `totalBytes + result.Count > _maxMessageBytes` на каждом чанке (при переполнении — `"message_too_large"` + `CloseOutputAsync(MessageTooBig)` + выход).
  - Проверка паузы приёма перед маршрутизацией декодированного `CommandDescription`: если `_isIngressPaused()` → ставит `EncodeError("busy", message.GameId, "ingress_paused")` в outbound `Channel` и не вызывает `_router.Route(...)`.
  - `WebSocketMessageType.Close` → `CloseOutputAsync(NormalClosure)` → выход (`TIME_WAIT`-safe).
  - `result.MessageType != WebSocketMessageType.Text` (`Binary`) → ошибка `"unsupported_frame"` → `CloseOutputAsync(InvalidMessageType)` → выход.
  - **Безопасный жизненный цикл `DrainOutboundAsync` (`impl-2`):** создаётся связанный `using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);`; `DrainOutboundAsync(socket, loopCts.Token)` оборачивает `await foreach (var payload in _outboundReader.ReadAllAsync(token))` в явные `catch (OperationCanceledException)` и `catch (WebSocketException)` (исключает необработанные faulted-задачи при обрыве сокета). В `finally` вызывается `_sender.Complete()`, затем `await drainTask;` (досылает финальный error-фрейм при открытом сокете), затем `loopCts.Cancel()` и `onDisconnected()`.
  - Receive-цикл перехватывает `OperationCanceledException` и `WebSocketException` двумя явными `catch`-блоками (все 4 `catch`-блока — 2 в receive и 2 в `DrainOutboundAsync` — покрываются `FakeWebSocket`).
- **`IServerExceptionRecording` / `ServerExceptionRecording`**:
  - Потокобезопасный журнал `ConcurrentQueue<Exception>`, используемый обработчиком `"Exception.Handle"` (`INV-7`, запрет `Console.WriteLine`).
- **`ServerBootstrap`** *(infra `Bootstrap`, единый файл Composition Root + маршруты)*:
  - `RunAsync(string[] args, CancellationToken ct) => RunAsync(args, ct, null, null);`
  - `RunAsync(string[] args, CancellationToken ct, Action<object>? configureServerScope, Action<Uri>? onStarted)`:
    1. Вызывает `new InitCommand().Execute()`.
    2. В `try/finally` (сохраняя внешний скоуп теста) получает `rootScope` (Level 1 — строго примитивы `scopes.dll`, 0 per-server регистраций!), создаёт изолированный **L2A `serverScope = Ioc.Resolve<object>("IoC.Scope.Create", rootScope)`** (исключает `scopes.dll` Trap #1 при запуске 2 серверов в одном xUnit-процессе!).
    3. Внутри `InSeparateScopeCommand(serverScope, ...)` синхронно регистрирует **хост-зависимости (L2A)**: `"Server.Scope.Root"`, `"Server.Router"`, `"Server.Ingress.Paused"` (простой `volatile` флаг `int` с делегатами чтения/переключения), `"Server.Exceptions"`, `"Exception.Handle"` (для команд inbox `ServerThreadLoopCommand` вне `GameScope`), `"Server.Thread.TryEnqueue"`, `"Endpoint.*"`, `"Server.Admin.Shutdown"`, `"Server.Admin.HotReload"`, `RegisterIoCDependencyServerThread`, и фабрику **`baseInitializer`** (`RegisterIoCDependencyEndpointGameBase(connections, exceptions)`), которая при сборке каждого **L2B `GameBaseScope_vN`** (ребёнка `rootScope`, переключаемого через `ProxyScope`, строго **`depth == 4`**) регистрирует ВСЕ игровые ключи Spec `03` (`"Exception.Handle"`, `"Server.Connections"`, `"Commands.InterpretMessage"`, `"Game.Item.Owner"`, `"Game.Message.Authorize"`, `"Game.Message.Command"`, `"Game.State.Serialize"`, `"Game.State.Broadcast"`, и **свежий изолированный экземпляр** `"Snapshot.Codecs"`). Затем вызывает `configureServerScope?.Invoke(serverScope)`, а если в `args` передан `--plugins-dir <path>`, вызывает `"Server.Admin.HotReload"` с `<path>` **до** открытия входящего приёма (гарантирует доступность плагинов при рестарте с `ServerSnapshot`).
    4. Синхронно резолвит потокобезопасные сервисы (`IGameRouter`, `Func<bool>` флага паузы приёма, `IGameConnectionRegistry`, `ICommandMessageDecoding`, делегаты `shutdown` и `hotReload`) **до** запуска Kestrel (`INV-7`).
    5. Собирает `WebApplication`, вызывает `UseWebSockets()` и маппит **4 прямых минимальных маршрута** (без `AdminEndpointRegistry` и `mapExtraRoutes`):
       - `GET /health` → `200 OK` `{"status":"ok"}`
       - `GET /ws` → не-WS → `400`; WS → `AcceptWebSocketAsync`, опциональная подписка `gameId` из query string в `IGameConnectionRegistry`, `WebSocketMessageLoop.RunAsync`, отписка в `finally`
       - `POST /admin/shutdown` (опционально `?snapshotPath=...`) → вызывает `"Server.Admin.Shutdown"` с `snapshotPath` и `IHostApplicationLifetime.StopApplication()` → `200 OK` `{"status":"shutting_down"}`
       - `POST /admin/hot-reload` (`?pluginsDir=...`) → вызывает `"Server.Admin.HotReload"` с `pluginsDir` → `200 OK` `{"status":"reloaded"}` (в Spec `02` делегирует в `"Scopes.HotReload"`; при отсутствии `pluginsDir` → `400 Bad Request`)
    6. Стартует `app.StartAsync(ct)`, уведомляет `onStarted?.Invoke(boundUri)`, ожидает `app.WaitForShutdownAsync(ct)` и корректно вызывает `app.StopAsync()`.

### 2.3. Реестр `IoC`-ключей Spec 03 (разделение L2A `serverScope` / L2B `GameBaseScope_vN` / L4 `GameScope`, `design-1`)

> **Инвариант `depth == 4` (`INV-7`, `design-1`):** `RootScope` (L1) хранит только примитивы `scopes.dll`. `serverScope` (L2A, ребёнок `RootScope`) хранит хост-сервисы конкретного экземпляра сервера. `GameBaseScope_vN` (L2B, ребёнок `RootScope`, переключаемый через `ProxyScope` L3) хранит **все** ключи, разрешаемые во время исполнения `GameScope` (L4), через замыкание на per-server `IGameConnectionRegistry` и `IServerExceptionRecording`.

| Ключ `IoC` | Уровень скоупа | Аргументы | Возврат | Описание |
| :--- | :---: | :--- | :--- | :--- |
| `"Server.Scope.Root"` | **L2A (`serverScope`)** | `[]` | `object` | Ссылка на `RootScope` (явный родитель для `GameBaseScope_vN` в Spec `02`) |
| `"Server.Router"` | **L2A (`serverScope`)** | `[]` | `IGameRouter` | Потокобезопасный $O(1)$ роутер `gameId -> IGameEndpointRoute` |
| `"Server.Ingress.Paused"` | **L2A (`serverScope`)** | `[]` или `[bool paused]` | `Func<bool>` / `ICommand` | Простой `volatile` флаг паузы приёма WebSocket (для `ServerSnapshot` Spec `04`) |
| `"Server.Admin.Shutdown"` | **L2A (`serverScope`)** | `[string? snapshotPath]` | `ICommand` | Грациозная остановка рабочих потоков (Spec `04` переопределяет для `ShutdownWithSnapshotCommand(snapshotPath)`) |
| `"Server.Admin.HotReload"` | **L2A (`serverScope`)** | `[string pluginsDir]` | `ICommand` | Горячая перезагрузка плагинов (по умолчанию `"Commands.Empty"`; со Spec `02` делегирует в `"Scopes.HotReload"`) |
| `"Server.Exceptions"` | **L2A (`serverScope`)** | `[]` | `IServerExceptionRecording` | Потокобезопасный приёмник исключений экземпляра сервера |
| `"Server.Thread.TryEnqueue"` | **L2A (`serverScope`)** | `[ICommandReceiver thread]` | `Func<ICommand, bool>` | Неблокирующий адаптер inbox потока (Spec `04` переопределяет на `ITryCommandReceiver.TryReceive`) |
| `"Endpoint.Message.Decoder"` | **L2A (`serverScope`)** | `[]` | `ICommandMessageDecoding` | `CommandMessageFactory` (разбор/кодирование JSON-кадров) |
| `"Endpoint.Route.Create"` | **L2A (`serverScope`)** | `[Func<ICommand, bool> tryEnqueue, IGameReceiverRegistry table]` | `IGameEndpointRoute` | Создаёт `ThreadGameEndpointRoute` |
| `"Endpoint.Target.Local.Create"` | **L2A (`serverScope`)** | `[ICommandReceiver schedulingReceiver]` или `[ICommandReceiver, Func<CommandDescription, ICommand>]` | `IGameCommandSending` | Создаёт `LocalEndpointGameSending` (с дефолтным `InterpretMessageCommand` или журнальной фабрикой Spec `04`) |
| `"Endpoint.GameBase.Initializer"` | **L2A (`serverScope`)** | `[]` | `ICommand` | Команда `RegisterIoCDependencyEndpointGameBase`, вызываемая из `baseInitializer` каждого `GameBaseScope_vN` |
| `"Exception.Handle"` | **L2B (`GameBaseScope_vN`)** *(+ L2A для inbox)* | `[ICommand cmd, Exception ex]` | `ICommand` | Регистрирует исключение в `IServerExceptionRecording` (`INV-7`, без `Console.WriteLine`) |
| `"Server.Connections"` | **L2B (`GameBaseScope_vN`)** | `[]` | `IGameConnectionRegistry` | Потокобезопасный реестр WebSocket-подписок (замыкание на экземпляр сервера) |
| `"Commands.InterpretMessage"` | **L2B (`GameBaseScope_vN`)** | `[CommandDescription msg]` | `ICommand` | Создаёт `InterpretMessageCommand` |
| `"Game.Item.Owner"` | **L2B (`GameBaseScope_vN`)** | `[object gameItem]` | `string` | Чтение свойства `"OwnerId"` из объекта для авторизации и дружественного огня (`TryGetValue`: возвращает `""`, если `"OwnerId"` отсутствует — безопасно для нейтральных сущностей `Asteroid`/`Bonus`, `D-020`) |
| `"Game.Message.Authorize"` | **L2B (`GameBaseScope_vN`)** | `[ICommand cmd, CommandDescription msg, object item]` | `ICommand` | При `msg.PlayerId.Length == 0` возвращает `cmd`; иначе резолвит `owner = Ioc.Resolve<string>("Game.Item.Owner", item)` и возвращает `Ioc.Resolve<ICommand>("Commands.Authorize", cmd, msg.PlayerId, owner)` |
| `"Game.Message.Command"` | **L2B (`GameBaseScope_vN`)** | `[CommandDescription msg]` | `ICommand` | **Точное правило арности (`comp-2`):** получает `item = repo.Get(msg.GameItemId)` и `decoded = CommandMessageFactory.DecodeArguments(msg, codecs)`: при `decoded == null` (`Args == null \|\| Args.Count == 0`) вызывает **1-аргументный** `Ioc.Resolve<ICommand>(msg.Type, item)`; при `decoded != null` вызывает **2-аргументный** `Ioc.Resolve<ICommand>(msg.Type, item, decoded)`. Затем оборачивает в `"Game.Message.Authorize"` и `"Commands.InTransaction"` (в L2B по умолчанию pass-through из Spec `02`, в L4 Spec `04` — реальный `InTransactionCommand`; при вызове `Macro.*` вложенные транзакционные фреймы `GameTransactions` корректно поддерживаются стеком за $O(1)$). |
| `"Game.State.Serialize"` | **L2B (`GameBaseScope_vN`)** *(переопределяется в L4)* | `[string gameId, long step]` | `ReadOnlyMemory<byte>` | Сериализация объектов из `"Game.Repository.ReadOnly"` + `IGameObjectIdSource` + `"Snapshot.Codecs"` через `CommandMessageFactory.EncodeTickState` |
| `"Game.State.Broadcast"` | **L2B (`GameBaseScope_vN`)** | `[string gameId]` | `ICommand` | Создаёт `BroadcastGameStateCommand` |
| `"Game.Step.State"` | **L4 (`GameScope`)** | `[]` | `GameStepState` | Экземпляр состояния шага конкретной игры |
| `"Game.Step.Current"` | **L4 (`GameScope`)** | `[]` | `long` | Текущий номер шага игры |
| `"Game.Step.Validate"` | **L4 (`GameScope`)** | `[CommandDescription msg]` | `bool` | Неблокирующая проверка `CurrentStep <= step <= CurrentStep + MaxFutureDelta` (переопределяется по OCP) |
| `"Game.Step.Advance"` | **L4 (`GameScope`)** | `[]` | `ICommand` | Инкрементирует `GameStepState.CurrentStep` |

### 2.4. Рассмотренные альтернативы
1. **Владение общими контрактами (`CommandDescription`, `IGameRouter`, `IGameReceiverRegistry`) в Spec `03`/`04` vs вынесение в Day-0 Spec `01` (`design-3`):** вынесено в Spec `01` — гарантирует 0 пересечений файлов между параллельными Track B (`03`) и Track C (`04`) и единую таблицу потоков (`IGameReceiverRegistry`).
2. **Единый L2 скоуп для сервера и игр vs разделение L2A `serverScope` и L2B `GameBaseScope_vN` (`design-1`):** разделение с регистрацией игровых ключей через `RegisterIoCDependencyEndpointGameBase` в `baseInitializer` сохраняет строгую глубину `depth == 4`, поддерживает 2 сервера в одном xUnit-процессе (без `scopes.dll` Trap #1) и гарантирует видимость `"Exception.Handle"`, `"Server.Connections"`, `"Game.Message.*"` из `GameScope` при любом hot-reload.
3. **Правка `ServerThread.cs` в Track B vs IoC-делегат `"Server.Thread.TryEnqueue"`:** выбран IoC-делегат — гарантирует 0 пересечений файлов с Track C, сохраняя полное тестирование backpressure (`QueueFull`).
4. **Строгий барьер по умолчанию vs неблокирующий `"Game.Step.Validate"` (`D-013`):** выбран неблокирующий валидатор по умолчанию, чтобы обязательный E2E-тест `ПЗ_6 69:05` (Игрок 1 молчит) не зависал; строгий барьер остаётся `git A` расширением.
5. **Динамический реестр `AdminEndpointRegistry` + `mapExtraRoutes` vs прямые маршруты в `ServerBootstrap`:** выбраны 4 прямых минимальных маршрута (`/health`, `/ws`, `/admin/shutdown`, `/admin/hot-reload`) — меньше файлов, проще чтение и 100% покрытие веток без лишних обёрток.

### 2.5. Риски и ограничения
- **Ловушка `scopes.dll` при многократном запуске сервера в тестах (`design-1`):** `ServerBootstrap` **никогда** не регистрирует per-server зависимости в L1 `RootScope` — все регистрации живут строго в изолированном L2A `serverScope` и L2B `GameBaseScope_vN`.
- **Ловушка `ooand-lint-gate.py`:** `ArraySegment<byte>`, `JsonSerializerOptions`, `Channel`, `WebApplication`, `CommandDescription` запрещены для `new` вне `INFRA_FILE_MARKERS`. В `WebSocketMessageLoop` используются `Memory<byte>`/`ReadOnlyMemory<byte>`, `byte[]`, `MemoryStream` и `CancellationTokenSource` (все разрешены), а все `new` инфраструктурных типов строго локализованы в `*Factory.cs`, `*Bootstrap.cs`, `Register*.cs`.
- **Достижимость веток Coverlet 100% (`impl-2`):** буфер приёма `new byte[_maxMessageBytes + 1]` делает ветку переполнения одиночного фрейма достижимой, а linked `CancellationTokenSource` + явные `catch` в `DrainOutboundAsync` исключают необработанные faulted-задачи.
- **Порог CI для бенчмарков:** локальная цель — $\ge 100\text{k}$ msg/s (in-process) и $\ge 20\text{–}50\text{k}$ msg/s (loopback WebSocket); утверждения в CI используют воспроизводимый нижний порог ($\ge 50\text{k}$ и $\ge 20\text{k}$ msg/s) с обязательным выводом фактической скорости через `ITestOutputHelper`.
- **Защита от `TIME_WAIT` (`ПЗ_3:652-696`):** все клиентские WebSocket-тесты и бенчмарк обязаны закрывать соединение через `await client.CloseAsync(WebSocketCloseStatus.NormalClosure, ..., ct)` перед `Dispose()`.

---

## Detailed Implementation Plan

### 1. Изменяемые и создаваемые файлы

> **Общие контракты, потребляемые из Spec `01` (не создаются и не изменяются в Spec `03`):** `SpaceBattle/SpaceBattle.Lib/Transfer/CommandDescription.cs`, `MessageRoutingStatus`, `IGameEndpointRoute`, `IGameRouter`, `IGameReceiverRegistry`.

| Действие | Файл (относительно корня канонического репозитория) | Точное назначение и ограничения `ooand-lint-gate` / `INV-*` |
| :--- | :--- | :--- |
| **Modify** | `SpaceBattle/SpaceBattle.sln` | Подключить проект `SpaceBattle.Server\SpaceBattle.Server.csproj` (Debug/Release). |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/SpaceBattle.Tests.csproj` | Добавить `<ProjectReference Include="..\SpaceBattle.Server\SpaceBattle.Server.csproj" />`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/GameRouter.cs` | $O(1)$ реализация `IGameRouter` (Spec `01`) на `ConcurrentDictionary<string, IGameEndpointRoute>`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/GameReceiverRegistry.cs` | Однопоточная реализация `IGameReceiverRegistry` (Spec `01`: `Set`, `RemoveIfSame`, `TryGet`, `GameIds`) на `Dictionary<string, IGameCommandSending>`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/ThreadGameEndpointRoute.cs` | Реализация `IGameEndpointRoute` через `Func<ICommand, bool>` и фабрику `DeliverMessageCommand` (0 `new`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/DeliverMessageCommand.cs` | Полиморфная доставка в `IGameCommandSending` из `IGameReceiverRegistry` (при отсутствии `gameId` — ранний `return`; 0 `is`/`as`/`switch`/`new`/`IDictionary`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/LocalEndpointGameSending.cs` | `IGameCommandSending` над `(ICommandReceiver, Func<CommandDescription, ICommand>)` — совместим с `SchedulingCommandReceiver` и `PendingCommandJournal` (Spec `04`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/InterpretMessageCommand.cs` | Валидация `"Game.Step.Validate"` + исполнение `"Game.Message.Command"` внутри `GameScope` (0 `new`/`IDictionary`/`switch`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/CommandArguments.cs` | Иммутабельный `sealed class` (`Get<T>`, `Contains`, `Count`) над `IReadOnlyDictionary<string, object>`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/ICommandMessageDecoding.cs` | `-ing` интерфейс декодирования входящего JSON и кодирования ошибок. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/CommandMessageFactory.cs` | *(Infra `Factory`)* Реализация `ICommandMessageDecoding`, `DecodeArguments` (`SnapshotValueCodecs`), `EncodeError`, `EncodeTickState`, `EncodeGameOver`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/GameStepState.cs` | Состояние шага игры (`CurrentStep`, `MaxFutureDelta`, `Advance`, `Restore`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/IStepValidating.cs` | `-ing` интерфейс `bool Validate(CommandDescription message);`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/NonBlockingStepValidating.cs` | Реализация `IStepValidating` (`CurrentStep <= Step <= CurrentStep + MaxFutureDelta`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/IClientMessageSending.cs` | `-ing` интерфейс `TrySend(ReadOnlyMemory<byte>)`, `Complete()`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/IGameConnectionRegistry.cs` | `Subscribe`, `Unsubscribe`, `Publish`, `GetConnectionCount`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/GameConnectionRegistry.cs` | Потокобезопасный реестр подписок с очисткой пустых игр и $O(1)$ неблокирующим `Publish`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/BroadcastGameStateCommand.cs` | Команда публикации тикового снапшота игры в `"Server.Connections"` из `GameBaseScope_vN` (0 `new`/`IDictionary`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/RegisterIoCDependencyEndpoint.cs` | *(Infra `Register`)* Регистрация L2A хост-ключей (`"Server.Router"`, `"Server.Ingress.Paused"`, `"Server.Thread.TryEnqueue"`, `"Endpoint.*"` — создаёт `ThreadGameEndpointRoute`, `LocalEndpointGameSending`, `DeliverMessageCommand` напрямую в `Register*`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/RegisterIoCDependencyEndpointGameBase.cs` | *(Infra `Register`)* `baseInitializer`-команда для L2B `GameBaseScope_vN`: регистрирует `"Exception.Handle"`, `"Server.Connections"`, `"Commands.InterpretMessage"`, `"Game.Item.Owner"` (`"OwnerId"`), `"Game.Message.Authorize"`, `"Game.Message.Command"` (1-arg vs 2-arg), `"Game.State.{Serialize,Broadcast}"`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/RegisterIoCDependencyGameStep.cs` | *(Infra `Register`)* Регистрация L4 `"Game.Step.{State,Current,Validate,Advance}"` внутри `GameScope`. |
| **Create** | `SpaceBattle/SpaceBattle.Server/SpaceBattle.Server.csproj` | `Microsoft.NET.Sdk.Web`, `net10.0`, ссылки на `SpaceBattle.Lib`, `core.dll`, `scopes.dll` (0 новых NuGet). |
| **Create** | `SpaceBattle/SpaceBattle.Server/Program.cs` | `public static Task Main(string[] args) => ServerBootstrap.RunAsync(args, CancellationToken.None);` (0 веток). |
| **Create** | `SpaceBattle/SpaceBattle.Server/ServerOptions.cs` | `sealed class` параметров хоста (не `record`). |
| **Create** | `SpaceBattle/SpaceBattle.Server/IServerExceptionRecording.cs` | `-ing` интерфейс `Record(ICommand command, Exception exception)`, `IReadOnlyCollection<Exception> Snapshot()`. |
| **Create** | `SpaceBattle/SpaceBattle.Server/ServerExceptionRecording.cs` | Потокобезопасная реализация на `ConcurrentQueue<Exception>` для `"Exception.Handle"`. |
| **Create** | `SpaceBattle/SpaceBattle.Server/ChannelClientMessageSending.cs` | `IClientMessageSending` + `ChannelReader<ReadOnlyMemory<byte>> Reader` (0 `new`). |
| **Create** | `SpaceBattle/SpaceBattle.Server/WebSocketMessageLoop.cs` | Тестируемый цикл `ReceiveAsync`/`DrainOutboundAsync` поверх `abstract class WebSocket` с `Func<bool> isIngressPaused`, буфером `maxMessageBytes + 1` и linked CTS (0 `switch`, 0 `ArraySegment`, 0 `IoC.Scope`). |
| **Create** | `SpaceBattle/SpaceBattle.Server/ServerBootstrap.cs` | *(Infra `Bootstrap`)* Единый Composition Root + 4 прямых маршрута (`/health`, `/ws`, `/admin/shutdown`, `/admin/hot-reload`), создание outbound `Channel`, разделение L2A `serverScope` / L2B `GameBaseScope_vN` (`baseInitializer`), синхронный резолв сервисов и грациозная остановка. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/EndpointTests/CommandMessageFactoryTests.cs` | Unit-тесты декодера/кодера JSON (`EncodeTickState`, `EncodeGameOver`, `EncodeError`), `CommandArguments` и граничных ошибок. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/EndpointTests/GameRouterAndDeliveryTests.cs` | Unit-тесты `GameRouter`, `GameReceiverRegistry` (`Set`, `RemoveIfSame`, `TryGet`, `GameIds`), `ThreadGameEndpointRoute`, `DeliverMessageCommand` (включая полиморфный `GameProxy`-стаб и отсутствие `gameId`), `LocalEndpointGameSending`. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/EndpointTests/InterpretAndStepTests.cs` | Unit-тесты `GameStepState`, `NonBlockingStepValidating`, `InterpretMessageCommand` (1-arg vs 2-arg `Args`), авторизации по `"OwnerId"` (`AuthorizeCommand`), `GameConnectionRegistry`, `BroadcastGameStateCommand`, видимости L2B `GameBaseScope_vN` из L4 `GameScope`. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/ServerTests/FakeWebSocket.cs` | Тестовый двойник `WebSocket` для детерминированного покрытия всех веток `WebSocketMessageLoop` и `DrainOutboundAsync`. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/ServerTests/WebSocketMessageLoopTests.cs` | Unit-тесты `WebSocketMessageLoop` (включая паузу `isIngressPaused() == true`, single-frame и fragmented overflow на `maxMessageBytes + 1`, `DrainOutboundAsync` `OperationCanceledException`/`WebSocketException`), `ChannelClientMessageSending` и `ServerExceptionRecording`. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/ServerTests/ServerBootstrapIntegrationTests.cs` | Интеграционные тесты `Program.Main`, `ServerBootstrap` (2 изолированных сервера в одном процессе — проверка отсутствия `scopes.dll` Trap #1), `/health`, `/ws` (не-WS 400 + реальный `ClientWebSocket` push + `CloseAsync`), `/admin/hot-reload` (`200`/`400`), `/admin/shutdown` и `/admin/shutdown?snapshotPath=...`. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/Benchmarks/InProcessRoutingBenchmarkTests.cs` | In-process бенчмарк 100k сообщений ($\ge 100\text{k}$ msg/s локально, CI floor $\ge 50\text{k}$ msg/s). |
| **Create** | `SpaceBattle/SpaceBattle.Tests/Benchmarks/WebSocketLoopbackBenchmarkTests.cs` | Loopback WebSocket бенчмарк 40k–50k фреймов ($\ge 20\text{–}50\text{k}$ msg/s, `TIME_WAIT`-safe `CloseAsync`). |

### 2. План тестирования xUnit (`100%` line & `100%` branch, `INV-8`)

Все тесты строго соблюдают: **0 комментариев**, **0 `Skip=`**, **0 `record` в `src`**, **0 `switch` в `src`**, изоляция через `new InitCommand().Execute()` + дочерний скоуп.

| Тестовый класс | Тест-кейсы (покрытие 100% строк и веток) |
| :--- | :--- |
| **`CommandMessageFactoryTests`** | (1) `[Theory]` `TryDecode` невалидные входы: битый JSON (`"invalid_json"`), `"null"`, `{}` (пустой `game_id`), отсутствующий `type`, пустой `type` (`"invalid_message"`); (2) Валидный JSON без `args` (`Args == null`, дефолтные `Id`/`PlayerId`/`Step`); (3) Валидный JSON с `args` (`int`, `Vector`, `Angle`) → `DecodeArguments` → `CommandArguments.Get<T>` / `Contains` / `Count` + `KeyNotFoundException`; (4) `DecodeArguments` при `Args == null` и пустом `Args` возвращает `null`; (5) `EncodeError`, `EncodeTickState` (с `winner == null` и ненулевым `winner`, пропуск несериализуемых свойств объекта) и `EncodeGameOver` (`"kind":"game_over"`, `"status":"finished"`, `"winner"`, `"alive_ships"` с несколькими игроками). |
| **`GameRouterAndDeliveryTests`** | (1) `GameRouter`: `Register` (валидный, пустой `gameId`, `null` route), `Route` (`null` → `ArgumentNullException`, `GameNotFound`, `QueueFull`, `Accepted`), `TryGetRoute` (`true`/`false`), `Unregister` (`true`/`false`); (2) `GameReceiverRegistry`: `Set` (первичная и перезапись на новый `IGameCommandSending`, невалидные аргументы), `TryGet` (`true`/`false`), `RemoveIfSame` (отсутствующий ключ → `false`, другой экземпляр → `false`, совпадающая ссылка → `true`), `GameIds`; (3) `ThreadGameEndpointRoute`: `TryRoute` (`null`, `true`, `false`); (4) `DeliverMessageCommand`: доставка локальному `LocalEndpointGameSending` (проверка постановки в `Game`, активации `Scheduler` и пользовательского `commandFactory` с `PendingCommandJournal`), полиморфная доставка в mock `IGameCommandSending` (имитация `GameProxy`), отсутствие `gameId` → чистый `return`; (5) Null-проверки конструкторов всех классов. |
| **`InterpretAndStepTests`** | (1) `GameStepState`: старт `0`, `Advance()`, `Restore(10)`, отрицательный `MaxFutureDelta` / `Restore(-1)` → `ArgumentOutOfRangeException`; (2) `NonBlockingStepValidating`: `step < CurrentStep` (`false`), `step == CurrentStep` (`true`), `step == CurrentStep + MaxFutureDelta` (`true`), `step > CurrentStep + MaxFutureDelta` (`false`); (3) `InterpretMessageCommand` (внутри L4 `GameScope` → L3 `ProxyScope` → L2B `GameBaseScope_vN` с `RegisterIoCDependencyEndpointGameBase`): отброс устаревшего шага без вызова команды; успешное выполнение 1-аргументной команды (`Args == null`, `Commands.Move`); успешное выполнение 2-аргументной команды с `Args` (`(item, CommandArguments)`); пустой `PlayerId` пропускает `AuthorizeCommand`; совпадение `PlayerId == OwnerId` пропускает команду; несовпадение `PlayerId != OwnerId` бросает `InvalidOperationException` и попадает в `"Exception.Handle"` из L2B `GameBaseScope_vN`; (4) `GameConnectionRegistry`: `Subscribe`, `GetConnectionCount` (`0` / `2`), `Publish` (пустая игра и активные подписчики), `Unsubscribe` (неизвестная игра, не последний клиент, последний клиент очищает словарь); (5) `BroadcastGameStateCommand`: публикует сериализованный tick-push в `GameConnectionRegistry` из L2B `GameBaseScope_vN`. |
| **`WebSocketMessageLoopTests`** | С `FakeWebSocket : WebSocket`: (1) Одиночный валидный `Text`-фрейм (`EndOfMessage = true`, `Count <= maxMessageBytes`) → `Accepted` (без аллокации `MemoryStream`); (2) Фрагментированный `Text`-фрейм (2 чанка: `EndOfMessage = false`, затем `true`) → корректная склейка и маршрутизация; (3) Переполнение `maxMessageBytes` на одиночном фрейме (`Count == maxMessageBytes + 1`, достижимо за счёт буфера `maxMessageBytes + 1`!) и на фрагментированном фрейме → `"message_too_large"` + `CloseOutputAsync(MessageTooBig)`; (4) `isIngressPaused() == true` → немедленный ответ `"busy"` (`"ingress_paused"`) без вызова `IGameRouter.Route`; (5) `Binary`-фрейм → `"unsupported_frame"` + `CloseOutputAsync(InvalidMessageType)`; (6) `Close`-фрейм → `CloseOutputAsync(NormalClosure)` + чистый выход; (7) Ошибки декодера (`invalid_json`, `invalid_message`) и роутера (`game_not_found`, `busy`) → запись JSON-ошибки в outbound `Channel` и отправка в `FakeWebSocket`; (8) `DrainOutboundAsync`: `socket.State != Open` выходит без броска, `SendAsync` бросающий `WebSocketException` и `OperationCanceledException` перехватывается чисто без faulted-task; (9) `ReceiveAsync` бросающий `WebSocketException` и `OperationCanceledException` → чистый выход и вызов `onDisconnected` в `finally`; (10) `ChannelClientMessageSending` (`DropOldest` переполнение, `Complete`) и `ServerExceptionRecording` (`Record`/`Snapshot`). |
| **`ServerBootstrapIntegrationTests`** | (1) **`Program.Main` 100% coverage test:** запуск `Program.Main(["--urls", $"http://127.0.0.1:{freePort}"])` в фоновом `Task` → `GET /health` (`200`) → `POST /admin/shutdown` (`200`) → `await mainTask` завершается без исключений; (2) `ServerBootstrap.RunAsync` с `configureServerScope` и `onStarted`: `GET /ws` без апгрейда → `400 Bad Request`; реальный `ClientWebSocket` на `/ws?gameId=g1` отправляет `CommandDescription`, игра (в L4 `GameScope` с L2B `GameBaseScope_vN`) выполняет команду и пушит `"kind":"state"` клиенту, клиент закрывает соединение `CloseAsync(NormalClosure)`; клиент без `gameId` в query string также корректно подключается и закрывается; (3) `POST /admin/hot-reload?pluginsDir=...` (`200`), `POST /admin/hot-reload` без `pluginsDir` (`400`), запуск с `--plugins-dir <dir>` загружает плагины до старта эндпоинта, и `POST /admin/shutdown?snapshotPath=snap.json` (проверяет передачу `snapshotPath == "snap.json"` в `"Server.Admin.Shutdown"`); (4) Параллельный/повторный запуск двух `ServerBootstrap.RunAsync` в одном процессе проходит без `scopes.dll` Trap #1; (5) Необработанное исключение команды в `ServerThread` и в `Game` фиксируется в `IServerExceptionRecording` через `"Exception.Handle"`. |
| **`InProcessRoutingBenchmarkTests`** | 2–4 `ServerThread`, 4 игры, 100 000 сообщений через `IGameRouter.Route` → `DeliverMessageCommand` → `Game` → `InterpretMessageCommand` → счётчик. Замер `Stopwatch`, вывод в `ITestOutputHelper`, проверка `throughput >= 50_000` msg/s (локальный ориентир $\ge 100\text{k}$ msg/s), `SoftStop` + `Join`. |
| **`WebSocketLoopbackBenchmarkTests`** | Реальный `SpaceBattle.Server` на `127.0.0.1:<freePort>`, 4 параллельных `ClientWebSocket`, 40 000 предсериализованных UTF-8 фреймов, ожидание точного числа обработанных команд на сервере, вывод `msg/s` в `ITestOutputHelper`, проверка `throughput >= 20_000` msg/s, обязательный `await ws.CloseAsync(NormalClosure)` для всех клиентов + `POST /admin/shutdown`. |

### 3. Команды верификации (из корня канонического репозитория)
```bash
python3 .agents/hooks/ooand-lint-gate.py --check-all .
dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes
dotnet build SpaceBattle/SpaceBattle.sln --no-incremental
dotnet test SpaceBattle/SpaceBattle.sln --no-build --collect:"XPlat Code Coverage"
python3 .agents/hooks/stop-verify-gate.py --verify-coverage-only .
python3 .agents/hooks/stop-verify-gate.py --print-sha .
```
Критерий прохождения: код возврата `0` на всех командах, `line-rate = 100.00%`, `branch-rate = 100.00%` (включая `SpaceBattle.Server/Program.cs`), 0 ошибок `ooand-lint-gate`, `verdict: CLEAN` в `.dev/.last-review.md` после `spec-review`.
