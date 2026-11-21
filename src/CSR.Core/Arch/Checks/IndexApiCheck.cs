using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Arch.Checks
{
    // API на числовых индексах: public / internal метод, конструктор или свойство с целым параметром или результатом,
    // имя которого - номер сущности (последнее слово из names: cell, fromBlock; или cellIndex - слово из indexWords
    // после слова из names); несколько out целых; return -1; открытый маркер-константа -1.
    // Приватные члены и override не проверяются: индекс внутри класса - норма
    public class IndexApiCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("names", "слова имен-номеров (без учета регистра)", "cell", "node", "area", "block"),
            CheckParameter.Strings(
                "indexWords",
                "последние слова, после которых слово из names тоже номер",
                "index",
                "id"),
            CheckParameter.Int("minOutInts", 2, "сколько out-параметров целого типа - находка; 0 - не проверять"),
            CheckParameter.Bool("notFoundMarker", true, "искать return -1 и открытые константы -1"),
        ];

        public override string Name
            => "index-api";

        public override string Description
            => "API на числовых индексах";

        public override string Question
            => "Отдать наружу объект или точку вместо номера и маркера -1? Номер массива или пути - норма";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        protected override void AnalyzeFile(CheckContext context)
        {
            foreach (var member in context.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (!ArchSyntax.IsApi(member) || member.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    continue;

                var reasons = member switch
                {
                    MethodDeclarationSyntax method => GetMethodReasons(context, method),
                    ConstructorDeclarationSyntax constructor => GetParameterReasons(context, constructor.ParameterList),
                    PropertyDeclarationSyntax property => GetPropertyReasons(context, property),
                    FieldDeclarationSyntax field => GetMarkerReasons(context, field),
                    _ => new List<string>(),
                };

                if (reasons.Count > 0)
                    context.Report(GetIdentifierSpan(member), $"{GetName(member)}: {string.Join("; ", reasons)}");
            }
        }

        private static List<string> GetMethodReasons(CheckContext context, MethodDeclarationSyntax method)
        {
            var reasons = GetParameterReasons(context, method.ParameterList);
            if (!ArchSyntax.IsIntegerType(method.ReturnType))
                return reasons;

            if (IsIndexName(context, method.Identifier.Text))
                reasons.Insert(0, $"результат {method.ReturnType} {method.Identifier.Text}");

            if (context.Settings.GetBool("notFoundMarker") && ReturnsMinusOne(method))
                reasons.Add("return -1");

            return reasons;
        }

        private static List<string> GetParameterReasons(CheckContext context, ParameterListSyntax parameters)
        {
            var reasons = new List<string>();
            int outInts = 0;
            foreach (var parameter in parameters.Parameters)
            {
                if (parameter.Type == null || !ArchSyntax.IsIntegerType(parameter.Type))
                    continue;

                if (parameter.Modifiers.Any(SyntaxKind.OutKeyword))
                    outInts++;

                if (IsIndexName(context, parameter.Identifier.Text))
                    reasons.Add($"параметр {parameter.Type} {parameter.Identifier.Text}");
            }

            var minOutInts = context.Settings.GetInt("minOutInts");
            if (minOutInts > 0 && outInts >= minOutInts)
                reasons.Add($"out целых: {outInts}");

            return reasons;
        }

        private static List<string> GetPropertyReasons(CheckContext context, PropertyDeclarationSyntax property)
        {
            var reasons = new List<string>();
            if (ArchSyntax.IsIntegerType(property.Type) && IsIndexName(context, property.Identifier.Text))
                reasons.Add($"свойство {property.Type} {property.Identifier.Text}");

            return reasons;
        }

        private static List<string> GetMarkerReasons(CheckContext context, FieldDeclarationSyntax field)
        {
            var reasons = new List<string>();
            var modifiers = field.Modifiers;
            bool isConstant = modifiers.Any(SyntaxKind.ConstKeyword)
                || (modifiers.Any(SyntaxKind.StaticKeyword) && modifiers.Any(SyntaxKind.ReadOnlyKeyword));
            if (!context.Settings.GetBool("notFoundMarker") || !isConstant
                || !ArchSyntax.IsIntegerType(field.Declaration.Type))
                return reasons;

            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer != null && IsMinusOne(variable.Initializer.Value))
                    reasons.Add($"маркер {variable.Identifier.Text} = -1");
            }

            return reasons;
        }

        // Номер сущности по имени: последнее слово из names или indexWords после слова из names
        private static bool IsIndexName(CheckContext context, string name)
        {
            var words = ArchSyntax.SplitWords(name);
            if (words.Count == 0)
                return false;

            var names = context.Settings.GetStrings("names");
            var last = words[words.Count - 1];
            if (ContainsWord(names, last))
                return true;

            var indexWords = context.Settings.GetStrings("indexWords");
            return words.Count > 1 && ContainsWord(indexWords, last)
                && words.Take(words.Count - 1).Any(word => ContainsWord(names, word));
        }

        private static bool ContainsWord(IReadOnlyList<string> words, string word)
            => words.Any(candidate => string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase));

        private static bool ReturnsMinusOne(MethodDeclarationSyntax method)
        {
            var returns = method.DescendantNodes()
                .Where(node => node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() == null)
                .Where(node => node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>() == null)
                .OfType<ReturnStatementSyntax>();
            if (returns.Any(statement => statement.Expression != null && IsMinusOne(statement.Expression)))
                return true;

            return method.ExpressionBody != null && IsMinusOne(method.ExpressionBody.Expression);
        }

        private static bool IsMinusOne(ExpressionSyntax expression)
        {
            return expression is PrefixUnaryExpressionSyntax unary && unary.IsKind(SyntaxKind.UnaryMinusExpression)
                && unary.Operand is LiteralExpressionSyntax { Token.ValueText: "1" };
        }

        private static string GetName(MemberDeclarationSyntax member)
        {
            return member switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
                PropertyDeclarationSyntax property => property.Identifier.Text,
                FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.Text,
                _ => "",
            };
        }

        private static TextSpan GetIdentifierSpan(MemberDeclarationSyntax member)
        {
            return member switch
            {
                MethodDeclarationSyntax method => method.Identifier.Span,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.Span,
                PropertyDeclarationSyntax property => property.Identifier.Span,
                FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.Span,
                _ => member.Span,
            };
        }
    }
}
