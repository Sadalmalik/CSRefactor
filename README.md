# CSRefactor (`csr`)

Консольный инструмент анализа и рефакторинга C# для ИИ-агентов. Механику проверяет и правит инструмент, агенту остаются суждения: агент получает готовый отчёт и не читает длинные инструкции по стилю.

Анализ — Roslyn: метрики и проверки по синтаксису файла, ссылки и переименование по загруженному решению. Инструмент не вызывает сторонние анализаторы и форматтеры и не требует их установки.

## Что умеет

| Команда | Назначение |
|---|---|
| `csr metrics` | метрики по синтаксису: сложность, длина, вложенность, размеры типов и файлов, CRAP с покрытием |
| `csr style` | проверки стиля по конфигу: форматирование, комментарии, идиомы, модификаторы, запрещённые конструкции (вызовы, типы, статическое состояние, корутины); у проверок — метки правил ваших гайдов и готовые замены |
| `csr fix` | применяет замены находок `style` к файлам |
| `csr arch` | архитектурные признаки по конфигу: находка — повод решить, у признака — вопрос; по файлам и по решению |
| `csr refs` | все ссылки на символ по решению |
| `csr rename` | семантическое переименование с защитой сериализуемых имён |
| `csr move` | перенос типа в свой файл (для Unity `.meta` — вместе с файлом) или члена в другой тип |
| `csr extract` | извлечение метода из строк файла: входы — параметры, выход — возврат |
| `csr serve` | держит решение загруженным: `refs`, `rename`, `move`, `extract` и `arch --solution` отвечают за доли секунды |
| `csr similar` | похожий код по синтаксису: пары и кластеры похожих членов и фрагментов, наборов параметров и полей, классов одной схемы, систем вариантов (граф типов); `--changed` — изменённый код против всего |
| `csr structure` | структурные метрики на графе типов: глубина владения и наследования, объекты-боги, циклы и свалки папок, концентраторы, размер сборок и глубина зависимостей в слое |

В разработке: `csr format` — форматирование только изменённых строк (Roslyn `Formatter` и свои правила).

Правила, пороги и уровни задаёт конфиг: проверки в инструменте общие, конфиг выбирает их и подписывает находки метками своих правил. Разные команды и люди — разные конфиги.

## Требования

- Windows x64. `csr.exe` из релиза — self-contained: .NET внутри, установленный .NET для `metrics`, `style`, `fix`, `arch` по файлам не нужен.
- Команды по решению (`refs`, `rename`, `move`, `extract`, `serve`, `arch --solution`) грузят проекты через MSBuild отдельным процессом (BuildHost Roslyn из папок `BuildHost-*` рядом с `csr.exe`):
  - проекты SDK-стиля (`<Project Sdk="...">`) — .NET SDK, которым собирается проект (`dotnet` в `PATH`);
  - старые `.csproj` (Unity: `<Project ToolsVersion="4.0">`) — .NET Framework 4.7.2 или новее (в Windows 10/11 есть) и MSBuild из Visual Studio 2017+ или Build Tools for Visual Studio с компонентом MSBuild; ссылки проекта Unity указывают на сборки редактора Unity — нужна та же версия редактора. Rider свой MSBuild не регистрирует, и без Visual Studio или Build Tools он не находится; обход — в [DOCS.md](DOCS.md#требования-к-машине).
- Сборка из исходников — .NET SDK 10 или новее.

## Установка

### Из релиза

Одна команда PowerShell (Windows PowerShell 5.1 или PowerShell 7) скачивает архив последнего релиза, распаковывает его в `%LOCALAPPDATA%\Programs\csr` и добавляет папку в `PATH` пользователя. Клонировать репозиторий и ставить .NET SDK не нужно. Повторный запуск — обновление.

```
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/Sadalmalik/CSRefactor/main/scripts/install-release.ps1).TrimStart([char]0xFEFF)))
```

Скрипт выполняется из памяти: политика выполнения `.ps1` его не касается; `TrimStart` убирает метку UTF-8 (BOM) в начале файла — с ней `ScriptBlock.Create` не разбирает скрипт. Параметры дописываются после команды, например конкретная версия и остановка запущенного `csr serve`:

```
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/Sadalmalik/CSRefactor/main/scripts/install-release.ps1).TrimStart([char]0xFEFF))) -Version v1.0.0 -Stop
```

| Параметр | Назначение |
|---|---|
| `-Version` | тег релиза (`v1.0.0`); по умолчанию — последний релиз (пререлизы не берутся) |
| `-Zip` | архив `csr-<тег>-win-x64.zip`: путь к файлу или URL — вместо поиска релиза на GitHub |
| `-Configs` | папка с конфигами по умолчанию (`*.jsonc`): заменяет папку `config` рядом с `csr.exe` |
| `-Link` | вместо копии — ссылка (junction) `config` → `-Configs`; требует `-Configs` |
| `-Destination` | папка установки; по умолчанию `%LOCALAPPDATA%\Programs\csr` |
| `-NoPath` | не менять `PATH` пользователя |
| `-Stop` | остановить процессы из папки установки (`csr serve` и его BuildHost); без параметра — ошибка со списком процессов |

В архиве — `csr.exe`, папки `BuildHost-net472` и `BuildHost-netcore`, `version.txt` и папка `config` с двумя нейтральными конфигами: `style.jsonc` и `arch.jsonc` с пустым списком проверок (`"checks": {}`). После установки из релиза `csr style`, `csr fix` и `csr arch` работают и находок не дают; `project.jsonc`, `metrics.jsonc` и `refactor.jsonc` в архиве нет — действуют встроенные значения. Без `-Configs` папка `config` из архива кладётся, только если её в установке ещё нет; при обновлении свои конфиги не заменяются. Версия установки и источник конфигов — в `version.txt` рядом с `csr.exe`.

Проверки включает конфиг команды, а не установка:

- проектный слой — `.csr/style.jsonc` и `.csr/arch.jsonc` в репозитории проекта: накладываются на `config` рядом с `csr.exe` и действуют для всех, кто работает с репозиторием; там же `.csr/project.jsonc`, `.csr/metrics.jsonc`, `.csr/refactor.jsonc`;
- личный слой — `-Configs <папка>` при установке: папка заменяет `config` рядом с `csr.exe`;
- разовый запуск — `--config <файл>`.

Отправная точка — примеры в `examples/` (`style.example.jsonc` и т. д.: скопировать в `.csr` без `.example`); порядок поиска — в [DOCS.md](DOCS.md#где-искать-конфиги).

### Из исходников

Скрипт `scripts/install.ps1` публикует `src/CSR` из текущей ветки (`scripts/publish.ps1`: `win-x64`, один файл, .NET внутри) и ставит результат тем же `install-release.ps1`. Нужен .NET SDK 10 или новее.

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1 [-Configs <папка>] [-Link] [-Destination <папка>] [-NoPath]
```

`-ExecutionPolicy Bypass` действует только на этот запуск: политика по умолчанию в Windows (`Restricted`) запрещает запуск `.ps1`, системная настройка не меняется. Параметры — как у установки из релиза; без `-Configs` папка `config` установки не меняется. Запущенный из папки установки `csr` (в том числе `csr serve`) — ошибка, процесс нужно остановить. В `version.txt` — коммит, ветка и дата.

Пример: установка с личными конфигами по ссылке:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1 -Configs D:\my\csr-configs -Link
```

Архив релиза локально (то же делает workflow `.github/workflows/release.yml` на тег `v*`): `scripts/build-release.ps1 -Tag v1.0.0` → `dist/csr-v1.0.0-win-x64.zip`.

Сборка и запуск без установки:

```
dotnet build CSR.slnx
dotnet run --project src/CSR -- help
```

Установка как dotnet tool из локальной сборки (нужен .NET 10 на машине):

```
dotnet pack src/CSR -c Release -o artifacts
dotnet tool install -g CSRefactor --add-source artifacts
```

## Использование

Конфиги — примеры в `examples/`; опции и форматы — в [DOCS.md](DOCS.md). Без `--config` и `--project` `csr` ищет конфиги сам: `config` рядом с `csr.exe` и `.csr` в репозитории проекта.

```
csr metrics src --format md
csr style src --config style.jsonc --project project.jsonc
csr fix src --config style.jsonc --diff HEAD --apply
csr arch --solution My.sln --config arch.jsonc --format md
csr refs Namespace.Type.Method --solution My.sln --format text
csr rename Namespace.Type.OldName NewName --solution My.sln
csr move Namespace.Type --solution My.sln
csr move Namespace.Type.Method --to Namespace.Other --solution My.sln
csr extract src/File.cs:40-52 NewMethod --solution My.sln
```

- `csr style` по изменённым строкам: `--diff HEAD` (находки только в строках, изменённых от ревизии).
- `csr fix` без `--apply` печатает дифф и ничего не пишет.
- Код возврата: `0` — успех, `1` — есть находки уровня `error` (для `rename`, `move` и `extract` — отказ или новые ошибки компиляции), `2` — ошибка запуска.

## Документация

[DOCS.md](DOCS.md) — подробное описание: все команды и опции, конфиги (`style.jsonc`, `arch.jsonc`, `project.jsonc`), каждая проверка `style` и каждый признак `arch` с параметрами и примерами «было → стало», метрики и их пороги.

## Тесты

```
dotnet test CSR.slnx
```

## Структура

| Путь | Что |
|---|---|
| `src/CSR` | консольное приложение `csr`: разбор аргументов, файлы, вывод |
| `src/CSR.Core` | ядро (`netstandard2.0` и `net10.0`): на вход синтаксическое дерево и параметры, на выход метрики, находки стиля и архитектурные признаки; без консоли, файловой системы и JSON |
| `src/CSR.Reports` | разбор конфигов `.jsonc` и диффа, отчёты JSON и Markdown |
| `src/CSR.Workspace` | загрузка решения (`MSBuildWorkspace`), символы, ссылки, переименование, сервер `csr serve`; отдельно от ядра |
| `tests/` | тесты (NUnit): ядро, конфиги и отчёты, CLI, `refs` / `rename` / `serve` на маленьком решении |
| `examples/` | примеры конфигов |
| `packaging/config/` | нейтральные конфиги архива релиза |
| `scripts/` | установка (`install.ps1` из исходников, `install-release.ps1` из релиза), публикация и архив релиза |
| `.github/workflows/` | релиз на тег `v*` |
| `spikes/` | прототипы замеров, не часть инструмента |

## Лицензия

GPL-3.0, см. `LICENSE`.
