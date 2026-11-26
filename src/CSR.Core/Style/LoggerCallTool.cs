using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style
{
    // Вызов логгера проекта (project.jsonc)
    public static class LoggerCallTool
    {
        public static bool IsLoggerCall(CheckContext context, InvocationExpressionSyntax invocation)
            => context.Project.IsLoggerMethod(Invocations.GetTargetText(invocation));
    }
}
