# CSRefactor (`csr`)

Консольный инструмент анализа и рефакторинга C# для ИИ-агентов. Механику проверяет и правит инструмент, агенту остаются суждения: агент получает готовый отчёт и не читает длинные инструкции по стилю.

Статус: работают `csr metrics`, `csr style`, `csr fix`, `csr arch` (признаки по одному файлу), `csr refs`, `csr rename` и `csr serve`; остальные команды в разработке.

## Что будет уметь

- `csr style` — проверка стиля по конфигу (работает: форматирование, комментарии, идиомы, модификаторы; дальше — имена и порядок членов).
- `csr arch` — проверка архитектурных признаков; отдельный режим и отдельный конфиг (работает: признаки по одному файлу; дальше — по всему решению).
- `csr format` — форматирование только изменённых строк (движок — ReSharper Command Line Tools).
- `csr refs`, `csr rename` — ссылки на символ и семантическое переименование с защитой сериализуемых имён (работает); `csr serve` держит решение загруженным.
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
| `binary-op-wrap` | место `&&` / `\|\|` при переносе (замена — перенос оператора на соседнюю строку) | `prefer`: `leading` / `trailing`, `operators` |
| `xml-doc` | документационный комментарий `///` (замена по `explanation`: `comment` — текст `<summary>` одной строкой `//`, если других текстов нет и строка помещается; блок без текста удаляется; иначе без замены) | `explanation`: `comment` / `delete` / `none`, `max` |
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
| `expression-arrow` | место `=>` у члена с телом-выражением: в конце строки; у свойства — не с новой строки; у метода — строка длиннее `max` (замена — перенос `=>` на следующую строку +4) | `properties`: `next-line` / `any`, `methods`: `fit` / `next-line` / `any`, `max` |
| `signature-wrap` | сигнатура не столбиком: однострочная длиннее `max` или с `minParameters`+ параметрами, перенос посреди списка, `)` на отдельной строке (замена — параметр на строку +4, `: base(…)` отдельной строкой) | `max`, `minParameters`, `initializerOnOwnLine` |
| `call-wrap` | длинный вызов в одну строку, перенос посреди списка аргументов (замена — аргумент на строку +4; вложенный — следующим проходом `csr fix`); не трогает условия `if` / `while`, логгер, многострочные аргументы и строку продолжения, которая помещается с отступом +4; вызов внутри тернарника, бинарного выражения или цепочки вызовов — находка без замены (F24, F25, F20) | `max` |
| `region-layout` | пустые строки вокруг `#region` / `#endregion`: внутри, между регионами, до члена вне региона; у `{` и `}` типа — вплотную | `inside`, `between`, `outside` |
| `condition-wrap` | цепочка `&&` / `\|\|` в строке длиннее `max` (`if`, `while`, `return`, инициализатор переменной) — по операнду на строку с оператором в начале, +4; аргументы вызова в перенесённом условии `if` / `while` на уровне тела — на +8; строка продолжения цепочки — на +4 к инструкции (в `if` / `while` всегда, в присваивании — по `assignmentIndent`, `return` не трогается) | `max`, `argumentIndent`, `continuationIndent`, `assignmentIndent` |
| `initializer-layout` | инициализатор объекта, коллекции, массива: однострочный длиннее `max`; в многострочном — `{` в строке с `new`, два элемента в строке, нет запятой после последнего (замена — `{` под `new`, элемент на строку +4) | `max`, `trailingComma` |

Методы логгера для `log-call-wrap`, `message-text` и `comment-typography` — в `project.jsonc` (`logger.methods`, маски с `*`). Пример конфига — `examples/style.example.jsonc`.

## `csr arch`

Архитектурные признаки по синтаксису каждого файла, без загрузки решения: файлы только читаются. Признак — не ошибка, а повод решить: находка говорит, что сработало, а у признака в сводке — вопрос, который решает агент или человек. Режим и конфиг отдельные от `style`.

```
csr arch <файлы | папки>... --config arch.jsonc [--config ещё.jsonc]... [--project project.jsonc] [--diff <ревизия | файл.diff>] [--scope file | changed-lines] [--format json | md] [--out файл]
csr arch --solution <файл .sln | .slnx | .csproj> [--projects <маска>]... --config arch.jsonc... [--project project.jsonc] [--format json | md] [--out файл] [--no-server]
```

Режим решения (`--solution`, `--projects`): решение загружается слоем `CSR.Workspace` (или берётся у живого `csr serve`), признаки по одному файлу идут по документам выбранных проектов, признаки по решению — по их компиляциям. `--projects` — маски имён проектов (без них — все проекты); в отчёте `scope` — `solution`.

- Конфиг — формат `style.jsonc` с признаками ниже: уровень, метка `rule`, параметры, `"enabled": false`, слои; признак вне конфига выключен.
- У каждого признака есть `paths` и `excludePaths` — маски путей файлов (`*` — любые символы, без учёта регистра): например, ветвление по типу искать только в общем коде, логику в компонентах сцены — не в редакторном и тестовом.
- JSON: `summary[]` (`check`, `rule`, `severity`, `count`, `question`) и `findings[]` (`file`, `line`, `endLine`, `column`, `check`, `rule`, `severity`, `message`). Коды возврата — как у `style`.

| Признак | Что ловит | Параметры |
|---|---|---|
| `type-branch` | `x is T t`, `x is T { }`, `x is T`, `x as T`, `GetType() == typeof(T)`; `switch` по типам — одна находка; `this is T` — база знает наследника. Не ловит: встроенные типы, параметры типа, интерфейсы по имени, `x is A.B` без переменной, `Equals` | `ignoreInterfaces`, `ignoreTypes`, `asCast`, `ignoreMethods` |
| `behaviour-logic` | у наследника `MonoBehaviour` больше `maxMethods` своих методов (не сообщения движка и не отрисовка); `x.GetComponent…()` в классе, который не компонент сцены | `baseTypes`, `messages`, `ignoreAttributes`, `maxMethods`, `lookupMethods` |
| `index-api` | открытые методы, конструкторы и свойства с целым номером сущности (`int cell`, `fromBlock`, `cellIndex`), несколько `out int`, `return -1`, открытая константа `-1` | `names`, `indexWords`, `minOutInts`, `notFoundMarker` |
| `static-data-methods` | `static class`, где методы принимают первым один тип данных (или разные типы данных у большинства методов — класс-свалка); открытые статические методы обычного класса с чужим типом первым | `minMethods`, `minShare`, `minDataShare`, `staticClassesOnly`, `ignoreTypes` |
| `numeric-constant` | `const` и `static readonly` числа с именем-подсказкой (`Radius`, `Timeout`, `Speed`, `Angle`…) | `hints`, `staticReadonly` |
| `numeric-literal` | число в сравнении, арифметике, `+=` в логике (`speed > 0.1f`); шумный, уровень подсказки | `neutral`, `integers`, `ignoreMethods` |
| `tuple-type` | кортеж в сигнатуре или поле | — |
| `partial-file` | часть `partial`-типа в файле `Type.<Аспект>.cs` со своими полями и методами (подсистема); аспекты вроде `Gizmos` допустимы | `aspects`, `minFields`, `minMethods` |
| `debug-draw` | `Debug.DrawLine` / `DrawRay` в логике с длительностью или без проверки флага; `bool`-флаг отладки полем объекта, а не настроек | `drawMethods`, `durationArgument`, `debugMethods`, `flagNames`, `settingsTypes` |
| `obsolete-reason` | `[Obsolete]` без причины | — |
| `variant-flag` | только `--solution`: не private bool-свойство или поле, по которому ветвится код других типов (`if`, `while`, `?:`, `when`, операнды `&&`, `\|\|`, `!`) в `minFiles` файлах и больше; находка у объявления со списком файлов | `minFiles`, `minUses`, `names`, `ignoreNames` |
| `persisted-class` | тип с членами под атрибутами сохранения (`JsonProperty`, `DataMember`): конструкторы есть, пустого нет; производное свойство помечено к сохранению; поле-кэш без атрибута исключения | `saveAttributes`, `ignoreAttributes`, `cacheNames` |
| `single-implementation` | только `--solution`: интерфейс, который реализует один неабстрактный тип выбранных проектов; контракт-комментарий над интерфейсом (`// Реализуют …`) — точка расширения, не находка | `contractMasks`, `ignoreAttributes`, `ignoreMarkers` |
| `dead-member` | только `--solution`: член без ссылок, поле или свойство, которое только записывается, параметр, который не читается; override, virtual, реализации интерфейсов, сообщения движка и тесты не ищутся; у данных (атрибуты сериализации, поля `[Serializable]`-типа) — только запись без чтения, с пометкой «сохраняется: удаление — с миграцией» | `accessibility` (по умолчанию `private`, `internal`), `writeOnly`, `parameters`, `ignoreNames`, `dataAttributes`, `ignoreAttributes` |
| `parameter-set` | только `--solution`: метод с 4+ входными параметрами и 3+ вызовами, где параметры вызывающего передаются дальше | `minParameters`, `minCalls`, `minForwarded` |
| `project-mechanism` | повтор механизма, который в проекте уже есть: тип по имени или по полю и методам (свой пул: поле `Stack<T>` и `Rent` / `Return`), вызов или `new` в обход механизма | — (механизмы — в `project.jsonc`) |

Механизмы проекта для `project-mechanism` — в `project.jsonc`: `mechanisms` (имя → `use` — чем заменить; `types`, `fields`, `methods`, `calls`, `creates` — как распознать повтор; `excludePaths`) и `logger.bypass` (вызовы в обход логгера: замена — `logger.methods`). Пример — `examples/project.example.jsonc`.

Признаки по решению считают ссылки только внутри выбранных проектов: член, которым пользуется проект вне `--projects`, Lua или рефлексия, попадёт в находки — поэтому `public` в `dead-member` по умолчанию не ищется. Пример конфига — `examples/arch.example.jsonc`.

## `csr fix`

Замены находок `csr style` (`replacement`) применяются к файлам: агент получает исправленный код и правит только то, что требует суждения. По умолчанию — дифф на stdout (пути — как в отчёте `style`), сводка в stderr; запись — только `--apply` (кодировка, BOM и переводы строк сохраняются).

```
csr fix <файлы | папки>... --config style.jsonc [--config ещё.jsonc]... [--project project.jsonc] [--diff <ревизия | файл.diff>] [--scope file | changed-lines] [--checks a,b] [--hints] [--apply] [--format diff | json]
```

- Проверки запускаются заново, замены применяются проходами: за проход — все, что не пересекаются по строкам, затем файл разбирается снова (несколько находок на одной строке — по очереди). До устойчивого текста, не больше 8 проходов.
- По умолчанию — замены уровней `error` и `warning`; `--hints` — и `hint` (`coalesce-assignment` путает объекты Unity, `commented-code` удаляет строки — суждение). `--checks` — только перечисленные проверки.
- `--scope changed-lines` — только находки на изменённых строках; строки, сдвинутые заменами выше, отслеживаются.
- Если после замен в файле больше синтаксических ошибок, чем было, файл не меняется и попадает в список `reverted` — это ошибка замены проверки.
- `--format json`: `applied`, `fixed[]` (`check`, `rule`, `count`), `files[]`, `diff`, `reverted[]`, `remaining` — отчёт `csr style` по исправленному тексту.
- Коды возврата: 0 — после исправлений нет находок уровня `error`, 1 — есть, 2 — ошибка запуска.

## `csr refs`

Все ссылки на символ по решению (Roslyn `SymbolFinder`) — для агента вместо поиска текстом. Решение загружается через `MSBuildWorkspace`; файлы только читаются.

```
csr refs <символ> [--solution файл] [--format json | text] [--no-server]
```

- Символ: `Namespace.Type.Member`, `Namespace.Type.Method(int, Item)` (перегрузка) или `файл.cs:строка[:колонка]` (с 1; без колонки — первое объявление в строке, иначе первый символ, на который ссылается строка). Без параметров в запросе — все перегрузки метода. Неоднозначный символ — ошибка со списком кандидатов в той же записи. Типы и члены из DLL (`UnityEngine.MonoBehaviour`) тоже ищутся.
- Решение: `--solution` (`.sln`, `.slnx`, `.csproj`) или ближайшее `.sln` / `.slnx` вверх от файла символа и текущей папки, затем в её подпапках.
- Ответ — сам символ и связанные: переопределения, реализации интерфейсов, аксессоры (реализации — даже без ссылок). У ссылки — файл (от папки решения), строка, колонка, строка кода, член-контейнер и пометки `generated` (код генератора), `implicit`, `candidate`. `--format text` — строки `файл:строка:колонка: код`.
- Коды возврата: 0 — успех, 2 — ошибка запуска или символ не найден.

## `csr rename`

Семантическое переименование (Roslyn `Renamer`). По умолчанию — только дифф на stdout (unified, пути `a/` и `b/` от папки решения, применим `git apply`), сводка — в stderr; запись файлов — только с `--apply`.

```
csr rename <символ> <новое имя> [--solution файл] [--config refactor.jsonc] [--format diff | json] [--apply] [--migrate] [--force] [--no-verify] [--no-strings] [--no-server]
```

- Сериализуемые имена — отказ с объяснением: поле, которое сериализует Unity (`[SerializeField]`, `[SerializeReference]`, публичное поле экземпляра `[Serializable]`-типа и наследника `UnityEngine.Object`, автосвойство с `[field: SerializeField]`), `[JsonProperty]` и `[DataMember]` без явного имени, атрибуты из конфига; методы-сообщения Unity (`Update`, `OnEnable`…) — отказ всегда.
- `--migrate` сохраняет старое имя для данных: `[FormerlySerializedAs("старое")]` (для автосвойства — `[field: FormerlySerializedAs("<Старое>k__BackingField")]`, `using UnityEngine.Serialization` добавляется), `[JsonProperty("старое")]`, `[DataMember(Name = "старое")]`. Поле в объявлении с несколькими переменными (`public int a, b;`) не переносится — отказ.
- Строковые совпадения старого имени целым словом — список «проверить вручную», переименование их не меняет: строковые литералы C# (`nameof` — не строка, его правит `Renamer`), файлы `.lua`, `.json`, `.yaml`, `.yml`, `.xml`; ассеты Unity (`.prefab`, `.asset`, `.unity`) — только если имя может быть в сериализованных данных: сериализуемое поле, тип `UnityEngine.Object` или `[Serializable]`, публичный метод компонента (`UnityEvent`).
- Проверка компиляцией (`--no-verify` — без неё): затронутые проекты до и после; если ошибок стало больше — база «пустая правка» тех же файлов (генераторы кода дают ошибки на любую правку в памяти). Новые ошибки — список и код 1.
- Замечания: файл назван по типу (переименовать вручную; для `MonoBehaviour` — вместе с `.meta`), тип `[Serializable]` (имя может быть в `[SerializeReference]`).
- `--apply` не пишет при новых ошибках компиляции и при строковых совпадениях; `--force` — записать всё равно. Кодировка и BOM файлов сохраняются.
- `refactor.jsonc` — атрибуты сериализации проекта, расширения и папки поиска строк, лимиты; пример — `examples/refactor.example.jsonc`.
- Коды возврата: 0 — дифф готов или записан, 1 — отказ, новые ошибки компиляции или запись не выполнена, 2 — ошибка запуска.

## `csr serve`

Процесс держит решение загруженным: открытие большого решения Unity — около 30 с на каждый вызов, через сервер `refs` — доли секунды. `refs` и `rename` находят живой сервер решения сами (именованный канал текущего пользователя, имя — по пути решения); `--no-server` — загрузить решение в процессе.

```
csr serve [решение] [--idle минуты] [--detach] [--log файл]
csr serve [решение] --status | --stop
csr serve --list
```

- Перед каждой командой сервер применяет изменения с диска: изменённые `.cs` перечитываются; при изменении содержимого `.sln`, `.slnx`, `.csproj` и при `.props`, `.targets` решение перезагружается; новые и удалённые `.cs` — перезагрузка для проектов SDK (старые проекты Unity перечисляют файлы сами, перезагрузку даст изменение `.csproj`).
- `--detach` запускает сервер отдельным процессом и возвращается, когда решение загружено; журнал — в кэше CSR. `--idle` — остановка после простоя (по умолчанию 120 минут, 0 — без остановки).
- Решение с проектами старого формата (Unity) грузится через теневую копию `.sln`, `obj` и папка вывода — в кэше CSR (`%LOCALAPPDATA%\csr` или переменная `CSR_CACHE`): рядом с кодом проекта ничего не пишется.

## Тесты

```
dotnet test CSR.slnx
```

Покрытие тестами (coverlet, OpenCover) и CRAP самого CSR:

```
dotnet test CSR.slnx --collect "XPlat Code Coverage" --results-directory TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
csr metrics src --coverage TestResults/<id1>/coverage.opencover.xml --coverage TestResults/<id2>/coverage.opencover.xml --coverage TestResults/<id3>/coverage.opencover.xml --format md
```

## Структура

| Путь | Что |
|---|---|
| `src/CSR` | консольное приложение `csr`: разбор аргументов, файлы, вывод |
| `src/CSR.Core` | ядро (`netstandard2.0` и `net10.0`): на вход синтаксическое дерево и параметры, на выход метрики, находки стиля и архитектурные признаки; без консоли, файловой системы и JSON; основа будущего пакета анализаторов Roslyn |
| `src/CSR.Reports` | разбор конфигов `.jsonc` и диффа, отчёты JSON и Markdown |
| `src/CSR.Workspace` | загрузка решения (`MSBuildWorkspace`), символы, ссылки, переименование, сервер `csr serve`; отдельно от ядра — ядро решения не грузит |
| `tests/CSR.Core.Tests`, `tests/CSR.Reports.Tests`, `tests/CSR.Tests` | тесты (NUnit): ядро, конфиги и отчёты, CLI (`Program.Main` целиком, git — временный репозиторий) |
| `tests/CSR.Workspace.Tests` | тесты `refs`, `rename`, `serve` на маленьком решении `TestData/Sample` (копируется во временную папку, `dotnet restore`) |
| `examples/` | примеры конфигов |
| `spikes/` | прототипы замеров, не часть инструмента |
| `Directory.Build.props` | общие настройки проектов |
| `Directory.Packages.props` | версии NuGet-пакетов |

## Лицензия

GPL-3.0, см. `LICENSE`.
