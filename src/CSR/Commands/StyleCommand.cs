using CSR.Reports.Checks;
using CSR.Reports.Config;
using CSR.Reports.Style;

namespace CSR.Commands
{
    // csr style: находки стиля по синтаксису файлов. Ничего не пишет рядом с кодом
    public static class StyleCommand
    {
        public const string Usage = """
            csr style <файлы | папки>... [--config <style.jsonc>]... [опции]

              --config <style.jsonc>         проверки, уровни, метки правил, параметры; несколько - накладываются;
                                             без --config - config/style.jsonc рядом с csr.exe, затем
                                             .csr/style.jsonc в папке цели или выше (до корня репозитория)
              --project <project.jsonc>      символы препроцессора, методы логгера; без --project - .csr/project.jsonc,
                                             иначе config/project.jsonc рядом с csr.exe
              --diff <ревизия | файл.diff>   измененные строки: git diff -U0 от ревизии или готовый дифф
              --scope file | changed-lines   все находки файла или только на измененных строках
                                             (без --diff - file; с --diff - changed-lines; changed-lines без --diff - от HEAD)
              --format json | md             формат отчета (по умолчанию json)
              --out <файл>                   записать отчет в файл вместо stdout

            Коды возврата: 0 - нет находок уровня error, 1 - есть, 2 - ошибка запуска
            """;

        private static readonly FileCommand Command = new()
        {
            Name = "style",
            HasScope = true,
            Analyze = Analyze,
        };

        public static int Run(IEnumerable<string> args)
            => FileCommandTool.Run(Command, args);

        private static FileResult Analyze(FileRun run)
        {
            var options = run.Configs.LoadChecks("style.jsonc", StyleConfig.Apply);
            var findings = run.Analyze(options);
            var report = new CheckReport { FileCount = run.Files.Count, Scope = run.Scope, Findings = findings };
            return new FileResult
            {
                Text = run.Format == "md" ? StyleMarkdownWriter.Write(report) : StyleJsonWriter.Write(report),
                Summary = $"файлов {run.Files.Count}, находок {findings.Count}",
                ExitCode = report.HasErrors ? 1 : 0,
            };
        }
    }
}
