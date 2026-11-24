# CSR: документация

Подробное описание команд, конфигов, проверок `style`, признаков `arch` и метрик. Введение, установка и короткое использование — в [README.md](README.md).

Все примеры получены прогоном `csr` на синтетических файлах: код в блоках «было» — вход, находки и код в блоках «стало» — вывод `csr style`, `csr arch` и `csr fix`.

## Оглавление

- [Сводка](#сводка)
  - [Проверки `style`](#проверки-style-сводная-таблица)
  - [Признаки `arch`](#признаки-arch-сводная-таблица)
- [1. Общее](#1-общее)
  - [Требования к машине](#требования-к-машине)
- [2. Команды](#2-команды)
  - [`csr metrics`](#csr-metrics)
  - [`csr style`](#csr-style)
  - [`csr fix`](#csr-fix)
  - [`csr arch`](#csr-arch)
  - [`csr refs`](#csr-refs)
  - [`csr rename`](#csr-rename)
  - [`csr move`](#csr-move)
  - [`csr extract`](#csr-extract)
  - [`csr serve`](#csr-serve)
  - [`csr similar`](#csr-similar)
  - [`csr structure`](#csr-structure)
- [3. Конфиги](#3-конфиги)
- [4. Проверки `style`](#4-проверки-style)
  - Форматирование: [`line-length`](#line-length), [`directive-column`](#directive-column), [`binary-op-wrap`](#binary-op-wrap)
  - Комментарии и тексты: [`xml-doc`](#xml-doc), [`comment-separator`](#comment-separator), [`comment-typography`](#comment-typography), [`comment-ending`](#comment-ending), [`commented-code`](#commented-code), [`todo-format`](#todo-format), [`log-call-wrap`](#log-call-wrap), [`message-text`](#message-text)
  - Идиомы и типы: [`null-check`](#null-check), [`coalesce-assignment`](#coalesce-assignment), [`out-var`](#out-var), [`bool-local-type`](#bool-local-type), [`local-type`](#local-type), [`tuple-type`](#tuple-type)
  - Модификаторы: [`explicit-private`](#explicit-private), [`forbidden-modifier`](#forbidden-modifier), [`custom-delegate`](#custom-delegate)
  - Раскладка кода столбиком: [`expression-arrow`](#expression-arrow), [`signature-wrap`](#signature-wrap), [`call-wrap`](#call-wrap), [`region-layout`](#region-layout), [`condition-wrap`](#condition-wrap), [`initializer-layout`](#initializer-layout), [`ternary-wrap`](#ternary-wrap)
  - Именование: [`naming`](#naming), [старые проверки](#старые-проверки-именования-до-v04)
  - Запрещённые конструкции: [`forbidden`](#forbidden), [`coroutine`](#coroutine), [`static-state`](#static-state), [`local-function`](#local-function), [`path-concat`](#path-concat), [`bool-field-count`](#bool-field-count), [`type-per-file`](#type-per-file), [`closure-in-method`](#closure-in-method), [`nested-loop`](#nested-loop)
- [5. Признаки `arch`](#5-признаки-arch)
  - По одному файлу: [`type-branch`](#type-branch), [`behaviour-logic`](#behaviour-logic), [`index-api`](#index-api), [`static-data-methods`](#static-data-methods), [`project-mechanism`](#project-mechanism), [`numeric-constant`](#numeric-constant), [`numeric-literal`](#numeric-literal), [`tuple-type`](#tuple-type-arch), [`partial-file`](#partial-file), [`debug-draw`](#debug-draw), [`obsolete-reason`](#obsolete-reason), [`persisted-class`](#persisted-class)
  - По решению: [`variant-flag`](#variant-flag), [`single-implementation`](#single-implementation), [`dead-member`](#dead-member), [`parameter-set`](#parameter-set), [`unraised-event`](#unraised-event), [`class-cohesion`](#class-cohesion)
- [6. Метрики `metrics`](#6-метрики-metrics)

## Сводка

### Проверки `style` (сводная таблица)

Колонка «Замена»: **да** — у каждой находки есть замена для `csr fix`; **часть** — у части находок или только при условии (см. подраздел); **нет** — находка без замены, правит агент. Параметры — только собственные; `severity`, `rule`, `enabled` есть у любой проверки.

| Проверка | Что делает | Замена | Параметры |
|---|---|---|---|
| [`line-length`](#line-length) | строка длиннее порога | нет | `max`, `ignoreComments`, `ignoreStringLines` |
| [`directive-column`](#directive-column) | директива препроцессора не с колонки 0 | да | `directives` |
| [`binary-op-wrap`](#binary-op-wrap) | место `&&` / `||` при переносе | да | `prefer`, `operators` |
| [`xml-doc`](#xml-doc) | документационный комментарий `///` | часть | `explanation`, `max` |
| [`comment-separator`](#comment-separator) | комментарий-разделитель `// ---` | нет | `minRun` |
| [`comment-typography`](#comment-typography) | типографские знаки вместо ASCII | да | `replace`, `attributes`, `logs`, `toString` |
| [`comment-ending`](#comment-ending) | точка в конце комментария, заменяемые буквы | да | `prefer`, `letters`, `standaloneOnly` |
| [`commented-code`](#commented-code) | закомментированный код | часть | — |
| [`todo-format`](#todo-format) | формат TODO | часть | `forbidMetadata`, `requireCyrillic` |
| [`log-call-wrap`](#log-call-wrap) | вызов лога не в одну строку или длиннее порога | нет | `maxLength` |
| [`message-text`](#message-text) | текст лога или исключения | часть | `concatenation`, `trailingPeriod`, `exceptions` |
| [`null-check`](#null-check) | форма проверки на null | да | `prefer` |
| [`coalesce-assignment`](#coalesce-assignment) | `if (x == null) x = …` вместо `??=` | да | — |
| [`out-var`](#out-var) | тип переменной в `out`-аргументе | часть | `prefer`, `discardUnused` |
| [`bool-local-type`](#bool-local-type) | `var` или `bool` у локальной с булевым инициализатором | да | `prefer` |
| [`local-type`](#local-type) | `var` или явный тип у локальной и `foreach` | часть | `prefer`, `foreach`, `foreachExplicitTypes`, `delegateTypes` |
| [`tuple-type`](#tuple-type) | кортеж в сигнатуре или поле | нет | — |
| [`explicit-private`](#explicit-private) | явный `private` у членов | да | `prefer`, `members` |
| [`forbidden-modifier`](#forbidden-modifier) | запрещённые модификаторы | нет | `modifiers` |
| [`custom-delegate`](#custom-delegate) | свой `delegate` вместо `Action` / `Func` | нет | — |
| [`expression-arrow`](#expression-arrow) | место `=>` у члена с телом-выражением | да | `properties`, `methods`, `max` |
| [`signature-wrap`](#signature-wrap) | сигнатура столбиком | да | `max`, `minParameters`, `initializerOnOwnLine` |
| [`call-wrap`](#call-wrap) | длинный вызов столбиком | часть | `max` |
| [`region-layout`](#region-layout) | пустые строки вокруг регионов | да | `inside`, `between`, `outside` |
| [`condition-wrap`](#condition-wrap) | перенос логического условия | да | `max`, `argumentIndent`, `continuationIndent`, `assignmentIndent` |
| [`initializer-layout`](#initializer-layout) | инициализатор столбиком | да | `max`, `trailingComma` |
| [`ternary-wrap`](#ternary-wrap) | тернарный оператор столбиком | часть | `max`, `nested` |
| `field-prefix` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `instancePrefix`, `staticPrefix`, `staticReadonly`, `constants`, `prefixedAccess`, `pascalAfterPrefix`, `properties` |
| `bool-name` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `members`, `forbiddenWords`, `negationPrefixes`, `ignoreWords` |
| [`naming`](#naming) | имена по правилам конфига: символы → требование → уровень и метка | нет | `rules` |
| `event-name` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `allowed`, `forbidden`, `delegatePrefixes` |
| `type-name` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `interfacePrefix`, `typeParameterPrefix`, `derivedNames` |
| `namespace-name` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `roots`, `forbiddenWords`, `maxDepth` |
| `enum-plural` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `pluralWords`, `singularEndings` |
| `collection-name` | устарела до v0.4: [`naming`](#старые-проверки-именования-до-v04) | нет | `members`, `collectionTypes`, `arrays`, `pluralWords`, `singularEndings` |
| [`forbidden`](#forbidden) | запрещённые вызовы, члены и типы по записям: вид, маска, альтернатива | нет | `entries` |
| `forbidden-call` | устарела до v0.4: [`forbidden`](#forbidden) | нет | `calls`, `excludePaths` |
| `forbidden-type` | устарела до v0.4: [`forbidden`](#forbidden) | нет | `types`, `excludePaths` |
| [`coroutine`](#coroutine) | метод-корутина | нет | `returnTypes`, `requireYield`, `use`, `excludePaths` |
| [`static-state`](#static-state) | статическое изменяемое состояние или синглтон | нет | `mutableTypes`, `singleton`, `ignoreAttributes`, `ignoreNames`, `use`, `excludePaths` |
| [`local-function`](#local-function) | локальная функция | нет | `use`, `excludePaths` |
| [`path-concat`](#path-concat) | склейка пути сложением строк | часть | `separators`, `replace`, `excludePaths` |
| [`bool-field-count`](#bool-field-count) | много полей `bool` в классе | нет | `baseTypes`, `minFields`, `excludePaths` |
| [`type-per-file`](#type-per-file) | несколько типов в файле | нет | `baseTypes`, `excludePaths` |
| [`closure-in-method`](#closure-in-method) | лямбда с захватом в часто вызываемом методе | нет | `methods`, `captureThis`, `excludePaths` |
| [`nested-loop`](#nested-loop) | вложенный цикл по независимой коллекции | нет | `sizeMembers`, `ignoreCollections`, `excludePaths` |

### Признаки `arch` (сводная таблица)

Признак — не ошибка, а повод решить: у каждого есть вопрос-решение. «Область»: **файл** — по синтаксису одного файла; **решение** — только `csr arch --solution`. У любого признака есть `paths` и `excludePaths`, а также `severity`, `rule`, `enabled`. Слова имени (подсказки `numeric-constant`, имена `index-api`, `debug-draw`, `persisted-class`) делятся как у [`naming`](#naming): части через `_` и границы регистра, цифры — часть слова (`Vector3Pool` — `Vector3`, `Pool`), префикс поля (одна-две строчные буквы и `_` или `_` в начале: `m_`, `k_`, `_`) отбрасывается.

| Признак | Что находит | Область | Параметры |
|---|---|---|---|
| [`type-branch`](#type-branch) | ветвление по конкретному типу | файл | `ignoreInterfaces`, `ignoreTypes`, `asCast`, `ignoreMethods` |
| [`behaviour-logic`](#behaviour-logic) | логика в компоненте сцены | файл | `baseTypes`, `messages`, `ignoreAttributes`, `maxMethods`, `lookupMethods` |
| [`index-api`](#index-api) | API на числовых индексах | файл | `names`, `indexWords`, `minOutInts`, `notFoundMarker` |
| [`static-data-methods`](#static-data-methods) | статические методы с данными первым параметром | файл | `minMethods`, `minShare`, `minDataShare`, `staticClassesOnly`, `ignoreTypes` |
| [`project-mechanism`](#project-mechanism) | повтор механизма проекта | файл | — |
| [`numeric-constant`](#numeric-constant) | поведенческое число константой | файл | `hints`, `staticReadonly` |
| [`numeric-literal`](#numeric-literal) | поведенческое число литералом | файл | `neutral`, `integers`, `ignoreMethods` |
| [`tuple-type`](#tuple-type-arch) | кортеж в сигнатуре или поле | файл | — |
| [`partial-file`](#partial-file) | partial-файл с подсистемой | файл | `aspects`, `minFields`, `minMethods` |
| [`debug-draw`](#debug-draw) | отладочная отрисовка мимо механизма проекта | файл | `drawMethods`, `durationArgument`, `debugMethods`, `flagNames`, `settingsTypes` |
| [`obsolete-reason`](#obsolete-reason) | [Obsolete] без причины | файл | — |
| [`persisted-class`](#persisted-class) | сохраняемый класс без пары конструкторов или с несохраняемым состоянием в сохранении | файл | `saveAttributes`, `ignoreAttributes`, `constructorAttributes`, `cacheNames` |
| [`variant-flag`](#variant-flag) | bool-флаг варианта в общем коде | решение | `minFiles`, `minUses`, `names`, `ignoreNames` |
| [`single-implementation`](#single-implementation) | интерфейс с одной реализацией | решение | `contractMasks`, `ignoreAttributes`, `ignoreMarkers` |
| [`dead-member`](#dead-member) | мертвый код | решение | `accessibility`, `writeOnly`, `parameters`, `ignoreNames`, `dataAttributes`, `ignoreAttributes` |
| [`parameter-set`](#parameter-set) | повторяющийся набор параметров | решение | `minParameters`, `minCalls`, `minForwarded` |
| [`unraised-event`](#unraised-event) | событие без отправителя | решение | `eventTypes`, `raiseMethods`, `fieldEvents` |
| [`class-cohesion`](#class-cohesion) | класс из несвязанных частей | решение | `minMethods`, `minPartSize`, `ignoreNames` |

## 1. Общее

- Вызов: `csr <команда> [аргументы] [опции]`; `csr help [команда]` — справка. Команды `metrics`, `style`, `fix`, `similar` и `arch` без `--solution` читают файлы и разбирают их по синтаксису, решение не загружается. Команды `refs`, `rename`, `move`, `extract`, `serve` и `arch --solution` загружают решение Roslyn (`MSBuildWorkspace`) и пользуются семантикой.
- Файлы и папки: в аргументах — `.cs` файлы и папки; папки обходятся рекурсивно, `bin`, `obj` и скрытые папки (имя начинается с точки) пропускаются. Нет такого файла или папки — код возврата 2.
- Пути в отчётах — относительно текущей папки, если файл внутри неё, иначе полные; разделитель `/`. Маски `paths` и `excludePaths` признаков `arch` применяются к пути в том же виде.
- Форматы вывода: `json` (по умолчанию, для агентов), `md` (для человека), `diff` (`fix`, `rename`, `move`, `extract`; унифицированный, как у git: 3 строки контекста, у последней строки без перевода — `\ No newline at end of file`), `text` (`refs`). `--out файл` пишет отчёт в файл вместо stdout. Сводка и служебные строки идут в stderr, отчёт — в stdout.
- Коды возврата: `0` — успех (для `style`, `fix`, `arch` — нет находок уровня `error`); `1` — есть находки уровня `error` (`style`, `fix`, `arch`), отказ или новые ошибки компиляции (`rename`), сервер не запущен (`serve --status`, `--stop`); `2` — ошибка запуска (аргументы, конфиг, нет файла, символ не найден).
- Режимы выборки находок: `file` — все находки файла; `changed-lines` — только на изменённых строках (`--diff`, `--scope`).
- Конфиг нужен проверкам: проверки движка нейтральны, что считать нарушением, задаёт конфиг (раздел [3](#3-конфиги)). Без `--config` `csr style`, `csr fix` и `csr arch` ищут конфиги сами ([поиск конфигов](#где-искать-конфиги)); нет ни одного — код 2.

### Требования к машине

Установка — [README.md](README.md#установка). `csr.exe` — self-contained (.NET внутри): для команд по файлам (`metrics`, `style`, `fix`, `arch` без `--solution`) на машине ничего не нужно. Команды по решению загружают проекты через `MSBuildWorkspace`: MSBuild работает в отдельном процессе BuildHost из папок рядом с `csr.exe`. Вид процесса зависит от формата проекта:

| Проект | BuildHost | Что нужно на машине |
|---|---|---|
| SDK-стиль: `<Project Sdk="Microsoft.NET.Sdk">` | `BuildHost-netcore`, запускается через `dotnet` | .NET SDK, которым собирается проект (с учётом `global.json`), `dotnet` в `PATH` |
| старый формат: `<Project ToolsVersion="4.0">` (Unity) | `BuildHost-net472` | .NET Framework 4.7.2+ и MSBuild: Visual Studio 2017+ (любая редакция) или Build Tools for Visual Studio с компонентом MSBuild |

- MSBuild для старого формата ищется через `Microsoft.Build.Locator`: экземпляры Visual Studio и Build Tools, зарегистрированные установщиком Visual Studio, и окружение Developer Command Prompt (`VSINSTALLDIR`, `VSCMD_VER`). Из найденных берётся экземпляр с наибольшей версией.
- Rider: свой MSBuild (`<Rider>\tools\MSBuild`) в установщике Visual Studio не регистрирует, и без Visual Studio или Build Tools MSBuild не находится. Обход — окружение Developer Command Prompt, указывающее на Rider (проверено на Rider 2026.1: старый `.csproj` загружен MSBuild из Rider):

  ```
  $env:VSINSTALLDIR = 'C:\Program Files\JetBrains\JetBrains Rider 2026.1.1\tools\'
  $env:VSCMD_VER = '17.0'
  csr refs Game.Player.Move
  ```

  `VSCMD_VER` — версия этого экземпляра; при установленной Visual Studio с большей версией берётся Visual Studio.
- Проекты Unity ссылаются на сборки редактора по полным путям (`<HintPath>` в папке установки Unity): нужна версия редактора, из которой сгенерированы `.csproj`, иначе ссылки на `UnityEngine` не разрешаются и семантика неполная.
- Нет нужного MSBuild или `dotnet` — код 2 и текст: причина от BuildHost и что поставить для видов проектов решения:

  ```
  csr: загрузка решения: MSBuild недоступен (An error occurred trying to start process 'dotnet.exe' ...)
    проекты SDK-стиля: нужен .NET SDK, которым собирается проект, и dotnet в PATH
  ```

  Для старого формата — строки про Visual Studio / Build Tools и про обход для Rider. `csr serve` при перезагрузке решения отвечает тем же текстом.

## 2. Команды

### `csr metrics`

Метрики по синтаксису каждого файла. Файлы только читаются.

```
csr metrics <файлы | папки>... [--config metrics.jsonc] [--project project.jsonc] [--format json | md]
            [--top N] [--all] [--coverage opencover.xml]... [--edn файл] [--out файл]
```

| Опция | Назначение |
|---|---|
| `--config metrics.jsonc` | пороги, включение метрик, размер топа ([раздел 3](#metricsjsonc)); без `--config` — [поиск](#где-искать-конфиги) |
| `--project project.jsonc` | символы препроцессора (по умолчанию `UNITY_EDITOR`); без `--project` — [поиск](#где-искать-конфиги) |
| `--format json \| md` | формат отчёта (по умолчанию `json`) |
| `--top N` | размер топа по каждой метрике (по умолчанию 10); `0` — все |
| `--all` | в JSON — все функции и типы по файлам (ключ `items`) |
| `--coverage opencover.xml` | покрытие OpenCover XML: покрытие и CRAP функций, топ по CRAP; несколько файлов объединяются |
| `--edn файл` | снимок `.metrics/crap.edn` в формате crapper (без `--coverage` — `:coverage nil`) |
| `--out файл` | записать отчёт в файл |

Что считается и какие пороги — в разделе [6](#6-метрики-metrics). Отчёт содержит сводку по каждой метрике (перцентили p50, p90, p99, max; число значений выше порога) и топ. Код возврата: `0` — отчёт построен, `2` — ошибка запуска.

Пример (`csr metrics Sorter.cs --format md --top 2`, сокращено до сводки):

````text
# Метрики

Файлов: 1, типов: 1, функций: 2.

## Сводка

| Уровень | Метрика | p50 | p90 | p99 | max | Порог | Выше порога |
|---|---|---:|---:|---:|---:|---:|---:|
| Функция | Цикломатическая сложность | 5 | 5 | 5 | 5 | 10 | 0 |
| Функция | Когнитивная сложность | 9 | 9 | 9 | 9 | 15 | 0 |
| Функция | Строк | 18 | 18 | 18 | 18 | 60 | 0 |
| Функция | Строк кода | 17 | 17 | 17 | 17 | — | — |
| Функция | Вложенность | 3 | 3 | 3 | 3 | 3 | 0 |
| Функция | Параметров | 2 | 2 | 2 | 2 | 5 | 0 |
| Тип | Строк | 25 | 25 | 25 | 25 | — | — |
| Тип | Строк кода | 22 | 22 | 22 | 22 | — | — |
| Тип | Членов | 3 | 3 | 3 | 3 | — | — |
| Тип | Регионов | 0 | 0 | 0 | 0 | — | — |
| Файл | Строк | 26 | 26 | 26 | 26 | 400 | 0 |
| Файл | Строк кода | 22 | 22 | 22 | 22 | 316 | 0 |
| Файл | Типов | 1 | 1 | 1 | 1 | — | — |

...
````

Фрагмент JSON (`csr metrics Sorter.cs --top 1`, первая метрика; `...` — пропущено):

```text
{
  "files": 1,
  "types": 1,
  "functions": 2,
  "metrics": [
    {
      "id": "function.cyclomatic",
      "title": "Цикломатическая сложность",
      "count": 2,
      "p50": 5,
      "p90": 5,
      "p99": 5,
      "max": 5,
      "threshold": 10,
      "aboveThreshold": 0,
      "top": [
        {
          "value": 5,
          "name": "Sorter.Sort",
          "kind": "method",
          "namespace": "",
          "file": "Sorter.cs",
          "line": 5,
          "endLine": 22
        }
      ]
    }
    ...
  ]
  ...
}
```

### `csr style`

Находки стиля по синтаксису каждого файла. Файлы только читаются.

```
csr style <файлы | папки>... [--config style.jsonc]... [--project project.jsonc]
          [--diff <ревизия | файл.diff>] [--scope file | changed-lines] [--format json | md] [--out файл]
```

| Опция | Назначение |
|---|---|
| `--config style.jsonc` | проверки, уровни, метки правил, параметры; несколько `--config` накладываются по порядку ([слои](#слои-конфигов)); без `--config` — [поиск конфигов](#где-искать-конфиги) |
| `--project project.jsonc` | символы препроцессора, методы логгера (нужны проверкам текстов логов); без `--project` — [поиск](#где-искать-конфиги) |
| `--diff <ревизия \| файл.diff>` | изменённые строки: `git diff -U0` от ревизии или готовый файл диффа (с контекстом или без: изменены строки `+`) |
| `--scope file \| changed-lines` | все находки файла или только на изменённых строках |
| `--format json \| md` | формат отчёта (по умолчанию `json`) |
| `--out файл` | записать отчёт в файл |

**Режимы выборки.** Без `--diff` режим — `file`. С `--diff` режим по умолчанию — `changed-lines`: остаются находки на изменённых строках. `--scope changed-lines` без `--diff` берёт дифф от `HEAD`. `--diff` — ревизия (`git diff -U0` от неё в репозитории git; неотслеживаемые файлы входят целиком) или путь к готовому файлу диффа. Вне репозитория git ревизия — ошибка запуска.

**Вывод JSON** — `files`, `scope`, `summary[]` (`check`, `rule`, `severity`, `count`) и `findings[]` (`file`, `line`, `endLine`, `column`, `check`, `rule`, `severity`, `message`, `replacement`). `replacement` — новый текст строк `line..endLine`; пустой список — удалить строки; ключа нет — замены нет. Пример (`csr style Demo.cs --config config.jsonc`, проверка `explicit-private` с меткой `S23`):

```json
{
  "files": 1,
  "scope": "file",
  "summary": [
    {
      "check": "explicit-private",
      "rule": "S23",
      "severity": "warning",
      "count": 2
    }
  ],
  "findings": [
    {
      "file": "Demo.cs",
      "line": 3,
      "endLine": 3,
      "column": 5,
      "check": "explicit-private",
      "rule": "S23",
      "severity": "warning",
      "message": "нет явного 'private'",
      "replacement": [
        "    private int m_Count;"
      ]
    },
    {
      "file": "Demo.cs",
      "line": 5,
      "endLine": 5,
      "column": 5,
      "check": "explicit-private",
      "rule": "S23",
      "severity": "warning",
      "message": "нет явного 'private'",
      "replacement": [
        "    private void Reset()"
      ]
    }
  ]
}
```

**Вывод Markdown** (`--format md`): сводка по проверкам и список находок по файлам с кодом строки после замены, если замена есть. Пример: репозиторий git, файл `Demo.cs` с классом `Report` закоммичен, класс `Other` добавлен после коммита; конфиг `cfg.jsonc`:

```jsonc
{
  "checks": {
    "explicit-private": { "severity": "warning", "rule": "S23" },
    "null-check": { "severity": "warning", "prefer": "equality" }
  }
}
```

```csharp
public class Report
{
    int m_Count;

    void Reset()
    {
        m_Count = 0;
    }
}

public class Other
{
    int m_Value;

    public bool Has(object item)
    {
        return item is null;
    }
}
```

`csr style Demo.cs --config cfg.jsonc --format md`:

````markdown
# Стиль

Файлов: 1, находок: 4, режим: file.

| Проверка | Правило | Уровень | Находок |
|---|---|---|---:|
| `explicit-private` | S23 | warning | 3 |
| `null-check` |  | warning | 1 |

## `Demo.cs`

- 3: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private int m_Count;
  ```
- 5: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private void Reset()
  ```
- 13: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private int m_Value;
  ```
- 17: `null-check` (warning) - 'null' -> '== null'
  ```csharp
          return item == null;
  ```
````

Режим `changed-lines` (`csr style Demo.cs --config cfg.jsonc --diff HEAD --format md`) на том же файле — только находки на добавленных строках (старые находки в строках 3 и 5 отброшены):

````markdown
# Стиль

Файлов: 1, находок: 2, режим: changed-lines.

| Проверка | Правило | Уровень | Находок |
|---|---|---|---:|
| `explicit-private` | S23 | warning | 1 |
| `null-check` |  | warning | 1 |

## `Demo.cs`

- 13: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private int m_Value;
  ```
- 17: `null-check` (warning) - 'null' -> '== null'
  ```csharp
          return item == null;
  ```
````

Коды возврата: `0` — нет находок уровня `error`, `1` — есть, `2` — ошибка запуска.

### `csr fix`

Применяет замены находок `csr style` (`replacement`) к файлам. Агент получает исправленный код и правит только то, что требует суждения.

```
csr fix <файлы | папки>... [--config style.jsonc]... [--project project.jsonc]
        [--diff <ревизия | файл.diff>] [--scope file | changed-lines] [--checks a,b] [--hints]
        [--apply] [--format diff | json] [--out файл]
```

| Опция | Назначение |
|---|---|
| `--config`, `--project`, `--diff`, `--scope` | как у `csr style` |
| `--checks a,b` | только перечисленные проверки |
| `--hints` | и замены уровня `hint` (по умолчанию применяются только `error` и `warning`) |
| `--apply` | записать файлы; без него файлы не меняются |
| `--format diff \| json` | `diff` (по умолчанию) — унифицированный дифф в stdout, сводка в stderr; `json` — отчёт в stdout |
| `--out файл` | записать дифф или `json` в файл вместо stdout |

- Проверки запускаются заново, замены применяются проходами: за проход — все, что не пересекаются по строкам, затем файл разбирается снова (несколько находок на одной строке — по очереди). До устойчивого текста, не больше 8 проходов.
- Замены уровня `hint` по умолчанию не применяются: `coalesce-assignment` путает объекты Unity, `commented-code` удаляет строки — это суждение, а не механика. Нужны — `--hints`.
- `--scope changed-lines` — только находки на изменённых строках; строки, сдвинутые заменами выше, отслеживаются.
- Кодировка, BOM и переводы строк файла сохраняются; пути в диффе — как в отчёте `style`.
- Если после замен в файле больше синтаксических ошибок, чем было, файл не меняется и попадает в список `reverted`: это ошибка замены проверки.
- Коды возврата: `0` — после исправлений нет находок уровня `error`, `1` — есть, `2` — ошибка запуска.

Вывод (`csr fix Demo.cs --config cfg.jsonc`, без `--apply`; stdout — дифф, stderr — сводка):

````diff
--- a/Demo.cs
+++ b/Demo.cs
@@ -1,8 +1,8 @@
 public class Report
 {
-    int m_Count;
+    private int m_Count;
 
-    void Reset()
+    private void Reset()
     {
         m_Count = 0;
     }
@@ -10,10 +10,10 @@
 
 public class Other
 {
-    int m_Value;
+    private int m_Value;
 
     public bool Has(object item)
     {
-        return item is null;
+        return item == null;
     }
 }
````

````text
csr fix: исправлено 4 в 1 файлах, без записи (--apply - записать); осталось находок 0 (error 0), 0,2 с
  explicit-private (S23): 3
  null-check (-): 1
````

В JSON (`--format json`): `applied` (записаны ли файлы), `fixed[]` (`check`, `rule`, `count`), `files[]` (изменённые файлы), `diff` (текст диффа), `reverted[]` (откаченные файлы) и `remaining` — отчёт `csr style` по исправленному тексту: находки, которые остаются агенту. Пример (`csr fix Demo.cs --config cfg.jsonc --diff HEAD --format json`):

```json
{
  "applied": false,
  "fixed": [
    {
      "check": "explicit-private",
      "rule": "S23",
      "count": 1
    },
    {
      "check": "null-check",
      "count": 1
    }
  ],
  "files": [
    "Demo.cs"
  ],
  "diff": "--- a/Demo.cs\n+++ b/Demo.cs\n@@ -10,10 +10,10 @@\n \n public class Other\n {\n-    int m_Value;\n+    private int m_Value;\n \n     public bool Has(object item)\n     {\n-        return item is null;\n+        return item == null;\n     }\n }\n",
  "reverted": [],
  "remaining": {
  "files": 1,
  "scope": "changed-lines",
  "summary": [],
  "findings": []
}
}
```

### `csr arch`

Архитектурные признаки: находка — повод решить, а не ошибка. Режим и конфиг отдельные от `style`; формат конфига тот же ([раздел 3](#3-конфиги)).

```
csr arch <файлы | папки>... [--config arch.jsonc]... [--project project.jsonc]
         [--diff <ревизия | файл.diff>] [--scope file | changed-lines] [--format json | md] [--out файл]
csr arch --solution <файл .sln | .slnx | .csproj> [--projects <маска>]... [--config arch.jsonc]...
         [--project project.jsonc] [--format json | md] [--out файл] [--no-server]
```

- Опции `--config`, `--project`, `--diff`, `--scope`, `--format`, `--out` — как у `csr style`; без `--config` и `--project` — тот же [поиск конфигов](#где-искать-конфиги) (`arch.jsonc`; в режиме решения поиск идёт от папки решения). `--project` дополнительно задаёт механизмы проекта для признака [`project-mechanism`](#project-mechanism).
- **Режим файлов** (аргументы — файлы и папки): признаки по синтаксису одного файла, решение не загружается.
- **Режим решения** (`--solution`): решение загружается слоем `CSR.Workspace` (или берётся у живого [`csr serve`](#csr-serve)); признаки по одному файлу идут по документам выбранных проектов, признаки по решению ([`variant-flag`](#variant-flag), [`single-implementation`](#single-implementation), [`dead-member`](#dead-member), [`parameter-set`](#parameter-set), [`unraised-event`](#unraised-event), [`class-cohesion`](#class-cohesion)) — по их компиляциям. `--projects` — маски имён проектов (`*` — любые символы; несколько объединяются; без `--projects` — все проекты). В отчёте `scope` — `solution`. `--no-server` — загрузить решение в процессе, не обращаясь к серверу. Признаки по решению в режиме файлов не запускаются: находок по ним нет.
- Ссылки признаков по решению считаются только внутри выбранных проектов: член, которым пользуется проект вне `--projects`, Lua или рефлексия, попадёт в находки.
- **Вывод JSON:** `files`, `scope`, `summary[]` (`check`, `rule`, `severity`, `count`, `question`) и `findings[]` (`file`, `line`, `endLine`, `column`, `check`, `rule`, `severity`, `message`). Замен у находок нет. `question` — вопрос-решение признака.
- Коды возврата — как у `style`.

Пример режима файлов (`csr arch Demo.cs --config config.jsonc --format md`, признак `type-branch`):

````markdown
# Архитектура

Файлов: 1, находок: 3, режим: file.

| Признак | Правило | Уровень | Находок | Что решить |
|---|---|---|---:|---|
| `type-branch` | A1 | warning | 3 | Может ли тип сам отдать нужное через метод базы или интерфейса, чтобы этот код не знал частный случай? |

## `Demo.cs`

- 19: `type-branch` A1 (warning) - проверка на тип 'Circle': shape is Circle circle
- 22: `type-branch` A1 (warning) - проверка на тип 'Square': shape as Square
- 31: `type-branch` A1 (warning) - switch по типам: Circle, Square
````

Пример режима решения (`csr arch --solution Demo/Demo.csproj --config config.jsonc --format md`; проект из одиннадцати файлов, шесть признаков по решению; stderr — ход загрузки):

````markdown
# Архитектура

Файлов: 11, находок: 12, режим: solution.

| Признак | Правило | Уровень | Находок | Что решить |
|---|---|---|---:|---|
| `dead-member` |  | warning | 6 | Удалить вместе с местами записи, если код в задаче; если вне задачи - записать отдельной задачей |
| `single-implementation` |  | warning | 1 | Интерфейс - точка расширения или граница подсистемы? Если нет - убрать, оставив класс; если да - контракт-комментарий |
| `unraised-event` |  | warning | 2 | Отправитель потерян или событие устарело? Устарело - удалить вместе с обработчиками; потерян - вернуть отправку |
| `class-cohesion` |  | hint | 1 | Части - разные задачи? Если да - вынести часть в свой класс; если нет - оставить |
| `parameter-set` |  | hint | 1 | Собрать набор в структуру аргументов (...Args)? Методы остаются на месте |
| `variant-flag` |  | hint | 1 | Флаг заменяет вариант поведения? Тогда - метод базы или интерфейса, который вариант реализует сам |

## `Demo/Counter.cs`

- 4: `dead-member` (warning) - поле 'Counter.m_Written' только записывается: мест 1
- 5: `dead-member` (warning) - поле 'Counter.m_Unused' не используется
- 7: `dead-member` (warning) - параметр 'scale' метода 'Counter.Add' не читается
- 14: `dead-member` (warning) - метод 'Counter.Clear' не используется

## `Demo/Events.cs`

- 5: `unraised-event` (warning) - событие 'DoorOpened' никто не поднимает; обрабатывают: DoorListener
- 20: `dead-member` (warning) - параметр 'handler' метода 'EventBus.Subscribe' не читается
- 28: `unraised-event` (warning) - событие 'Door.Jammed' нигде не поднимается; подписок: 1

## `Demo/Render.cs`

- 18: `parameter-set` (hint) - метод 'Renderer.Paint' (4 входных параметров): вызовов 3, из них 2 передают параметры вызывающего дальше

## `Demo/Settings.cs`

- 3: `variant-flag` (hint) - флаг 'Settings.FastMode' ветвит код других типов: мест 3 в 3 файлах: UseA.cs, UseB.cs, UseC.cs

## `Demo/Slot.cs`

- 8: `dead-member` (warning) - поле 'Slot.m_Capacity' только записывается: мест 1 (сохраняется: удаление - с миграцией)

## `Demo/Storage.cs`

- 1: `single-implementation` (warning) - интерфейс 'IStorage' - одна реализация: 'FileStorage' (Storage.cs)

## `Demo/Toolbox.cs`

- 1: `class-cohesion` (hint) - 'Toolbox': несвязанных частей 2 (методов 12): 1) Widen, Narrow, Scale, Measure и еще 2 | поля m_Width; 2) Add, Remove, Sum, Count и еще 2 | поля m_Total
````

````text
csr: загрузка C:\work\Demo\Demo.csproj (сервер не запущен; csr serve держит решение загруженным)
csr: решение загружено: проектов 1, документов 14
csr: загрузка 1,8 с
csr arch: проектов 1, файлов 11, находок 12, 1,4 с
````

Фильтр по путям: `paths` и `excludePaths` — маски путей файлов (`*` — любые символы, без учёта регистра), в виде, как путь показан в отчёте. Путь проверяется и с ведущим `/`: маска `Common/*` ловит `Common/Core.cs` в текущей папке, `*/Common/*` — и `src/Common/Core.cs`, и `Common/Core.cs`. То же правило — у `excludePaths` механизмов в `project.jsonc`, у `excludePaths` проверок `style` и записей `forbidden`, у `paths` / `excludePaths` в `similar.jsonc`.

### `csr refs`

Все ссылки на символ по решению (Roslyn `SymbolFinder`) — для агента вместо поиска текстом. Решение загружается через `MSBuildWorkspace`, файлы только читаются.

```
csr refs <символ> [--solution файл] [--format json | text] [--no-server]
```

- Символ: `Namespace.Type.Member`; `Namespace.Type.Method(int, Item)` (перегрузка); `файл.cs:строка[:колонка]` (с 1; без колонки — первое объявление в строке, иначе первый символ, на который ссылается строка). Без параметров в запросе — все перегрузки метода. Неоднозначный символ — ошибка со списком кандидатов в той же записи. Типы и члены из DLL (`UnityEngine.MonoBehaviour`) тоже ищутся.
- Решение: `--solution` (`.sln`, `.slnx`, `.csproj`) или ближайшее `.sln` / `.slnx` вверх от файла символа и текущей папки, затем в её подпапках.
- Ответ — сам символ и связанные: переопределения, реализации интерфейсов, аксессоры (реализации — даже без ссылок). У ссылки — файл (от папки решения), строка, колонка, строка кода, член-контейнер и пометки `generated` (код генератора), `implicit`, `candidate`.
- `--no-server` — загрузить решение в процессе, не искать живой [`csr serve`](#csr-serve).
- Коды возврата: `0` — успех, `2` — ошибка запуска или символ не найден.

Пример (`csr refs Counter.Add --solution Demo/Demo.csproj --format text`; первые строки — служебный вывод в stderr, затем отчёт в stdout; код — в разделе «Признаки `arch`» для `dead-member`):

````text
csr: загрузка C:\work\Demo\Demo.csproj (сервер не запущен; csr serve держит решение загруженным)
csr: решение загружено: проектов 1, документов 14
csr: загрузка 1,4 с
csr refs: ссылок 1 в 1 файлах, 1,5 с
# Counter.Add(int, int) (method): ссылок 1
Counter.cs:7:16: объявление: public int Add(int value, int scale)
Program.cs:6:17: counter.Add(1, 2);
````

JSON (`--format json`, по умолчанию):

```json
{
  "query": "Counter.Add",
  "references": 1,
  "files": 1,
  "definitions": [
    {
      "symbol": "Counter.Add(int, int)",
      "kind": "method",
      "declarations": [
        {
          "file": "Counter.cs",
          "line": 7,
          "column": 16,
          "text": "public int Add(int value, int scale)"
        }
      ],
      "references": [
        {
          "file": "Program.cs",
          "line": 6,
          "column": 17,
          "text": "counter.Add(1, 2);",
          "container": "Program.Main()"
        }
      ]
    }
  ]
}
```

### `csr rename`

Семантическое переименование (Roslyn `Renamer`). По умолчанию — только дифф на stdout (унифицированный, пути `a/` и `b/` от папки решения, применим `git apply`), сводка в stderr; запись файлов — только с `--apply`.

```
csr rename <символ> <новое имя> [--solution файл] [--config refactor.jsonc] [--format diff | json]
           [--apply] [--migrate] [--force] [--no-verify] [--no-strings] [--no-server]
```

| Опция | Назначение |
|---|---|
| `--solution файл` | `.sln`, `.slnx` или `.csproj` (по умолчанию — ближайшее решение вверх по папкам) |
| `--config refactor.jsonc` | атрибуты сериализации проекта, где и что искать в строках ([раздел 3](#refactorjsonc)); без `--config` — `.csr/refactor.jsonc` от папки решения вверх, иначе `config/refactor.jsonc` рядом с `csr.exe` |
| `--format diff \| json` | `diff` (по умолчанию) — дифф в stdout, сводка в stderr; `json` — всё в stdout |
| `--apply` | записать файлы |
| `--migrate` | сохранить старое имя для данных: `[FormerlySerializedAs]` (Unity), имя в `[JsonProperty]` / `[DataMember]` |
| `--force` | с `--apply`: записать при строковых совпадениях и новых ошибках компиляции |
| `--no-verify` | не компилировать затронутые проекты до и после |
| `--no-strings` | не искать строковые совпадения |
| `--no-server` | загрузить решение в процессе |

- **Сериализуемые имена — отказ с объяснением:** поле, которое сериализует Unity (`[SerializeField]`, `[SerializeReference]`, публичное поле экземпляра `[Serializable]`-типа и наследника `UnityEngine.Object`, автосвойство с `[field: SerializeField]`), `[JsonProperty]` и `[DataMember]` без явного имени, атрибуты из конфига; методы-сообщения Unity (`Update`, `OnEnable`…) — отказ всегда.
- **`--migrate`** сохраняет старое имя для данных: `[FormerlySerializedAs("старое")]` (для автосвойства — `[field: FormerlySerializedAs("<Старое>k__BackingField")]`, `using UnityEngine.Serialization` добавляется), `[JsonProperty("старое")]`, `[DataMember(Name = "старое")]`. Поле в объявлении с несколькими переменными (`public int a, b;`) не переносится — отказ.
- **Строковые совпадения** старого имени целым словом — список «проверить вручную», переименование их не меняет: строковые литералы C# (`nameof` не строка, его правит `Renamer`), файлы `.lua`, `.json`, `.yaml`, `.yml`, `.xml`; ассеты Unity (`.prefab`, `.asset`, `.unity`) — только если имя может быть в сериализованных данных: сериализуемое поле, тип `UnityEngine.Object` или `[Serializable]`, публичный метод компонента (`UnityEvent`).
- **Проверка компиляцией** (`--no-verify` — без неё): затронутые проекты до и после; если ошибок стало больше — база «пустая правка» тех же файлов (генераторы кода дают ошибки на любую правку в памяти). Новые ошибки — список и код 1.
- **Замечания:** файл назван по типу (переименовать вручную; для `MonoBehaviour` — вместе с `.meta`), тип `[Serializable]` (имя может быть в `[SerializeReference]`).
- `--apply` не пишет при новых ошибках компиляции и при строковых совпадениях; `--force` — записать всё равно. Кодировка и BOM файлов сохраняются.
- Коды возврата: `0` — дифф готов или записан, `1` — отказ, новые ошибки компиляции или запись не выполнена, `2` — ошибка запуска.

Пример (`csr rename Profile.Level Rank --solution Demo/Demo.csproj`):

````text
--- a/Profile.cs
+++ b/Profile.cs
@@ -6,6 +6,6 @@
     [DataMember]
     public int Score;
 
-    public int Level;
+    public int Rank;
 }
 
--- a/Program.cs
+++ b/Program.cs
@@ -17,8 +17,8 @@
 {
     public static int Read(Profile profile)
     {
-        profile.Level++;
-        return profile.Score + profile.Level;
+        profile.Rank++;
+        return profile.Score + profile.Rank;
     }
 }
 
````

````text
csr rename: Profile.Level -> Rank: файлов 2, без записи (--apply - записать)
  время: переименование 0,5 с, строки 0,0 с (файлов 0)
  компиляция: проектов 1, ошибок до 0, после 0, новых 0 (0,2 с)
````

Отказ для сериализуемого имени (`[DataMember]` без `Name`) и то же переименование с `--migrate`:

````text
csr rename: Profile.Score -> Points: отказ: имя сериализуется; --migrate сохранит старое имя для данных
  сериализация: [DataMember] без Name: при сериализации пишется имя члена; --migrate: [DataMember(Name = "Score")]
````

````diff
--- a/Profile.cs
+++ b/Profile.cs
@@ -3,8 +3,8 @@
 [DataContract]
 public class Profile
 {
-    [DataMember]
-    public int Score;
+    [DataMember(Name = "Score")]
+    public int Points;
 
     public int Level;
 }
--- a/Program.cs
+++ b/Program.cs
@@ -18,7 +18,7 @@
     public static int Read(Profile profile)
     {
         profile.Level++;
-        return profile.Score + profile.Level;
+        return profile.Points + profile.Level;
     }
 }
 
````

### `csr move`

Перенос типа верхнего уровня в свой файл (имя файла — имя типа) или члена в другой тип. По умолчанию — только дифф на stdout (пути от папки решения, применим `git apply`), сводка в stderr; запись файлов — только с `--apply`.

```
csr move <тип> [--to файл.cs] [--solution файл] [--format diff | json] [--apply] [--force] [--no-verify] [--no-server]
csr move <член> --to <тип> [--config refactor.jsonc] [опции как выше]
```

| Опция | Назначение |
|---|---|
| `тип`, `член` | как в [`csr refs`](#csr-refs): `Namespace.Type[.Member]` или `файл.cs:строка[:колонка]` |
| `--to файл.cs` | для типа: файл назначения (от текущей папки); по умолчанию — `<Тип>.cs` в папке исходного файла |
| `--to тип` | для члена: тип-приёмник, `Namespace.Type` (класс или структура в коде решения) |
| `--config refactor.jsonc` | для члена: атрибуты сериализации проекта, как у [`csr rename`](#csr-rename) |
| `--solution файл` | `.sln`, `.slnx` или `.csproj` (по умолчанию — ближайшее решение вверх по папкам) |
| `--format diff \| json` | `diff` (по умолчанию) — дифф в stdout, сводка в stderr; `json` — всё в stdout |
| `--apply` | записать файлы |
| `--force` | с `--apply`: записать при новых ошибках компиляции |
| `--no-verify` | не компилировать затронутые проекты до и после |
| `--no-server` | загрузить решение в процессе |

#### Перенос типа в свой файл

- **Тип не один в файле:** новый файл получает директивы `using` исходного файла, нужные типу (пространства имён его типов, методов расширения, в том числе неявных — `foreach`, `await`, запросы LINQ, инициализаторы коллекций; `using static`, псевдонимы), и те же объявления пространств имён. Из исходного файла убираются строки типа вместе с его комментариями и атрибутами и директивы `using`, которые были нужны только ему; на стыке остаётся не больше одной пустой строки. Текст типа не переформатируется: строки переносятся как есть.
- **Тип один в файле** — файл переносится целиком без правок текста; `.meta` Unity рядом переносится вместе с ним, GUID сохраняется (ссылки ассетов на скрипт не теряются). В диффе — заголовки переноса git (`rename from` / `rename to`).
- **Отказ с причиной:** вложенный тип (перенос меняет его имя и доступ), `partial`-тип в нескольких объявлениях, тип из метаданных или из сгенерированного кода, файл назначения уже есть, файл назначения вне папки проекта (проект его не увидит), тип уже в файле со своим именем. Символ — не тип: код 2.
- **Замечания:** проект перечисляет файлы явно (`<Compile Include>`, старые `.csproj` Unity) — Unity пересоздаст `.csproj`, иначе добавить файл вручную; наследник `UnityEngine.Object` в новом файле — `.meta` создаст Unity при импорте.
- **Проверка компиляцией** — как у [`csr rename`](#csr-rename); `--apply` не пишет при новых ошибках, `--force` — записать всё равно. Кодировка и BOM исходного файла сохраняются, новый файл — в той же кодировке.
- Коды возврата: `0` — дифф готов или записан, `1` — отказ, новые ошибки компиляции или запись не выполнена, `2` — ошибка запуска.

Пример. Файл `Shapes.cs`:

```csharp
using System.Text;

namespace Demo
{
    public class Circle
    {
        public double Radius;

        public double Area
            => System.Math.PI * Radius * Radius;
    }

    // Подписи фигур для отчета
    public static class Labels
    {
        public static string Describe(Circle circle)
            => new StringBuilder("circle ").Append(circle.Radius).ToString();
    }
}
```

`csr move Demo.Labels --solution Demo/Demo.csproj`: `using System.Text` нужен только `Labels` — уходит вместе с ним:

````diff
--- /dev/null
+++ b/Labels.cs
@@ -0,0 +1,11 @@
+using System.Text;
+
+namespace Demo
+{
+    // Подписи фигур для отчета
+    public static class Labels
+    {
+        public static string Describe(Circle circle)
+            => new StringBuilder("circle ").Append(circle.Radius).ToString();
+    }
+}
--- a/Shapes.cs
+++ b/Shapes.cs
@@ -1,5 +1,3 @@
-using System.Text;
-
 namespace Demo
 {
     public class Circle
@@ -9,12 +7,5 @@
         public double Area
             => System.Math.PI * Radius * Radius;
     }
-
-    // Подписи фигур для отчета
-    public static class Labels
-    {
-        public static string Describe(Circle circle)
-            => new StringBuilder("circle ").Append(circle.Radius).ToString();
-    }
 }
 
````

````text
csr move: Demo.Labels -> Labels.cs: файлов 2, без записи (--apply - записать)
  компиляция: проектов 1, ошибок до 0, после 0, новых 0 (0,2 с)
````

Тип один в файле `Figures.cs` с `.meta` рядом (`csr move Demo.Square --solution Demo/Demo.csproj`):

````text
diff --git a/Figures.cs b/Square.cs
similarity index 100%
rename from Figures.cs
rename to Square.cs
diff --git a/Figures.cs.meta b/Square.cs.meta
similarity index 100%
rename from Figures.cs.meta
rename to Square.cs.meta
````

````text
csr move: Demo.Square -> Square.cs: файлов 2, без записи (--apply - записать)
  замечание: Unity: .meta переносится вместе с файлом, GUID сохраняется
  замечание: в файле нет других типов: файл переносится целиком
  компиляция: проектов 1, ошибок до 0, после 0, новых 0 (0,4 с)
````

Отказ (`csr move Demo.Circle --to Demo/Figures.cs`):

````text
csr move: Demo.Circle -> Figures.cs: отказ: файл Figures.cs уже есть
````

#### Перенос члена в другой тип

- **Статический член** (метод, свойство, поле, константа): ссылки получают квалификатор приёмника (`Pricing.Rate()` → `Inventory.Rate()`, `Rate()` в исходном типе → `Inventory.Rate()`); имя приёмника — кратчайшее в месте ссылки, с пространством имён, если оно не подключено. В теле члена ссылки на статические члены и вложенные типы исходного типа получают его квалификатор.
- **Метод экземпляра** — только с единственным параметром типа-приёмника: приёмник есть в каждом месте вызова (аргумент). Вызов `M(a, p)` или `x.M(a, p)` становится `p.M(a)` (сложный аргумент — в скобках); в теле `p.X` → `X`, `p` → `this`; параметр убирается из сигнатуры.
- Строки члена с его комментариями и атрибутами переносятся как есть, отступ — как у членов приёмника (директивы `#` не сдвигаются); член ставится в конец приёмника. Перевод строки вставленного текста — перевод соседней строки, иначе первый в файле, иначе `LF`; так же у `extract` и `rename --migrate`. Директивы `using` исходного файла, нужные члену, добавляются в файл приёмника; ставшие лишними в исходном файле убираются.
- **Отказ с причиной:** поле экземпляра (данные объекта), свойство экземпляра, сериализуемые данные (как в [`csr rename`](#csr-rename): `[SerializeField]`, `[JsonProperty]`, атрибуты из конфига) — перенос между типами потерял бы их; метод экземпляра без единственного параметра типа-приёмника; метод экземпляра используется не только вызовом (группа методов, делегат); член использует экземпляр исходного типа (`this`, `base`, члены экземпляра без квалификатора) или его `private`-члены; `private`-член используется вне себя; `override`, `virtual`, `abstract`, реализация интерфейса; `partial`-член; поле в объявлении с несколькими переменными; директивы препроцессора в члене; в приёмнике уже есть член с этим именем; исходный тип или приёмник обобщённый; приёмник — не класс и не структура; член, приёмник или ссылка на член — в сгенерированном коде (генератор пересоздаст код со старым типом).

Пример. Файл `Geometry.cs` (`Circle` — в `Shapes.cs` из примера выше):

```csharp
namespace Demo
{
    public static class Geometry
    {
        public const double Tau = 6.283185307179586;

        public static double Circumference(Circle circle)
            => Tau * circle.Radius;
    }

    public class Painter
    {
        public string Outline(Circle circle, string color)
        {
            var length = Geometry.Circumference(circle);
            return $"{color}: {length:F1}, r={circle.Radius}";
        }

        public string Draw(Circle circle)
            => Outline(circle, "black");
    }
}
```

Статический метод (`csr move Demo.Geometry.Circumference --to Demo.Circle --solution Demo/Demo.csproj`): вызов получает квалификатор `Circle`, в теле `Tau` — квалификатор `Geometry`:

````diff
--- a/Geometry.cs
+++ b/Geometry.cs
@@ -3,16 +3,13 @@
     public static class Geometry
     {
         public const double Tau = 6.283185307179586;
-
-        public static double Circumference(Circle circle)
-            => Tau * circle.Radius;
     }
 
     public class Painter
     {
         public string Outline(Circle circle, string color)
         {
-            var length = Geometry.Circumference(circle);
+            var length = Circle.Circumference(circle);
             return $"{color}: {length:F1}, r={circle.Radius}";
         }
 
--- a/Shapes.cs
+++ b/Shapes.cs
@@ -8,6 +8,9 @@
 
         public double Area
             => System.Math.PI * Radius * Radius;
+
+        public static double Circumference(Circle circle)
+            => Geometry.Tau * circle.Radius;
     }
 
     // Подписи фигур для отчета
````

````text
csr move: Demo.Geometry.Circumference -> Demo.Circle: файлов 2, без записи (--apply - записать)
  компиляция: проектов 1, ошибок до 0, после 0, новых 0 (0,1 с)
````

Метод экземпляра с параметром типа-приёмника (`csr move Demo.Painter.Outline --to Demo.Circle`): параметр `circle` становится `this`, вызов `Outline(circle, "black")` — `circle.Outline("black")`:

````diff
--- a/Geometry.cs
+++ b/Geometry.cs
@@ -10,14 +10,8 @@
 
     public class Painter
     {
-        public string Outline(Circle circle, string color)
-        {
-            var length = Geometry.Circumference(circle);
-            return $"{color}: {length:F1}, r={circle.Radius}";
-        }
-
         public string Draw(Circle circle)
-            => Outline(circle, "black");
+            => circle.Outline("black");
     }
 }
 
--- a/Shapes.cs
+++ b/Shapes.cs
@@ -8,6 +8,12 @@
 
         public double Area
             => System.Math.PI * Radius * Radius;
+
+        public string Outline(string color)
+        {
+            var length = Geometry.Circumference(this);
+            return $"{color}: {length:F1}, r={Radius}";
+        }
     }
 
     // Подписи фигур для отчета
````

````text
csr move: Demo.Painter.Outline -> Demo.Circle: файлов 2, без записи (--apply - записать)
  компиляция: проектов 1, ошибок до 0, после 0, новых 0 (0,1 с)
````

Поле экземпляра (`csr move Demo.Report.m_Lines --to Demo.Circle`):

````text
csr move: Demo.Report.m_Lines -> Demo.Circle: отказ: поле экземпляра - данные объекта, не переносится
````

### `csr extract`

Извлечение метода из строк файла. Своё извлечение по анализу потоков данных и управления Roslyn (`AnalyzeDataFlow`, `AnalyzeControlFlow`): готовый `ExtractMethod` Roslyn — внутренний API пакета `Features`, CSR его не подключает. По умолчанию — только дифф на stdout, сводка в stderr; запись файлов — только с `--apply`.

```
csr extract <файл.cs:строка-строка> <имя> [--solution файл] [--format diff | json] [--apply] [--force] [--no-verify] [--no-server]
```

| Опция | Назначение |
|---|---|
| `файл.cs:строка-строка` | строки с 1, включительно; путь — от текущей папки. Строки — целые инструкции одного блока метода, конструктора или аксессора |
| `имя` | имя нового метода |
| `--solution файл` | `.sln`, `.slnx` или `.csproj` (по умолчанию — ближайшее решение вверх по папкам от файла и от текущей папки) |
| `--format diff \| json` | `diff` (по умолчанию) — дифф в stdout, сводка в stderr; `json` — всё в stdout |
| `--apply` | записать файлы |
| `--force` | с `--apply`: записать при новых ошибках компиляции |
| `--no-verify` | не компилировать затронутые проекты до и после |
| `--no-server` | загрузить решение в процессе |

- **Входы и выходы:** переменные, чьё значение читается в строках, — параметры (сначала параметры метода, затем локальные — по месту объявления); переменная, которая меняется в строках и читается после, — возвращаемое значение; несколько таких переменных — `out`-параметры. Выход, объявленный в строках, в месте вызова объявляется так же, как в исходном коде: `var x = M(...)` или `out var x` (с явным типом — так же с типом); в теле метода его объявление становится присваиванием `out`-параметра. Тип возврата у `var` — тип инициализатора.
- **Метод:** `private`; `static`, если член статический или строкам не нужен экземпляр (нет `this`, `base` и обращений к членам экземпляра без квалификатора). Ставится сразу после члена, из которого извлекается. Строки переносятся как есть со сдвигом на отступ тела метода; сигнатура длиннее 120 символов — параметры столбиком.
- **Отказ с причиной:** строки — не целые инструкции одного блока; в строках нет кода; переменная читается и меняется (нужен `ref`); меняется параметр `out` / `ref` метода; переменная объявлена в строках и используется после, но не выход; выход-`out`, объявленный в строках не одной переменной с инициализатором; `return`, `break`, `continue`, `goto` за пределы строк; `await`, `yield`; строки в лямбде или локальной функции; член не в классе или структуре; тип переменной — параметр типа обобщённого метода или анонимный; многострочная verbatim-строка (сдвиг отступа изменил бы её текст). Неверное имя или строки вне файла — код 2.
- **Проверка компиляцией** — как у [`csr rename`](#csr-rename): ошибки, которых анализ не видит (например, переменная выхода присваивается не во всех ветвях), попадают в список новых ошибок; `--apply` без `--force` их не записывает.
- Коды возврата: `0` — дифф готов или записан, `1` — отказ, новые ошибки компиляции или запись не выполнена, `2` — ошибка запуска.

Пример. Файл `Report.cs`:

```csharp
namespace Demo
{
    public class Report
    {
        private int m_Lines;

        public string Build(Circle circle, string title)
        {
            var header = title.ToUpperInvariant();
            var area = circle.Area;
            var line = $"{header}: {area:F2}";
            m_Lines++;
            return line;
        }
    }
}
```

`csr extract Demo/Report.cs:9-11 Format --solution Demo/Demo.csproj`: входы `circle` и `title`, выход `line`; экземпляр не нужен — метод `static`:

````diff
--- a/Report.cs
+++ b/Report.cs
@@ -6,12 +6,18 @@
 
         public string Build(Circle circle, string title)
         {
-            var header = title.ToUpperInvariant();
-            var area = circle.Area;
-            var line = $"{header}: {area:F2}";
+            var line = Format(circle, title);
             m_Lines++;
             return line;
         }
+
+        private static string Format(Circle circle, string title)
+        {
+            var header = title.ToUpperInvariant();
+            var area = circle.Area;
+            var line = $"{header}: {area:F2}";
+            return line;
+        }
     }
 }
 
````

````text
csr extract: Report.cs:9-11 -> Format: файлов 1, без записи (--apply - записать)
  компиляция: проектов 1, ошибок до 0, после 0, новых 0 (0,2 с)
````

Строка 12 (`m_Lines++;`) извлекается в `private void Count()` — член экземпляра нужен. Строки 12–13 захватывают `return`:

````text
csr extract: Report.cs:12-13 -> Count: отказ: return в строках - не поддержано
````

### `csr serve`

Процесс держит решение загруженным: открытие большого решения Unity — около 30 с на каждый вызов, через сервер `refs` — доли секунды. `refs`, `rename`, `move`, `extract` и `arch --solution` находят живой сервер решения сами (именованный канал текущего пользователя, имя — по пути решения); `--no-server` — загрузить решение в процессе.

```
csr serve [решение] [--idle минуты] [--detach] [--log файл]
csr serve [решение] --status | --stop
csr serve --list
```

- Перед каждой командой сервер применяет изменения с диска: изменённые `.cs` перечитываются; при изменении содержимого `.sln`, `.slnx`, `.csproj` и при `.props`, `.targets` решение перезагружается; новые и удалённые `.cs` — перезагрузка для проектов SDK (старые проекты Unity перечисляют файлы сами, перезагрузку даст изменение `.csproj`).
- `--detach` запускает сервер отдельным процессом и возвращается, когда решение загружено; журнал — в кэше CSR. `--idle` — остановка после простоя (по умолчанию 120 минут, `0` — без остановки). Без `--detach` сервер работает до `--stop`, Ctrl+C или простоя.
- `--status` — состояние сервера решения; `--stop` — остановить; `--list` — все запущенные серверы.
- Решение с проектами старого формата (Unity) грузится через теневую копию `.sln`, `obj` и папка вывода — в кэше CSR (`%LOCALAPPDATA%\csr` или переменная `CSR_CACHE`): рядом с кодом проекта ничего не пишется.
- Коды возврата: `0` — успех, `1` — сервер не запущен (`--status`, `--stop`), `2` — ошибка запуска.

Пример:

````text
> csr serve Demo/Demo.csproj --detach
решение: C:\work\Demo\Demo.csproj
процесс: 12056
канал: csr-0e5da4c7633b
проектов: 1, документов: 12, загрузка 1,3 с, перезагрузок 0
csr serve: запущен за 1,7 с; журнал: %LOCALAPPDATA%\csr\servers\0e5da4c7633b.log
> csr serve --list
12056	2026-11-21 16:32	C:\work\Demo\Demo.csproj
> csr serve Demo/Demo.csproj --status
решение: C:\work\Demo\Demo.csproj
процесс: 12056
канал: csr-0e5da4c7633b
проектов: 1, документов: 12, загрузка 1,3 с, перезагрузок 0
> csr refs Profile.Level --solution Demo/Demo.csproj --format text
# Profile.Level (field): ссылок 2
Profile.cs:9:16: объявление: public int Level;
Program.cs:20:17: profile.Level++;
Program.cs:21:40: return profile.Score + profile.Level;
csr refs: ссылок 2 в 1 файлах, 1,3 с
> csr serve Demo/Demo.csproj --stop
csr serve: остановлен
````

### `csr similar`

Ищет похожий код по синтаксису файлов, без загрузки решения: работает так же быстро и без MSBuild, как `style`. На выходе — пары похожих мест и кластеры (связные группы пар). Цель — увидеть повтор между задачами, который ревью одного диффа не замечает.

Уровни (вид единицы — ключ `--kinds` и `kind` в JSON):

| Уровень | Вид | Что похоже |
|---|---|---|
| L1 | `member`, `fragment` | тела членов и части тел (окна операторов) |
| L2 | `parameters`, `fields` | наборы входных параметров методов и наборы полей типов |
| L3 | `type` | типы: схема (базы, переопределения, поля, сигнатуры) и тела членов; параллельные системы — по общему ядру |
| L4 | `system` | системы вариантов: хост → база вариантов → варианты → результат; по запросу (`--kinds system`), нужна компиляция файлов |

```
csr similar <файлы | папки>... [--config <similar.jsonc>] [опции]
```

| Ключ | Описание |
|---|---|
| `--config <similar.jsonc>` | порог, минимумы, окно фрагмента, `paths` / `excludePaths`. Без ключа ищется `.csr/similar.jsonc` в папке цели или выше, затем `config/similar.jsonc` рядом с `csr.exe`; если нет ни того ни другого — значения по умолчанию |
| `--project <project.jsonc>` | символы препроцессора; поиск — как у `style` |
| `--changed` | только пары с изменённым кодом (`git diff` от `HEAD` и новые файлы), сравнение — со всем кодом цели |
| `--diff <ревизия \| файл.diff>` | изменённые строки для `--changed` (включает его): `git diff -U0` от ревизии или готовый дифф |
| `--kinds <виды>` | виды единиц через запятую: `member`, `fragment`, `parameters`, `fields`, `type`, `system`; по умолчанию все, кроме `system` |
| `--threshold <0..1>` | порог оценки членов и фрагментов (L1), по умолчанию `0.82` |
| `--set-threshold <0..1>` | порог наборов параметров и полей (L2), по умолчанию `0.5` |
| `--min-shared <N>` | наименьшее число общих элементов пары наборов, по умолчанию `3` |
| `--type-threshold <0..1>` | порог типов (L3), по умолчанию `0.75` |
| `--system-threshold <0..1>` | порог систем (L4), по умолчанию `0.4` |
| `--min-lines <N>` | наименьшее число строк единицы, по умолчанию `4` |
| `--min-nodes <N>` | наименьшее число узлов формы, по умолчанию `20` |
| `--min-shapes <N>` | наименьшее число разных поддеревьев формы, по умолчанию `20` |
| `--fragments <N>` | операторов в окне фрагмента, по умолчанию `4`; `0` — без фрагментов |
| `--top <N>` | в `md` — первые N кластеров (с `--changed` — пар); `0` — все |
| `--format json \| md` | `json` — для агентов (по умолчанию), `md` — для человека |
| `--out <файл>` | записать отчёт в файл вместо stdout |

Ключи командной строки перекрывают значения из конфига. Коды возврата: `0` — успех (найденные пары ошибкой не считаются), `2` — ошибка запуска.

**Что сравнивается**

- **Член** — метод, конструктор, деструктор, оператор, свойство и индексатор с телом, событие с `add` / `remove`, локальная функция, лямбда и анонимный метод с блоком. Форма члена — вся декларация без атрибутов и модификаторов. Лямбда внутри метода не сравнивается со своим методом.
- **Фрагмент** — окно из `--fragments` подряд идущих операторов одного блока; так находится частичный клон: два метода совпадают на части тела. Окна, совпавшие подряд, склеиваются в один фрагмент: окна одной диагонали (тот же блок слева и справа, тот же сдвиг номеров операторов) с перекрытием или встык; оценка — наибольшая из окон. Окно, равное всему телу члена, не берётся: это сам член. Фрагменты пары членов, которые уже похожи целиком, не выводятся.

- **Набор параметров** (L2) — входные параметры (без `out`) метода, конструктора, оператора, локальной функции. `override` и явные реализации интерфейса не берутся: набор задан базой.
- **Набор полей** (L2) — поля экземпляра (без `const` и `static`), автосвойства экземпляра и параметры основного конструктора типа.
- **Тип** (L3) — класс, структура, запись; части `partial` — по отдельности, вложенный тип — отдельная единица. Тип без поведения (нет члена с телом, кроме конструктора) — данные, его сравнивает набор полей L2.

- **Система** (L4) — хост точки расширения: тип, который держит полем или коллекцией абстрактный класс или интерфейс с двумя и больше вариантами (прямые наследники и реализации). Строится по графу типов.

Единицы разных видов не сравниваются.

**Форма и оценка** (по образцу `dryer`):

- Нормализованное дерево Roslyn. Остаются вид узла (оператор, вид инструкции), имя вызываемого метода и тип в `new`. Заглушками становятся локальные имена, параметры, поля, имена типов вне `new` и литералы.
- Решения для C#: `this.X` — как `X`; `nameof(x)` — литерал; текст интерполяции — литерал, выражения в ней — форма; аргументы типа — заглушка типа; `switch`-выражение и шаблоны — по форме, константа шаблона — литерал, тип шаблона — заглушка.
- Отпечатки — хеши всех поддеревьев и листьев. Оценка пары — коэффициент Жаккара: общие отпечатки / все отпечатки обеих форм. Пара с оценкой `1.00` совпадает по форме целиком.
- Имя вызова входит во все поддеревья над вызовом: `Math.Max` вместо `Math.Min` опускает оценку метода из примера ниже до `0.5`.
- `--min-shapes` отсекает цепочки одинаковых простых операторов (`m_A = a; m_B = b; …`): узлов в такой цепочке много, а разных поддеревьев мало.

**Наборы (L2).** Элемент — тип как написан (без пробелов) и имя без префикса поля в нижнем регистре: `Vector3 position`. Синонимы имён (`pos` → `position`) задаются в `similar.jsonc` (`synonyms`), встроенного словаря нет. Пара — не меньше `--min-shared` общих элементов и коэффициент Жаккара не ниже `--set-threshold`. У кластера — общая часть (элементы, которые есть у всех мест) и свои элементы каждого места: вход для решения «общая структура + параметр по виду».

Отличие от признака `arch` `parameter-set`: там один метод, который вызывают с набором из многих мест (решение загружается); здесь — один набор у разных методов и типов, по синтаксису. Общего кода у них нет: `parameter-set` остаётся в `arch`.

**Типы (L3).** Профиль типа по частям:
- базы — имя без пространства и число аргументов типа (`base RuleBase`, `base IEnumerable`1`);
- переопределения — `override` и имя члена (`override Apply(1)`);
- поля — модификаторы и тип (`field private static readonly string[]`);
- сигнатуры остальных членов — вид, модификаторы, тип и число параметров (`method private static bool(1)`).

Повтор элемента считается по количеству: два метода `bool(1)` — два элемента. Оценка пары — взвешенное среднее Жаккара по частям профиля и оценки тел. Тела — члены с телом и инициализаторы полей; они сопоставляются жадно по форме L1, вес пары членов — её размер в узлах, поэтому одинаковые короткие члены схемы (`Name => "..."`) не перевешивают основную логику. Часть, пустая у обоих типов, в среднее не входит. Веса по умолчанию: базы 1, переопределения 1, поля 1, сигнатуры 1, тела 2.

Второй признак L3 — **общее ядро**: у пары типов не меньше `minCoreMembers` (4) членов одной формы (отпечатки L1 совпадают целиком, член не короче 7 узлов). Он находит параллельные системы: ядро скопировано, у каждой копии много своей логики, и взвешенная оценка ниже порога. Форма, которая встречается больше чем у 20 типов, считается обвязкой и в ядро не идёт. У пары в JSON — `sharedMembers`.

У кластера типов — общая схема, свои элементы каждого места и тела. Тела описываются так: сколько членов той же формы, что у пары (отличаются данными — именами и литералами), и какие члены со своей логикой.

**Системы (L4).** Для систем строится граф типов. Источник — компиляция Roslyn из файлов цели и сборок платформы .NET, на которой запущен `csr`; решение не загружается, MSBuild не нужен. Типы исходного кода разрешаются по пространствам имён и `using`, поэтому одноимённые типы разных пространств не путаются. Типы сторонних библиотек (Unity, пакеты) не разрешены: связей с ними в графе нет, в метках они идут по имени, как написаны.

Граф:
- узлы — типы исходного кода (части `partial` — один узел) с метками без имени: вид, `abstract`, `sealed`, обобщённость, число полей; формы абстрактных, виртуальных и интерфейсных членов; типы полей;
- рёбра по видам — наследование, реализация, поле, коллекция (массив, `List<T>`, значение словаря), возврат, параметр, создание (`new`), вызов виртуального или интерфейсного члена.

Граф — отдельный компонент (`CSR.Core.Graph`): на нём же будут структурные метрики (глубина наследования и вложенности, связность папок и пространств, циклы, концентраторы).

Признаки системы — устройство образца «хост → база вариантов → варианты → результат»:
- хост и база — метка, поле или коллекция, число вариантов по ступеням (2–3, 4–9, 10 и больше);
- база — формы её абстрактных, виртуальных и интерфейсных членов целиком и по частям: `метод структура(float)`, `возврат структура`, `параметр float`;
- результат — то, что возвращают члены базы (если у базы их нет — общее для вариантов): метка и типы полей (`поле Vector3`).

Типы библиотек идут в признаки по имени (`Vector3`, `float`, `List<класс>`), типы исходного кода — по виду (`класс`, `структура`). Это та же логика, что в форме L1: вызовы библиотек остаются, свои имена — нет. Без форм и типов полей устройство «хост → список вариантов → результат» встречается в большом проекте сотни раз, и системы одного назначения от остальных не отличаются.

Оценка пары — коэффициент Жаккара признаков, порог `0.4`. Не пара, а одна система:
- корень одной внутри другой;
- общих типов больше половины меньшей системы (два хоста одной базы).

Точка расширения, у которой больше 5 хостов, — инфраструктура (вычислители, действия, условия, которые держат полем сотни типов). В инвентаре она есть, в сравнение не идёт. Система с меньше чем 8 признаками (одна база с двумя вариантами без результата) не сравнивается: такие совпадают с любой другой.

**Инвентарь систем.** С `--kinds system` в отчёте есть раздел «Системы вариантов», в JSON — массив `inventory`. В нём каждый хост каждой точки расширения: хост, база вариантов, поле или коллекция, варианты и их число, результат, число хостов у базы. В `md` — по базам, сначала базы с большим числом вариантов. Это вход для аудита.

**Масштаб.** Кандидатов отбирает префиксный фильтр (AllPairs):
- отпечатки единицы упорядочены от редких к частым;
- две единицы с оценкой не ниже порога `t` обязательно делят отпечаток в префиксах длины `n - ceil(t * n) + 1`.

Отбор точный, без пропусков. Затем у кандидатов считается точный коэффициент Жаккара.

Для типов кандидаты берутся по профилю целиком с порогом 0,3. Отбор не точный: пару с оценкой выше порога и непохожим профилем он пропустит. Общее ядро ищется отдельно, по индексу форм членов.

Замер на проекте Unity из 9249 файлов (120 тыс. единиц):

| Что | Время | Пар | Кластеров |
|---|---|---|---|
| L1 | 4,5 с | 6890 | 1788 |
| L2 и L3 (`--kinds parameters,fields,type`) | 3,3 с | 4352 | 984 |
| все уровни | 7,7 с | 11242 | 2784 |
| `--changed` | 7,2 с | 14 | 6 |
| L4 (`--kinds system`): инвентарь — 1444 хоста, 241 база; в сравнении — 204 системы | 12 с | 301 | 5 |

При `--changed` время уходит на разбор всех файлов цели. У L4 время — компиляция файлов и граф типов.

**Пример**

Было (`Demo/`): `Totals.Sum` и `Counter.Count` отличаются именами и литералами, `Loader.Load` и `Importer.Import` совпадают первыми пятью операторами.

```csharp
// Demo/Totals.cs
public class Totals
{
    public int Sum(int[] values)
    {
        int total = 0;
        foreach (var value in values)
        {
            if (value > 10 && IsValid(value))
                total += Math.Max(value * 2, 1);
        }

        return total;
    }
}

// Demo/Counter.cs
public class Counter
{
    public int Count(int[] items)
    {
        int result = 5;
        foreach (var item in items)
        {
            if (item > 3 && IsValid(item))
                result += Math.Max(item * 7, 9);
        }

        return result;
    }
}

// Demo/Loader.cs
public class Loader
{
    public int Load(string root, string name)
    {
        var path = Path.Combine(root, name);
        if (!File.Exists(path))
            throw new InvalidOperationException("missing " + name);
        var text = File.ReadAllText(path);
        var lines = text.Split('\n').Where(line => line.Length > 0).ToList();
        var comments = lines.Count(line => line.StartsWith("#"));

        int sum = 0;
        foreach (var line in lines)
            sum += int.Parse(line.Trim());
        return sum - comments;
    }
}

// Demo/Importer.cs
public class Importer
{
    public Dictionary<string, string> Import(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        if (!File.Exists(path))
            throw new InvalidOperationException("no file " + file);
        var text = File.ReadAllText(path);
        var lines = text.Split(';').Where(line => line.Length > 1).ToList();
        var comments = lines.Count(line => line.StartsWith("//"));

        var result = new Dictionary<string, string>();
        if (lines.Count == comments)
            Log.Warning("empty");
        else
            result[lines[0]] = string.Join(",", lines.Skip(1));

        return result;
    }
}
```

Вывод `csr similar Demo --format md`:

````markdown
# csr similar

Файлов 4, членов 4, фрагментов 10, наборов параметров 0, наборов полей 0, типов 0; пороги 0.82 (L1), 0.50 (L2), 0.75 (L3); пар 2, кластеров 2.

## 1. фрагменты: мест 2, пар 1, оценка 1.00

- `Demo/Importer.cs:5-10` `Importer.Import`, узлов 83
- `Demo/Loader.cs:5-10` `Loader.Load`, узлов 83

## 2. члены: мест 2, пар 1, оценка 1.00

- `Demo/Counter.cs:3-13` `Counter.Count`, узлов 46
- `Demo/Totals.cs:3-13` `Totals.Sum`, узлов 46
````

В stderr: `csr similar: файлов 4, единиц 14, пар 2, кластеров 2, 0,1 с`. Фрагмент `5-10` — два окна по 4 оператора, склеенные в одно (`"windows": 2` в JSON). Наборов параметров нет: у методов примера меньше трёх входных параметров. Типов тоже нет: в каждом классе меньше четырёх полей и членов.

`--changed` после коммита без `Counter.cs` и добавления файла (`csr similar Demo --changed --format md`): пары только у изменённого кода, слева — изменённая единица.

````markdown
# csr similar

Файлов 4, членов 4, фрагментов 10, наборов параметров 0, наборов полей 0, типов 0; пороги 0.82 (L1), 0.50 (L2), 0.75 (L3); пар 1, кластеров 1.

## члены: `Demo/Counter.cs:3-13` `Counter.Count`, узлов 46

- 1.00: `Demo/Totals.cs:3-13` `Totals.Sum`, узлов 46
````

Пример L2 и L3. Было (`Demo/`):
- `Projectiles.Launch(Vector3 position, Vector3 direction, float speed, int damage)` и `Units.Move(Vector3 pos, Vector3 dir, float speed, string animation)`;
- классы `PrefixRule` и `SuffixRule` одной схемы: база `RuleBase`, маски в поле, `Name`, `Apply` и помощник `Matches`; отличаются только маски, тексты и число в условии.

```csharp
// Demo/PrefixRule.cs (SuffixRule.cs - то же с ["*Base", "*Impl"], "suffix" и Depth > 1)
public class PrefixRule : RuleBase
{
    private static readonly string[] Masks = ["m_*", "s_*"];

    public override string Name
        => "prefix";

    public override void Apply(Context context)
    {
        foreach (var node in context.Nodes)
        {
            if (Matches(node.Text) && node.Depth > 2)
                context.Report(node, "prefix: " + node.Text);
        }
    }

    private static bool Matches(string text)
        => Masks.Any(mask => text.StartsWith(mask.TrimEnd('*')));
}
```

Конфиг `similar.jsonc`: `{ "kinds": ["parameters", "fields", "type"], "synonyms": { "pos": "position", "dir": "direction" } }`. Вывод `csr similar Demo --config similar.jsonc --format md`:

````markdown
# csr similar

Файлов 4, членов 0, фрагментов 0, наборов параметров 2, наборов полей 0, типов 2; пороги 0.82 (L1), 0.50 (L2), 0.75 (L3); пар 2, кластеров 2.

## 1. типы: мест 2, пар 1, оценка 1.00

Общее: `base RuleBase`, `override Name`, `override Apply(1)`, `field private static readonly string[]`, `property public override string =>`, `method public override void(1)`, `method private static bool(1)`

- `Demo/PrefixRule.cs:1-19` `PrefixRule`, полей и членов 4
  - та же форма (отличия - имена и литералы): членов 4 из 4
- `Demo/SuffixRule.cs:1-19` `SuffixRule`, полей и членов 4
  - та же форма (отличия - имена и литералы): членов 4 из 4

## 2. наборы параметров: мест 2, пар 1, оценка 0.60

Общее: `Vector3 position`, `Vector3 direction`, `float speed`

- `Demo/Projectiles.cs:3-3` `Projectiles.Launch`, элементов 4
  - своё: `int damage`
- `Demo/Units.cs:3-3` `Units.Move`, элементов 4
  - своё: `string animation`
````

Без синонимов у наборов одно общее имя `speed`, и пары нет (нужно не меньше трёх).

Пример L4. Было (`Demo/`): две системы вариантов с разными именами и телами.

```csharp
// Demo/Motions.cs
using System.Numerics;
using System.Collections.Generic;

public struct MotionFrame { public Vector3 Move; public Vector3 Look; public bool IsGrounded; }

public abstract class MotionHandler
{
    public abstract MotionFrame Calculate(float deltaTime);
}

public class WalkHandler : MotionHandler
{
    public override MotionFrame Calculate(float deltaTime) => new MotionFrame { IsGrounded = true };
}

public class JumpHandler : MotionHandler
{
    public override MotionFrame Calculate(float deltaTime) => new MotionFrame();
}

public class MotionGroup
{
    private readonly List<MotionHandler> m_Handlers = new();

    public MotionFrame Tick(float deltaTime)
    {
        var frame = new MotionFrame();
        foreach (var handler in m_Handlers)
            frame = handler.Calculate(deltaTime);
        return frame;
    }
}

// Demo/Motors.cs
using System.Numerics;
using System.Collections.Generic;

public struct MotorResult { public Vector3 Position; public Quaternion Rotation; }

public abstract class ProjectileMotor
{
    public abstract MotorResult Step(float time);
}

public class ArrowMotor : ProjectileMotor
{
    public override MotorResult Step(float time) => new MotorResult();
}

public class OrbitMotor : ProjectileMotor
{
    public override MotorResult Step(float time) { var result = new MotorResult(); return result; }
}

public class MissileMotor : ProjectileMotor
{
    public override MotorResult Step(float time) => default;
}

public class ProjectileMotors
{
    private ProjectileMotor[] m_Motors;

    public void Update(float time)
    {
        foreach (var motor in m_Motors)
            Apply(motor.Step(time));
    }

    private void Apply(MotorResult result) { }
}
```

Вывод `csr similar Demo --kinds system --format md` (сокращено):

````markdown
## 1. системы: мест 2, пар 1, оценка 0.82

Общее: `хост: класс`, `база: абстрактный класс`, `база: в коллекции`, `база: вариантов 2-3`, `база: метод структура(float)`, `база: возврат структура`, `база: параметр float`, `результат: структура`, `результат: поле Vector3`

- `Demo/Motions.cs:21-32` `MotionGroup`, типов 5
  - своё: `результат: поле bool`
  - база вариантов (коллекция): MotionHandler
  - варианты (2): JumpHandler, WalkHandler
  - результат: MotionFrame
- `Demo/Motors.cs:26-37` `ProjectileMotors`, типов 6
  - своё: `результат: поле Quaternion`
  - база вариантов (коллекция): ProjectileMotor
  - варианты (3): ArrowMotor, MissileMotor, OrbitMotor
  - результат: MotorResult

## Системы вариантов: баз 2, хостов 2

- база `ProjectileMotor` (`Demo/Motors.cs:6`), вариантов 3, хостов 1
  - хосты: `ProjectileMotors`
  - варианты: `ArrowMotor`, `MissileMotor`, `OrbitMotor`
  - результат: `MotorResult`
````

На самом CSR (`--kinds system`) находится пара каталогов: `StyleCatalog` (коллекция `SyntaxCheck`, 45 вариантов) и `ArchCatalog` (коллекция `ArchCheck`, 13 вариантов), оценка `0.80`–`1.00`. Это каталог проверок двух режимов одного движка.

JSON (`--format json`, сокращено):
- верхний уровень — счётчики единиц по видам (`members`, `fragments`, `parameterSets`, `fieldSets`, `types`, `systems`) и пороги (`threshold`, `setThreshold`, `typeThreshold`, `systemThreshold`);
- `clusters` — вид (`member`, `fragment`, `parameters`, `fields`, `type`, `system`), число пар, наибольшая и наименьшая оценка, `common` (L2–L4) и места;
- `pairs` — вид, оценка, `windows` (окон фрагмента подряд), `sharedMembers` (L3: членов одной формы), `left` и `right`;
- у места — `file`, `startLine`, `endLine`, `member`, `nodes` (узлов формы; у набора — элементов, у типа — полей и членов), `shapes` (отпечатков). В кластере у места ещё `own` (свои элементы) и `bodies` (L3: тела; L4: роли и типы в них);
- `inventory` (с `--kinds system`) — хосты точек расширения: `host` и `extensionPoint` (`name`, `file`, `startLine`), `holder` (`field` / `collection`), `variantCount`, `variants`, `results`, `hostsOfExtensionPoint`.

```json
{
  "files": 4, "members": 4, "fragments": 10, "parameterSets": 0, "fieldSets": 0, "types": 0,
  "threshold": 0.82, "setThreshold": 0.5, "typeThreshold": 0.75, "changed": false,
  "clusters": [
    { "kind": "fragment", "pairs": 1, "maxScore": 1, "minScore": 1,
      "units": [
        { "file": "Demo/Importer.cs", "startLine": 5, "endLine": 10, "member": "Importer.Import", "nodes": 83, "shapes": 54 },
        { "file": "Demo/Loader.cs", "startLine": 5, "endLine": 10, "member": "Loader.Load", "nodes": 83, "shapes": 54 } ] }
  ],
  "pairs": [
    { "kind": "member", "score": 1, "windows": 1, "sharedMembers": 0,
      "left": { "file": "Demo/Counter.cs", "startLine": 3, "endLine": 13, "member": "Counter.Count", "nodes": 46, "shapes": 30 },
      "right": { "file": "Demo/Totals.cs", "startLine": 3, "endLine": 13, "member": "Totals.Sum", "nodes": 46, "shapes": 30 } }
  ]
}
```

**Ограничения**

- Только синтаксис: одинаковые по форме вызовы разных перегрузок неразличимы, разные имена одного метода (псевдоним, обёртка) — различны.
- L4 ищет только системы вариантов. Типы, которые повторяют друг друга без общей точки расширения (копии одной части с разными именами), L4 не связывает — это L3.
- L4 без загрузки решения: типы сторонних библиотек не разрешены. Виртуальный вызов члена такого типа и наследование от него в граф не попадают. Режим по решению (`--solution`) — отдельная задача.
- Кластер L4 — тоже связная компонента: через пары чуть выше порога цепочкой собираются разнородные хосты. Пары в JSON упорядочены по оценке.
- L3 видит схему класса, а не смысл. Семь проверок имён `style` («отбор символов → требование к имени») на уровне методов не похожи: наибольшая оценка пары их методов — `0.56`. На уровне типов у них схема всех проверок `style` (база, `Name`, `Description`, `Parameters`, `Analyze`): при пороге `0.5` они попадают в один кластер вместе с остальными проверками `style`, отдельного кластера не образуют. Тела у них разные. Пара `forbidden-call` / `forbidden-type` (тот же движок) — `0.82`, выше порога по умолчанию.
- Наборы L2: синоним задаётся только конфигом; конструкторы наследников, которые передают параметры базе, дают кластеры одинаковых наборов.
- Отбор кандидатов L3 по профилю (порог 0,3) не точный: пару с высокой оценкой тел и непохожим профилем он пропустит.
- Кластер — связная компонента пар: через цепочку пар с оценкой чуть выше порога в один кластер попадают места, не похожие напрямую. Оценка кластера — диапазон по его парам.
- Повтор внутри одного места (подряд идущие похожие блоки одного метода) даёт кластер из одного места: перекрытые фрагменты файла склеиваются.
- Сгенерированный код и тесты исключаются через `excludePaths`.

### `csr structure`

Структурные метрики на графе типов (тот же граф, что у `csr similar` L4: компиляция файлов цели без загрузки решения). Метрики — вход аудита архитектуры, не находки. Превышение порога — отметка в отчёте.

```
csr structure <файлы | папки>... [опции]
```

| Ключ | Описание |
|---|---|
| `--config <structure.jsonc>` | пороги отметок и размер топов. Без ключа ищется `.csr/structure.jsonc` в папке цели или выше, затем `config/structure.jsonc` рядом с `csr.exe`; если нет ни того ни другого — значения по умолчанию |
| `--project <project.jsonc>` | символы препроцессора; поиск — как у `style` |
| `--top <N>` | размер топов, по умолчанию `10`; `0` — все |
| `--format json \| md` | `json` — для агентов (по умолчанию), `md` — для человека |
| `--out <файл>` | записать отчёт в файл вместо stdout |

Коды возврата: `0` — успех (отметки ошибкой не считаются), `2` — ошибка запуска.

**Метрики**

| Уровень | Метрика | Отметка |
|---|---|---|
| тип | глубина наследования среди типов исходного кода, число прямых наследников | глубже `inheritanceDepth` |
| тип | WMC — сумма цикломатической сложности функций; ATFD — число разных полей и свойств чужих типов исходного кода, к которым тип обращается; TCC — доля пар методов экземпляра с общим полем | объект-бог (Lanza — Marinescu): WMC ≥ `godWmc`, ATFD > `godAtfd`, TCC < `godTcc` |
| тип | связанные типы (соседи в обе стороны), зависимые (входящая степень) | топ концентраторов |
| владение | глубина вложенности полиморфных частей и сама цепочка | глубже `ownershipDepth` |
| папка, пространство имён | Ca, Ce, нестабильность I = Ce / (Ca + Ce), абстрактность A, расстояние D = \|A + I − 1\|; внутренние и внешние рёбра; сообщества и модулярность Q; доля типов без внутренних связей | свалка; топ по D |
| между папками и между пространствами | циклы зависимостей (сильно связные компоненты) и замыкающие связи | каждый цикл |

**Глубина владения.** Уровень цепочки — тип держит полем или коллекцией базу вариантов (абстрактный класс или интерфейс с двумя и больше вариантами). Следующий уровень — вариант этой базы или наследник варианта держит свою базу вариантов.

Пример: агент движения → движения → составное движение и его наследник-цепочка → состояния. Это три уровня, ровно предел по умолчанию.

Не уровни:
- повтор базы в цепочке — рекурсия (выражение держит выражения), это один уровень;
- инфраструктурная база (больше `infrastructureHosts` хостов: действия, вычислители, условия, которые держат полем сотни типов);
- реестр (больше `registryVariants` вариантов: части сущности, обработчики событий);
- реализация интерфейса-возможности типом другой системы: через интерфейс цепочка идёт только в варианты без своей базы из исходного кода.

**Свалка.** Папка не меньше `dumpSize` типов, у которой внутренний граф распадается: сообществ не меньше `dumpCommunities` или половина типов без внутренних связей. Сообщества ищутся распространением меток по рёбрам без направления. Варианты одной базы считаются связанными: папка независимых вариантов — каталог расширений, а не свалка.

**Циклы.** Замыкающие связи цикла показываются в таком порядке:
- сначала связи, у которых есть обратная, по отношению «рёбер туда / рёбер обратно»: связь 3 раза туда при 353 обратно — кандидат на разворот;
- затем остальные по возрастанию числа рёбер.

**Пример**

Было (`Demo/`): четыре уровня вложенных частей и взаимная ссылка двух папок.

```csharp
// Demo/Movement/Motions.cs
using System.Collections.Generic;

public abstract class Motion { }
public class Walk : Motion { }
public class Composite : Motion { private List<MotionState> m_States; }

public abstract class MotionState { }
public class CurveState : MotionState { private CurvePath m_Path; }
public class IdleState : MotionState { }

public abstract class CurvePath { }
public class SplinePath : CurvePath { private List<Segment> m_Segments; }
public class LinePath : CurvePath { }

public abstract class Segment { }
public class Arc : Segment { }
public class Straight : Segment { }

public class MovementAgent
{
    private List<Motion> m_Motions;
    private Unit m_Owner;
}

// Demo/Units/Unit.cs
public class Unit
{
    private MovementAgent m_Movement;
}
```

Вывод `csr structure Demo --format md --top 3` (сокращено):

````markdown
# csr structure

Файлов 2, типов 14, ребер 14; папок 2, пространств 1.

Отметки: владение глубже 3 - 1, наследование глубже 4 - 0, объектов-богов - 0, свалок (папки) - 0, циклов папок - 1, циклов пространств - 0.

## Глубина владения (порог 3)

- 4 **глубже порога**: `MovementAgent (Demo/Movement/Motions.cs:19)`: MovementAgent -> Motion | Composite -> MotionState | CurveState -> CurvePath | SplinePath -> Segment
- 3: `Composite (Demo/Movement/Motions.cs:5)`: Composite -> MotionState | CurveState -> CurvePath | SplinePath -> Segment
- 2: `CurveState (Demo/Movement/Motions.cs:8)`: CurveState -> CurvePath | SplinePath -> Segment

## Циклы папок: 1

- пакетов 2: `Demo/Movement`, `Demo/Units`
  - `Demo/Movement` -> `Demo/Units`: связей 1 (обратно 1), например `MovementAgent` -> `Unit` (Field)
  - `Demo/Units` -> `Demo/Movement`: связей 1 (обратно 1), например `Unit` -> `MovementAgent` (Field)
````

JSON:
- `ownership` — порог и цепочки (все отмеченные и топ остальных): `root`, `depth`, `deep`, `steps` (`host`, `extensionPoint`);
- `gods`, `inheritance`, `hubs` — типы: `inheritanceDepth`, `children`, `coupling`, `dependents`, `wmc`, `atfd`, `tcc`, `methods`, `god`;
- `folders`, `namespaces` — все пакеты: `types`, `afferent`, `efferent`, `instability`, `abstractness`, `distance`, `internalEdges`, `externalEdges`, `communities`, `modularity`, `isolatedShare`, `dump`;
- `folderCycles`, `namespaceCycles` — `packages` и `links` (`from`, `to`, `count`, `reverse`, `example`).

**Замер.**
- CSR (`src`, 222 файла): 1,2 с; 323 типа, 1069 рёбер. Глубина владения не больше 1, объектов-богов и свалок нет, 2 цикла папок. Главная замыкающая связь — `Style/Checks` → `Style`: 4 связи при 35 обратных, проверки берут помощник из `Style`.
- Проект Unity (9249 файлов): 19 с; 12487 типов, 39547 рёбер.
  - Глубина владения: 259 цепочек глубины 1, 30 — глубины 2, 16 — глубины 3, 3 — глубины 4.
  - Объектов-богов — 311 (порог Lanza — Marinescu по умолчанию).
  - Свалок — 35 папок из 1721.
  - Циклов папок — 41; самый большой — 518 папок.

**Ограничения**

- Граф без загрузки решения: типы сторонних библиотек не разрешены, глубина наследования считается только по типам исходного кода. Наследник `MonoBehaviour` имеет глубину 0, а не 4.
- Обращения к чужим данным (ATFD) и связность (TCC) — по полям и свойствам, к которым обращаются по имени. Обращение через локальную переменную чужого типа считается, через `dynamic` и рефлексию — нет.
- Сообщества ищутся распространением меток. Результат зависит от порядка узлов (порядок фиксирован — по ключу типа), это приближение, а не оптимум модулярности.
- Не сделано: стоимость распространения изменений (MacCormack), посредничество (betweenness) концентраторов, история правок (горячие точки, совместные изменения) — следующим шагом.

## 3. Конфиги

Конфиги — JSON с комментариями (`//`, `/* */`) и запятыми после последнего элемента (`.jsonc`). Неизвестный ключ, неизвестная проверка или параметр, значение не того вида — ошибка конфига, код 2 (`csr: конфиг: неизвестная проверка 'nope'`).

| Файл | Для чего | Пример в репозитории |
|---|---|---|
| `style.jsonc` | проверки `csr style` и `csr fix` | `examples/style.example.jsonc` |
| `arch.jsonc` | признаки `csr arch` | `examples/arch.example.jsonc` |
| `project.jsonc` | общие сведения о проекте для `metrics`, `style`, `fix`, `arch` | `examples/project.example.jsonc` |
| `metrics.jsonc` | пороги и топ `csr metrics` | `examples/metrics.example.jsonc` |
| `refactor.jsonc` | `csr rename`: сериализация, поиск строк | `examples/refactor.example.jsonc` |
| `similar.jsonc` | уровни, пороги и минимумы `csr similar` (L1–L4) | `examples/similar.example.jsonc` |
| `structure.jsonc` | пороги отметок `csr structure` | `examples/structure.example.jsonc` |

Имя файла важно для [поиска конфигов](#где-искать-конфиги): без `--config` и `--project` `csr` ищет файл именно с этим именем.

### `style.jsonc` и `arch.jsonc`

Общий формат: объект `checks`, ключ — имя проверки или признака, значение — объект.

```jsonc
{
  "checks": {
    "line-length": { "severity": "error", "rule": "F20", "max": 120 },
    "null-check": { "severity": "warning", "rule": "I5", "prefer": "equality" },
    "commented-code": { "severity": "hint" }
  }
}
```

- **Проверка, которой нет в конфиге, выключена.** Конфиг выбирает проверки из каталога движка и задаёт их параметры; движок нейтрален и ID правил не знает.
- `severity` — уровень находки: `error`, `warning`, `hint`; по умолчанию `warning`. Уровень `error` даёт код возврата 1; `csr fix` по умолчанию применяет замены `error` и `warning`, `hint` — только с `--hints`.
- `rule` — метка правила вашего гайда (`F20`, `C14`, `ARCH-1`…): попадает в находку и сводку. Для движка — произвольная строка; по метке агент находит правило в гайде.
- `enabled: false` — выключить проверку (в следующем слое).
- `prefer` — параметр «выбор между двумя записями»: допустимые значения у каждой проверки свои (`leading` / `trailing`, `explicit` / `var`, `equality` / `pattern`…). Правило-выбор принадлежит гайду, не движку: один и тот же `null-check` по разным конфигам требует `== null` или `is null`.
- Остальные ключи — параметры проверки (в [разделах 4](#4-проверки-style) и [5](#5-признаки-arch) у каждой проверки своя таблица): целые, `true` / `false`, строки из набора, списки строк, карты «знак → замена».
- У признаков `arch` дополнительно `paths` и `excludePaths` — маски путей ([см. `csr arch`](#csr-arch)).

#### Слои конфигов

Несколько `--config` накладываются по порядку: следующий слой меняет уровень, метку и параметры проверок, которые уже есть, добавляет новые и выключает проверки через `"enabled": false`. Ключ, которого в слое нет, остаётся от предыдущего. Типовое разделение: личный слой — общий для всех проектов, слой проекта — отличия.

Пример. Личный слой `base.jsonc`:

```jsonc
// Личный слой: общий для всех проектов
{
  "checks": {
    "null-check": { "severity": "warning", "rule": "I5", "prefer": "pattern" },
    "explicit-private": { "severity": "warning", "rule": "S23" }
  }
}
```

Слой проекта `layer.jsonc`:

```jsonc
// Слой проекта: меняет параметр и уровень, выключает проверку
{
  "checks": {
    "null-check": { "severity": "error", "prefer": "equality" },
    "explicit-private": { "enabled": false }
  }
}
```

Файл `Demo.cs`:

```csharp
public class Report
{
    int m_Count;

    public bool Has(object item)
    {
        return item is null;
    }
}
```

`csr style Demo.cs --config base.jsonc` — одна находка, `explicit-private` (`S23`, строка 3): форма `is null` у `null-check` по `prefer: pattern` допустима. `csr style Demo.cs --config base.jsonc --config layer.jsonc` — `explicit-private` выключена, форма `null-check` теперь `equality` с уровнем `error` и меткой `I5` (унаследована из первого слоя):

````text
> csr style Demo.cs --config base.jsonc
- 3: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private int m_Count;
  ```

> csr style Demo.cs --config base.jsonc --config layer.jsonc
- 7: `null-check` I5 (error) - 'null' -> '== null'
  ```csharp
          return item == null;
  ```
````

### `project.jsonc`

Общие сведения о проекте для режимов `metrics`, `style`, `fix` и `arch` (`--project project.jsonc`).

```jsonc
// Общие сведения о проекте для режимов metrics, style и arch
{
  // Символы препроцессора при разборе (по умолчанию UNITY_EDITOR): код под #if без символа не анализируется
  "preprocessorSymbols": ["UNITY_EDITOR"],

  // Методы логгера для проверок текста лога: текст вызова без пробелов; * - любые символы.
  // bypass - вызовы в обход логгера проекта: csr arch (project-mechanism) предложит заменить их на methods
  "logger": {
    "methods": ["Log.Info", "Log.Warning", "Log.Error"],
    "bypass": ["Debug.Log*", "Console.Write*"]
  },

  // Механизмы проекта для csr arch (project-mechanism): как распознать самодельный повтор и чем заменить.
  // use - чем заменить; types - маски имен типов; fields + methods - тип с полем такого типа и методом на каждую
  // маску; calls - вызовы в обход, creates - new в обход; excludePaths - где лежит сам механизм
  "mechanisms": {
    "pool": {
      "use": "UnityEngine.Pool: ObjectPool<T>, ListPool<T>",
      "fields": ["Stack<*>", "Queue<*>"],
      "methods": ["Rent*", "Return*"]
    },
    "timer": {
      "use": "Timers.Schedule",
      "creates": ["Timer", "System.Threading.Timer"],
      "excludePaths": ["*/Timers/*"]
    }
  }
}
```

| Ключ | Значение |
|---|---|
| `preprocessorSymbols` | символы препроцессора при разборе (по умолчанию `["UNITY_EDITOR"]`): код под `#if` без символа не анализируется |
| `logger.methods` | методы логгера для проверок текстов логов (`log-call-wrap`, `message-text`, `comment-typography`): текст вызова без пробелов, `*` — любые символы (`*.Logger.Error`) |
| `logger.bypass` | вызовы в обход логгера проекта; признак [`project-mechanism`](#project-mechanism) предлагает заменить их на `logger.methods`. Требует `logger.methods` |
| `mechanisms` | механизмы проекта для `project-mechanism`: имя → `use` (чем заменить) и признаки повтора: `types` (маски имён типов), `fields` + `methods` (тип с полем такого типа и методом на каждую маску), `calls` (вызовы в обход), `creates` (`new` в обход), `excludePaths` (где лежит сам механизм). Нужен `use` и хотя бы один из `types`, `fields`, `methods`, `calls`, `creates` |

### `metrics.jsonc`

Метрики — сведения, не находки: порог задаёт счёт «выше порога» в сводке и пометку в топе, код возврата не меняет.

```jsonc
// Пример конфига csr metrics: csr metrics <папка> --config metrics.jsonc --project project.jsonc
// Метрики - сведения, не находки: порог задает счет "выше порога" в сводке, код возврата не меняет
{
  // Размер топа по каждой метрике
  "top": 10,

  // Уровни: function, type, file. У метрики: "enabled" (по умолчанию true) и "threshold" (null - без порога)
  "function": {
    "cyclomatic": { "threshold": 10 },
    "cognitive": { "threshold": 15 },
    "lines": { "threshold": 60 },
    "code-lines": { "threshold": null },
    "nesting": { "threshold": 3 },
    "parameters": { "threshold": 5 }
  },
  "type": {
    "lines": { "threshold": null },
    "code-lines": { "threshold": null },
    "members": { "threshold": null },
    "regions": { "enabled": false }
  },
  "file": {
    "lines": { "threshold": 400 },
    "code-lines": { "threshold": 316 },
    "types": { "threshold": null }
  }
}
```

Уровни: `function`, `type`, `file`. У метрики — `enabled` (по умолчанию `true`) и `threshold` (`null` — без порога). Пороги по умолчанию — в разделе [6](#6-метрики-metrics).

### `similar.jsonc`

Параметры `csr similar`. Ключи командной строки перекрывают значения из конфига; ключ, которого нет в конфиге, берётся по умолчанию.

```jsonc
// Пример конфига csr similar: csr similar <папка> --config similar.jsonc
// Ключи командной строки (--kinds, --threshold, --min-lines, --min-nodes, --min-shapes, --fragments, --set-threshold,
// --min-shared, --type-threshold) перекрывают значения отсюда.
// Описание - DOCS.md, раздел csr similar
{
  // Виды единиц: member, fragment (L1), parameters, fields (L2), type (L3), system (L4; нужна компиляция файлов)
  "kinds": ["member", "fragment", "parameters", "fields", "type"],

  // L1: порог коэффициента Жаккара по отпечаткам формы (0..1)
  "threshold": 0.82,

  // Единица меньше любого из минимумов не сравнивается: строки, узлы формы, разные поддеревья формы
  "minLines": 4,
  "minNodes": 20,
  "minShapes": 20,

  // Операторов в окне фрагмента; 0 - без фрагментов
  "fragmentStatements": 4,

  // L2: порог наборов, наименьшее число общих элементов, синонимы имен (в нижнем регистре)
  "setThreshold": 0.5,
  "minShared": 3,
  "synonyms": { "pos": "position", "dir": "direction" },

  // L3: порог типов, наименьшее число полей и членов типа, общее ядро (членов одной формы; 0 - не искать), веса
  "typeThreshold": 0.75,
  "minTypeMembers": 4,
  "minCoreMembers": 4,
  "weights": { "bases": 1, "overrides": 1, "fields": 1, "signatures": 1, "bodies": 2 },

  // L4: порог систем, наименьшее число типов системы
  "systemThreshold": 0.4,
  "minSystemTypes": 4,

  // Маски путей файлов (* - любые символы, разделитель /); путь от текущей папки проверяется и с ведущим /
  "paths": [],
  "excludePaths": ["*/Tests/*", "*.g.cs", "*.Designer.cs"]
}
```

| Ключ | Значения | По умолчанию | Описание |
|---|---|---|---|
| `kinds` | список: `member`, `fragment`, `parameters`, `fields`, `type`, `system` | все, кроме `system` | виды единиц |
| `threshold` | число от 0 до 1 | `0.82` | порог оценки пары членов и фрагментов (L1) |
| `minLines` | целое ≥ 0 | `4` | наименьшее число строк единицы |
| `minNodes` | целое ≥ 0 | `20` | наименьшее число узлов формы |
| `minShapes` | целое ≥ 0 | `20` | наименьшее число разных поддеревьев формы |
| `fragmentStatements` | целое ≥ 0 | `4` | операторов в окне фрагмента; меньше 2 — без фрагментов |
| `setThreshold` | число от 0 до 1 | `0.5` | порог пары наборов (L2) |
| `minShared` | целое ≥ 0 | `3` | наименьшее число общих элементов пары наборов (и наименьший набор) |
| `synonyms` | объект «имя → имя» | `{}` | синонимы имён элементов набора, без учёта регистра |
| `typeThreshold` | число от 0 до 1 | `0.75` | порог пары типов (L3) |
| `minTypeMembers` | целое ≥ 0 | `4` | наименьшее число полей и членов типа |
| `minCoreMembers` | целое ≥ 0 | `4` | пара типов по общему ядру: членов одной формы не меньше; `0` — не искать |
| `weights` | объект: `bases`, `overrides`, `fields`, `signatures`, `bodies` | `1`, `1`, `1`, `1`, `2` | веса частей оценки типа |
| `systemThreshold` | число от 0 до 1 | `0.4` | порог пары систем (L4) |
| `minSystemTypes` | целое ≥ 0 | `4` | наименьшее число типов системы (хост, базы, варианты, результаты) |
| `paths` | список масок | `[]` | файлы области; пусто — все файлы цели |
| `excludePaths` | список масок | `[]` | исключённые файлы (сгенерированный код, тесты) |

Значения проверяются по одним правилам в конфиге и в опциях командной строки с тем же смыслом (`--threshold`, `--min-lines` и др.): целые — не меньше 0, пороги — от 0 до 1 (в командной строке — с точкой или запятой). Ошибка — код возврата 2.

### `structure.jsonc`

Пороги отметок `csr structure`. Ключ `--top` командной строки перекрывает `top`.

```jsonc
// Пример конфига csr structure: csr structure <папка> --config structure.jsonc
// Пороги - отметки в отчете (вход аудита), не находки. Описание - DOCS.md, раздел csr structure
{
  // Глубина вложенности владения: больше - отметка
  "ownershipDepth": 3,

  // Глубина наследования среди типов исходного кода: больше - отметка
  "inheritanceDepth": 4,

  // Объект-бог (Lanza - Marinescu): WMC не меньше, ATFD больше, TCC меньше
  "godWmc": 47,
  "godAtfd": 5,
  "godTcc": 0.33,

  // Свалка: папка не меньше dumpSize типов и сообществ не меньше dumpCommunities (или половина типов без связей)
  "dumpSize": 30,
  "dumpCommunities": 8,

  // Глубина владения: база с больше чем infrastructureHosts хостами или registryVariants вариантами - не уровень
  "infrastructureHosts": 5,
  "registryVariants": 30,

  // Размер топов отчета
  "top": 10
}
```

| Ключ | Значения | По умолчанию | Описание |
|---|---|---|---|
| `ownershipDepth` | целое ≥ 0 | `3` | глубина вложенности владения: больше — отметка |
| `inheritanceDepth` | целое ≥ 0 | `4` | глубина наследования среди типов исходного кода: больше — отметка |
| `godWmc` | целое ≥ 0 | `47` | объект-бог: WMC не меньше |
| `godAtfd` | целое ≥ 0 | `5` | объект-бог: ATFD больше |
| `godTcc` | число от 0 до 1 | `0.33` | объект-бог: TCC меньше |
| `dumpSize` | целое ≥ 0 | `30` | свалка: наименьшее число типов папки |
| `dumpCommunities` | целое ≥ 0 | `8` | свалка: сообществ не меньше |
| `infrastructureHosts` | целое ≥ 0 | `5` | глубина владения: база с большим числом хостов — не уровень |
| `registryVariants` | целое ≥ 0 | `30` | глубина владения: база с большим числом вариантов — не уровень |
| `top` | целое ≥ 0 | `10` | размер топов; `0` — все |

### `refactor.jsonc`

Параметры `csr rename`: атрибуты сериализации проекта, расширения и папки поиска строк, лимиты.

```jsonc
// Параметры csr rename: сериализуемые имена проекта и поиск строковых совпадений
{
  "serialization": {
    // Атрибуты проекта, при которых имя члена пишется в данные: переименование - отказ.
    // Встроенные: SerializeField, SerializeReference, публичные поля [Serializable] и MonoBehaviour (Unity),
    // [JsonProperty] и [DataMember] без явного имени
    "attributes": ["MyGame.Persistence.SavedAttribute"]
  },

  "strings": {
    // Файлы, где ищется старое имя целым словом (в .cs - только строковые литералы)
    "extensions": [".lua", ".json", ".yaml", ".yml", ".xml"],

    // Ассеты Unity: только для имен, которые могут быть в сериализованных данных
    "unityExtensions": [".prefab", ".asset", ".unity"],

    // Папки поиска относительно папки решения; пусто - папка решения
    "roots": [],

    // Имена папок, которые пропускаются
    "exclude": ["Library", "Temp", "Logs", "obj", "bin", ".git", ".vs", ".idea", "node_modules"],

    // Файлы больше порога не читаются (число таких файлов - в отчете)
    "maxFileSizeKb": 20480,

    // Сколько совпадений показать (всего - в отчете)
    "maxMatches": 200
  }
}
```

### Где искать конфиги

Если `--config` или `--project` заданы, используются только они: `--config` — слои по порядку, как перечислены. Без них `csr` ищет конфиги сам. Два места:

- **По умолчанию** — папка `config` рядом с `csr.exe`: `config/style.jsonc`, `config/arch.jsonc`, `config/project.jsonc`, `config/metrics.jsonc`, `config/refactor.jsonc`, `config/similar.jsonc`, `config/structure.jsonc`. Туда её кладёт установщик ([README.md](README.md#установка)): из релиза — `style.jsonc` и `arch.jsonc` без проверок (`"checks": {}`), если папки ещё нет; параметр `-Configs` — свои конфиги. С конфигами из релиза проверки включает только проектный слой `.csr`.
- **Проектный** — папка `.csr` с файлами тех же имён: `.csr/style.jsonc` и т. д. Поиск идёт от папки цели вверх: папка первого аргумента (у файла — его папка; у `csr arch --solution` и `csr rename` — папка решения). Берётся ближайшая `.csr`, где есть нужный файл. Поиск останавливается на корне репозитория — папке с `.git` (она проверяется последней), выше не идёт.

Как места сочетаются:

| Конфиг | Без `--config` / `--project` |
|---|---|
| `style.jsonc` (`style`, `fix`), `arch.jsonc` (`arch`) | два слоя: сначала по умолчанию, затем проектный (накладывается сверху: [слои](#слои-конфигов)); если нашёлся только один — он один |
| `project.jsonc`, `metrics.jsonc`, `refactor.jsonc`, `similar.jsonc`, `structure.jsonc` | не накладываются: проектный, иначе по умолчанию; нет ни одного — значения встроенные |

Не нашёлся ни один слой `style.jsonc` или `arch.jsonc` — код 2 с перечислением, где искали. Типовое разделение: личные правила — в `config` рядом с `csr.exe` (общие для всех проектов), отличия проекта — в `.csr` в репозитории.

Пример. Репозиторий (есть `.git`): `.csr/style.jsonc` в корне, код в `src/Demo.cs`. Слой по умолчанию (`config/style.jsonc` рядом с `csr.exe`):

```jsonc
// config/style.jsonc рядом с csr.exe: слой по умолчанию
{
  "checks": {
    "explicit-private": { "severity": "warning", "rule": "S23" },
    "null-check": { "severity": "warning", "rule": "I5", "prefer": "pattern" }
  }
}
```

Проектный слой `.csr/style.jsonc`:

```jsonc
{
  "checks": {
    "null-check": { "severity": "error", "prefer": "equality" }
  }
}
```

Файл `src/Demo.cs`:

```csharp
public class Report
{
    int m_Count;

    public bool Has(object item)
    {
        return item is null;
    }
}
```

`csr style src --format md` из корня репозитория (без `--config`): слой по умолчанию задал `explicit-private` и метку `I5`, проектный слой сменил форму `null-check` на `equality` и уровень на `error`:

````markdown
- 3: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private int m_Count;
  ```
- 7: `null-check` I5 (error) - 'null' -> '== null'
  ```csharp
          return item == null;
  ```
````

Тот же файл вне репозитория с `.csr` — применяется только слой по умолчанию. Если нет ни слоя по умолчанию, ни `.csr`:

````text
csr: не задан --config, нет .csr/style.jsonc в папке цели или выше и config/style.jsonc рядом с csr.exe
````

## 4. Проверки `style`

Проверки по синтаксису одного файла. Каждый подраздел: что находит; параметры с допустимыми значениями и значениями по умолчанию; пример — конфиг, код «было», находки, код «стало» после `csr fix`; ограничения.

Общие ключи любой проверки — `severity`, `rule`, `enabled` (см. [раздел 3](#3-конфиги)) — в таблицах параметров не повторяются. Примеры запускались командами `csr style Demo.cs --config config.jsonc [--project project.jsonc]` и `csr fix Demo.cs --config config.jsonc --apply`; у проверок с уровнем `hint` в примере добавлен `--hints`.

### Форматирование

#### `line-length`

Строка длиннее `max` символов. Длина считается по тексту строки с отступом; табуляция — один символ.

**Замена:** нет. Перенос строки выбирает проверка своего вида (`call-wrap`, `signature-wrap`, `condition-wrap`, `ternary-wrap`, `initializer-layout`, `binary-op-wrap`) или агент.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `max` | целое число | `120` | наибольшая длина строки в символах |
| `ignoreComments` | `true` / `false` | `true` | не проверять строки, которые начинаются с комментария |
| `ignoreStringLines` | `true` / `false` | `true` | не проверять строки, которые начинаются со строкового литерала (перенесенный текст сообщения) |

**Пример**

В примере `max` = 60, чтобы строка была короткой; по умолчанию — 120.

Конфиг:

```jsonc
{ "checks": { "line-length": { "severity": "error", "rule": "F20", "max": 60 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public string Describe(int count, string name)
    {
        var text = "Elements: " + count + ", owner: " + name + ", state: ready";
        // A long comment line is skipped by default (ignoreComments) because it starts with a comment
        return text;
    }
}
```

Находки (`--format md`):

````markdown
- 5: `line-length` F20 (error) - строка длиннее 60 символов: 80
````

`csr fix` код не меняет: у находок нет замены.

**Вариант `ignoreComments: false`, `max: 40`**

Конфиг:

```jsonc
{ "checks": { "line-length": { "severity": "error", "max": 40, "ignoreComments": false, "ignoreStringLines": true } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    // A long comment line that is checked here
    private string m_Text =
        "a long string literal that starts the line";
    private int m_Count = 1000000 + 2000000 + 3000000;
}
```

Находки (`--format md`):

````markdown
- 3: `line-length` (error) - строка длиннее 40 символов: 47
- 6: `line-length` (error) - строка длиннее 40 символов: 54
````

`csr fix` код не меняет: у находок нет замены.


**Ограничения**

- Строка, которая начинается с комментария, не проверяется (`ignoreComments`).
- Строка, которая начинается со строкового литерала (перенесённый текст сообщения), не проверяется (`ignoreStringLines`).

#### `directive-column`

Директива препроцессора (`#region`, `#endregion`, `#if`, `#else`, `#endif`, `#pragma` …) не в колонке 0.

**Замена:** есть. Замена убирает отступ перед `#`.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `directives` | список строк | `[]` | директивы без # (region, endregion, if, ...); пусто - все |

**Пример**

Конфиг:

```jsonc
{ "checks": { "directive-column": { "severity": "warning" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    #region Fields
    private int m_Count;
    #endregion
}
```

Находки (`--format md`):

````markdown
- 3: `directive-column` (warning) - '#region' не с колонки 0
  ```csharp
  #region Fields
  ```
- 5: `directive-column` (warning) - '#endregion' не с колонки 0
  ```csharp
  #endregion
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
#region Fields
    private int m_Count;
#endregion
}
```

**Вариант `directives: ["region", "endregion"]`**

Конфиг:

```jsonc
{ "checks": { "directive-column": { "severity": "warning", "directives": ["region", "endregion"] } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    #region Fields
    private int m_Count;
    #endregion

    #if DEBUG
    private int m_Debug;
    #endif
}
```

Находки (`--format md`):

````markdown
- 3: `directive-column` (warning) - '#region' не с колонки 0
  ```csharp
  #region Fields
  ```
- 5: `directive-column` (warning) - '#endregion' не с колонки 0
  ```csharp
  #endregion
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
#region Fields
    private int m_Count;
#endregion

    #if DEBUG
    private int m_Debug;
    #endif
}
```


**Ограничения**

- Параметр `directives` ограничивает проверку перечисленными директивами (без `#`); пустой список — все.

#### `binary-op-wrap`

Бинарный оператор из `operators` стоит не там, где требует `prefer`: в конце строки при `leading` или в начале строки продолжения при `trailing`. Находка занимает две строки: оператор и соседнюю.

**Замена:** есть. Замена переносит оператор на соседнюю строку. Отступ строки продолжения не меняется (его правит `condition-wrap`).

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `leading` / `trailing` | `"leading"` | leading - оператор в начале строки, trailing - в конце |
| `operators` | список строк | `["&&", "||"]` | проверяемые операторы |

**Пример**

Конфиг:

```jsonc
{ "checks": { "binary-op-wrap": { "severity": "warning", "rule": "F24", "prefer": "leading" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public bool IsReady(int count, bool enabled, bool visible)
    {
        return count > 0 &&
            enabled &&
            visible;
    }
}
```

Находки (`--format md`):

````markdown
- 5-6: `binary-op-wrap` F24 (warning) - '&&' в конце строки - перенести в начало следующей
  ```csharp
          return count > 0
              && enabled &&
  ```
- 6-7: `binary-op-wrap` F24 (warning) - '&&' в конце строки - перенести в начало следующей
  ```csharp
              enabled
              && visible;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public bool IsReady(int count, bool enabled, bool visible)
    {
        return count > 0
            && enabled
            && visible;
    }
}
```

**Ограничения**

- Проверяются только операторы из `operators`; по умолчанию `&&` и `||`.

### Комментарии и тексты

#### `xml-doc`

Документационный комментарий `///` или `/** */`. Проверка включается в конфигах, где пояснения пишутся обычным комментарием `//`.

**Замена:** есть у части находок. Замена зависит от `explanation`. `comment` (по умолчанию): текст `<summary>` — одной строкой `//`, если в блоке нет других текстов (`param`, `returns`, `remarks`) и строка помещается в `max`; блок без текста удаляется. `delete` — блок удаляется всегда. `none` — замены нет.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `explanation` | `comment` / `delete` / `none` | `"comment"` | comment - summary в строку //, delete - удалить блок, none - без замены |
| `max` | целое число | `120` | длина строки // для explanation: comment |

**Пример**

Применено с `--hints`: в примере уровень `hint`. Блок с `<param>` замены не получил, пустой — удалён.

Конфиг:

```jsonc
{ "checks": { "xml-doc": { "severity": "hint", "explanation": "comment" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    /// <summary>
    /// Number of elements.
    /// </summary>
    public int Count;

    /// <summary>Adds an item.</summary>
    /// <param name="item">Item to add.</param>
    public void Add(int item)
    {
    }

    /// <summary>
    /// </summary>
    public int Total;
}
```

Находки (`--format md`):

````markdown
- 3-5: `xml-doc` (hint) - XML-документация
  ```csharp
      // Number of elements
  ```
- 8-9: `xml-doc` (hint) - XML-документация
- 14-15: `xml-doc` (hint) - XML-документация
  - замена: удалить строки
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
public class Report
{
    // Number of elements
    public int Count;

    /// <summary>Adds an item.</summary>
    /// <param name="item">Item to add.</param>
    public void Add(int item)
    {
    }

    public int Total;
}
```

Осталось без замены: 1.

**Вариант `explanation: delete`**

Конфиг:

```jsonc
{ "checks": { "xml-doc": { "severity": "warning", "explanation": "delete" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    /// <summary>
    /// Number of elements.
    /// </summary>
    public int Count;
}
```

Находки (`--format md`):

````markdown
- 3-5: `xml-doc` (warning) - XML-документация
  - замена: удалить строки
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public int Count;
}
```


**Ограничения**

- Блок с `param`, `returns`, `remarks` или `summary` длиннее `max` остаётся без замены: текст не теряется, пересказ имени агент удаляет сам.

#### `comment-separator`

Комментарий `//` на отдельной строке, который начинается с серии знаков `- = * _ # ~ + /` длиной не меньше `minRun`: `// --- Fields ---`, `// ======`. Вместо разделителей — `#region`.

**Замена:** нет. Подпись разделителя может быть названием региона, решает агент.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `minRun` | целое число | `3` | наименьшая длина серии знаков - = * _ # ~ + / в начале комментария |

**Пример**

Конфиг:

```jsonc
{ "checks": { "comment-separator": { "severity": "warning" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    // --- Fields ---
    private int m_Count;

    // ==========
    public int Count => m_Count;
}
```

Находки (`--format md`):

````markdown
- 3: `comment-separator` (warning) - комментарий-разделитель
- 6: `comment-separator` (warning) - комментарий-разделитель
````

`csr fix` код не меняет: у находок нет замены.

**Ограничения**

- Серия короче `minRun` не считается: `// -- note` не находка при значении по умолчанию 3.
- Комментарий в конце строки с кодом не проверяется.

#### `comment-typography`

Типографские знаки (`—`, `–`, `«»`, `→`, `×`, `≈`, `≤`, `≥`) в комментариях и в текстах: вызовах логгера (методы — в `project.jsonc`, `logger.methods`), атрибутах-подсказках из `attributes`, методах `ToString`. Знак и замена — карта `replace`.

**Замена:** есть. Замена подставляет знак из `replace` по месту: в комментарии — как есть, в строковом литерале — с экранированием кавычки (`\"` в обычной строке, `""` в verbatim).

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `replace` | объект «знак: замена» | объект из 9 записей (ниже) | знак и его замена |
| `attributes` | список строк | `["Tooltip"]` | атрибуты, строки которых проверяются |
| `logs` | `true` / `false` | `true` | проверять тексты вызовов логгера (методы - в project.jsonc) |
| `toString` | `true` / `false` | `true` | проверять строки внутри методов ToString |

<details>
<summary>Значение `replace` по умолчанию</summary>

```json
{
  "—": "-",
  "–": "-",
  "«": "\"",
  "»": "\"",
  "→": "->",
  "×": "*",
  "≈": "~",
  "≤": "<=",
  "≥": ">="
}
```

</details>

**Пример**

Для примера задан логгер `Log.Info` в `project.jsonc` (`--project project.jsonc`).

Конфиг:

```jsonc
{ "checks": { "comment-typography": { "severity": "warning", "rule": "C14", "attributes": ["Tooltip"] } } }
```

`project.jsonc`:

```jsonc
{
  "logger": {
    "methods": [
      "Log.Info",
      "Log.Warning",
      "Log.Error"
    ]
  }
}
```

Было (`Demo.cs`):

```csharp
public class Report
{
    // Radius — in meters, range ≤ 10 → clamped
    [Tooltip("Radius — in meters")]
    public float Radius;

    public void Run()
    {
        Log.Info("Size ≤ limit");
    }

    public override string ToString()
    {
        return "Report × 2";
    }
}
```

Находки (`--format md`):

````markdown
- 3: `comment-typography` C14 (warning) - типографские знаки: '—' -> '-', '→' -> '->', '≤' -> '<='
  ```csharp
      // Radius - in meters, range <= 10 -> clamped
  ```
- 4: `comment-typography` C14 (warning) - типографские знаки: '—' -> '-'
  ```csharp
      [Tooltip("Radius - in meters")]
  ```
- 9: `comment-typography` C14 (warning) - типографские знаки: '≤' -> '<='
  ```csharp
          Log.Info("Size <= limit");
  ```
- 14: `comment-typography` C14 (warning) - типографские знаки: '×' -> '*'
  ```csharp
          return "Report * 2";
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    // Radius - in meters, range <= 10 -> clamped
    [Tooltip("Radius - in meters")]
    public float Radius;

    public void Run()
    {
        Log.Info("Size <= limit");
    }

    public override string ToString()
    {
        return "Report * 2";
    }
}
```

**Ограничения**

- Тексты логов проверяются, если вызов логгера описан в `project.jsonc`; без `logger.methods` проверка логов ничего не находит.
- Карта `replace` заменяет значение по умолчанию целиком: чтобы сменить один знак, перечисли все нужные.

#### `comment-ending`

Однострочный комментарий `//` с точкой в конце (при `prefer: no-period`) или без неё (при `period`); буквы из карты `letters` (по умолчанию `ё` → `е`).

**Замена:** есть. Замена убирает или добавляет точку и подставляет буквы.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `no-period` / `period` / `any` | `"no-period"` | no-period - без точки в конце, period - с точкой, any - не проверять |
| `letters` | объект «знак: замена» | `{"ё": "е", "Ё": "Е"}` | буквы и их замена |
| `standaloneOnly` | `true` / `false` | `true` | точку проверять только у отдельного комментария: без строк-комментариев выше и ниже |

**Пример**

Применено с `--hints`: в примере уровень `hint`. Два последовательных комментария точку сохранили.

Конфиг:

```jsonc
{ "checks": { "comment-ending": { "severity": "hint", "prefer": "no-period" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    // Counts the elements.
    private int m_Count;

    // Takes the first item.
    // Throws if empty.
    public int First => m_Count;

    // Берёт второй элемент
    public int Second => m_Count;
}
```

Находки (`--format md`):

````markdown
- 3: `comment-ending` (hint) - точка в конце комментария
  ```csharp
      // Counts the elements
  ```
- 10: `comment-ending` (hint) - 'ё' -> 'е'
  ```csharp
      // Берет второй элемент
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
public class Report
{
    // Counts the elements
    private int m_Count;

    // Takes the first item.
    // Throws if empty.
    public int First => m_Count;

    // Берет второй элемент
    public int Second => m_Count;
}
```

**Вариант `prefer: period`**

Конфиг:

```jsonc
{ "checks": { "comment-ending": { "severity": "warning", "prefer": "period" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    // Counts the elements
    private int m_Count;
}
```

Находки (`--format md`):

````markdown
- 3: `comment-ending` (warning) - нет точки в конце комментария
  ```csharp
      // Counts the elements.
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    // Counts the elements.
    private int m_Count;
}
```


**Ограничения**

- При `standaloneOnly: true` точка проверяется только у отдельного комментария: без строк-комментариев выше и ниже. Многострочный комментарий из нескольких строк `//` не трогается.
- `prefer: any` — только замена букв.

#### `commented-code`

Комментарий `//`, который разбирается как операторы или члены C#: кандидат — строка с признаком кода (`;`, `{`, `}` в конце, ключевое слово оператора в начале); подряд идущие строки, которые вместе разбираются как код, — одна находка. Код в хвосте строки после кода — отдельная находка.

**Замена:** есть у части находок. Замена удаляет строки комментария-кода; у хвоста — оставляет код строки. По умолчанию `csr fix` такие замены не применяет (удаление строк — суждение): нужен `--hints` при уровне `hint`.

**Параметры**

Собственных параметров нет.

**Пример**

Применено с `--hints`.

Конфиг:

```jsonc
{ "checks": { "commented-code": { "severity": "hint" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    private int m_Count;

    public void Run()
    {
        // m_Count = 0;
        // Reset();
        m_Count++; // m_Total++;
        // Counts the elements
    }
}
```

Находки (`--format md`):

````markdown
- 7-8: `commented-code` (hint) - закомментированный код
  - замена: удалить строки
- 9: `commented-code` (hint) - закомментированный код в конце строки
  ```csharp
          m_Count++;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
public class Report
{
    private int m_Count;

    public void Run()
    {
        m_Count++;
        // Counts the elements
    }
}
```

**Ограничения**

- Распознавание синтаксическое: комментарий-пояснение без `;` и без ключевого слова (`// Counts the elements`) не считается кодом.
- Параметров нет.

#### `todo-format`

Комментарий `// TODO` (и `FIXME`) не в форме `// TODO: <что сделать>`; при `forbidMetadata` — с автором (`(ivan)`, `@ivan`), датой или номером задачи (`PROJ-123`, `#123`); при `requireCyrillic` — с текстом без кириллицы.

**Замена:** есть у части находок. Замена приводит префикс к форме `// TODO: текст`, когда это единственная неточность. Автора, дату и номер задачи проверка не удаляет.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `forbidMetadata` | `true` / `false` | `true` | автор, дата, номер задачи в TODO - находка |
| `requireCyrillic` | `true` / `false` | `false` | текст TODO - на кириллице |

**Пример**

Применено с `--hints`: в примере уровень `hint`.

Конфиг:

```jsonc
{ "checks": { "todo-format": { "severity": "hint", "forbidMetadata": true } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run()
    {
        // TODO: add a retry
        // todo fix this
        // TODO(ivan): handle empty list 2024-05-01
        // TODO PROJ-123 cache the result
    }
}
```

Находки (`--format md`):

````markdown
- 6: `todo-format` (hint) - ожидается 'TODO: '
  ```csharp
          // TODO: fix this
  ```
- 7: `todo-format` (hint) - ожидается 'TODO: '; автор в TODO; дата в TODO
- 8: `todo-format` (hint) - ожидается 'TODO: '; номер задачи в TODO
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
public class Report
{
    public void Run()
    {
        // TODO: add a retry
        // TODO: fix this
        // TODO(ivan): handle empty list 2024-05-01
        // TODO PROJ-123 cache the result
    }
}
```

Осталось без замены: 2.

**Ограничения**

- Находка с метаданными или с несколькими замечаниями остаётся без замены.

#### `log-call-wrap`

Вызов метода логгера (`logger.methods` из `project.jsonc`) в несколько строк либо в одну строку длиннее `maxLength`. Допустим перенос всех аргументов на следующую строку после `(`. Длинный текст делится на части по строкам: части — строковые литералы и интерполяции, `+` в конце строки, каждая следующая часть с новой строки; другие переносы в вызове — находка. Если после деления весь текст помещается в `maxLength` одной строкой, находка требует записать его без склейки.

**Замена:** нет. Сначала сокращается текст сообщения, затем переносится аргумент — решает агент.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `maxLength` | целое число | `0` | наибольшая длина строки с однострочным вызовом лога; 0 - не проверять |

**Пример**

Для примера задан логгер `Log.Info` в `project.jsonc`; `maxLength` = 60.

Конфиг:

```jsonc
{ "checks": { "log-call-wrap": { "severity": "warning", "maxLength": 60 } } }
```

`project.jsonc`:

```jsonc
{
  "logger": {
    "methods": [
      "Log.Info",
      "Log.Warning",
      "Log.Error"
    ]
  }
}
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(int count)
    {
        Log.Info("Started");
        Log.Info(
            "Started",
            count);
        Log.Info("Processed " + count + " elements in the current pass");
        Log.Info(
            "Finished");
        Log.Info(
            "Processed the whole batch of elements in the current pass " +
            "and stored the result");
        Log.Info(
            "Processed " +
            "elements");
    }
}
```

Находки (`--format md`):

````markdown
- 6-8: `log-call-wrap` (warning) - вызов лога не в одну строку
- 9: `log-call-wrap` (warning) - лог в строке длиннее 60: 73 - сократить текст
- 15-17: `log-call-wrap` (warning) - текст лога помещается в строку - одной строкой без склейки
````

`csr fix` код не меняет: у находок нет замены.

**Ограничения**

- Без `logger.methods` проверка ничего не находит.
- `maxLength: 0` (по умолчанию) отключает проверку длины и проверку «помещается в строку».
- Текст, разделённый по строкам, не считается конкатенацией и для `message-text`.

#### `message-text`

Текст вызова логгера (`logger.methods`) или исключения в `throw new …Exception(…)`: склейка строк через `+` вместо интерполяции `$"…"`; точка в конце текста. Текст, разделённый по строкам (части — строковые литералы и интерполяции, `+` в конце строки, каждая следующая часть с новой строки), — не конкатенация.

**Замена:** есть у части находок. Замена убирает точку в конце текста. Склейку в интерполяцию проверка не переписывает.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `concatenation` | `true` / `false` | `true` | склейка строк через + - находка |
| `trailingPeriod` | `true` / `false` | `true` | точка в конце текста - находка |
| `exceptions` | `true` / `false` | `true` | проверять тексты исключений в throw |

**Пример**

Для примера задан логгер `Log.Info`, `Log.Warning`, `Log.Error` в `project.jsonc`.

Конфиг:

```jsonc
{ "checks": { "message-text": { "severity": "warning" } } }
```

`project.jsonc`:

```jsonc
{
  "logger": {
    "methods": [
      "Log.Info",
      "Log.Warning",
      "Log.Error"
    ]
  }
}
```

Было (`Demo.cs`):

```csharp
using System;

public class Report
{
    public void Run(int count, string name)
    {
        Log.Info("Processed " + count + " elements");
        Log.Warning($"Bad name {name}.");
        throw new InvalidOperationException("Bad state.");
        Log.Info(
            "Processed the whole batch " +
            "of elements");
    }
}
```

Находки (`--format md`):

````markdown
- 7: `message-text` (warning) - текст склеен через + - нужна интерполяция $"..."
- 8: `message-text` (warning) - точка в конце текста
  ```csharp
          Log.Warning($"Bad name {name}");
  ```
- 9: `message-text` (warning) - точка в конце текста
  ```csharp
          throw new InvalidOperationException("Bad state");
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
using System;

public class Report
{
    public void Run(int count, string name)
    {
        Log.Info("Processed " + count + " elements");
        Log.Warning($"Bad name {name}");
        throw new InvalidOperationException("Bad state");
        Log.Info(
            "Processed the whole batch " +
            "of elements");
    }
}
```

Осталось без замены: 1.

**Ограничения**

- Склейка — находка без замены.
- Разделённый по строкам текст (`"…" +` и следующая часть на новой строке) не находка.
- `exceptions: false` отключает проверку исключений.

### Идиомы и типы

#### `null-check`

Проверка на `null` не в той форме, которую задаёт `prefer`: `equality` — `== null` и `!= null`, `pattern` — `is null` и `is not null`.

**Замена:** есть. Замена меняет форму проверки.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `equality` / `pattern` | `"equality"` | equality - == null и != null, pattern - is null и is not null |

**Пример**

Конфиг:

```jsonc
{ "checks": { "null-check": { "severity": "warning", "prefer": "equality" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(object item, object other)
    {
        if (item is null)
            return;
        if (other is not null)
            Use(other);
    }

    private void Use(object value)
    {
    }
}
```

Находки (`--format md`):

````markdown
- 5: `null-check` (warning) - 'null' -> '== null'
  ```csharp
          if (item == null)
  ```
- 7: `null-check` (warning) - 'not null' -> '!= null'
  ```csharp
          if (other != null)
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public void Run(object item, object other)
    {
        if (item == null)
            return;
        if (other != null)
            Use(other);
    }

    private void Use(object value)
    {
    }
}
```

**Вариант `prefer: pattern`**

Конфиг:

```jsonc
{ "checks": { "null-check": { "severity": "warning", "prefer": "pattern" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public bool Has(object item, object other)
    {
        return item == null || other != null;
    }
}
```

Находки (`--format md`):

````markdown
- 5: `null-check` (warning) - '== null' -> 'is null'
  ```csharp
          return item is null || other != null;
  ```
- 5: `null-check` (warning) - '!= null' -> 'is not null'
  ```csharp
          return item == null || other is not null;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public bool Has(object item, object other)
    {
        return item is null || other is not null;
    }
}
```


**Ограничения**

- Проверка синтаксическая: `==` с пользовательским оператором (объекты Unity) от `is null` не отличается; выбор формы — за конфигом.

#### `coalesce-assignment`

`if (x == null) x = value;` без `else` — запись через `??=`.

**Замена:** есть. Замена пишет `x ??= value;`. По умолчанию `csr fix` её не применяет при уровне `hint`: у объектов Unity сравнение с `null` перегружено, `??=` не равнозначно — решение за агентом (`--hints`).

**Параметры**

Собственных параметров нет.

**Пример**

Применено с `--hints`.

Конфиг:

```jsonc
{ "checks": { "coalesce-assignment": { "severity": "hint" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Report
{
    private List<int> m_Items;

    public void Prepare()
    {
        if (m_Items == null)
            m_Items = new List<int>();
    }
}
```

Находки (`--format md`):

````markdown
- 9-10: `coalesce-assignment` (hint) - -> 'm_Items ??= new List<int>();'
  ```csharp
          m_Items ??= new List<int>();
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
using System.Collections.Generic;

public class Report
{
    private List<int> m_Items;

    public void Prepare()
    {
        m_Items ??= new List<int>();
    }
}
```

**Ограничения**

- Семантики нет: тип `x` проверка не знает, поэтому уровень находки задаёт конфиг.
- Параметров нет.

#### `out-var`

Объявление переменной в `out`-аргументе не в форме `prefer`: `explicit` — `out int x`, `var` — `out var x`. При `discardUnused` переменная, которую дальше по коду функции не читают, — находка с заменой на `out _`.

**Замена:** есть у части находок. Замена на `out _` есть. Замены `out var x` → `out T x` нет: тип проверка не знает (нет семантики). Замена `out T x` → `out var x` есть.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `explicit` / `var` | `"explicit"` | explicit - out T x, var - out var x |
| `discardUnused` | `true` / `false` | `true` | переменная, которую дальше по коду функции не читают, - находка с заменой на out _ |

**Пример**

Конфиг:

```jsonc
{ "checks": { "out-var": { "severity": "warning", "prefer": "explicit", "discardUnused": true } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public int Parse(string text, string other)
    {
        if (int.TryParse(text, out var value))
            return value;
        if (int.TryParse(other, out int unused))
            return 0;
        return -1;
    }
}
```

Находки (`--format md`):

````markdown
- 5: `out-var` (warning) - 'out var value' - тип явно
- 7: `out-var` (warning) - 'unused' дальше не читается -> 'out _'
  ```csharp
          if (int.TryParse(other, out _))
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public int Parse(string text, string other)
    {
        if (int.TryParse(text, out var value))
            return value;
        if (int.TryParse(other, out _))
            return 0;
        return -1;
    }
}
```

Осталось без замены: 1.

**Вариант `prefer: var`**

Конфиг:

```jsonc
{ "checks": { "out-var": { "severity": "warning", "prefer": "var" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public int Parse(string text)
    {
        if (int.TryParse(text, out int value))
            return value;
        return -1;
    }
}
```

Находки (`--format md`):

````markdown
- 5: `out-var` (warning) - 'out int' -> 'out var'
  ```csharp
          if (int.TryParse(text, out var value))
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public int Parse(string text)
    {
        if (int.TryParse(text, out var value))
            return value;
        return -1;
    }
}
```


**Ограничения**

- Чтение переменной ищется по имени в теле функции, без семантики.

#### `bool-local-type`

Локальная переменная с булевым инициализатором (сравнение, `&&`, `||`, `!`, `true`, `false`, `is`) объявлена не так, как задаёт `prefer`: `explicit` — `bool x = …`, `var` — `var x = …`.

**Замена:** есть. Замена меняет тип в объявлении.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `explicit` / `var` | `"explicit"` | explicit - bool x = ..., var - var x = ... |

**Пример**

Конфиг:

```jsonc
{ "checks": { "bool-local-type": { "severity": "warning", "prefer": "explicit" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(int count, bool enabled)
    {
        var ready = count > 0 && enabled;
        var name = "report";
        if (ready)
            Use(name);
    }

    private void Use(string value)
    {
    }
}
```

Находки (`--format md`):

````markdown
- 5: `bool-local-type` (warning) - 'var ready' -> 'bool ready'
  ```csharp
          bool ready = count > 0 && enabled;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public void Run(int count, bool enabled)
    {
        bool ready = count > 0 && enabled;
        var name = "report";
        if (ready)
            Use(name);
    }

    private void Use(string value)
    {
    }
}
```

**Вариант `prefer: var`**

Конфиг:

```jsonc
{ "checks": { "bool-local-type": { "severity": "warning", "prefer": "var" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(int count)
    {
        bool ready = count > 0;
    }
}
```

Находки (`--format md`):

````markdown
- 5: `bool-local-type` (warning) - 'bool ready' -> 'var ready'
  ```csharp
          var ready = count > 0;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public void Run(int count)
    {
        var ready = count > 0;
    }
}
```


**Ограничения**

- Булевость выводится из синтаксиса инициализатора; вызов метода, возвращающего `bool`, не считается булевым выражением.

#### `local-type`

Явный тип у локальной переменной или переменной `foreach`, где `var` даёт тот же тип (при `prefer: var`), либо `var` там, где нужен явный тип (при `prefer: explicit`).

**Замена:** есть у части находок. Замена `тип` → `var` есть. Замены `var` → явный тип нет: тип проверка не знает.

**Исключения** (при `prefer: var` явный тип остаётся, потому что замена сломала бы сборку или поменяла бы тип):
- `bool`, числовые типы, `object`, nullable, кортежи; `const`; несколько переменных в одном объявлении; переменная, которой дальше присваивается новое значение (`x = …`, `out x`, `ref x`).
- Инициализатор без типа: `null`, `default`, `[]`, `stackalloc`, лямбда, анонимный метод, `new` другого типа (объявлен базовый тип).
- Тернарник, если тип хотя бы одной ветки не виден в коде (вызов, поле, `null`): ветки разных типов с `var` не собираются (CS0173).
- Тип-делегат из `delegateTypes` и инициализатор-имя без вызова: `Action<T> handler = OnEvent;`, `Action<T> handler = o.Add;`. Это может быть группа методов. В C# 9 (Unity) у неё нет естественного типа, и `var` не соберётся. В C# 10+ выведенный тип может отличаться от объявленного.
- `foreach` с типом из `foreachExplicitTypes` (необобщённый `IEnumerable`) или с типом-делегатом. Пример: `foreach (Action<T> handler in evt.GetInvocationList())` — элемент приводится, и `var` дал бы `Delegate`.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `var` / `explicit` | `"var"` | var - var там, где тип виден справа или не важен; explicit - явный тип |
| `foreach` | `true` / `false` | `true` | проверять переменную foreach |
| `foreachExplicitTypes` | список строк | `[]` | типы элементов, у которых в foreach остается явный тип (необобщенный IEnumerable, например Transform) |
| `delegateTypes` | список строк | `["Action", "Action<*>", "Func<*>", "Predicate<*>", "Comparison<*>", "Converter<*>", "EventHandler", "EventHandler<*>"]` | маски типов-делегатов: явный тип остается при инициализаторе-имени (группа методов) и в foreach |

**Пример**

Применено с `--hints`: в примере уровень `hint`.

Конфиг:

```jsonc
{ "checks": { "local-type": { "severity": "hint", "prefer": "var" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Report
{
    public void Run(List<string> names)
    {
        List<string> copy = new List<string>(names);
        Dictionary<string, int> map = new Dictionary<string, int>();
        string first = names[0];
        foreach (string name in copy)
            map[name] = first.Length;
    }
}
```

Находки (`--format md`):

````markdown
- 7: `local-type` (hint) - 'List<string> copy' -> 'var copy'
  ```csharp
          var copy = new List<string>(names);
  ```
- 8: `local-type` (hint) - 'Dictionary<string, int> map' -> 'var map'
  ```csharp
          var map = new Dictionary<string, int>();
  ```
- 9: `local-type` (hint) - 'string first' -> 'var first'
  ```csharp
          var first = names[0];
  ```
- 10: `local-type` (hint) - 'foreach (string name' -> 'foreach (var name'
  ```csharp
          foreach (var name in copy)
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
using System.Collections.Generic;

public class Report
{
    public void Run(List<string> names)
    {
        var copy = new List<string>(names);
        var map = new Dictionary<string, int>();
        var first = names[0];
        foreach (var name in copy)
            map[name] = first.Length;
    }
}
```

**Вариант `prefer: explicit` (замены нет: тип проверка не знает)**

Конфиг:

```jsonc
{ "checks": { "local-type": { "severity": "hint", "prefer": "explicit" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Report
{
    public void Run(List<string> names)
    {
        var copy = new List<string>(names);
        var first = names[0];
        var count = names.Count;
    }
}
```

Находки (`--format md`):

````markdown
- 8: `local-type` (hint) - 'var first' - тип явно
- 9: `local-type` (hint) - 'var count' - тип явно
````

`csr fix` код не меняет: у находок нет замены.


**Что проверка не трогает (находок нет)**

Конфиг:

```jsonc
{ "checks": { "local-type": { "severity": "hint", "prefer": "var" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Report
{
    public void Run(List<string> names, object other)
    {
        int count = names.Count;
        bool ready = count > 0;
        object any = other;
        IEnumerable<string> items = new List<string>();
        string text;
        string name = null;
        List<string> copy = new List<string>();
        copy = null;
        const string Key = "k";
        var list = new List<int>();
    }
}
```

Находок нет: код проходит проверку без замечаний.


**Ограничения**

- Не проверяются: `bool` (его правит `bool-local-type`), числовые типы, `object`, nullable-типы, объявление без инициализатора, инициализатор `null`, `default`, `stackalloc`, `[]`, лямбда, `new` другого типа (объявлен базовый тип), `const`, несколько переменных в объявлении.
- Тернарник — только если тип каждой ветки виден в коде и равен объявленному (`new T`, приведение, `as T`, строковый литерал): ветки неизвестных типов (`null`, вызов, поле) могут быть разных типов, с `var` такой код не собирается.
- Переменная, которой дальше присваивается новое значение, не трогается: тип мог быть выбран под него. Передача как `out` / `ref` после объявления — тоже присваивание; `in` — чтение.
- Переменная `foreach`, тип элемента которой в `foreachExplicitTypes` (необобщённый `IEnumerable`, например `Transform`), остаётся с явным типом.

#### `tuple-type`

Кортеж-тип в сигнатуре (результат, параметр), поле, свойстве, событии. Вместо него — именованная структура или `out`.

**Замена:** нет. Имя и состав структуры выбирает агент.

**Параметры**

Собственных параметров нет.

**Пример**

Конфиг:

```jsonc
{ "checks": { "tuple-type": { "severity": "warning" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    private (int, string) m_Pair;

    public (int Count, string Name) Get()
    {
        var local = (1, "a");
        return local;
    }
}
```

Находки (`--format md`):

````markdown
- 3: `tuple-type` (warning) - кортеж '(int, string)' в сигнатуре или поле
- 5: `tuple-type` (warning) - кортеж '(int Count, string Name)' в сигнатуре или поле
````

`csr fix` код не меняет: у находок нет замены.

**Ограничения**

- Кортежи в теле метода, в инициализаторах и деконструкция не проверяются.
- Параметров нет. Признак `arch` с тем же именем и вопросом-решением — в разделе «Признаки `arch`».

### Модификаторы

#### `explicit-private`

Модификатор `private` у членов класса и структуры: пишется (`prefer: explicit`) или не пишется (`implicit`). Виды членов — параметр `members`: `field`, `method`, `property`, `event`, `constructor`, `type`.

**Замена:** есть. Замена добавляет или убирает `private`. В находке и замене — только строка объявления, не тело члена.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `prefer` | `explicit` / `implicit` | `"explicit"` | explicit - private пишется, implicit - не пишется |
| `members` | список строк | `["field", "method"]` | виды членов: field, method, property, event, constructor, type |

**Пример**

Конфиг:

```jsonc
{ "checks": { "explicit-private": { "severity": "warning", "rule": "S23", "prefer": "explicit" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    int m_Count;

    void Reset()
    {
        m_Count = 0;
    }

    private int m_Total;
}
```

Находки (`--format md`):

````markdown
- 3: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private int m_Count;
  ```
- 5: `explicit-private` S23 (warning) - нет явного 'private'
  ```csharp
      private void Reset()
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    private int m_Count;

    private void Reset()
    {
        m_Count = 0;
    }

    private int m_Total;
}
```

**Вариант `prefer: implicit`**

Конфиг:

```jsonc
{ "checks": { "explicit-private": { "severity": "warning", "prefer": "implicit", "members": ["field", "method", "property"] } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    private int m_Count;

    private void Reset()
    {
        m_Count = 0;
    }

    private int Count => m_Count;
}
```

Находки (`--format md`):

````markdown
- 3: `explicit-private` (warning) - лишний 'private'
  ```csharp
      int m_Count;
  ```
- 5: `explicit-private` (warning) - лишний 'private'
  ```csharp
      void Reset()
  ```
- 10: `explicit-private` (warning) - лишний 'private'
  ```csharp
      int Count => m_Count;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    int m_Count;

    void Reset()
    {
        m_Count = 0;
    }

    int Count => m_Count;
}
```


**Что не проверяется по умолчанию и что проверяется при расширенном `members`**

Конфиг:

```jsonc
{ "checks": { "explicit-private": { "severity": "warning", "prefer": "explicit", "members": ["field", "method", "property", "event", "constructor", "type"] } } }
```

Было (`Demo.cs`):

```csharp
public interface IShape
{
    float Area();
}

public class Square : IShape
{
    static Square()
    {
    }

    Square()
    {
    }

    float IShape.Area() => 1f;

    class Inner
    {
    }

    event System.Action Changed;

    int Value { get; }
}
```

Находки (`--format md`):

````markdown
- 12: `explicit-private` (warning) - нет явного 'private'
  ```csharp
      private Square()
  ```
- 18: `explicit-private` (warning) - нет явного 'private'
  ```csharp
      private class Inner
  ```
- 22: `explicit-private` (warning) - нет явного 'private'
  ```csharp
      private event System.Action Changed;
  ```
- 24: `explicit-private` (warning) - нет явного 'private'
  ```csharp
      private int Value { get; }
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public interface IShape
{
    float Area();
}

public class Square : IShape
{
    static Square()
    {
    }

    private Square()
    {
    }

    float IShape.Area() => 1f;

    private class Inner
    {
    }

    private event System.Action Changed;

    private int Value { get; }
}
```


**Ограничения**

- Не проверяются: члены интерфейсов, явные реализации интерфейса, статические конструкторы, деструкторы, `partial`-методы без модификатора доступа.
- Модификатор доступа вложенного типа проверяется, только если в `members` есть `type`.

#### `forbidden-modifier`

Модификатор или сочетание из `modifiers` у типа или члена: `sealed`, `private protected`, `protected internal`. Сочетание — через пробел, в любом порядке.

**Замена:** нет. Удаление модификатора меняет поведение (наследование, доступ), решает агент.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `modifiers` | список строк | `["sealed", "private protected", "protected internal"]` | модификатор или сочетание через пробел (в любом порядке) |

**Пример**

Конфиг:

```jsonc
{ "checks": { "forbidden-modifier": { "severity": "warning", "modifiers": ["sealed"] } } }
```

Было (`Demo.cs`):

```csharp
public sealed class Report
{
    private protected int m_Count;
}
```

Находки (`--format md`):

````markdown
- 1: `forbidden-modifier` (warning) - модификатор 'sealed'
````

`csr fix` код не меняет: у находок нет замены.

**Значение `modifiers` по умолчанию — все три модификатора (`sealed`, `private protected`, `protected internal`)**

Конфиг:

```jsonc
{ "checks": { "forbidden-modifier": { "severity": "warning" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    private protected int m_Count;
    protected internal int Total;
}

public sealed class Leaf
{
}
```

Находки (`--format md`):

````markdown
- 3: `forbidden-modifier` (warning) - модификатор 'private protected'
- 4: `forbidden-modifier` (warning) - модификатор 'protected internal'
- 7: `forbidden-modifier` (warning) - модификатор 'sealed'
````

`csr fix` код не меняет: у находок нет замены.


**Ограничения**

- Находка — по тексту модификаторов объявления, без семантики.

#### `custom-delegate`

Объявление своего `delegate`. Вместо него — `Action<…>` или `Func<…>`.

**Замена:** нет. Правка затрагивает места использования делегата.

**Параметры**

Собственных параметров нет.

**Пример**

Конфиг:

```jsonc
{ "checks": { "custom-delegate": { "severity": "hint" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public delegate void Handler(int value);

    public delegate bool Predicate(string text, int index);
}
```

Находки (`--format md`):

````markdown
- 3: `custom-delegate` (hint) - свой delegate 'Handler'
- 5: `custom-delegate` (hint) - свой delegate 'Predicate'
````

`csr fix` код не меняет: у находок нет замены.

**Ограничения**

- Параметров нет.

### Раскладка кода столбиком

#### `expression-arrow`

Место `=>` у члена с телом-выражением. `=>` в конце строки — всегда находка. У свойства и индексатора (`properties: next-line`) — с новой строки, +4 к отступу члена. У метода, конструктора, оператора, локальной функции (`methods`): `fit` — в строке сигнатуры, если строка помещается в `max`, иначе на следующей; `next-line` — всегда с новой строки; `any` — любое.

**Замена:** есть. Замена переносит `=>` и выражение на следующую строку с отступом +4, либо возвращает в строку сигнатуры.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `properties` | `next-line` / `any` | `"next-line"` | свойства и индексаторы: next-line - '=>' с новой строки; any - любое |
| `methods` | `fit` / `next-line` / `any` | `"fit"` | методы, конструкторы, операторы, локальные функции: fit - в строке сигнатуры, если помещается в max; next-line - всегда с новой строки; any - любое |
| `max` | целое число | `120` | длина строки для methods: fit |

**Пример**

В примере `max` = 60. Метод `Twice()` помещается в строку и остался в одной.

Конфиг:

```jsonc
{ "checks": { "expression-arrow": { "severity": "warning", "rule": "F30", "properties": "next-line", "methods": "fit", "max": 60 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    private int m_Count;

    public int Count => m_Count;

    public int Twice() => m_Count * 2;

    public int Combine(int first, int second, int third) => first + second + third + m_Count;
}
```

Находки (`--format md`):

````markdown
- 5: `expression-arrow` F30 (warning) - '=>' свойства - с новой строки
  ```csharp
      public int Count
          => m_Count;
  ```
- 9: `expression-arrow` F30 (warning) - строка с '=>' длиннее порога
  ```csharp
      public int Combine(int first, int second, int third)
          => first + second + third + m_Count;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    private int m_Count;

    public int Count
        => m_Count;

    public int Twice() => m_Count * 2;

    public int Combine(int first, int second, int third)
        => first + second + third + m_Count;
}
```

**Вариант `properties: any`, `methods: next-line`**

Конфиг:

```jsonc
{ "checks": { "expression-arrow": { "severity": "warning", "properties": "any", "methods": "next-line" } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    private int m_Count;

    public int Count => m_Count;

    public int Twice() => m_Count * 2;
}
```

Находки (`--format md`):

````markdown
- 7: `expression-arrow` (warning) - строка с '=>' длиннее порога
  ```csharp
      public int Twice()
          => m_Count * 2;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    private int m_Count;

    public int Count => m_Count;

    public int Twice()
        => m_Count * 2;
}
```


**Ограничения**

- `any` отключает проверку соответствующей группы членов.

#### `signature-wrap`

Сигнатура не в форме столбиком: однострочная длиннее `max` или с `minParameters` и больше параметрами; перенос посреди списка (первый параметр в строке имени); `)` на отдельной строке; `: base(…)` или `where` в строке `)` у сигнатуры столбиком.

**Замена:** есть. Замена: перенос после `(`, параметр на строку с отступом +4, `)` на строке последнего параметра; `: base(…)` и `: this(…)` (`initializerOnOwnLine`) и каждое ограничение `where` — отдельной строкой +4.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `max` | целое число | `120` | длина строки однострочной сигнатуры (до ')') |
| `minParameters` | целое число | `5` | с этого числа параметров - столбиком; 0 - только по длине |
| `initializerOnOwnLine` | `true` / `false` | `true` | в сигнатуре столбиком ': base(...)' - отдельной строкой |

**Пример**

В примере `max` = 60.

Конфиг:

```jsonc
{ "checks": { "signature-wrap": { "severity": "warning", "rule": "F21", "max": 60, "minParameters": 5 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public Report(int count, string name)
        : base()
    {
    }

    public void Fill(int a, int b, int c, int d, int e)
    {
    }

    public int Combine(int first, int second,
        int third)
    {
        return first + second + third;
    }

    public T Make<T>(int count, string name) where T : class
    {
        return null;
    }

    public T Build<T>(
        int count,
        string name) where T : class
    {
        return null;
    }
}
```

Находки (`--format md`):

````markdown
- 8: `signature-wrap` F21 (warning) - параметров 5 - столбиком
  ```csharp
      public void Fill(
          int a,
          int b,
          int c,
          int d,
          int e)
  ```
- 12-13: `signature-wrap` F21 (warning) - перенос посреди списка параметров - после '(', параметр на строку
  ```csharp
      public int Combine(
          int first,
          int second,
          int third)
  ```
- 23-25: `signature-wrap` F21 (warning) - ': base' или 'where' в строке ')' - отдельной строкой
  ```csharp
      public T Build<T>(
          int count,
          string name)
          where T : class
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public Report(int count, string name)
        : base()
    {
    }

    public void Fill(
        int a,
        int b,
        int c,
        int d,
        int e)
    {
    }

    public int Combine(
        int first,
        int second,
        int third)
    {
        return first + second + third;
    }

    public T Make<T>(int count, string name) where T : class
    {
        return null;
    }

    public T Build<T>(
        int count,
        string name)
        where T : class
    {
        return null;
    }
}
```

**Ограничения**

- `minParameters: 0` — столбиком только по длине.

#### `call-wrap`

Вызов в одну строку длиннее `max` либо перенос посреди списка аргументов (первый аргумент в строке вызова).

**Замена:** есть у части находок. Замена: перенос после `(`, аргумент на строку с отступом +4, `)` на строке последнего аргумента. Вложенный вызов переносится следующим проходом `csr fix`.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `max` | целое число | `120` | длина строки |

**Пример**

В примере `max` = 60.

Конфиг:

```jsonc
{ "checks": { "call-wrap": { "severity": "warning", "rule": "F22", "max": 60 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(int first, int second)
    {
        var total = Combine(first, second, first + second, Scale(first, second));
        Combine(first,
            second, 1, 2);
    }

    private int Combine(int a, int b, int c, int d)
        => a + b + c + d;

    private int Scale(int a, int b)
        => a * b;
}
```

Находки (`--format md`):

````markdown
- 5: `call-wrap` F22 (warning) - вызов длиннее порога - столбиком
  ```csharp
          var total = Combine(
              first,
              second,
              first + second,
              Scale(first, second));
  ```
- 6-7: `call-wrap` F22 (warning) - перенос посреди списка аргументов - после '(', аргумент на строку
  ```csharp
          Combine(
              first,
              second,
              1,
              2);
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public void Run(int first, int second)
    {
        var total = Combine(
            first,
            second,
            first + second,
            Scale(first, second));
        Combine(
            first,
            second,
            1,
            2);
    }

    private int Combine(int a, int b, int c, int d)
        => a + b + c + d;

    private int Scale(int a, int b)
        => a * b;
}
```

**Вызов внутри выражения — находка без замены (`max` = 50)**

Конфиг:

```jsonc
{ "checks": { "call-wrap": { "severity": "warning", "max": 50 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(int first, int second)
    {
        if (Combine(first, second, first + second, first - second) > 0)
            return;
        var text = first > 0 ? Combine(first, second, first, second) : 0;
        var sum = Combine(first, second, 1, 2) + Combine(second, first, 3, 4);
    }

    private int Combine(int a, int b, int c, int d)
        => a + b + c + d;
}
```

Находки (`--format md`):

````markdown
- 7: `call-wrap` (warning) - вызов длиннее порога - столбиком; вызов внутри выражения - раскладка по F24 / F25 или вынос в переменную
- 8: `call-wrap` (warning) - вызов длиннее порога - столбиком; вызов внутри выражения - раскладка по F24 / F25 или вынос в переменную
````

`csr fix` код не меняет: у находок нет замены.


**Ограничения**

- Не трогаются: условия `if` / `while` (правило переноса — `condition-wrap`), вызовы логгера (`log-call-wrap`), строки с комментарием, многострочные аргументы, строка продолжения, которая помещается с обычным отступом +4.
- Вызов внутри тернарника, бинарного выражения или цепочки вызовов — находка без замены: раскладка по правилам переноса выражений или вынос в переменную.

#### `region-layout`

Число пустых строк вокруг `#region` / `#endregion`: внутри региона — `inside` (после `#region` и перед `#endregion`), между `#endregion` и следующим `#region` — `between`, между регионом и членом вне региона — `outside`. У `{` и `}` типа — вплотную.

**Замена:** есть. Замена добавляет или убирает пустые строки.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `inside` | целое число | `1` | пустых строк после #region и перед #endregion |
| `between` | целое число | `2` | пустых строк между #endregion и следующим #region |
| `outside` | целое число | `1` | пустых строк между регионом и членом вне региона |

**Пример**

Применено с `--hints`: в примере уровень `hint`.

Конфиг:

```jsonc
{ "checks": { "region-layout": { "severity": "hint", "rule": "F18", "inside": 1, "between": 2, "outside": 1 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    #region Fields
    private int m_Count;
    private int m_Total;
    #endregion
    #region Methods

    public void Reset()
    {
    }

    #endregion
    public int Count => m_Count;
}
```

Находки (`--format md`):

````markdown
- 3-4: `region-layout` F18 (hint) - после #region: пустых строк 0, нужно 1
  ```csharp
      #region Fields
  
      private int m_Count;
  ```
- 5-6: `region-layout` F18 (hint) - перед #endregion: пустых строк 0, нужно 1
  ```csharp
      private int m_Total;
  
      #endregion
  ```
- 6-7: `region-layout` F18 (hint) - после #endregion: пустых строк 0, нужно 2
  ```csharp
      #endregion
  
  
      #region Methods
  ```
- 13-14: `region-layout` F18 (hint) - после #endregion: пустых строк 0, нужно 1
  ```csharp
      #endregion
  
      public int Count => m_Count;
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --hints --apply`):

```csharp
public class Report
{
    #region Fields

    private int m_Count;
    private int m_Total;

    #endregion


    #region Methods

    public void Reset()
    {
    }

    #endregion

    public int Count => m_Count;
}
```

**Ограничения**

- Соседняя директива (`#if`, вложенный регион) не проверяется.

#### `condition-wrap`

Цепочка `&&` / `||` в строке длиннее `max` (`if`, `while`, `return`, инициализатор переменной) и строка продолжения цепочки с неверным отступом.

**Замена:** есть. Замена: операнд на строку, оператор в начале строки продолжения, отступ +4 (в `return` `||` встаёт под первый операнд). Аргументы вызова в перенесённом условии `if` / `while` — на +8 (`argumentIndent`), чтобы не совпасть с телом. Продолжение цепочки: в `if` / `while` — +4 к инструкции (`continuationIndent`), в присваивании и инициализаторе — по `assignmentIndent`, в `return` не трогается.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `max` | целое число | `120` | длина строки с цепочкой |
| `argumentIndent` | `true` / `false` | `true` | аргументы вызова в перенесенном условии if / while - на +8 |
| `continuationIndent` | `true` / `false` | `true` | продолжение цепочки в условии if / while - на +4 |
| `assignmentIndent` | `true` / `false` | `false` | продолжение цепочки в присваивании и инициализаторе - на +4 |

**Пример**

В примере `max` = 60.

Конфиг:

```jsonc
{ "checks": { "condition-wrap": { "severity": "warning", "rule": "F24", "max": 60 } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public void Run(int count, bool enabled, bool visible, string name)
    {
        if (count > 0 && enabled && visible && name.Length > 3 && count < 100)
            Use(name);
    }

    private void Use(string value)
    {
    }
}
```

Находки (`--format md`):

````markdown
- 5: `condition-wrap` F24 (warning) - цепочка '&&' длиннее порога - по операнду на строку
  ```csharp
          if (count > 0
              && enabled
              && visible
              && name.Length > 3
              && count < 100)
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public void Run(int count, bool enabled, bool visible, string name)
    {
        if (count > 0
            && enabled
            && visible
            && name.Length > 3
            && count < 100)
            Use(name);
    }

    private void Use(string value)
    {
    }
}
```

**Ограничения**

- Правит перенос и отступ; место оператора (`&&` в конце строки) правит `binary-op-wrap`.

#### `initializer-layout`

Инициализатор объекта, коллекции или массива. Находка: однострочный длиннее `max`; в многострочном — `{` в конце строки с `new`, два элемента в строке, нет запятой после последнего (`trailingComma`). Короткий однострочный — допустим.

**Замена:** есть. Замена: `{` на следующей строке под `new` с отступом строки `new`, элемент на строку с отступом +4, запятая после последнего элемента.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `max` | целое число | `120` | длина строки однострочного инициализатора |
| `trailingComma` | `true` / `false` | `true` | запятая после последнего элемента многострочного |

**Пример**

В примере `max` = 60.

Конфиг:

```jsonc
{ "checks": { "initializer-layout": { "severity": "warning", "rule": "F23", "max": 60, "trailingComma": true } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Report
{
    public int First;
    public int Second;

    public Report Create()
    {
        var names = new List<string> { "alpha", "beta", "gamma", "delta" };
        var item = new Report { First = 1, Second = 2 };
        var items = new List<Report> {
            new Report(), new Report(),
            new Report()
        };
        return item;
    }
}
```

Находки (`--format md`):

````markdown
- 10: `initializer-layout` F23 (warning) - инициализатор длиннее порога - столбиком
  ```csharp
          var names = new List<string>
          {
              "alpha",
              "beta",
              "gamma",
              "delta",
          };
  ```
- 12-15: `initializer-layout` F23 (warning) - '{' в строке с 'new' - на следующую строку
  ```csharp
          var items = new List<Report>
          {
              new Report(),
              new Report(),
              new Report(),
          };
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
using System.Collections.Generic;

public class Report
{
    public int First;
    public int Second;

    public Report Create()
    {
        var names = new List<string>
        {
            "alpha",
            "beta",
            "gamma",
            "delta",
        };
        var item = new Report { First = 1, Second = 2 };
        var items = new List<Report>
        {
            new Report(),
            new Report(),
            new Report(),
        };
        return item;
    }
}
```

**Ограничения**

- Короткий однострочный инициализатор (`new Report { First = 1, Second = 2 }` в примере) не находка.

#### `ternary-wrap`

Тернарник в одну строку длиннее `max`; вложенный тернарник в одну строку при `nested`; `?` или `:` в конце строки.

**Замена:** есть у части находок. Замена: условие в строке, `? a` и `: b` с новой строки, +4 к строке тернарника; вложенный в ветви — ещё +4 на уровень. Замена есть для тернарника в голове инструкции (значение, `return`, `=>`, аргумент).

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `max` | целое число | `120` | длина строки с тернарником |
| `nested` | `true` / `false` | `true` | вложенный тернарник в одну строку - столбиком при любой длине |

**Пример**

В примере `max` = 60.

Конфиг:

```jsonc
{ "checks": { "ternary-wrap": { "severity": "warning", "rule": "F25", "max": 60, "nested": true } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public string Describe(int count, bool enabled)
    {
        var text = count > 0 ? "many elements in the current pass" : "no elements at all";
        return count > 10 ? "many" : count > 0 ? "some" : "none";
    }
}
```

Находки (`--format md`):

````markdown
- 5: `ternary-wrap` F25 (warning) - тернарник длиннее порога - столбиком
  ```csharp
          var text = count > 0
              ? "many elements in the current pass"
              : "no elements at all";
  ```
- 6: `ternary-wrap` F25 (warning) - тернарник длиннее порога - столбиком
  ```csharp
          return count > 10
              ? "many"
              : count > 0
                  ? "some"
                  : "none";
  ```
````

Стало (`csr fix Demo.cs --config config.jsonc --apply`):

```csharp
public class Report
{
    public string Describe(int count, bool enabled)
    {
        var text = count > 0
            ? "many elements in the current pass"
            : "no elements at all";
        return count > 10
            ? "many"
            : count > 0
                ? "some"
                : "none";
    }
}
```

**Вариант `nested: false`**

Конфиг:

```jsonc
{ "checks": { "ternary-wrap": { "severity": "warning", "max": 120, "nested": false } } }
```

Было (`Demo.cs`):

```csharp
public class Report
{
    public string Describe(int count)
    {
        return count > 10 ? "many" : count > 0 ? "some" : "none";
    }

    public string Short(int count)
        => count > 0 ? "some" : "none";
}
```

Находок нет: код проходит проверку без замечаний.


**Ограничения**

- Тернарник внутри другого выражения — находка без замены.
- `nested: false` — вложенный тернарник столбиком только при превышении `max`.

### Именование

Проверки имён по синтаксису одного файла, без семантики: тип берётся из объявления, `var` не проверяется. Все слова, префиксы и маски задаются параметрами конфига. Замены нет ни у одной проверки: переименование затрагивает другие файлы, поэтому имя меняют через [`csr rename`](#csr-rename) (у полей Unity он сохраняет сериализованные данные). Если правило можно вывести из имени, в сообщении есть предлагаемое имя. Члены `override` и явные реализации интерфейса не проверяются: их имя задаёт база.

#### `naming`

Имена по правилам конфига, как `dotnet_naming_rule` в `.editorconfig`: правило — какие символы, какое требование к имени, уровень и метка. Правил сколько угодно; новый случай — запись конфига, а не новая проверка.

Правило — запись списка `rules`:

| Поле | Значение |
|---|---|
| `symbols` | селектор: какие символы задевает правило (ниже); без `symbols` — все |
| поля требований | одно или несколько требований к имени (таблица видов ниже); правило без требования и без `skip` — ошибка конфига |
| `severity` | уровень находок правила: `error`, `warning`, `hint`; без поля — уровень проверки `naming` |
| `rule` | метка правила в находке; без поля — метка проверки `naming` |
| `subject` | подлежащее сообщения вместо вида символа: `"bool"` — `bool 'IsNotReady': …`; без поля — вид символа (`закрытое поле`, `событие`, `пространство`) |
| `skip` | виды требований, которые правило занимает без проверки: `["prefix"]` у константы — следующие правила с префиксом к ней не применяются |

Селектор `symbols` — все заданные условия вместе; пустое или незаданное условие — любое значение:

| Поле | Значения |
|---|---|
| `kinds` | виды: `namespace`, `class`, `struct`, `record`, `interface`, `enum`, `delegate`, `typeParameter`, `field`, `event`, `property`, `parameter`, `local` |
| `access` | доступ: `private`, `protected`, `internal`, `public`. Без модификатора — по месту: член интерфейса — `public`, тип вне типа — `internal`, член типа — `private`; `protected internal` и `private protected` — `protected`, `file` — `internal` |
| `modifiers` | все перечисленные модификаторы есть у объявления: `static`, `readonly`, `const`, `abstract`, `virtual`, `sealed`, `partial`, `async`, `new`, `extern`, `unsafe`, `volatile`, `required` |
| `types` | маски объявленного типа без пространства имён и `?`: `bool`, `List<*>`, `*[]`, любой массив — `*[*]` (и `int[,]`); `var` и объявление без типа не совпадают |
| `attributes` | маски атрибутов объявления без суффикса `Attribute` (`Flags`, `Serial*`); атрибуты с целью (`[field: …]`) — не атрибуты объявления |
| `bases` | маски прямого базового типа без пространства имён и аргументов типа (`MonoBehaviour`, `*Command`) |

Опечатка в поле или в значении `kinds`, `access`, `modifiers` — ошибка конфига с путём (`naming.rules[2]: symbols: неизвестное поле 'kind'`). Так же — уточняющее поле вида без главного (`negation` без `forbiddenWords` или `negationPrefixes`), поле не своего стиля префикса, пустые `roots`, `maxParts` меньше 1, вид и в `skip`, и в полях правила.

**Каскад.** Правила идут по порядку. Для каждого вида требования действует первое подходящее правило, которое его задаёт; остальные правила этого вида к символу не применяются. Разные виды требований складываются: поле `bool` получает префикс из правила полей и запрет слов из правила `bool`. Исключения выражает порядок: частное правило — раньше общего; занять вид без проверки — `skip`. Вид требования занимается целиком: правило только с `allowed` закрывает вид `masks`, и `forbidden` следующего правила к этому символу уже не применится.

Виды требований (поля записи правила):

| Вид | Поля | Нарушение |
|---|---|---|
| `masks` | `allowed` — маски допустимых имён (пусто — любое имя), `forbidden` — маски запрещённых | `событие 'OnSpawned': запрещенная форма 'On*ed'`, `событие 'Spawned': допустимо OnBefore*, OnAfter*` |
| `words` | `forbiddenWords` — запрещённые слова, `negationPrefixes` — приставки (`Dis` — `Disabled`), `ignoreWords` — слова с приставкой, которые не находка (`Display`); без учёта регистра, имя делится на слова без префикса поля, пространство — по частям; `negation: true` — слова означают отрицание | `пространство 'Game.Editor': слово 'Editor'`, с `negation` — `bool 'IsNotReady': отрицание 'Not' - имя по состоянию` |
| `number` | `number`: `plural` или `singular` — число по последнему слову имени без префикса поля; множественное — слово из `pluralWords` или на `s`, кроме окончаний `singularEndings` (по умолчанию `ss`, `us`, `is`: `Status`, `Class`, `Axis`) | `enum 'DamageType': имя во множественном числе`, с `subject: "коллекция"` — `коллекция 'm_Item': имя во множественном числе` |
| `namespace` | `roots` — корни: имя пространства начинается с корня (`Company` или `Company.*`) | `пространство 'Tools.Debug' вне корня: Company` |
| `inNamespace` | `inNamespace: true` — объявление вне типа внутри пространства имён: тип верхнего уровня вне `namespace` — нарушение (правило — для видов типов) | с `subject: "тип"` — `тип 'Helper' вне пространства имен` |
| `depth` | `maxParts` — не больше частей в имени пространства | `пространство 'Company.Game.Ui.Hud.Bars': частей 5, предел 4` |
| `prefix` | главные поля — `prefix`, `forbiddenPrefixes`. `prefix` с `prefixStyle: "field"` (по умолчанию): префикс поля (`m_Name`), `pascalAfterPrefix: true` — после префикса заглавная буква; `""` — без префикса полей (`m_`, `s_`, `k_`, `_` и `knownPrefixes`, только с `""`). `prefix` с `prefixStyle: "word"`: непустой префикс перед заглавной буквой или цифрой (`IStorage`, `TValue`), `prefixAlone: true` — имя может быть самим префиксом (`T`). Поле чужого стиля — ошибка конфига. `forbiddenPrefixes` — запрещённые префиксы перед заглавной буквой или цифрой (`OnChanged`; `Onboarding` — не префикс) | `'count': закрытое поле с префиксом 'm_' - 'm_Count'`, `'m_total': после префикса - заглавная буква - 'm_Total'`, `'m_Value': свойство без префикса 'm_' - 'Value'`, `интерфейс 'Storage': префикс 'I'`, `delegate 'OnSpawnHandler': без префикса 'On'` |
| `case` | `case`: `pascal` (`MaxSize`; без `_`; короткая аббревиатура `UI`, `ID` допустима) или `upper` (`MAX_SIZE`) | `'MAX_COUNT': константа в PascalCase - 'MaxCount'` |

Не проверяются имена, которые задаёт база: члены `override`, явные реализации интерфейса, параметры таких методов и индексаторов. Вид `local` — объявление локальной (`int x = 0;`) и `out`-переменная с типом; переменные `for`, `foreach`, `using`, `catch` и шаблонов не собираются.

**Замена:** нет — переименование задевает другие файлы, имя меняют через [`csr rename`](#csr-rename).

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `rules` | список правил | `[]` | правила: `symbols`, требования, `skip`, `severity`, `rule`, `subject` |

**Пример**

Конфиг:

```jsonc
{ "checks": { "naming": { "severity": "warning", "rules": [
  { "symbols": { "kinds": ["event"] }, "allowed": ["OnBefore*", "OnAfter*"], "forbidden": ["On*ed", "On*ing"], "rule": "N-Events" },
  { "symbols": { "kinds": ["delegate"] }, "forbiddenPrefixes": ["On"], "severity": "hint" }
] } } }
```

Было (`Demo.cs`):

```csharp
using System;

public delegate void OnSpawnHandler(int count);

public class Spawner
{
    public event Action OnBeforeSpawn;
    public event Action OnSpawned;
    public event Action Spawned;
}
```

Находки (`--format md`):

````markdown
| Проверка | Правило | Уровень | Находок |
|---|---|---|---:|
| `naming` | N-Events | warning | 2 |
| `naming` |  | hint | 1 |

- 3: `naming` (hint) - delegate 'OnSpawnHandler': без префикса 'On'
- 8: `naming` N-Events (warning) - событие 'OnSpawned': запрещенная форма 'On*ed'
- 9: `naming` N-Events (warning) - событие 'Spawned': допустимо OnBefore*, OnAfter*
````

В сводке отчёта у `naming` строка на каждую пару метки и уровня.

#### Старые проверки именования (до v0.4)

Прежние проверки именования — псевдонимы `naming`: те же имя и параметры в конфиге, находки — с прежним именем проверки и прежними текстами. Параметры переводятся в правила `naming`; при загрузке конфига `csr style` и `csr fix` пишут в stderr, что проверка устарела. В `v0.4` псевдонимы удаляются: конфиг переводится на `naming` по таблице. Если включены и псевдоним, и `naming` с пересекающимися правилами, одно нарушение даст две находки под разными именами проверок.

| Старая проверка | Правила `naming` |
|---|---|
| `field-prefix` | по порядку: `{ "symbols": { "kinds": ["field"], "modifiers": ["const"] }, "case": <constants>, "skip": ["prefix"] }` (при `constants: "any"` — `"skip": ["case", "prefix"]`); доступы не из `prefixedAccess` — `{ "symbols": { "kinds": ["field"], "access": [<доступы>] }, "prefix": "", "subject": "поле" }`; при `staticReadonly: "pascal"` — `{ "symbols": { "kinds": ["field"], "modifiers": ["static", "readonly"] }, "prefix": "", "subject": "static readonly" }`; `{ "symbols": { "kinds": ["field"], "modifiers": ["static"] }, "prefix": <staticPrefix>, "pascalAfterPrefix": <pascalAfterPrefix> }`; `{ "symbols": { "kinds": ["field"] }, "prefix": <instancePrefix>, "pascalAfterPrefix": … }`; при `properties` — `{ "symbols": { "kinds": ["property"] }, "prefix": "" }`; у правил с `prefix: ""` — `knownPrefixes: [<instancePrefix>, <staticPrefix>]`. Свойство `override` больше не находка: его имя задаёт база, находка — на базовом свойстве; свойства интерфейсов теперь проверяются |
| `event-name` | `{ "symbols": { "kinds": ["event"] }, "allowed": <allowed>, "forbidden": <forbidden> }`, `{ "symbols": { "kinds": ["delegate"] }, "forbiddenPrefixes": <delegatePrefixes> }`; по умолчанию `allowed: ["On*"]`, `forbidden: ["OnWill*", "OnDid*"]`, `delegatePrefixes: ["On"]` |
| `bool-name` | `{ "symbols": { "kinds": <members>, "types": ["bool", "Boolean"] }, "forbiddenWords": <forbiddenWords>, "negationPrefixes": <negationPrefixes>, "ignoreWords": <ignoreWords>, "negation": true, "subject": "bool" }`; по умолчанию `members: ["field", "property", "parameter", "local"]`, `forbiddenWords: ["Not", "No", "Non", "Dont"]` |
| `enum-plural` | `{ "symbols": { "kinds": ["enum"], "attributes": ["Flags"] }, "number": "plural", "pluralWords": <pluralWords>, "singularEndings": <singularEndings> }`, `{ "symbols": { "kinds": ["enum"] }, "number": "singular", … }`; по умолчанию `pluralWords: ["Mask"]`. Текст: `enum 'DamageType': имя во множественном числе` (раньше `enum 'DamageType' с [Flags]: …`) |
| `collection-name` | `{ "symbols": { "kinds": <members>, "types": <collectionTypes> и "*[*]" при arrays }, "number": "plural", "pluralWords": <pluralWords>, "singularEndings": <singularEndings>, "subject": "коллекция" }`; по умолчанию `members: ["field", "property"]`, `arrays: true`, типы и слова — как у прежней проверки. С `members: ["local"]` проверяются и `out`-переменные с типом |
| `namespace-name` | при `roots`: `{ "symbols": { "kinds": ["namespace"] }, "roots": <roots> }`, `{ "symbols": { "kinds": ["class", "struct", "record", "interface", "enum"] }, "inNamespace": true, "subject": "тип" }`; при `forbiddenWords`: `{ "symbols": { "kinds": ["namespace"] }, "forbiddenWords": <forbiddenWords> }`; при `maxDepth` > 0: `{ "symbols": { "kinds": ["namespace"] }, "maxParts": <maxDepth> }` |
| `type-name` | `{ "symbols": { "kinds": ["interface"] }, "prefix": <interfacePrefix>, "prefixStyle": "word" }`, `{ "symbols": { "kinds": ["typeParameter"] }, "prefix": <typeParameterPrefix>, "prefixStyle": "word", "prefixAlone": true }`; на каждую пару `derivedNames` — `{ "symbols": { "kinds": ["class", "struct", "record", "interface"], "bases": [<база>] }, "allowed": [<маска имени>] }`; по умолчанию `interfacePrefix: "I"`, `typeParameterPrefix: "T"`. Текст находки наследника — как у `masks`: `класс 'Save': допустимо *Command` (раньше `'Save' наследует 'CommandBase': имя по маске '*Command'`). Правила пар — один вид `masks`, по каскаду действует первая пара с подходящей базой: тип с двумя базами под разные пары проверяется только по первой |


### Запрещённые конструкции

Запреты команды по синтаксису файла: что запрещено и чем заменить, задаёт конфиг. У каждой проверки есть параметр `excludePaths` — маски путей файлов, где проверка не работает. Решение не загружается: тип выражения, базовые типы предков и вызовы через переменные не видны.

#### `forbidden`

Запрещённое API по записям конфига: что запрещено и чем заменить. Запись — вид, маска имени, альтернатива, где не действует, уровень и метка. На каждое обращение — первая подходящая запись его вида.

| Поле записи | Значение |
|---|---|
| `kind` | `call` — обращение к члену: имя `Тип.Член`, последний идентификатор слева от точки и член (`UnityEngine.Debug.Log` → `Debug.Log`, `Service<T>.Instance` → `Service.Instance`, встроенный тип — ключевым словом: `string.Format`); `type` — имя в позиции типа: объявление поля, параметра, переменной, результат, аргумент типа, `new`, приведение, `typeof` (правая часть без пространства имён с аргументами типа: `ArrayList`, `List<int>`; встроенный — ключевым словом: `object`, `dynamic`) |
| `mask` | маска имени (`*` — любые символы) |
| `use` | чем заменить: текст в сообщении; без поля — сообщение без альтернативы |
| `excludePaths` | маски путей файлов, где запись не действует (`*/Editor/*`; путь от текущей папки проверяется и с ведущим `/`) |
| `severity`, `rule` | уровень и метка находок записи; без поля — проверки `forbidden` |

Опечатка в поле, неизвестный `kind`, запись без `kind` или `mask` — ошибка конфига с путём (`forbidden.entries[1]: kind: допустимо call, type`).

**Замена:** нет — альтернатива — другой API проекта, замену выбирает агент.

**Ограничения:** вызов через `using static` (`Log("...")` без типа) и обращение через переменную (`logger.Log`) по имени типа не распознаются; массив целиком (`int[]`) с маской `type` не сравнивается — сравнивается тип элемента; переменная с именем `dynamic` (`var dynamic = 1`) — не тип.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `entries` | список записей | `[]` | записи: `kind`, `mask`, `use`, `excludePaths`, `severity`, `rule` |

**Пример**

Конфиг:

```jsonc
{ "checks": { "forbidden": { "severity": "error", "entries": [
  { "kind": "call", "mask": "Debug.Log*", "use": "логгер проекта", "rule": "F-Log" },
  { "kind": "call", "mask": "Gizmos.*", "excludePaths": ["*/Editor/*"] },
  { "kind": "type", "mask": "dynamic", "use": "строгая типизация" },
  { "kind": "type", "mask": "ArrayList", "use": "List<T>", "severity": "warning" }
] } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections;

public class Demo
{
    private dynamic m_Payload;
    private ArrayList m_Items = new ArrayList();

    private void Show(int value)
    {
        Debug.Log("value " + value);
        Gizmos.DrawLine(Vector3.zero, Vector3.up);
    }
}
```

Находки (`--format md`):

````markdown
- 5: `forbidden` (error) - запрещенный тип 'dynamic': строгая типизация
- 6: `forbidden` (warning) - запрещенный тип 'ArrayList': List<T>
- 6: `forbidden` (warning) - запрещенный тип 'ArrayList': List<T>
- 10: `forbidden` F-Log (error) - запрещенный член 'Debug.Log': логгер проекта
- 11: `forbidden` (error) - запрещенный член 'Gizmos.DrawLine'
````

`csr fix` код не меняет: у находок нет замены.

**Старые проверки (до v0.4).** `forbidden-call` и `forbidden-type` — псевдонимы `forbidden`: те же имя и параметры, находки — с прежним именем и прежними текстами; при загрузке конфига `csr style` и `csr fix` пишут в stderr, что проверка устарела.

| Старая проверка | Записи `forbidden` |
|---|---|
| `forbidden-call` | на каждую пару `calls` — `{ "kind": "call", "mask": <маска>, "use": <текст>, "excludePaths": <excludePaths> }` |
| `forbidden-type` | на каждую пару `types` — `{ "kind": "type", "mask": <маска>, "use": <текст>, "excludePaths": <excludePaths> }` |

#### `coroutine`

Метод-корутина: тип результата из `returnTypes` (по умолчанию `IEnumerator`) и, при `requireYield`, `yield` в теле самого метода (не во вложенной локальной функции или лямбде). Проверяются методы и локальные функции; `GetEnumerator` коллекции — не корутина. Итераторы `IEnumerable<T>` по умолчанию не находка — их добавляют в `returnTypes`, если команда запрещает и их.

**Замена:** нет: перевод на асинхронный метод меняет вызывающий код.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `returnTypes` | список строк | `["IEnumerator"]` | маски типа результата без пространства имён (`IEnumerator`, `IEnumerable<*>`) |
| `requireYield` | `true` / `false` | `true` | только с `yield` в теле; `false` — любой метод с таким результатом (в том числе возвращающий чужой итератор) |
| `use` | строка | `""` | чем заменить: текст в сообщении |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Пример**

Конфиг:

```jsonc
{ "checks": { "coroutine": { "severity": "error", "use": "асинхронный метод или Update" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections;
using System.Collections.Generic;

public class Demo
{
    private IEnumerator Fade()
    {
        yield return null;
    }

    private IEnumerable<int> Numbers()
    {
        yield return 1;
    }
}
```

Находки (`--format md`):

````markdown
- 6: `coroutine` (error) - корутина 'Fade' (IEnumerator): асинхронный метод или Update
````

`csr fix` код не меняет: у находок нет замены.


#### `static-state`

Статическое изменяемое состояние:

- `static`-поле без `readonly` и `const`;
- `static readonly`-поле изменяемого типа из `mutableTypes` (коллекции, массивы): ссылка неизменна, содержимое меняется;
- `static`-автосвойство с `set` (любой доступности) и только с `get` изменяемого типа из `mutableTypes`;
- `static event`;
- синглтон — `static`-поле или свойство своего типа (в том числе `readonly` и только с `get`), при `singleton`.

`static readonly` других типов и `const` — не находка. Исключения: атрибут члена или его типа из `ignoreAttributes` (например `ThreadStatic`), имя члена из `ignoreNames`, путь из `excludePaths`.

**Замена:** нет: перенос состояния в сервис меняет архитектуру.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `mutableTypes` | список строк | `["List<*>", "Dictionary<*>", "HashSet<*>", "Queue<*>", "Stack<*>", "*[]"]` | маски типов `static readonly`-полей и `get`-свойств, содержимое которых меняется (без пространства имён) |
| `singleton` | `true` / `false` | `true` | `static`-поле или свойство своего типа — находка |
| `ignoreAttributes` | список строк | `[]` | атрибуты члена или его типа, при которых член не проверяется (без суффикса `Attribute`) |
| `ignoreNames` | список строк | `[]` | маски имён членов, которые не проверяются |
| `use` | строка | `""` | чем заменить: текст в сообщении |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Ограничение:** тип поля известен только по синтаксису: `static readonly`-поле класса с изменяемыми полями (не из `mutableTypes`) — не находка; обобщённый синглтон (`static T Instance` в `Singleton<T>`) не распознаётся.

**Пример**

Конфиг:

```jsonc
{ "checks": { "static-state": { "severity": "error", "use": "сервис проекта" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Registry
{
    public static Registry Instance { get; private set; }
    private static int s_Count;
    private static readonly List<int> Ids = new();
    private static readonly string Name = "registry";
    private const int Max = 3;
    public static bool IsReady { get; set; }
}
```

Находки (`--format md`):

````markdown
- 5: `static-state` (error) - синглтон 'Instance' типа Registry: сервис проекта
- 6: `static-state` (error) - статическое изменяемое поле 's_Count': сервис проекта
- 7: `static-state` (error) - статическое поле 'Ids' изменяемого типа List<int>: сервис проекта
- 10: `static-state` (error) - статическое свойство 'IsReady' с set: сервис проекта
````

`csr fix` код не меняет: у находок нет замены.


#### `local-function`

Локальная функция. Редакторный и тестовый код исключается масками путей `excludePaths` (например `*/Editor/*`): сборка определяется по папке, `.asmdef` проверка не читает.

**Замена:** нет: перенос в `private`-метод требует передать захваченные переменные параметрами.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `use` | строка | `""` | чем заменить: текст в сообщении |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Пример**

Конфиг:

```jsonc
{ "checks": { "local-function": { "severity": "error", "use": "private-метод", "excludePaths": ["*/Editor/*"] } } }
```

Было (`Demo.cs`):

```csharp
public class Demo
{
    private int Sum(int[] values)
    {
        var total = 0;
        foreach (var value in values)
            total += Clamp(value);

        return total;

        int Clamp(int value) => value < 0 ? 0 : value;
    }
}
```

Находки (`--format md`):

````markdown
- 11: `local-function` (error) - локальная функция 'Clamp': private-метод
````

`csr fix` код не меняет: у находок нет замены.


#### `path-concat`

Склейка пути сложением строк с литералом-разделителем из `separators`: `a + "/" + b` → `Path.Combine(a, b)`. Ищется в каждой цепочке `+`, где одна из частей — литерал-разделитель.

**Замена:** только при `replace: true` и только для цепочки вида `a + "/" + b [+ "/" + c …]`, где каждая часть — строка по синтаксису файла: строковый литерал без разделителя на краях, интерполяция, вызов `ToString()` или имя, у которого все объявления в члене (параметры, локальные, `foreach`, `out var`, шаблоны, `catch`, переменные запроса — в любых блоках и лямбдах) явно `string`; если в члене объявлений нет — поле или свойство типа с типом `string`. `var`, параметр лямбды без типа и не найденное имя — не строка, замены нет. В константе (`const`, аргумент атрибута, значение параметра по умолчанию, `case`) замены нет: вызов там недопустим. Имя — `Path`, если в файле есть `using System.IO` и каждое имя `Path` в файле — обращение к статическому члену `System.IO.Path` (`Path.Combine`, `Path.GetFileName` …); иначе `global::System.IO.Path` (член `Path` типа или базового типа скрыл бы класс). По умолчанию замена выключена: результат `Path.Combine` отличается от сложения — в Windows части соединяются через `\` (для путей, где важен `/`, например ассеты Unity `Assets/…`), абсолютная правая часть отбрасывает левую, `null` — исключение вместо пустой строки, пустая левая часть теряет ведущий `/`.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `separators` | список строк | `["/"]` | литералы-разделители пути |
| `replace` | `true` / `false` | `false` | предлагать замену на `Path.Combine` |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Пример**

Конфиг:

```jsonc
{ "checks": { "path-concat": { "severity": "warning", "replace": true } } }
```

Было (`Demo.cs`):

```csharp
using System.IO;

public class Demo
{
    private const string Root = "Build";
    private const string Cache = Root + "/" + "cache.json";

    private string GetPath(string folder, string file, int index)
    {
        var name = folder + "/" + file;
        var numbered = folder + "/" + index;
        return Path.GetFullPath(name + numbered);
    }
}
```

Находки (`--format md`):

````markdown
- 6: `path-concat` (warning) - склейка пути через "/": Path.Combine
- 10: `path-concat` (warning) - склейка пути через "/": Path.Combine
  ```csharp
          var name = Path.Combine(folder, file);
  ```
- 11: `path-concat` (warning) - склейка пути через "/": Path.Combine
````

`csr fix --apply` меняет только строку 10: в `Cache` — константа, в `numbered` — часть типа `int`.


#### `bool-field-count`

Класс — наследник типа из `baseTypes` (прямой базовый тип в списке наследования) с `minFields` и больше полями `bool` экземпляра: набор флагов — кандидат в `enum` или `[Flags]`. `static`- и `const`-поля не считаются.

**Замена:** нет.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `baseTypes` | список строк | `["MonoBehaviour"]` | маски имени базового типа без пространства имён и аргументов типа (`Singleton` — и `Singleton<Panel>`), как `baseTypes` в `arch`; пусто — любой класс |
| `minFields` | число | `3` | сколько полей `bool` — находка |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Ограничение:** базовый тип — только из списка наследования самого класса: наследник наследника по синтаксису не виден.

**Пример**

Конфиг:

```jsonc
{ "checks": { "bool-field-count": { "severity": "warning", "baseTypes": ["MonoBehaviour"], "minFields": 3 } } }
```

Было (`Demo.cs`):

```csharp
public class Door : MonoBehaviour
{
    [SerializeField]
    private bool m_IsOpen;
    private bool m_IsLocked;
    private bool m_IsBroken;
    private int m_Health;
}
```

Находки (`--format md`):

````markdown
- 1: `bool-field-count` (warning) - полей bool в 'Door': 3 (m_IsOpen, m_IsLocked, m_IsBroken)
````

`csr fix` код не меняет: у находок нет замены.


#### `type-per-file`

Несколько типов верхнего уровня в файле (классы, структуры, интерфейсы, перечисления, делегаты), если среди них есть наследник типа из `baseTypes`. Находка — на каждом типе, имя которого не совпадает с именем файла, с командой переноса: `csr move` переносит тип в свой файл (раздел [`csr move`](#csr-move)). Вложенные типы не считаются.

**Замена:** нет: перенос — команда `csr move` по решению.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `baseTypes` | список строк | `["MonoBehaviour", "ScriptableObject"]` | маски имени базового типа без пространства имён и аргументов типа; пусто — любой файл с несколькими типами |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Пример**

Конфиг:

```jsonc
{ "checks": { "type-per-file": { "severity": "warning" } } }
```

Было (`Door.cs`):

```csharp
namespace Demo.Units
{
    public class Door : MonoBehaviour
    {
        private DoorState m_State;
    }

    public enum DoorState
    {
        Open,
        Closed,
    }
}
```

Находки (`--format md`):

````markdown
- 8: `type-per-file` (warning) - тип 'DoorState' не один в файле Door.cs: csr move Demo.Units.DoorState
````

`csr fix` код не меняет: у находок нет замены.


#### `closure-in-method`

Лямбда или анонимный метод внутри метода из `methods` (по умолчанию `Update`, `LateUpdate`, `FixedUpdate`), захватывающие параметры или локальные переменные метода: объект замыкания создаётся на каждом вызове. Захват по синтаксису: имя, объявленное в методе вне лямбды и использованное в ней; `const`-локальная и имя в `nameof` — не захват. Вложенные лямбды входят в находку внешней. С `captureThis` захват — и явный `this` / `base`. `static`-лямбды не проверяются.

**Замена:** нет.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `methods` | список строк | `["Update", "LateUpdate", "FixedUpdate"]` | маски имён методов, где замыкания — находка |
| `captureThis` | `true` / `false` | `false` | явный `this` или `base` в лямбде — тоже захват |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Ограничение:** обращение к полю без `this.` (тоже захват экземпляра) по синтаксису не отличить от статического члена — не находка.

**Пример**

Конфиг:

```jsonc
{ "checks": { "closure-in-method": { "severity": "hint" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Demo : MonoBehaviour
{
    private readonly List<Unit> m_Units = new();

    private void Update()
    {
        var range = 10f;
        m_Units.RemoveAll(unit => unit.Distance > range);
        m_Units.Sort(static (a, b) => a.Order - b.Order);
    }
}
```

Находки (`--format md`):

````markdown
- 10: `closure-in-method` (hint) - лямбда в 'Update' захватывает range
````

`csr fix` код не меняет: у находок нет замены.


#### `nested-loop`

Вложенный цикл по коллекции, не зависящей от внешнего цикла: перебор пар, O(N²). Внутренний цикл — `foreach` или `for` с условием по члену размера из `sizeMembers` (`i < list.Count`). Не находка, если коллекция внутреннего цикла использует имя, объявленное во внешнем цикле (обход иерархии: строка → её ячейки), и если она из `ignoreCollections` (короткие постоянные наборы: направления, смещения). Внешний цикл — `foreach` или `for`: `while` и `do` — обход очереди, не перебор пар. Граница поиска внешнего цикла — лямбда и локальная функция.

**Замена:** нет: замена — индекс (словарь, множество) или сортировка, решает агент.

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `sizeMembers` | список строк | `["Count", "Length"]` | члены размера коллекции в условии `for` |
| `ignoreCollections` | список строк | `[]` | маски коллекций внутреннего цикла (текст выражения без пробелов), которые не находка |
| `excludePaths` | список строк | `[]` | маски путей файлов, где проверка не работает (`*` — любые символы, разделитель `/`) |

**Ограничение:** размер коллекций по синтаксису неизвестен: обход сетки `for` в `for` (строки × столбцы) и перебор двух коротких списков — тоже находка, поэтому уровень в примере `hint`.

**Пример**

Конфиг:

```jsonc
{ "checks": { "nested-loop": { "severity": "hint" } } }
```

Было (`Demo.cs`):

```csharp
using System.Collections.Generic;

public class Demo
{
    private void Link(List<Unit> units)
    {
        foreach (var unit in units)
        {
            foreach (var other in units)
                unit.Touch(other);

            foreach (var part in unit.Parts)
                part.Reset();
        }
    }
}
```

Находки (`--format md`):

````markdown
- 9: `nested-loop` (hint) - вложенный цикл по 'units' внутри цикла по 'units': O(N²)
````

`csr fix` код не меняет: у находок нет замены.

## 5. Признаки `arch`

Признак — повод решить, а не нарушение. У каждого — вопрос-решение (поле `question` в сводке отчёта), который агент или человек разрешает, глядя на код. Замен нет. Признаки по файлу работают в обоих режимах `csr arch`, признаки по решению — только с `--solution`.

У любого признака есть параметры `paths` и `excludePaths` (маски путей файлов, `*` — любые символы, без учёта регистра; по умолчанию пустые: `paths` пустой — все файлы), а также `severity`, `rule`, `enabled`. В таблицах ниже они не повторяются.

Примеры файловых признаков запускались командой `csr arch Demo.cs --config config.jsonc [--project project.jsonc]`, признаков по решению — `csr arch --solution Demo/Demo.csproj --config config.jsonc` на одном проекте из одиннадцати файлов (код — в подразделах).

### По одному файлу

#### `type-branch`

Ветвление по конкретному типу: `x is T t`, `x is T { }`, `x is T`, `x as T`, `x.GetType() == typeof(T)`, `case T t:` и `T t =>` в `switch`. `switch` по типам — одна находка со списком типов. `this is T` — база знает наследника; тоже находка.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Может ли тип сам отдать нужное через метод базы или интерфейса, чтобы этот код не знал частный случай?

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `ignoreInterfaces` | `true` / `false` | `true` | не искать проверки на интерфейс (имя I и заглавная буква) |
| `ignoreTypes` | список строк | `[]` | маски имен типов, проверки на которые не ищутся |
| `asCast` | `true` / `false` | `true` | искать приведение x as T |
| `ignoreMethods` | список строк | `["Equals"]` | маски имен методов, где проверки типа не ищутся |

**Пример**

Конфиг:

```jsonc
{ "checks": { "type-branch": { "severity": "warning", "rule": "A1" } } }
```

Код (`Demo.cs`):

```csharp
public abstract class Shape
{
}

public class Circle : Shape
{
    public float Radius;
}

public class Square : Shape
{
    public float Side;
}

public class Renderer
{
    public float Area(Shape shape)
    {
        if (shape is Circle circle)
            return 3.14f * circle.Radius * circle.Radius;

        var square = shape as Square;
        if (square != null)
            return square.Side * square.Side;

        return 0f;
    }

    public string Name(Shape shape)
    {
        switch (shape)
        {
            case Circle c:
                return "circle";
            case Square s:
                return "square";
        }

        return "";
    }
}
```

Находки (`--format md`):

````markdown
- 19: `type-branch` A1 (warning) - проверка на тип 'Circle': shape is Circle circle
- 22: `type-branch` A1 (warning) - проверка на тип 'Square': shape as Square
- 31: `type-branch` A1 (warning) - switch по типам: Circle, Square
````


**Ограничения**

- Не ищутся: встроенные типы, параметры типа, интерфейсы по имени (`I` и заглавная буква; `ignoreInterfaces`), `x is A.B` без переменной (не отличить от значения `enum`), `case T:` без переменной, `OfType<T>()`, методы `Equals` (`ignoreMethods`).
- `asCast: false` исключает `x as T`.

#### `behaviour-logic`

Логика в компоненте сцены: у наследника базового типа (`baseTypes`, по умолчанию `MonoBehaviour`) больше `maxMethods` своих методов — не сообщений движка (`messages`), не отрисовки, не помеченных атрибутами из `ignoreAttributes`. Вызов поиска компонента (`lookupMethods`: `GetComponent` и др.) у другого объекта в классе, который не компонент сцены, — логика достаёт сцену.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Это представление, данные сцены, редактор или стенд? Если логика - в обычный класс; компонент сцены отдает данные

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `baseTypes` | список строк | `["MonoBehaviour"]` | маски простых имен базовых типов компонента сцены |
| `messages` | список строк | список из 31 значения (ниже) | маски методов-сообщений движка и отрисовки, которые не считаются |
| `ignoreAttributes` | список строк | `["ContextMenu"]` | методы с этими атрибутами не считаются |
| `maxMethods` | целое число | `0` | сколько своих методов допустимо |
| `lookupMethods` | список строк | список из 7 значений (ниже) | вызовы поиска компонентов, которые ищутся вне компонентов сцены |

<details>
<summary>Значение `messages` по умолчанию</summary>

```json
["Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset", "OnGUI", "OnDrawGizmos*", "OnTrigger*", "OnCollision*", "OnMouse*", "OnApplication*", "OnBecame*", "OnAnimator*", "OnParticle*", "OnTransform*", "OnRender*", "OnWillRender*", "OnPre*", "OnPost*", "OnRectTransform*", "OnCanvas*", "OnBeforeTransform*", "OnControllerColliderHit", "OnJointBreak*", "OnSceneGUI", "Draw*"]
```

</details>

<details>
<summary>Значение `lookupMethods` по умолчанию</summary>

```json
["GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentsInChildren", "GetComponentInParent", "GetComponentsInParent", "TryGetComponent"]
```

</details>

**Пример**

В примере `maxMethods` = 1: метод `Update` (сообщение движка) не считается, `Open` и `Close` — два своих метода.

Конфиг:

```jsonc
{ "checks": { "behaviour-logic": { "severity": "hint", "maxMethods": 1 } } }
```

Код (`Demo.cs`):

```csharp
public class Door : MonoBehaviour
{
    private void Update()
    {
    }

    public void Open()
    {
    }

    public void Close()
    {
    }
}

public class DoorHelper
{
    public void Prepare(GameObject target)
    {
        var body = target.GetComponent<Rigidbody>();
    }
}
```

Находки (`--format md`):

````markdown
- 1: `behaviour-logic` (hint) - MonoBehaviour 'Door': своих методов 2 (порог 1): Open, Close
- 20: `behaviour-logic` (hint) - 'GetComponent' вне компонента сцены: target.GetComponent<Rigidbody>()
````


**Ограничения**

- Наследование видно только напрямую: промежуточные базы проекта добавляются в `baseTypes`.
- По умолчанию `maxMethods` = 0: любой свой метод — находка.

#### `index-api`

Открытый API на числовых номерах сущностей: `public` и `internal` метод, конструктор или свойство с целым параметром или результатом, имя которого — номер сущности (последнее слово из `names`: `cell`, `fromBlock`; либо слово из `indexWords` после слова из `names`: `cellIndex`); не меньше `minOutInts` параметров `out` целого типа; `return -1`; открытая константа `-1`.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Отдать наружу объект или точку вместо номера и маркера -1? Номер массива или пути - норма

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `names` | список строк | `["cell", "node", "area", "block"]` | слова имен-номеров (без учета регистра) |
| `indexWords` | список строк | `["index", "id"]` | последние слова, после которых слово из names тоже номер |
| `minOutInts` | целое число | `2` | сколько out-параметров целого типа - находка; 0 - не проверять |
| `notFoundMarker` | `true` / `false` | `true` | искать return -1 и открытые константы -1 |

**Пример**

Конфиг:

```jsonc
{ "checks": { "index-api": { "severity": "warning" } } }
```

Код (`Demo.cs`):

```csharp
public class Grid
{
    public const int NoCell = -1;

    public int GetCellIndex(int cell)
    {
        return cell;
    }

    public bool TryFind(int x, out int cell, out int block)
    {
        cell = 0;
        block = 0;
        return true;
    }

    public int FindNode(string name)
    {
        return -1;
    }

    private int Next(int cell)
    {
        return cell + 1;
    }
}
```

Находки (`--format md`):

````markdown
- 3: `index-api` (warning) - NoCell: маркер NoCell = -1
- 5: `index-api` (warning) - GetCellIndex: результат int GetCellIndex; параметр int cell
- 10: `index-api` (warning) - TryFind: параметр int cell; параметр int block; out целых: 2
- 17: `index-api` (warning) - FindNode: результат int FindNode; return -1
````


**Ограничения**

- Приватные члены и `override` не проверяются: индекс внутри класса — норма.
- `minOutInts: 0` — не проверять `out`; `notFoundMarker: false` — не искать `-1`.

#### `static-data-methods`

Статические методы с данными первым параметром. В `static class` не меньше `minMethods` методов и не меньше `minShare` процентов принимают первым один тип. В обычном классе или структуре — не меньше `minMethods` открытых статических методов с одним чужим типом первым. `static class`, где у `minDataShare` процентов методов первый параметр — данные разных типов, — класс-свалка задач разных типов.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Перенести методы в сам тип данных (регионами)? Общая математика без привязки к одному типу - норма

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `minMethods` | целое число | `3` | сколько методов с общим первым параметром - находка |
| `minShare` | целое число | `60` | доля таких методов в static class, процентов |
| `minDataShare` | целое число | `80` | доля методов static class с данными первым параметром разных типов, процентов; 0 - не искать |
| `staticClassesOnly` | `true` / `false` | `false` | искать только в static class |
| `ignoreTypes` | список строк | список из 31 значения (ниже) | маски текста типа первого параметра, которые не считаются |

<details>
<summary>Значение `ignoreTypes` по умолчанию</summary>

```json
["Vector2*", "Vector3*", "Vector4", "Quaternion", "Color", "Color32", "Rect*", "Bounds*", "Matrix4x4", "Transform", "GameObject", "Component", "Object", "Type", "StringBuilder", "Span<*", "ReadOnlySpan<*", "IEnumerable*", "ICollection*", "IList*", "IReadOnly*", "List<*", "Dictionary<*", "HashSet<*", "Scene", "BinaryReader", "BinaryWriter", "Stream", "TextReader", "TextWriter", "*[]"]
```

</details>

**Пример**

Конфиг:

```jsonc
{ "checks": { "static-data-methods": { "severity": "warning" } } }
```

Код (`Demo.cs`):

```csharp
public class Order
{
    public int Count;
}

public static class OrderTools
{
    public static int Total(Order order)
    {
        return order.Count;
    }

    public static bool IsEmpty(Order order)
    {
        return order.Count == 0;
    }

    public static string Format(Order order, string prefix)
    {
        return prefix + order.Count;
    }
}
```

Находки (`--format md`):

````markdown
- 6: `static-data-methods` (warning) - static class 'OrderTools': 3 из 3 методов принимают первым 'Order': Total, IsEmpty, Format
````


**Ограничения**

- Встроенные типы и типы, подходящие под `ignoreTypes` (библиотечные: `Vector3`, `Transform`, `List<*>` …), не считаются.
- `staticClassesOnly: true` — только `static class`; `minDataShare: 0` — не искать класс-свалку.

#### `project-mechanism`

Повтор механизма, который в проекте уже есть (пул, логгер, таймеры). Механизмы описаны в `project.jsonc`, а не в параметрах признака. Тип-повтор распознаётся по имени (`types`) или по полю и методам (`fields` + `methods`); вызов (`calls`) и `new` (`creates`) — в обход механизма.

**Область:** по синтаксису одного файла. **Вопрос-решение:** В проекте уже есть механизм для этой задачи - заменить им? Если свой нужен - назвать причину

**Параметры**

Собственных параметров нет.

**Пример**

Механизмы для примера — в `project.jsonc` (`--project project.jsonc`): `pool` и `logger.bypass`.

Конфиг:

```jsonc
{ "checks": { "project-mechanism": { "severity": "warning" } } }
```

`project.jsonc`:

```jsonc
{
  "logger": {
    "methods": [
      "Log.Info"
    ],
    "bypass": [
      "Console.Write*"
    ]
  },
  "mechanisms": {
    "pool": {
      "use": "ObjectPool<T>",
      "fields": [
        "Stack<*>",
        "Queue<*>"
      ],
      "methods": [
        "Rent*",
        "Return*"
      ]
    }
  }
}
```

Код (`Demo.cs`):

```csharp
using System;
using System.Collections.Generic;

public class BulletStore
{
    private readonly Stack<Bullet> m_Free = new Stack<Bullet>();

    public Bullet Rent()
    {
        return m_Free.Count > 0 ? m_Free.Pop() : new Bullet();
    }

    public void Return(Bullet bullet)
    {
        m_Free.Push(bullet);
    }
}

public class Report
{
    public void Run()
    {
        Console.WriteLine("done");
    }
}
```

Находки (`--format md`):

````markdown
- 4: `project-mechanism` (warning) - свой 'pool' в 'BulletStore' (поле Stack<Bullet>, методы Rent, Return); в проекте: ObjectPool<T>
- 23: `project-mechanism` (warning) - 'Console.WriteLine' в обход 'logger'; в проекте: Log.Info
````


**Ограничения**

- Без механизмов в `project.jsonc` признак ничего не находит.
- Файлы, где лежит сам механизм, исключаются через `excludePaths` механизма.

#### `numeric-constant`

Поведенческое число константой: `const` (поле или локальная) или `static readonly` числового типа, в словах имени которого есть подсказка из `hints` (`Radius`, `Timeout`, `Speed`, `Angle`…). Поведение подбирает дизайнер — число должно быть полем настроек.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Это число подбирает дизайнер? Тогда - поле настроек системы с подсказкой; технический допуск - оставить const

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `hints` | список строк | список из 18 значений (ниже) | слова имени поведенческого числа (DeadZone - два слова подряд) |
| `staticReadonly` | `true` / `false` | `true` | искать и static readonly поля |

<details>
<summary>Значение `hints` по умолчанию</summary>

```json
["Radius", "Distance", "Range", "Speed", "Velocity", "Acceleration", "Angle", "Timeout", "Delay", "Duration", "Interval", "Cooldown", "Time", "Threshold", "DeadZone", "Height", "Force", "Chance"]
```

</details>

**Пример**

Конфиг:

```jsonc
{ "checks": { "numeric-constant": { "severity": "hint" } } }
```

Код (`Demo.cs`):

```csharp
public class Mover
{
    private const float Radius = 2.5f;
    private const int MaxItems = 10;
    private static readonly float MoveSpeed = 4f;
}
```

Находки (`--format md`):

````markdown
- 3: `numeric-constant` (hint) - константа float Radius = 2.5f (подсказка 'Radius')
- 5: `numeric-constant` (hint) - константа float MoveSpeed = 4f (подсказка 'Speed')
````


**Ограничения**

- Составная подсказка (`DeadZone`) ищется как два слова подряд.
- `staticReadonly: false` — только `const`. Константа без подсказки в имени (`MaxItems`) не находка.

#### `numeric-literal`

Поведенческое число литералом в логике: операнд сравнения, арифметики или составного присваивания в теле члена (`speed > 0.1f`, `angle - 100f`, `timer += 0.5f`). Шум высокий — уровень подсказки.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Это число подбирает дизайнер? Тогда - поле настроек системы с подсказкой; эпсилон и допуск - оставить

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `neutral` | список строк | `["0", "1", "-1", "2", "0.5"]` | маски нейтральных значений: нормализованный текст, у отрицательных '-' |
| `integers` | `true` / `false` | `true` | искать и целые литералы |
| `ignoreMethods` | список строк | `["OnDrawGizmos*", "Draw*", "OnGUI", "GetHashCode"]` | маски имен членов, где литералы не ищутся |

**Пример**

Конфиг:

```jsonc
{ "checks": { "numeric-literal": { "severity": "hint" } } }
```

Код (`Demo.cs`):

```csharp
public class Mover
{
    private float m_Speed;
    private float m_Angle;

    public void Step(float delta)
    {
        if (m_Speed > 0.1f)
            m_Angle -= 100f * delta;
        m_Speed *= 0.5f;
    }
}
```

Находки (`--format md`):

````markdown
- 8: `numeric-literal` (hint) - число 0.1f в 'm_Speed > 0.1f'
- 9: `numeric-literal` (hint) - число 100f в '100f * delta'
````


**Ограничения**

- Не ищутся: нейтральные значения (`neutral`, маски по нормализованному тексту: `0.50f` → `0.5`, `1e-5` → `1e-05`), индексы в `[]`, инициализаторы полей и констант, атрибуты, члены из `ignoreMethods` (отрисовка).
- `integers: false` — только дробные литералы.

#### `tuple-type` (arch)

Кортеж в сигнатуре или поле. Та же проверка, что `tuple-type` в `style`; здесь у находки вопрос-решение: несколько результатов — именованная структура или `out`.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Несколько результатов - именованная структура или out?

**Параметры**

Собственных параметров нет.

**Пример**

Конфиг:

```jsonc
{ "checks": { "tuple-type": { "severity": "warning" } } }
```

Код (`Demo.cs`):

```csharp
public class Search
{
    public (int Index, float Distance) Nearest(float[] values)
    {
        return (0, values[0]);
    }
}
```

Находки (`--format md`):

````markdown
- 3: `tuple-type` (warning) - кортеж '(int Index, float Distance)' в сигнатуре или поле
````


**Ограничения**

- Кортежи в теле метода, в инициализаторах и деконструкция не проверяются. Параметров, кроме `paths` и `excludePaths`, нет.

#### `partial-file`

`partial`-тип с подсистемой в отдельном файле: часть типа лежит в файле не своего имени (`Type.Swim.cs`, а не `Type.cs`) и содержит не меньше `minFields` своих полей и `minMethods` методов.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Это обособленный аспект или режим и подсистема? Подсистему - отдельным классом за точкой расширения, группы - регионами

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `aspects` | список строк | `["Gizmos", "Debug", "g", "Designer", "Generated"]` | маски аспектов файла Type.<аспект>.cs, которые допустимы |
| `minFields` | целое число | `1` | сколько своих полей в части - находка |
| `minMethods` | целое число | `5` | сколько методов в части - находка |

**Пример**

В примере файлов три: `Player.cs`, `Player.Swim.cs`, `Player.Gizmos.cs`. Находка — только у `Player.Swim.cs`.

Конфиг:

```jsonc
{ "checks": { "partial-file": { "severity": "warning" } } }
```

Файл `Player.cs`:

```csharp
public partial class Player
{
    private int m_Health;
}
```

Файл `Player.Swim.cs`:

```csharp
public partial class Player
{
    private float m_Depth;

    public void Dive()
    {
    }

    public void Surface()
    {
    }

    public void Hold()
    {
    }

    public void Breathe()
    {
    }

    public void Drift()
    {
    }
}
```

Файл `Player.Gizmos.cs`:

```csharp
public partial class Player
{
    private int m_Mark;

    public void DrawA()
    {
    }

    public void DrawB()
    {
    }

    public void DrawC()
    {
    }

    public void DrawD()
    {
    }

    public void DrawE()
    {
    }
}
```

Находки (`--format md`):

````markdown
## `Player.Swim.cs`

- 1: `partial-file` (warning) - часть 'Player' в файле Player.Swim.cs: полей 1, методов 5, строк 24
````


**Ограничения**

- Аспект из `aspects` (`Type.Gizmos.cs`) и часть в файле `Type.cs` не ищутся: `partial` по требованию генератора — не находка.

#### `debug-draw`

Отладочная отрисовка мимо механизма проекта. Вызов из `drawMethods` в логике (не в члене из `debugMethods`) с длительностью (аргумент `durationArgument`) или без проверки флага (`if`, `?:`, `&&`, ранний `if (…) return` выше в блоке); `bool`-флаг отладки (`flagNames`) полем или свойством типа, имя которого не подходит под `settingsTypes`.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Отрисовать механизмом отрисовки проекта под флагом из настроек системы, отдельным методом или регионом Debug?

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `drawMethods` | список строк | `["Debug.DrawLine", "Debug.DrawRay"]` | маски вызовов отрисовки (текст вызова без пробелов) |
| `durationArgument` | целое число | `4` | номер аргумента длительности (с 1); 0 - не проверять |
| `debugMethods` | список строк | `["OnDrawGizmos*", "Draw*", "*Debug*", "*Gizmo*"]` | маски членов отладочной отрисовки, где вызовы не ищутся |
| `flagNames` | список строк | `["*Debug*", "Draw*"]` | маски имен bool-флагов отладки (без префикса m_, _) |
| `settingsTypes` | список строк | `["*Settings", "*Config", "*Options"]` | маски имен типов настроек, где флаги отладки - норма |

**Пример**

Конфиг:

```jsonc
{ "checks": { "debug-draw": { "severity": "warning" } } }
```

Код (`Demo.cs`):

```csharp
public class Probe
{
    private bool m_DebugDraw;

    public void Check(Vector3 from, Vector3 to)
    {
        Debug.DrawLine(from, to, Color.red, 2f);
        Debug.DrawRay(from, to);
        if (m_DebugDraw)
            Debug.DrawRay(from, to);
    }
}
```

Находки (`--format md`):

````markdown
- 3: `debug-draw` (warning) - флаг отладки 'm_DebugDraw' полем 'Probe'
- 7: `debug-draw` (warning) - Debug.DrawLine с длительностью и без флага в 'Check'
- 8: `debug-draw` (warning) - Debug.DrawRay без флага в 'Check'
````


**Ограничения**

- `durationArgument: 0` — не проверять длительность.
- Вызовы внутри `OnDrawGizmos*`, `Draw*`, `*Debug*` не ищутся (`debugMethods`).

#### `obsolete-reason`

`[Obsolete]` без причины: без аргументов или с пустой строкой.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Какая причина и на что переходить? Дописать в [Obsolete("...")]

**Параметры**

Собственных параметров нет.

**Пример**

Конфиг:

```jsonc
{ "checks": { "obsolete-reason": { "severity": "warning" } } }
```

Код (`Demo.cs`):

```csharp
using System;

public class Legacy
{
    [Obsolete]
    public void Old()
    {
    }

    [Obsolete("")]
    public void Older()
    {
    }

    [Obsolete("Use Next instead")]
    public void Previous()
    {
    }
}
```

Находки (`--format md`):

````markdown
- 5: `obsolete-reason` (warning) - [Obsolete] без причины: Old
- 10: `obsolete-reason` (warning) - [Obsolete("")] без причины: Older
````


**Ограничения**

- Параметров, кроме `paths` и `excludePaths`, нет.

#### `persisted-class`

Сохраняемый класс: у типа есть члены с атрибутами из `saveAttributes`. Находки: конструкторы есть, пустого для десериализации нет; производное свойство (только `get` с телом) помечено к сохранению; поле-кэш (`cacheNames`) без атрибута исключения из `ignoreAttributes`.

**Область:** по синтаксису одного файла. **Вопрос-решение:** Входное состояние сохраняется поштучно, производное исключено явно, пустой конструктор для десериализации есть?

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `saveAttributes` | список строк | `["JsonProperty", "DataMember"]` | атрибуты сохранения члена |
| `ignoreAttributes` | список строк | `["JsonIgnore", "NonSerialized", "IgnoreDataMember"]` | атрибуты явного исключения из сохранения |
| `constructorAttributes` | список строк | `["JsonConstructor"]` | атрибуты конструктора десериализации |
| `cacheNames` | список строк | `["*Cache*", "*Cached*"]` | маски имен полей-кэшей и производных значений (без m_, _) |

**Пример**

Конфиг:

```jsonc
{ "checks": { "persisted-class": { "severity": "warning" } } }
```

Код (`Demo.cs`):

```csharp
public class Profile
{
    [JsonProperty]
    public string Name;

    [JsonProperty]
    public int Total => m_Score * 2;

    [JsonIgnore]
    private int m_CachedLevel;

    private int m_CachedTotal;

    private int m_Score;

    public Profile(string name)
    {
        Name = name;
    }
}
```

Находки (`--format md`):

````markdown
- 1: `persisted-class` (warning) - сохраняемый 'Profile' без пустого конструктора десериализации
- 7: `persisted-class` (warning) - производное свойство 'Total' сохраняется
- 12: `persisted-class` (warning) - поле-кэш 'm_CachedTotal' без явного исключения из сохранения
````


**Ограничения**

- Структура и конструктор с атрибутом из `constructorAttributes` (`JsonConstructor`) — норма.
- Атрибуты сравниваются по имени, по синтаксису одного файла.

### По решению (`--solution`)

Проект примеров: `Demo.csproj` и одиннадцать файлов `.cs` (`Counter`, `Events`, `Program`, `Render`, `Settings`, `Slot`, `Storage`, `Toolbox`, `UseA`, `UseB`, `UseC`); перед запуском выполнен `dotnet restore`. Вывод каждого подраздела — один общий запуск `csr arch --solution Demo/Demo.csproj`, из отчёта оставлены находки своего признака.

Файл `Demo.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
```

Файл `Program.cs` (точка входа, использует классы примеров):

```csharp
public static class Program
{
    public static void Main()
    {
        var counter = new Counter();
        counter.Add(1, 2);
        var renderer = new Renderer();
        renderer.PaintBorder(1, 2, 3, 4);
        renderer.PaintFill(1, 2, 3, 4);
        renderer.PaintShadow(1, 2, 3, 4);
        IStorage storage = new FileStorage();
        storage.Save("a", "b");
    }
}
```

#### `variant-flag`

`bool`-флаг варианта: не `private` `bool`-свойство или поле, по которому ветвится код других типов (условие `if`, `while`, `?:`, `when`, операнд `&&`, `||`, `!`) не меньше чем в `minFiles` файлах. Находка стоит у объявления, в сообщении — файлы.

**Область:** по решению (`--solution`). **Вопрос-решение:** Флаг заменяет вариант поведения? Тогда - метод базы или интерфейса, который вариант реализует сам

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `minFiles` | целое число | `3` | в скольких файлах вне своего типа флаг ветвит код - находка |
| `minUses` | целое число | `0` | сколько мест ветвления всего - находка |
| `names` | список строк | `["*"]` | маски имен флагов (без учета регистра) |
| `ignoreNames` | список строк | `[]` | маски имен флагов, которые не ищутся (без учета регистра) |

**Пример**

В примере 3 файла (`UseA`, `UseB`, `UseC`) ветвятся по `Settings.FastMode`; порог `minFiles` = 3 — по умолчанию.

Конфиг:

```jsonc
{ "checks": { "variant-flag": { "severity": "hint" } } }
```

Файл `Settings.cs`:

```csharp
public class Settings
{
    public bool FastMode;
}
```

Файл `UseA.cs` (`UseB.cs` и `UseC.cs` — такие же классы с другим именем):

```csharp
public class UseA
{
    public int Run(Settings settings)
    {
        if (settings.FastMode)
            return 1;
        return 2;
    }
}
```

Находки (`--format md`):

````markdown
## `Demo/Settings.cs`

- 3: `variant-flag` (hint) - флаг 'Settings.FastMode' ветвит код других типов: мест 3 в 3 файлах: UseA.cs, UseB.cs, UseC.cs
````


**Ограничения**

- Работает только в режиме `--solution`: места считаются по всем выбранным проектам.
- `names` и `ignoreNames` — маски имён флагов без учёта регистра; `minUses` — наименьшее общее число мест ветвления (по умолчанию 0).

#### `single-implementation`

Интерфейс с одной реализацией: среди неабстрактных классов и структур выбранных проектов интерфейс (с наследованием интерфейсов) реализует один тип.

**Область:** по решению (`--solution`). **Вопрос-решение:** Интерфейс - точка расширения или граница подсистемы? Если нет - убрать, оставив класс; если да - контракт-комментарий

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `contractMasks` | список строк | `["Реализу*", "Implement*"]` | маски текста комментария-контракта над интерфейсом |
| `ignoreAttributes` | список строк | `[]` | интерфейсы с этими атрибутами не ищутся |
| `ignoreMarkers` | `true` / `false` | `true` | не искать интерфейсы без членов |

**Пример**

В примере `IStorage` — находка; `IMode` помечен контракт-комментарием; у `IShape` две реализации.

Конфиг:

```jsonc
{ "checks": { "single-implementation": { "severity": "warning" } } }
```

Файл `Storage.cs`:

```csharp
public interface IStorage
{
    void Save(string key, string value);
}

public class FileStorage : IStorage
{
    public void Save(string key, string value)
    {
    }
}

// Реализуют игровые режимы
public interface IMode
{
    void Enter();
}

public class MenuMode : IMode
{
    public void Enter()
    {
    }
}

public interface IShape
{
    float Area();
}

public class Circle : IShape
{
    public float Area() => 3.14f;
}

public class Square : IShape
{
    public float Area() => 1f;
}
```

Находки (`--format md`):

````markdown
## `Demo/Storage.cs`

- 1: `single-implementation` (warning) - интерфейс 'IStorage' - одна реализация: 'FileStorage' (Storage.cs)
````


**Ограничения**

- Работает только в режиме `--solution`.
- Интерфейс — точка расширения или граница подсистемы — получает контракт-комментарий над объявлением (`contractMasks`, по умолчанию `Реализу*` и `Implement*`) либо атрибут из `ignoreAttributes`: это не находка. Интерфейс без членов (маркер) пропускается при `ignoreMarkers: true`.

#### `dead-member`

Мёртвый код по выбранным проектам: член без ссылок; поле или свойство, которое только записывается (`writeOnly`); параметр, который не читается (`parameters`). Видимость членов — `accessibility`.

**Область:** по решению (`--solution`). **Вопрос-решение:** Удалить вместе с местами записи, если код в задаче; если вне задачи - записать отдельной задачей

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `accessibility` | список строк | `["private", "internal"]` | видимость членов: private, protected, internal, public |
| `writeOnly` | `true` / `false` | `true` | искать поля и свойства, которые только записываются |
| `parameters` | `true` / `false` | `true` | искать параметры, которые не читаются |
| `ignoreNames` | список строк | список из 27 значений (ниже) | маски имен членов, вызываемых снаружи (сообщения движка) |
| `dataAttributes` | список строк | список из 5 значений (ниже) | атрибуты сериализации: ищется только запись без чтения |
| `ignoreAttributes` | список строк | список из 12 значений (ниже) | члены и типы с этими атрибутами не ищутся |

<details>
<summary>Значение `ignoreNames` по умолчанию</summary>

```json
["Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset", "OnGUI", "OnDrawGizmos*", "OnTrigger*", "OnCollision*", "OnMouse*", "OnApplication*", "OnBecame*", "OnAnimator*", "OnParticle*", "OnTransform*", "OnRender*", "OnBefore*", "OnAfter*", "GetEnumerator", "Dispose", "Deconstruct", "Main"]
```

</details>

<details>
<summary>Значение `dataAttributes` по умолчанию</summary>

```json
["SerializeField", "Serializable", "SerializeReference", "JsonProperty", "DataMember"]
```

</details>

<details>
<summary>Значение `ignoreAttributes` по умолчанию</summary>

```json
["Test*", "SetUp", "TearDown", "OneTime*", "MenuItem", "ContextMenu", "RuntimeInitializeOnLoadMethod", "InitializeOnLoad*", "UsedImplicitly", "Preserve", "DllImport", "Obsolete"]
```

</details>

**Пример**

В примере метод `Clear` назван не `Reset`: имя `Reset` в `ignoreNames` (сообщение движка).

Конфиг:

```jsonc
{ "checks": { "dead-member": { "severity": "warning" } } }
```

Файл `Counter.cs`:

```csharp
public class Counter
{
    private int m_Total;
    private int m_Written;
    private int m_Unused;

    public int Add(int value, int scale)
    {
        m_Written = value;
        m_Total += value;
        return m_Total;
    }

    private void Clear()
    {
        m_Total = 0;
    }
}
```

Файл `Slot.cs`:

```csharp
public class SerializeFieldAttribute : Attribute
{
}

public class Slot
{
    [SerializeField]
    private int m_Capacity;

    public void Fill()
    {
        m_Capacity = 5;
    }
}
```

Находки (`--format md`):

````markdown
## `Demo/Counter.cs`

- 4: `dead-member` (warning) - поле 'Counter.m_Written' только записывается: мест 1
- 5: `dead-member` (warning) - поле 'Counter.m_Unused' не используется
- 7: `dead-member` (warning) - параметр 'scale' метода 'Counter.Add' не читается
- 14: `dead-member` (warning) - метод 'Counter.Clear' не используется

## `Demo/Events.cs`

- 20: `dead-member` (warning) - параметр 'handler' метода 'EventBus.Subscribe' не читается

## `Demo/Slot.cs`

- 8: `dead-member` (warning) - поле 'Slot.m_Capacity' только записывается: мест 1 (сохраняется: удаление - с миграцией)
````


**Ограничения**

- Работает только в режиме `--solution`. Ссылки считаются только внутри выбранных проектов: `public` по умолчанию не ищется (потребители могут быть вне решения, в Lua или через рефлексию).
- Не ищутся: `override`, `virtual`, `abstract`, реализации интерфейсов, сообщения движка и члены из `ignoreNames` (в том числе `Reset`), члены с атрибутами из `ignoreAttributes` (тесты, вызов движком), члены перечислений, конструкторы, операторы.
- Данные (члены с атрибутами из `dataAttributes`, поля типа с таким атрибутом) заполняет загрузка или инспектор: для них ищется только запись без чтения; в сообщении — «сохраняется: удаление — с миграцией».

#### `parameter-set`

Повторяющийся набор параметров: метод с `minParameters` и больше входными параметрами (без `out`), вызываемый в `minCalls` и больше местах; в `minForwarded` и больше вызовах аргументы — параметры вызывающего метода, переданные дальше по цепочке.

**Область:** по решению (`--solution`). **Вопрос-решение:** Собрать набор в структуру аргументов (...Args)? Методы остаются на месте

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `minParameters` | целое число | `4` | сколько входных параметров - набор |
| `minCalls` | целое число | `3` | сколько вызовов - находка |
| `minForwarded` | целое число | `1` | в скольких вызовах набор передается по цепочке; 0 - не требовать |

**Пример**

В примере `Paint` вызывают три метода; в двух из них аргументы — собственные параметры вызывающего.

Конфиг:

```jsonc
{ "checks": { "parameter-set": { "severity": "hint" } } }
```

Файл `Render.cs`:

```csharp
public class Renderer
{
    public void PaintBorder(int x, int y, int width, int height)
    {
        Paint(x, y, width, height);
    }

    public void PaintFill(int x, int y, int width, int height)
    {
        Paint(x, y, width, height);
    }

    public void PaintShadow(int x, int y, int width, int height)
    {
        Paint(x + 1, y + 1, width, height);
    }

    private void Paint(int x, int y, int width, int height)
    {
        Console.WriteLine(x + y + width + height);
    }
}
```

Находки (`--format md`):

````markdown
## `Demo/Render.cs`

- 18: `parameter-set` (hint) - метод 'Renderer.Paint' (4 входных параметров): вызовов 3, из них 2 передают параметры вызывающего дальше
````


**Ограничения**

- Работает только в режиме `--solution`. `minForwarded: 0` — не требовать передачи по цепочке.

#### `unraised-event`

Событие без отправителя по выбранным проектам. Тип события — наследник базы или интерфейса из `eventTypes` — нигде не создаётся: нет `new`, `default(T)`, фабрики `T.Create()` и вызова метода из `raiseMethods` с типом события в аргументе типа (`Raise<T>()`), а обработчики есть или нет. Событие C# (`event`-поле), которое не поднимается в своём типе (нет вызова и чтения), но на которое подписываются (`fieldEvents`).

Отправитель ищется только в выбранных проектах. Если событие поднимает проект вне `--projects` (например, сборка UI, которая ссылается на сборку с событием), находка будет ложной. Проекты-отправители добавляйте в выборку (`--projects Code --projects "Game.UI*"`). Чтобы находки в этих проектах не попадали в отчёт, задайте `paths` / `excludePaths`.

**Область:** по решению (`--solution`). **Вопрос-решение:** Отправитель потерян или событие устарело? Устарело - удалить вместе с обработчиками; потерян - вернуть отправку

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `eventTypes` | список строк | `["IEvent", "IEvent<*>"]` | маски баз и интерфейсов типа события |
| `raiseMethods` | список строк | `["Raise*"]` | маски методов, которые поднимают событие по аргументу типа |
| `fieldEvents` | `true` / `false` | `true` | искать события C# (event), на которые подписываются |

**Пример**

В примере `DoorClosed` создаётся через `EventBus.Raise<DoorClosed>()`, `DoorOpened` — нигде; событие `Door.Jammed` не поднимается, на него подписан `DoorListener`.

Конфиг:

```jsonc
{ "checks": { "unraised-event": { "severity": "warning" } } }
```

Файл `Events.cs`:

```csharp
public interface IEvent
{
}

public class DoorOpened : IEvent
{
}

public class DoorClosed : IEvent
{
}

public static class EventBus
{
    public static void Raise<T>()
        where T : IEvent
    {
    }

    public static void Subscribe<T>(Action<T> handler)
        where T : IEvent
    {
    }
}

public class Door
{
    public event Action Jammed;

    public void Close()
    {
        EventBus.Raise<DoorClosed>();
    }
}

public class DoorListener
{
    public void Attach(Door door)
    {
        EventBus.Subscribe<DoorOpened>(_ => { });
        EventBus.Subscribe<DoorClosed>(_ => { });
        door.Jammed += OnJammed;
    }

    private void OnJammed()
    {
    }
}
```

Находки (`--format md`):

````markdown
## `Demo/Events.cs`

- 5: `unraised-event` (warning) - событие 'DoorOpened' никто не поднимает; обрабатывают: DoorListener
- 28: `unraised-event` (warning) - событие 'Door.Jammed' нигде не поднимается; подписок: 1
````


**Ограничения**

- Работает только в режиме `--solution`.
- Не ищутся: абстрактные типы событий и базы других типов, `override` и `abstract` события.

#### `class-cohesion`

Класс-комбайн: крупный класс (`partial` — все файлы), методы и свойства которого распадаются на части, не связанные вызовами и общими полями (связные компоненты графа «член — член, который он использует»; мера LCOM4). Находка — от двух частей по `minPartSize` методов; в сообщении у каждой части — методы, поля, регионы и внешние потребители.

**Область:** по решению (`--solution`). **Вопрос-решение:** Части - разные задачи? Если да - вынести часть в свой класс; если нет - оставить

**Параметры**

| Параметр | Значения | По умолчанию | Описание |
|---|---|---|---|
| `minMethods` | целое число | `12` | методов в классе, от которых он проверяется |
| `minPartSize` | целое число | `3` | методов в части, чтобы она считалась |
| `ignoreNames` | список строк | список из 12 значений (ниже) | маски методов, которые не связывают части (инициализация, очистка) |

<details>
<summary>Значение `ignoreNames` по умолчанию</summary>

```json
["Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset", "Dispose", "Clear", "Init", "Initialize", "ToString"]
```

</details>

**Пример**

В примере `Toolbox` — 12 методов в двух частях по шесть, каждая работает со своим полем.

Конфиг:

```jsonc
{ "checks": { "class-cohesion": { "severity": "hint" } } }
```

Файл `Toolbox.cs`:

```csharp
public class Toolbox
{
    private int m_Width;
    private int m_Total;

    public void Widen()
    {
        m_Width++;
    }

    public void Narrow()
    {
        m_Width--;
    }

    public void Scale()
    {
        Widen();
        Narrow();
    }

    public int Measure()
    {
        return m_Width;
    }

    public void Fit()
    {
        m_Width = Measure();
    }

    public void Stretch()
    {
        m_Width *= 2;
        Fit();
    }

    public void Add()
    {
        m_Total++;
    }

    public void Remove()
    {
        m_Total--;
    }

    public void Sum()
    {
        Add();
        Remove();
    }

    public int Count()
    {
        return m_Total;
    }

    public void Fold()
    {
        m_Total = Count();
    }

    public void Collapse()
    {
        m_Total = 0;
        Fold();
    }
}
```

Находки (`--format md`):

````markdown
## `Demo/Toolbox.cs`

- 1: `class-cohesion` (hint) - 'Toolbox': несвязанных частей 2 (методов 12): 1) Widen, Narrow, Scale, Measure и еще 2 | поля m_Width; 2) Add, Remove, Sum, Count и еще 2 | поля m_Total
````


**Ограничения**

- Работает только в режиме `--solution`.
- Конструкторы и методы из `ignoreNames` (инициализация, очистка: трогают все поля) в связях не участвуют; свойства связывают, но в размер части не входят (класс данных — группы свойств вокруг полей).
- Классы и структуры меньше `minMethods` методов не проверяются.


## 6. Метрики `metrics`

Метрики — по синтаксису каждого файла, без загрузки решения. Функция — метод, конструктор, деструктор, оператор, аксессор, локальная функция, свойство и индексатор с телом-выражением; лямбды входят в объемлющую функцию, локальные функции считаются отдельно.

| Уровень | Метрика (ключ) | Определение | Порог по умолчанию |
|---|---|---|---:|
| функция | `cyclomatic` | цикломатическая сложность по правилам crapper: 1 + `if`, циклы, `catch`, `?:`, метки `case`, ветви switch-выражения, `&&`, `\|\|`, `??`, `?.` | 10 |
| функция | `cognitive` | когнитивная сложность по спецификации SonarSource (G. Ann Campbell, 2017): +1 и надбавка за вложенность — `if`, `?:`, `switch`, switch-выражение, циклы, `catch`; +1 без надбавки — `else if`, `else`, `goto`, рекурсивный вызов, каждая последовательность одинаковых `&&` / `\|\|`; `??` и `?.` не считаются. Отличия от спецификации: рекурсия — по имени и числу аргументов (без семантики), только прямая | 15 |
| функция | `lines` | физические строки: от первой до последней строки функции | 60 |
| функция | `code-lines` | строки кода: строки, на которых есть хотя бы один токен; пустые строки, комментарии, директивы препроцессора и выключенный директивами код не считаются | — |
| функция | `nesting` | наибольшая вложенность операторов в теле: уровни `if` (`else if` — тот же уровень), циклов, `try`, `switch`, `using`, `lock`; функция с телом-выражением или без тела — 0 | 3 |
| функция | `parameters` | число параметров | 5 |
| тип | `lines` | физические строки типа | — |
| тип | `code-lines` | строки кода типа | — |
| тип | `members` | число членов (поля и события-поля считаются по переменным: `int a, b;` — два поля); в JSON — число членов по видам | — |
| тип | `regions` | число регионов `#region` | — |
| файл | `lines` | физические строки файла | 400 |
| файл | `code-lines` | строки кода файла | 316 |
| файл | `types` | число типов в файле | — |

Пороги по умолчанию: сложность — crapper и SonarSource, вложенность и размер файла — замеры на эталонном коде. «Выше порога» — значение строго больше порога.

**CRAP** (`--coverage`): `CRAP = CC² × (1 − покрытие)³ + CC`, как в crap4clj / crap4java; `CC` — цикломатическая сложность, покрытие — доля от 0 до 1. Покрытие читается из OpenCover XML (Unity Code Coverage, coverlet, OpenCover): покрытие функции — доля выполненных точек последовательности в её строках; функция без точек или отсутствующая в отчёте — покрытие 0. Несколько `--coverage` объединяются по точкам (строка и колонка): точка покрыта, если покрыта хоть в одном отчёте. Значения округляются до 4 знаков. В отчёт добавляются покрытие и CRAP функций и топ по CRAP; пороги CRAP нет.

Исходник примеров (`Sorter.cs`) и покрытие: `coverage.opencover.xml` — отчёт OpenCover с девятью точками последовательности (строки 7, 8, 10, 12, 20, 21 выполнены; 14, 15 и 24 — нет).

```csharp
public class Sorter
{
    private int m_Swaps;

    public int Sort(int[] items, bool descending)
    {
        var swaps = 0;
        for (var i = 0; i < items.Length; i++)
        {
            for (var j = i + 1; j < items.Length; j++)
            {
                if (descending ? items[j] > items[i] : items[j] < items[i])
                {
                    (items[i], items[j]) = (items[j], items[i]);
                    swaps++;
                }
            }
        }

        m_Swaps += swaps;
        return swaps;
    }

    public int Swaps => m_Swaps;
}
```

Пример (`csr metrics Sorter.cs --coverage coverage.opencover.xml --edn crap.edn --format md --top 3`, раздел CRAP; у `Sort` 6 из 8 точек покрыты — 75 %: `25 × 0,25³ + 5 = 5,3906`):

````markdown
## CRAP

| CRAP | Покрытие, % | Сложность | Имя | Место |
|---:|---:|---:|---|---|
| 5.3906 | 75 | 5 | `Sorter.Sort` | `Sorter.cs:5` |
| 2 | 0 | 1 | `Sorter.Swaps.get` | `Sorter.cs:24` |
````

Снимок `crap.edn` (формат `.metrics/crap.edn`: `:namespace` — пространство имён и типы, `:name` — член типа):

```clojure
{:entries [
  {:name "Sort", :namespace "Sorter", :complexity 5, :coverage 75.0, :crap 5.3906}
  {:name "Swaps.get", :namespace "Sorter", :complexity 1, :coverage 0.0, :crap 2.0}
]}
```

**Покрытие и CRAP самого CSR.** Покрытие тестами собирается coverlet в формате OpenCover; по одному отчёту на тестовый проект, все — в один `csr metrics`:

```
dotnet test CSR.slnx --collect "XPlat Code Coverage" --results-directory TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
csr metrics src --coverage TestResults/<id1>/coverage.opencover.xml --coverage TestResults/<id2>/coverage.opencover.xml --coverage TestResults/<id3>/coverage.opencover.xml --format md
```
