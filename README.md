# SpaceBattle (ООАиП)

Исполнители:
- Марютин Максим
- Чвалюк Иван
- Зюзин Дмитрий

Репозиторий настроен так, чтобы все участники команды (в Antigravity) работали по единым правилам, архитектурным инвариантам курса и автоматическим проверкам без внешней настройки. Снимок кода 1-го семестра сохранён в ветке `archive`.

---

## 1. Первый запуск после `git clone`

1. Убедись, что установлен `.NET 10 SDK` и `Python 3`.
2. Один раз включи командные git-хуки из репозитория (папка `.git/hooks/` гитом не версионируется, поэтому хук `pre-push` хранится в `scripts/githooks/`):
   ```bash
   git config core.hooksPath scripts/githooks
   ```
3. Открой корень репозитория в Antigravity — инструкции из `AGENTS.md`, правила `.agents/rules/ooand-invariants.md`, скиллы и субагенты из `.agents/` подхватятся автоматически.

---

## 2. Полный цикл разработки фичи

Никакой код в `SpaceBattle/` не пишется «сразу сходу» — `guard-gate` заблокирует правку `.cs` до появления решения и спеки. Исключений и обходных коридоров нет:

### Этап 1. Разбор вводной и фиксация решений (`egm-brainstorm` → `docs/DECISIONS.md`)
* **Когда запускать:** пришла новая задача/вводная с ПЗ, фото доски или неясные требования.
* **Что писать в чат агенту:**
  > «Используй скилл `egm-brainstorm`: разбери вот эту вводную ...»
* **Как работает:** агент задаёт уточняющие вопросы, параллельно запускает 4 Goldfish-агентов в чистом контексте, сохраняет черновик в `.dev/egm/concepts_brief.md` и помогает зафиксировать выбранные архитектурные развилки записью `D-NNN` в append-only журнал [`docs/DECISIONS.md`](docs/DECISIONS.md).

### Этап 2. Написание и стресс-тест спеки (`egm-draft` → `egm-review` → `egm-resolve`)
* **Написать спеку (`egm-draft`):**
  > «Запусти `egm-draft` по решению `D-NNN` и брифу».
  Собирает пошагово `.dev/egm/design_draft.md` и сохраняет утверждённую спеку в `docs/designs/<slug>.md` (с точными ключами `IoC`, списком `Create`/`Modify` файлов и тест-планом на **100% покрытие**). После этого `guard-gate` открывает доступ к `.cs` файлам.
* **Прогнать независимую критику спеки (`egm-review`):**
  > «Прогони `egm-review` для `docs/designs/<slug>.md`».
  В изолированных чистых контекстах (без памяти о диалоге автора) спеку атакуют 4 Goldfish-персоны, отчёт ложится в `.dev/egm/goldfish_findings.md`.
* **Разобрать замечания (`egm-resolve`):**
  > «Разбери замечания через `egm-resolve`».
  Классифицирует каждый пункт (`PROPOSE FIX` / `CONTEST` / `ESCALATE` / `DECLINE`) и после твоего подтверждения точечно обновляет `docs/designs/<slug>.md`.

### Этап 3. Код по TDD и тройная проверка (`Actor → Critic → Evaluator`)
* **Что писать в чат:**
  > «Реализуй фичу по спеке `docs/designs/<slug>.md` через полный конвейер».
* **Как проходит реализация:**
  1. **Actor (`csharp-developer` + `tdd-workflow`):** сначала пишет падающие xUnit-тесты (`NSubstitute`, отдельный `IoC` scope на тест), затем код реализации с **нулём комментариев** и **100% покрытием (`line` и `branch`)**.
  2. **Critic (`adversarial-skeptic`):** ищет гонки в скоупах, циклы графа команд, оверинжиниринг и дыры в логике (макс. 2 раунда доработки до эскалации).
  3. **Evaluator (`spec-review` → `code-reviewer-qa`):** в отдельном чистом контексте сверяет `git diff` с `ooand-invariants.md`, `DECISIONS.md` и спекой, прогоняет `dotnet build` / `dotnet test` и записывает `.dev/.last-review.md`.

### Этап 4. Память проекта (`memory-bank`)
После успешного `spec-review` агент по скиллу `memory-bank` обновляет `.memory-bank/context.md` (или профильные файлы внутри `.memory-bank/`, если структура расширена) при появлении новых модулей или ключей `IoC`.

---

## 3. Справочник агентов, скиллов и гейтов

### Субагенты (`.agents/agents/`)
| Агент | Назначение |
| :--- | :--- |
| **`zoom_out.md`** (`egm-zoom-out`) | *EGM Goldfish (этап спеки):* «скептик с высоты птичьего полёта» — отсекает YAGNI, лишние абстракции, переусложнение и проверяет переиспользование `IoC` / `Common/*.dll`. |
| **`design_critic.md`** (`egm-design`) | *EGM Goldfish (этап спеки):* критик архитектуры — требует альтернативы и ищет нарушения `INV-1..INV-9`, гонки в скоупах, циклы и точки отказа. |
| **`comprehension.md`** (`egm-comprehension`) | *EGM Goldfish (этап спеки):* «наивный сокомандник» — проверяет, что спека самодостаточна и понятна любому участнику команды без истории чата. |
| **`implementation_readiness.md`** (`egm-implementation-readiness`) | *EGM Goldfish (этап спеки):* проверяет, что спека готова к кодингу без догадок (`Zero-Decision`), файлы `Create`/`Modify` не врут о текущем коде и есть план тестов на 100%. |
| **`csharp-developer.md`** | *Actor (код):* пишет код `.NET 10` и xUnit-тесты по спеке и `tdd-workflow`. |
| **`adversarial-skeptic.md`** | *Critic (код и архитектура):* жёсткий скептик для сложных развилок, гонок и диффов кода. |
| **`code-reviewer-qa.md`** | *Evaluator (финальная приёмка):* сверяет дифф со спекой/инвариантами, честность тестов и 100% покрытие → `.dev/.last-review.md`. |

### Автоматические хуки и CI
* **`guard-gate.py` (`PreToolUse`):** блокирует опасные команды, `git add .`, авто-коммиты/пуши без явной просьбы, несанкционированные правки самого харнесса, удаление старых записей из `docs/DECISIONS.md` и написание `.cs` до фиксации `D-NNN` + спеки.
* **`ooand-lint-gate.py` (`PostToolUse` + `PostInvocation`):** мгновенно ловит комментарии (`//`, `/* */`, `///`) в `.cs`, прямой `new` в прикладном коде, `switch`, `IDictionary` в командах, `-able` интерфейсы, `Activator.CreateInstance` вне фабрики и обращение `Game` к `IoC.Scope.*`.
* **`stop-verify-gate.py` (`Stop`):** не даёт агенту завершить ход при правках `.cs`, если не прошли линтер, `dotnet build`, `dotnet test`, покрытие `< 100%` по строкам/веткам, упало число тестов или нет свежего `verdict: CLEAN` в `.dev/.last-review.md`.
* **`scripts/githooks/pre-push` и `.github/workflows/ci.yaml`:** локально перед `git push` и в GitHub Actions проверяют линтер, `dotnet format --verify-no-changes`, сборку и **100% line/branch coverage**.

---

## 4. Частые правила, о которых важно помнить

* **0 комментариев в C#:** любые `//`, `/* */`, `///` в коде и тестах считаются дефектом (`INV-8`).
* **100% покрытие:** учитываются и строки (`line`), и ветки (`branch`) в Coverlet.
* **Никаких авто-коммитов:** агент делает `git commit` / `git push` только по твоей прямой команде в сообщении, с точечным `git add <files>`.
* **Быстрая локальная проверка руками в любое время:**
  ```bash
  python3 .agents/hooks/ooand-lint-gate.py --check-all .
  dotnet format SpaceBattle/SpaceBattle.sln --verify-no-changes
  dotnet build SpaceBattle/SpaceBattle.sln --no-incremental
  dotnet test SpaceBattle/SpaceBattle.sln --collect:"XPlat Code Coverage"
  ```
