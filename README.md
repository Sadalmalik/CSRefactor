# CSRefactor (`csr`)

Консольный инструмент анализа и рефакторинга C# для ИИ-агентов. Механику проверяет и правит инструмент, агенту остаются суждения: агент получает готовый отчёт и не читает длинные инструкции по стилю.

Статус: каркас, команды в разработке.

## Что будет уметь

- `csr style` — проверка стиля по конфигу: форматирование, имена, порядок членов, комментарии.
- `csr arch` — проверка архитектурных признаков; отдельный режим и отдельный конфиг.
- `csr format` — форматирование только изменённых строк (движок — ReSharper Command Line Tools).
- `csr refs`, `csr rename` — ссылки на символ и семантическое переименование с защитой сериализуемых имён.
- `csr metrics` — цикломатическая сложность и CRAP, снимок `.metrics/crap.edn`.

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

## Структура

| Путь | Что |
|---|---|
| `src/CSR` | консольное приложение `csr` |
| `spikes/` | прототипы замеров, не часть инструмента |
| `Directory.Build.props` | общие настройки проектов |
| `Directory.Packages.props` | версии NuGet-пакетов |

## Лицензия

GPL-3.0, см. `LICENSE`.
