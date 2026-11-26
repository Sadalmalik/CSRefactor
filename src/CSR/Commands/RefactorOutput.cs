using CSR.Workspace.Loading;
using CSR.Workspace.Refactor;

namespace CSR.Commands
{
    // Общий конец move и extract: запись по --apply, дифф или JSON, код возврата
    public static class RefactorOutput
    {
        public static int Finish(WorkspaceContext context, RefactorPlan plan, CommandLine commandLine, string format)
        {
            var status = plan.GetStatus(commandLine.HasFlag("--apply"), commandLine.HasFlag("--force"));
            if (status == RefactorStatus.Applied)
                Apply(context.Solution, plan);

            if (format == "json")
            {
                context.Output.Write(RefactorReport.WriteJson(plan, status));
            }
            else
            {
                context.Output.Write(plan.Diff);
                context.Error.Write(RefactorReport.WriteSummary(plan, status));
            }

            return status is RefactorStatus.Refused or RefactorStatus.Blocked || plan.HasNewErrors ? 1 : 0;
        }

        public static string GetFormat(CommandLine commandLine)
        {
            var format = commandLine.GetOption("--format") ?? "diff";
            if (format is not ("diff" or "json"))
                throw new UsageException($"неизвестный формат '{format}'");

            return format;
        }

        // Файлы на диск, снимок решения - после правки
        private static void Apply(LoadedSolution loaded, RefactorPlan plan)
        {
            FileChangeTools.Apply(plan.Changes);
            if (plan.Solution != null)
                loaded.Update(plan.Solution);
        }
    }
}
