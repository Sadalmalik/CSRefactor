using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Повторяющийся набор параметров: метод с minParameters и больше входными параметрами (без out), который
    // вызывается в minCalls местах и больше, и хотя бы в minForwarded вызовах minParameters аргументов -
    // параметры вызывающего метода, переданные дальше по цепочке
    public class ParameterSetCheck : SolutionCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("minParameters", 4, "сколько входных параметров - набор"),
            CheckParameter.Int("minCalls", 3, "сколько вызовов - находка"),
            CheckParameter.Int("minForwarded", 1, "в скольких вызовах набор передается по цепочке; 0 - не требовать"),
        ];

        public override string Name
            => "parameter-set";

        public override string Description
            => "повторяющийся набор параметров";

        public override string Question
            => "Собрать набор в структуру аргументов (...Args)? Методы остаются на месте";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void AnalyzeSolution(SolutionCheckContext context)
        {
            var minParameters = context.Settings.GetInt("minParameters");
            var minCalls = context.Settings.GetInt("minCalls");
            var minForwarded = context.Settings.GetInt("minForwarded");
            foreach (var declared in context.Index.Types)
            {
                foreach (var method in declared.Symbol.GetMembers().OfType<IMethodSymbol>())
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    var inputs = method.Parameters.Count(parameter => parameter.RefKind != RefKind.Out);
                    bool isCallable = method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor;
                    if (!isCallable || inputs < minParameters || method.IsImplicitlyDeclared)
                        continue;

                    var calls = context.Index.GetReferences(method)
                        .Select(reference => (Reference: reference, Arguments: reference.GetArguments()))
                        .Where(call => call.Arguments != null)
                        .ToList();
                    if (calls.Count < minCalls)
                        continue;

                    int forwarded = calls
                        .Count(call => CountForwarded(call.Reference, call.Arguments!) >= minParameters);
                    if (forwarded < minForwarded)
                        continue;

                    var member = method.MethodKind == MethodKind.Constructor ? "ctor" : method.Name;
                    var name = $"{method.ContainingType.Name}.{member}";
                    var message = $"метод '{name}' ({inputs} входных параметров): вызовов {calls.Count}, "
                        + $"из них {forwarded} передают параметры вызывающего дальше";
                    context.Report(method.Locations.First(location => location.IsInSource), message);
                }
            }
        }

        // Аргументы-параметры вызывающего метода: имя, которое ссылается на параметр
        private static int CountForwarded(SymbolReference call, ArgumentListSyntax arguments)
        {
            int count = 0;
            foreach (var argument in arguments.Arguments)
            {
                if (argument.Expression is IdentifierNameSyntax name
                    && call.Model.GetSymbolInfo(name).Symbol is IParameterSymbol)
                    count++;
            }

            return count;
        }
    }
}
