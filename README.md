# CSRefactor (`csr`)

Консольный инструмент анализа и рефакторинга C# для ИИ-агентов. Механику проверяет и правит инструмент, агенту остаются суждения: агент получает готовый отчёт и не читает длинные инструкции по стилю.

Статус: работают `csr metrics` и `csr style`; остальные команды в разработке.

## Что будет уметь

- `csr style` — проверка стиля по конфигу (работает: форматирование, комментарии, идиомы, модификаторы; дальше — имена и порядок членов).
- `csr arch` — проверка архитектурных признаков; отдельный режим и отдельный конфиг.
- `csr format` — форматирование только изменённых строк (движок — ReSharper Command Line Tools).
- `csr refs`, `csr rename` — ссылки на символ и семантическое переименование с защитой сериализуемых имён.
- `csr metrics` — метрики по синтаксису, CRAP с покрытием и снимок `.metrics/crap.edn` (работает).

Правила, пороги и уровни задаются конфигами: проверки в инструменте общие, конфиг выбирает их и подписывает находки ID своих правил. Разные команды и люди — разные конфиги.

## Требования

- .NET SDK 10 или новее.
- Для `format` и инспекций R# — [ReSharper Command Line Tools](https://www.jetbrains.com/resharper/download/#section=commandline) (бесплатно).
- Для загрузки старых `.csproj` (Unity) — MSBuild из Visual Studio или Build Tools.

## Сборка и запуск

```
dotnet build CSR.slnx
dotnet run --project src/CSR -- help
```

Установка как dotnet tool из локальной сборки:

```
dotnet pack src/CSR -c Release -o artifacts
dotnet tool install -g CSRefactor --add-source artifacts
```

## `csr metrics`

Метрики по синтаксису каждого файла, без загрузки решения: файлы только читаются.

```
csr metrics <файлы | папки>... [--config metrics.jsonc] [--project project.jsonc] [--format json | md] [--top N] [--all] [--coverage opencover.xml] [--edn файл] [--out файл]
```

| Уровень | Метрики |
|---|---|
| функция | цикломатическая сложность (правила crapper: `if`, циклы, `catch`, `?:`, метки `case`, ветви switch-выражения, `&&`, `\|\|`, `??`, `?.`), когнитивная сложность (спецификация SonarSource), строки физические и строки кода, вложенность, число параметров |
| тип | строки, строки кода, число членов по видам, число регионов |
| файл | строки, строки кода, число типов |

Функция — метод, конструктор, деструктор, оператор, аксессор, локальная функция, свойство и индексатор с телом-выражением; лямбды входят в объемлющую функцию. Отчёт: перцентили p50 / p90 / p99 / max, число значений выше порога и топ по каждой метрике. JSON (по умолчанию) — для агентов, `--all` добавляет все функции и типы по файлам; Markdown (`--format md`) — для человека. В папках пропускаются `bin`, `obj` и скрытые папки.

Конфиги — JSON с комментариями:
- `metrics.jsonc` — размер топа, включение метрик и пороги, пример — `examples/metrics.example.jsonc`;
- `project.jsonc` — общие сведения о проекте для всех режимов: символы препроцессора (по умолчанию `UNITY_EDITOR`) и методы логгера, пример — `examples/project.example.jsonc`.

CRAP = CC² × (1 − покрытие)³ + CC, как в crap4clj / crap4java: `--coverage` читает OpenCover XML (Unity Code Coverage, coverlet), покрытие функции — доля выполненных точек последовательности в её строках; функция, которой нет в отчёте, — покрытие 0. В отчёт добавляются покрытие и CRAP функций и топ по CRAP; несколько `--coverage` объединяются по точкам (строка и колонка; точка покрыта, если покрыта хоть в одном отчёте). `--edn` пишет снимок в формате `.metrics/crap.edn` (`{:entries [{:name :namespace :complexity :coverage :crap}]}`, `:namespace` — пространство имён и типы, `:name` — член типа; без `--coverage` — `:coverage nil`).

Коды возврата: 0 — отчёт построен, 2 — ошибка запуска (аргументы, конфиг, файл покрытия).

## `csr style`

Находки стиля по синтаксису каждого файла, без загрузки решения: файлы только читаются.

```
csr style <файлы | папки>... --config style.jsonc [--config ещё.jsonc]... [--project project.jsonc] [--diff <ревизия | файл.diff>] [--scope file | changed-lines] [--format json | md] [--out файл]
```

- Проверки движка нейтральны: что считать нарушением, задаёт конфиг. Проверка, которой нет в конфиге, выключена. У проверки — уровень (`error`, `warning`, `hint`), метка правила `rule` (ID правила вашего гайда, попадает в находку) и параметры; правило-выбор между двумя записями — параметр `prefer`.
- Несколько `--config` накладываются по порядку: следующий меняет уровень, метку и параметры, `"enabled": false` выключает проверку.
- `--scope changed-lines` оставляет находки только на изменённых строках: `git diff -U0` от ревизии `--diff` (по умолчанию `HEAD`) или готовый файл диффа; неотслеживаемые файлы — целиком. С `--diff` режим по умолчанию — `changed-lines`.
- JSON: `findings[]` (`file`, `line`, `endLine`, `column`, `check`, `rule`, `severity`, `message`, `replacement`) и `summary[]`. `replacement` — новый текст строк `line..endLine`; пустой список — удалить строки.
- Коды возврата: 0 — нет находок уровня `error`, 1 — есть, 2 — ошибка запуска.

| Проверка | Что ловит | Параметры |
|---|---|---|
| `line-length` | строка длиннее порога | `max`, `ignoreComments`, `ignoreStringLines` |
| `directive-column` | директива `#region`, `#if` и др. не с колонки 0 | `directives` |
| `binary-op-wrap` | место `&&` / `\|\|` при переносе | `prefer`: `leading` / `trailing`, `operators` |
| `xml-doc` | документационный комментарий `///` | — |
| `comment-separator` | комментарий-разделитель `// ---`, `// ===` | `minRun` |
| `comment-typography` | `—`, `«»`, `→`, `×`, `≈`, `≤` в комментариях, текстах логов, атрибутов-подсказок, `ToString` | `replace`, `attributes`, `logs`, `toString` |
| `comment-ending` | точка в конце однострочного комментария, заменяемые буквы | `prefer`: `no-period` / `period` / `any`, `letters`, `standaloneOnly` |
| `commented-code` | закомментированный код, в том числе хвост после кода | — |
| `todo-format` | TODO не в форме `// TODO: …`, с автором, датой, номером задачи | `forbidMetadata`, `requireCyrillic` |
| `log-call-wrap` | вызов лога в несколько строк или длиннее порога | `maxLength` |
| `message-text` | конкатенация и точка в конце текста лога или исключения | `concatenation`, `trailingPeriod`, `exceptions` |
| `null-check` | `is null` или `== null` | `prefer`: `equality` / `pattern` |
| `coalesce-assignment` | `if (x == null) x = …` вместо `??=` | — |
| `out-var` | `out var x` или `out T x`; непрочитанная переменная → `out _` | `prefer`: `explicit` / `var`, `discardUnused` |
| `bool-local-type` | `var` или `bool` у локальной с булевым инициализатором | `prefer`: `explicit` / `var` |
| `local-type` | явный тип у локальной и переменной `foreach`, где `var` дает тот же тип (или `var` при `prefer: explicit`) | `prefer`: `var` / `explicit`, `foreach`, `foreachExplicitTypes` |
| `tuple-type` | кортеж в сигнатуре, поле, свойстве | — |
| `explicit-private` | `private` у членов класса | `prefer`: `explicit` / `implicit`, `members` |
| `forbidden-modifier` | запрещённые модификаторы (`sealed`, `private protected`, …) | `modifiers` |
| `custom-delegate` | свой `delegate` вместо `Action` / `Func` | — |

Методы логгера для `log-call-wrap`, `message-text` и `comment-typography` — в `project.jsonc` (`logger.methods`, маски с `*`). Пример конфига — `examples/style.example.jsonc`.

## Тесты

```
dotnet test CSR.slnx
```

Покрытие тестами (coverlet, OpenCover) и CRAP самого CSR:

```
dotnet test CSR.slnx --collect "XPlat Code Coverage" --results-directory TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
csr metrics src --coverage TestResults/<id1>/coverage.opencover.xml --coverage TestResults/<id2>/coverage.opencover.xml --format md
```

## Структура

| Путь | Что |
|---|---|
| `src/CSR` | консольное приложение `csr`: разбор аргументов, файлы, вывод |
| `src/CSR.Core` | ядро (`netstandard2.0` и `net10.0`): на вход синтаксическое дерево и параметры, на выход метрики и находки стиля; без консоли, файловой системы и JSON; основа будущего пакета анализаторов Roslyn |
| `src/CSR.Reports` | разбор конфигов `.jsonc` и диффа, отчёты JSON и Markdown |
| `tests/CSR.Core.Tests`, `tests/CSR.Reports.Tests` | тесты (NUnit) |
| `examples/` | примеры конфигов |
| `spikes/` | прототипы замеров, не часть инструмента |
| `Directory.Build.props` | общие настройки проектов |
| `Directory.Packages.props` | версии NuGet-пакетов |

## Лицензия

GPL-3.0, см. `LICENSE`.
