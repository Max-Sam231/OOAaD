# Spec 01: Сверка канонического репозитория, иммутабельные `Angle`/`Vector` и `[Dependency]` для `Velocity`

> **Статус:** Zero-Decision спецификация (**Day 0 Gate** — разблокирует параллельные треки `02`, `03`, `04`)  
> **Связанные решения:** [`D-010`](../DECISIONS.md), [`D-017`](../DECISIONS.md), [`D-018`](../DECISIONS.md), [`D-021`](../DECISIONS.md) (сохраняет `origin/*` `D-001..D-009` без изменений по `INV-9`)  
> **Зависимости:** нет (корневой узел DAG из [`docs/ROADMAP.md`](../ROADMAP.md))  
> **Инварианты:** `INV-1`, `INV-2`, `INV-3`, `INV-5`, `INV-6`, `INV-8`, `INV-9`

---

## 1. Контекст, проблема, цели и границы

### Проблема
Командный репозиторий (`origin = https://github.com/Max-Sam231/OOAaD.git`) раздвоен на две неслитые ветки 2-го семестра поверх `origin/main` (`4efb0a0`, PR #67):
1. **Fork A** (`origin/feature/adapter-codegen`, `b0f65b0`, `D-003, D-007..D-009`): содержит `GeneratedCodeCompiler.Compile<T>`, `ICommandFactory`, внешние шаблоны `Adapters/Templates/*.txt` и `<None Update="Adapters\Templates\*.txt" CopyToOutputDirectory="PreserveNewest" />` в `SpaceBattle.Lib.csproj`.
2. **Fork B** (`origin/feature/game-transactions`, `dd1fe75`, `D-004..D-006`): включает стек `server-thread-queues` → `game-transfer` → `game-transactions` (итого `121` `.cs` файл @ `100%/100%`), но ответвлён от `5325224` **до** коммитов `D-007..D-009`.

Кроме расхождения веток, в базовом коде `origin/*` остаются 4 дефекта/пробела, блокирующие дальнейшие треки:
- **Гонка данных `INV-8` в `Angle.cs`:** `public static int Denominator { get; set; } = 8;` мутируется в конструкторе `Angle`, разрушая многопоточные игры и скоупы. Кроме того, `RegisterIoCDependencySnapshotCodecs.cs:34` читает `Angle.Denominator` статически.
- **Аллокации и ссылочная модель `Vector.cs`:** `Vector` — `class` с LINQ (`.Zip().ToArray()`, `.SequenceEqual()`, `.Aggregate()`) на каждом тике и без защитного копирования массива (мутация исходного `int[]` снаружи ломает иммутабельность).
- **Отсутствие null-проверок в `SendCommand.cs`:** единственная команда без граничной проверки `ArgumentNullException` в конструкторе (`N-01`).
- **Незавершённый производный адаптер (`INV-6`, `A3`, `ПЗ_2:429-437`):** кодогенерация адаптеров на Fork A готова, но в `SpaceBattle.Lib` отсутствует реальная стратегия `[assembly: Dependency(typeof(IMovingObject), "Velocity", ...)]`, вычисляющая вектор скорости из `Angle` и скалярной `Speed` без накопления ошибки округления.

### Цели (Goals)
1. Свести `origin/main` + Fork A (`b0f65b0`) + Fork B (`dd1fe75`) в единую зелёную базу (`121+` `.cs` файлов, `D-001..D-009` + `D-010..D-017`, CI `working-directory: SpaceBattle`, разработка строго из корня клона — не из игнорируемого хуками `контекст/`).
2. Преобразовать `Angle` в потокобезопасный `readonly struct` без `static` состояния и обновить кодек `Angle` в `RegisterIoCDependencySnapshotCodecs.cs`.
3. Сделать `Vector` иммутабельным `sealed class` с защитным клонированием координат и циклами `for` (0 LINQ), сохранив совместимость с `MoveCommand.cs` (`??`) и `100%/100%` покрытие.
4. Добавить null-проверки в конструктор `SendCommand` (без изменения `Execute()`) и зафиксировать переиспользование существующего `CommandInjectableCommand` + `StopLongRunningOperationCommand` как паттерна GoF `Bridge` ($O(1)$ `Stop`).
5. Добавить `VelocityFromAngleAndSpeedStrategy` с `[assembly: Dependency(typeof(IMovingObject), "Velocity", ...)]` и согласовать 2 существующих теста `IMovingObject` (`RegisterIoCDependencyAdapterTests`, `InTransactionCommandTests`).
6. Зафиксировать на Day 0 общие тонкие контракты между параллельными треками B (`Spec 03`) и C (`Spec 04`) без пересечения файлов (`design-3`): расширенный `CommandDescription` (`GameId`, `PlayerId`, `Step`, nullable `Args?` с `WhenWritingNull`) и интерфейсы `MessageRoutingStatus`, `IGameEndpointRoute`, `IGameRouter`, `IGameReceiverRegistry`.

### Границы (Non-Goals)
- Не переписывать `Game.cs`, `ServerThread`, `RoundRobinGameScheduler`, `GameTransactions`, `GameSnapshotSerializer`, `TransferableGame` (это области Spec `02`–`04`).
- Не дублировать `CommandInjectableCommand` новым классом `BridgeCommand` (`INV-3`).
- Не менять `.agents/hooks/*` и не вводить `record`/`record struct` (`D-017`).

### Ключевые сценарии (`Given → When → Then`)
1. **Сверка веток:** `Given` `origin/main` (`4efb0a0`), Fork A (`b0f65b0`) и Fork B (`dd1fe75`) `When` выполнен merge Fork A → merge/rebase Fork B с сохранением `Adapters/Templates/*.txt` в `SpaceBattle.Lib.csproj` и append-only `docs/DECISIONS.md` (`D-001..D-009` + `D-010..D-017`) `Then` все существующие тесты проходят с покрытием `100%/100%`.
2. **Потокобезопасный `Angle`:** `Given` `a = new Angle(1, 8)` и `b = new Angle(1, 16)` в разных потоках/скоупах `When` создаётся `b` `Then` `a.Denominator == 8` не меняется, `a + b` бросает `ArgumentException`, `default(Angle)` имеет `(Numerator = 0, Denominator = 8)` и безопасно приводится к `0.0` радиан.
3. **Снапшот `Angle`:** `Given` `Angle(3, 16)` `When` выполняется round-trip через `Snapshot.Codecs` `Then` сериализуется `[3, 16]` с использованием экземплярного `Denominator` и восстанавливается равный `Angle(3, 16)`.
4. **Иммутабельный `Vector`:** `Given` `int[] arr = [1, 2]; var v = new Vector(arr);` `When` вызывающий код меняет `arr[0] = 99` `Then` `v[0]` остаётся `1`; `MoveCommand` (`??`) продолжает компилироваться без правок (`INV-3`).
5. **Производный `Velocity` через `[assembly: Dependency]`:** `Given` объект словаря `{ ["Position"] = new Vector(1, 2), ["Angle"] = new Angle(2, 8), ["Speed"] = 3 }` и зарегистрированный `RegisterIoCDependencyAdapter(typeof(IMovingObject))` `When` выполняется `Commands.Move` `Then` `Velocity` вычисляется на лету как `(0, 3)` (`Math.Round(..., MidpointRounding.AwayFromZero)`) и `Position` становится `(1, 5)`.

---

## 2. Архитектура, контракты и альтернативы

### 2.1. Точный протокол git-сверки (Day 0)
Аудит `git diff --stat` от общего предка `5325224` подтвердил **0 пересекающихся `.cs` файлов** между Fork A и Fork B. Конфликты возникают только в `docs/DECISIONS.md`, `.memory-bank/context.md` и `SpaceBattle/SpaceBattle.Lib/SpaceBattle.Lib.csproj`:
1. Работать строго в корне канонического git-клона (где `.github/workflows/ci.yaml` задаёт `working-directory: SpaceBattle` и открыто `SpaceBattle/SpaceBattle.sln`), а **не** внутри `контекст/repo/` (все хуки игнорируют `контекст/`).
2. Начинать от `origin/main` (`4efb0a0`), влить Fork A (`origin/feature/adapter-codegen`, `b0f65b0`), затем влить/перебазировать Fork B (`origin/feature/game-transactions`, `dd1fe75`, уже включающий `server-thread-queues` и `game-transfer`).
3. При разрешении конфликта в `SpaceBattle/SpaceBattle.Lib/SpaceBattle.Lib.csproj` обязательно сохранить `<None Update="Adapters\Templates\*.txt" CopyToOutputDirectory="PreserveNewest" />` из Fork A.
4. В `docs/DECISIONS.md` сохранить `D-001..D-009` дословно в монотонном порядке и дописать `D-010..D-017` (`INV-9`). В `.memory-bank/context.md` объединить каталоги обеих веток.
5. Ветка `archive` и `dev` в текущем снапшоте отсутствуют (1-й семестр зафиксирован на `dfb5143`); создавать их не требуется.

### 2.2. Контракты типов и стратегий (Zero-Decision)

#### `SpaceBattle.Lib.Angle` (`public readonly struct Angle : IEquatable<Angle>`)
- **Состояние:** `private const int DefaultDenominator = 8;`, `private readonly int _denominator;`, `public int Numerator { get; }`.
- **Свойство `Denominator`:** `public int Denominator => _denominator == 0 ? DefaultDenominator : _denominator;` — ровно 1 ветка, полностью исключающая деление на 0 у `default(Angle)`.
- **Конструктор:** `public Angle(int numerator, int denominator = DefaultDenominator)` — при `denominator <= 0` бросает `ArgumentException`; нормализует `Numerator` в `[0, denominator - 1]`.
- **Операторы и методы:**
  - `public static Angle operator +(Angle a, Angle b)` — при `a.Denominator != b.Denominator` бросает `ArgumentException`; иначе `new Angle(a.Numerator + b.Numerator, a.Denominator)`.
  - `public static implicit operator double(Angle angle)` — `2 * Math.PI * angle.Numerator / angle.Denominator`.
  - `public bool Equals(Angle other)` — `Numerator == other.Numerator && Denominator == other.Denominator`.
  - `public override bool Equals(object? obj)` — `obj is Angle other && Equals(other)`.
  - `public override int GetHashCode()` — `HashCode.Combine(Numerator, Denominator)`.
  - `public static bool operator ==(Angle left, Angle right)` и `!=`.
  - `public override string ToString()` — `$"({Numerator}, {Denominator})"`.

#### `SpaceBattle.Lib.Vector` (`public sealed class Vector : IEquatable<Vector>`)
- **Состояние:** `private readonly int[] _coordinates;` (всегда не-null, `INV-3`: сохраняет совместимость с `??` в нетронутом `MoveCommand.cs`, не ломая `CS0019`).
- **Конструктор:** `public Vector(params int[] coordinates)` — при `coordinates == null` или `coordinates.Length == 0` сохраняет `Array.Empty<int>()`; иначе клонирует `(int[])coordinates.Clone()` (защита от алиасинга массива).
- **Свойства и индексатор:**
  - `public int Dimension => _coordinates.Length;`
  - `public int this[int index] => _coordinates[index];` *(нативный индексатор массива бросает `IndexOutOfRangeException` для `< 0` и `>= Length`, устраняя лишние ветки проверки границ)*
- **Операторы и методы (строго циклы `for`, 0 LINQ):**
  - `public static Vector operator +(Vector a, Vector b)` — проверка `a == null` / `b == null` (`ArgumentNullException.ThrowIfNull`) и `a._coordinates.Length != b._coordinates.Length` → `ArgumentException`; суммирование в новый `int[a._coordinates.Length]` циклом `for`; возврат `new Vector(result)` (через `private Vector(int[] coordinates, bool clone)` без повторного клонирования).
  - `public bool Equals(Vector? other)` — при `other is null` → `false`, проверка длин, затем поэлементный `for`.
  - `public override bool Equals(object? obj)` — `Equals(obj as Vector)`.
  - `public override int GetHashCode()` — полиномиальный цикл `for` (`unchecked(hash * 31 + _coordinates[i])` с базой `17`).
  - `public static bool operator ==(Vector? left, Vector? right)` (`ReferenceEquals(left, right) || (left is not null && left.Equals(right))`) и `!=`.

#### `SpaceBattle.Lib.VelocityFromAngleAndSpeedStrategy` (`INV-6`, `A3`)
- **Сборочный атрибут (в том же файле):**
  `[assembly: Dependency(typeof(IMovingObject), "Velocity", typeof(VelocityFromAngleAndSpeedStrategy), "Resolve")]`
- **Сигнатура метода:** `public static object Resolve(object[] args)` (соответствует `method.CreateDelegate<Func<object[], object>>()` в `RegisterIoCDependencyAdapter.cs:54`).
- **Контракт:**
  - Читает `gameItem = (IDictionary<string, object>)args[0]`, `speed = (int)gameItem["Speed"]`, `angle = (Angle)gameItem["Angle"]`.
  - Переводит угол в радианы (`double radians = angle;`) и возвращает:
    `new Vector((int)Math.Round(speed * Math.Cos(radians), MidpointRounding.AwayFromZero), (int)Math.Round(speed * Math.Sin(radians), MidpointRounding.AwayFromZero))`.
  - Размещение в файле `VelocityFromAngleAndSpeedStrategy.cs` (не `*Command.cs`) полностью соответствует `INV-2` и `ooand-lint-gate.py`; `new Vector(...)` входит в `ALLOWED_NEW_TYPES`.
  - Не конфликтует с `SpaceBattle.Tests/AdaptersTests/AdapterTestTypes.cs` (там `[Dependency]` объявлен на `ITestMovingObject`, а не на `IMovingObject`).

#### Верификация `Bridge` ($O(1)$ `Stop`) и `SendCommand`
- **Существующий `Bridge`:** `SpaceBattle.Lib/Commands/CommandInjectableCommand.cs` (`ICommand, ICommandInjecting`) + `SpaceBattle.Lib/Commands/RegisterIoCDependencyActionsStop.cs` (`StopLongRunningOperationCommand` заменяет команду на `"Commands.Empty"` за $O(1)$) уже реализуют паттерн GoF `Bridge` из `ПЗ_6 13:45`. Никаких новых классов не создаётся (`INV-3`).
- **`SendCommand`:** конструктор проверяет `command ?? throw new ArgumentNullException(nameof(command))` и `receiver ?? throw new ArgumentNullException(nameof(receiver))`; метод `Execute()` остаётся без изменений.

#### Общие Day-0 контракты для развязки треков B (`03`) и C (`04`) (`design-3`)
Чтобы параллельные треки `03` и `04` имели **0 пересекающихся файлов**, единый конверт и тонкие интерфейсы маршрутизации/шлюза фиксируются в Spec `01` (обычный `class`/интерфейсы/`enum`, **0 `record`**, **0 `-able`**):
- **`SpaceBattle.Lib.CommandDescription`** (`SpaceBattle/SpaceBattle.Lib/Transfer/CommandDescription.cs`, `Modify`):
  - Сохраняет `Id` (`"id"`, `string`, `""`), `Type` (`"type"`, `string`, `""`), `GameItemId` (`"game_item_id"`, `int`, `0`).
  - Добавляет опциональные свойства с `[JsonPropertyName(...)]`:
    - `GameId` (`"game_id"`, `string`, default `""`)
    - `PlayerId` (`"player_id"`, `string`, default `""`)
    - `Step` (`"step"`, `long`, default `0`)
    - `Args` (`"args"`, `Dictionary<string, Dictionary<string, JsonElement>>?`, default `null`, `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`) — аргументы в формате `SnapshotValueCodecs` (`{"type":"...","value":...}`).
- **`SpaceBattle.Lib.MessageRoutingStatus`** (`SpaceBattle/SpaceBattle.Lib/Endpoint/MessageRoutingStatus.cs`, `Create`): `public enum MessageRoutingStatus { Accepted, GameNotFound, QueueFull }`.
- **`SpaceBattle.Lib.IGameEndpointRoute`** (`SpaceBattle/SpaceBattle.Lib/Endpoint/IGameEndpointRoute.cs`, `Create`): `bool TryRoute(CommandDescription message);`.
- **`SpaceBattle.Lib.IGameRouter`** (`SpaceBattle/SpaceBattle.Lib/Endpoint/IGameRouter.cs`, `Create`):
  - `MessageRoutingStatus Route(CommandDescription message);`
  - `void Register(string gameId, IGameEndpointRoute route);`
  - `bool Unregister(string gameId);`
  - `bool TryGetRoute(string gameId, [MaybeNullWhen(false)] out IGameEndpointRoute route);`
- **`SpaceBattle.Lib.IGameReceiverRegistry`** (`SpaceBattle/SpaceBattle.Lib/Endpoint/IGameReceiverRegistry.cs`, `Create`, однопоточная таблица конкретного `ServerThread` поверх `IGameCommandSending`):
  - `void Set(string gameId, IGameCommandSending target);`
  - `bool RemoveIfSame(string gameId, IGameCommandSending expected);`
  - `bool TryGet(string gameId, [MaybeNullWhen(false)] out IGameCommandSending target);`
  - `IReadOnlyCollection<string> GameIds { get; }`

### 2.3. Реестр `IoC`-ключей, затрагиваемых Spec 01

| Ключ `IoC` | Уровень скоупа | Аргументы | Возврат | Изменение |
| :--- | :---: | :--- | :--- | :--- |
| `"Snapshot.Codecs"` | GameBase (L2B) / Test | `[]` | `SnapshotValueCodecs` | Обновлён кодек `Angle` (экземплярный `Denominator`) и цикл `for` для `Vector`. |
| `"SpaceBattle.Lib.IMovingObject.Velocity"` | GameBase / Test | `[IDictionary<string, object> item]` | `Vector` | Автоматически привязывается `RegisterIoCDependencyAdapter(typeof(IMovingObject))` через `[assembly: Dependency]` → `VelocityFromAngleAndSpeedStrategy.Resolve`. |
| `"Commands.Send"`, `"Actions.Stop"`, `"Commands.CommandInjectable"` | GameBase / Test | без изменений | `ICommand` | Контракты сохранены; `SendCommand` валидирует `null` в конструкторе. |

### 2.4. Рассмотренные альтернативы
1. **Нормализация дробей `Angle` через НОК при сложении vs требование равных знаменателей:** выбран запрет сложения разных знаменателей (`ArgumentException`), т.к. угловая сетка фиксирована правилами игры, нет риска переполнения `int` и лишних веток НОД/НОК (`D-010`).
2. **`sealed class Vector` vs `readonly struct Vector`:** выбран иммутабельный `sealed class` (`D-018`) — нетронутый `MoveCommand.cs` использует `??` над `IMovingObject.Position`/`Velocity` (`CS0019` на `struct`), а `new Vector(...)` входит в `ALLOWED_NEW_TYPES` и сохраняет 100% совместимость с существующими null-тестами.
3. **Fallback на `gameItem["Velocity"]` внутри `VelocityFromAngleAndSpeedStrategy`:** отвергнут — хранение вектора скорости прямо нарушает `ПЗ_2:429-437` и добавляет лишнюю ветку; вместо этого 2 существующих теста (`RegisterIoCDependencyAdapterTests`, `InTransactionCommandTests`) обновляются на `(Angle, Speed)`.

### 2.5. Риски и ограничения
- **Ложное срабатывание `[WARN]` `INV-3` в `ooand-lint-gate.py`:** правка `SendCommand.cs` выдаст неблокирующее предупреждение (`git M` на `*Command.cs`) — это осознанный bugfix граничного контракта конструктора без изменения `Execute()`.
- **Храповик числа тестов (`stop-verify-gate.py`):** при обновлении `VectorTests.cs`, `AngleTests.cs`, `RegisterIoCDependencyAdapterTests.cs`, `InTransactionCommandTests.cs` запрещено уменьшать общее число `[Fact]`/`[Theory]` относительно `HEAD`.
- **Порядок `dotnet format`:** запускать `dotnet format` строго до `stop-verify-gate.py --print-sha` и `spec-review` (`D-017`).

---

## Detailed Implementation Plan

### 1. Изменяемые и создаваемые файлы

| Действие | Файл (относительно корня канонического репозитория) | Точное изменение и обоснование |
| :--- | :--- | :--- |
| **Modify** *(merge)* | `SpaceBattle/SpaceBattle.Lib/SpaceBattle.Lib.csproj` | Сохранить `<None Update="Adapters\Templates\*.txt" CopyToOutputDirectory="PreserveNewest" />` после слияния Fork A + Fork B. |
| **Modify** *(merge)* | `docs/DECISIONS.md`, `.memory-bank/context.md` | Свести `D-001..D-009` (verbatim) + `D-010..D-017`; объединить каталог модулей в `.memory-bank/context.md`. |
| **Modify** | `SpaceBattle/SpaceBattle.Lib/Struct/Angle.cs` | Преобразовать в `public readonly struct Angle : IEquatable<Angle>`, удалить `public static int Denominator`, добавить экземплярный `Denominator` (с безопасным `default(Angle)` → `8`), проверку совпадения знаменателей в `operator +`, сравнение `(Numerator, Denominator)` в `Equals`/`GetHashCode`. |
| **Modify** | `SpaceBattle/SpaceBattle.Lib/Struct/Vector.cs` | Преобразовать в `public sealed class Vector : IEquatable<Vector>`, удалить `using System.Linq`, добавить защитное клонирование в конструкторе, `Dimension`, реализовать `+`, `Equals`, `GetHashCode`, `==`/`!=` циклами `for`. |
| **Modify** | `SpaceBattle/SpaceBattle.Lib/Transfer/RegisterIoCDependencySnapshotCodecs.cs` | Заменить `Angle.Denominator` на `((Angle)value).Denominator` (строка 34); заменить LINQ в кодеке `Vector` на цикл `for`. |
| **Modify** | `SpaceBattle/SpaceBattle.Lib/Transfer/CommandDescription.cs` | Добавить опциональные `GameId`, `PlayerId`, `Step` и nullable `Args?` (`Dictionary<string, Dictionary<string, JsonElement>>?`, `WhenWritingNull`, default `null`). Plain `class` (0 `record`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/MessageRoutingStatus.cs` | `enum MessageRoutingStatus { Accepted, GameNotFound, QueueFull }`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/IGameEndpointRoute.cs` | `bool TryRoute(CommandDescription message);`. |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/IGameRouter.cs` | `Route`, `Register`, `Unregister`, `TryGetRoute` (0 `-able`). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Endpoint/IGameReceiverRegistry.cs` | `Set`, `RemoveIfSame`, `TryGet`, `GameIds` над `IGameCommandSending` (0 `-able`). |
| **Modify** | `SpaceBattle/SpaceBattle.Lib/Commands/SendCommand.cs` | Добавить `ArgumentNullException` проверки для `command` и `receiver` в конструкторе (`Execute()` не трогать). |
| **Create** | `SpaceBattle/SpaceBattle.Lib/Adapters/VelocityFromAngleAndSpeedStrategy.cs` | `[assembly: Dependency(typeof(IMovingObject), "Velocity", typeof(VelocityFromAngleAndSpeedStrategy), "Resolve")]` + `public static class VelocityFromAngleAndSpeedStrategy` с `public static object Resolve(object[] args)`. |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/StructsTests/AngleTests.cs` | Добавить тесты на независимость `Denominator` у двух экземпляров, `ArgumentException` при сложении разных знаменателей, неравенство разных знаменателей, `default(Angle)` (`Numerator`, `Denominator`, `double`, `+`, `ToString`). |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/StructsTests/VectorTests.cs` | Сохранить все существующие тесты (включая null-кейсы); добавить тесты на защитное клонирование массива, `Dimension`, `operator +` с `null`, неравенство разных размерностей и `Equals(object)` с `Vector` и не-`Vector`. |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/TransferTests/SnapshotTests.cs` | Добавить в `SerializableValues()` (или отдельный `[Fact]`) проверку round-trip `Angle(3, 16)` (нестандартный `Denominator`), подтверждающую сериализацию экземплярного `Denominator`. |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/CommandsTests/SendCommandTests.cs` | Добавить `[Fact]` тесты на `ArgumentNullException` при `command == null` и `receiver == null`. |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/AdaptersTests/RegisterIoCDependencyAdapterTests.cs` | Обновить `GeneratedAdapter_WorksWithExistingMoveCommandRegistration` (строки 118–131): задать `["Angle"] = new Angle(0, 8), ["Speed"] = 3`, проверить `Position` `(1, 2) -> (4, 2)` через сгенерированный адаптер и `[assembly: Dependency]`. |
| **Modify** | `SpaceBattle/SpaceBattle.Tests/TransactionsTests/InTransactionCommandTests.cs` | Обновить `_stored` (строки 11–17) и проверку (строки 111–127): заменить `["Velocity"] = new Vector(3, 4)` на `["Angle"] = new Angle(0, 8), ["Speed"] = 3`, проверяя откат `Position` при сбое `MacroCommand` в транзакции. |
| **Create** | `SpaceBattle/SpaceBattle.Tests/AdaptersTests/VelocityFromAngleAndSpeedStrategyTests.cs` | Полный набор unit- и integration-тестов для `VelocityFromAngleAndSpeedStrategy` и `IMovingObject.Velocity`. |

### 2. План тестирования xUnit (`100%` line & `100%` branch, `INV-8`)

Все новые и обновлённые тесты соблюдают: **0 комментариев**, **0 `Skip=`**, **0 `record` в `src`**, изоляция через `new InitCommand().Execute()` + дочерний `IoC.Scope.Create` при работе с `IoC`.

| Тестовый класс | Тест-кейсы (покрываемые ветки) |
| :--- | :--- |
| **`AngleTests`** | Существующие 12 кейсов + (1) `TwoAngles_DifferentDenominators_DoNotMutateEachOther` (проверка отсутствия гонки `INV-8`); (2) `Add_DifferentDenominators_ThrowsArgumentException` (`a.Denominator != b.Denominator` → `true`); (3) `Equals_DifferentDenominators_ReturnsFalse` (`Numerator ==` true, `Denominator ==` false); (4) `DefaultAngle_HasDefaultDenominatorAndZeroNumerator` (покрывает `_denominator == 0` → `true` и `false` на обычном `Angle`). |
| **`VectorTests`** | Существующие 20 кейсов (включая `Constructor_NullArray_CreatesEmptyVector` с `new Vector(null!)`, покрывающий ветку `coordinates == null`, и `Indexer_NegativeIndex`/`Indexer_IndexTooLarge`) + (1) `Constructor_MutatingInputArray_DoesNotAffectVector` (защитное клонирование, `Length > 0`); (2) `Constructor_EmptyArray_HasZeroDimension` (`Length == 0`); (3) `Add_NullOperand_ThrowsArgumentNullException`; (4) `Equals_DifferentDimensions_ReturnsFalse` (`Length != other.Length` → `true`); (5) `EqualsObject_VectorAndNonVector_ReturnsExpected` (`obj as Vector` → `true` и `false`). |
| **`SnapshotTests`** | Существующие кейсы + (1) `Codecs_RoundTripAngleWithCustomDenominator_PreservesDenominator` (`new Angle(3, 16)` → JSON `[3, 16]` → `Angle(3, 16)` без влияния на `new Angle(1, 8)`); (2) `CommandDescription_ExtendedFields_RoundTripAndOmitNullArgs` (покрывает 100% геттеров/сеттеров `GameId`, `PlayerId`, `Step`, `Args == null` → ключ `"args"` опущен в JSON, и `Args != null` → полный round-trip; интерфейсы `IGameRouter`/`IGameReceiverRegistry`/`IGameEndpointRoute` и `enum MessageRoutingStatus` не содержат исполняемых веток Coverlet). |
| **`SendCommandTests`** | Существующие 2 кейса + `Constructor_NullCommand_ThrowsArgumentNullException` + `Constructor_NullReceiver_ThrowsArgumentNullException`. |
| **`VelocityFromAngleAndSpeedStrategyTests`** | (1) `[Theory]` для углов `0/8`, `2/8` ($\pi/2$), `4/8` ($\pi$), `6/8` ($3\pi/2$), `1/8` ($\pi/4$, проверка округления `MidpointRounding.AwayFromZero`) и разных скоростей (`Speed = 2, 3, 5`); (2) `Resolve_MissingSpeedOrAngle_ThrowsKeyNotFoundException`; (3) Integration: `RegisterIoCDependencyAdapterFactory` + `RegisterIoCDependencyAdapter(typeof(IMovingObject))` + `MoveCommand` двигает корабль по `(Angle, Speed)` и при повороте `RotateCommand` на лету пересчитывает `Velocity` без накопления ошибки. |
| **`RegisterIoCDependencyAdapterTests` & `InTransactionCommandTests`** | Обновлённые интеграционные кейсы с `(Angle, Speed)` подтверждают работу `MoveCommand` через compile-once `IAdapterFactory` и откат `InTransactionCommand`. |

### 3. Команды верификации (из корня канонического репозитория)
```bash
python3 .agents/hooks/ooand-lint-gate.py --check-all .
dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes
dotnet build SpaceBattle/SpaceBattle.sln --no-incremental
dotnet test SpaceBattle/SpaceBattle.sln --no-build --collect:"XPlat Code Coverage"
python3 .agents/hooks/stop-verify-gate.py --verify-coverage-only .
python3 .agents/hooks/stop-verify-gate.py --print-sha .
```
Критерий прохождения: код возврата `0` на всех командах, `line-rate = 100.00%`, `branch-rate = 100.00%`, 0 ошибок линтера, `verdict: CLEAN` в `.dev/.last-review.md` после `spec-review`.
