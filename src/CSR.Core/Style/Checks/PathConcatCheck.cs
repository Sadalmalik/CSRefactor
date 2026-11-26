using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Склейка пути сложением с литералом-разделителем: a + "/" + b -> Path.Combine(a, b).
    // Замена (replace) - только для цепочки вида a + "/" + b [+ "/" + c ...], где каждая часть - строка по синтаксису
    // файла (литерал без разделителя на краях, интерполяция, ToString(), имя с объявленным типом string), и не
    // в константе (const, атрибут, значение по умолчанию, case). Path.Combine в Windows соединяет через \:
    // для путей, где важен /, замена меняет результат - поэтому по умолчанию выключена
    public class PathConcatCheck : SyntaxCheck
    {
        // Статические члены System.IO.Path: обращение к ним в файле - признак, что имя Path - этот класс
        private static readonly HashSet<string> PathMembers =
        [
            "Combine",
            "Join",
            "GetFileName",
            "GetFileNameWithoutExtension",
            "GetDirectoryName",
            "GetExtension",
            "GetFullPath",
            "GetRelativePath",
            "ChangeExtension",
            "HasExtension",
            "IsPathRooted",
            "GetTempPath",
            "GetInvalidFileNameChars",
            "GetInvalidPathChars",
            "DirectorySeparatorChar",
            "AltDirectorySeparatorChar",
        ];

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("separators", "литералы-разделители пути", "/"),
            CheckParameter.Bool("replace", false, "предлагать замену на Path.Combine"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "path-concat";

        public override string Description
            => "склейка пути сложением строк";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var separators = context.Settings.GetStrings("separators");
            bool replace = context.Settings.GetBool("replace");
            foreach (var chain in context.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                if (!chain.IsKind(SyntaxKind.AddExpression) || chain.Parent.IsKind(SyntaxKind.AddExpression))
                    continue;

                var operands = GetOperands(chain);
                var separator = operands.Select(operand => GetString(operand)).FirstOrDefault(separators.Contains);
                if (separator == null)
                    continue;

                var parts = replace && !IsConstant(chain) ? GetPathParts(operands, separator) : null;
                var message = $"склейка пути через \"{separator}\": Path.Combine";
                if (parts == null)
                {
                    context.Report(chain.Span, message);
                    continue;
                }

                var combine = $"{GetPathName(context.Root)}.Combine({string.Join(", ", parts)})";
                context.Report(chain.Span, message, context.ReplaceInLines(chain.Span, combine));
            }
        }

        // Константный контекст: const, аргумент атрибута, значение параметра по умолчанию, case - вызов недопустим
        private static bool IsConstant(ExpressionSyntax expression)
        {
            foreach (var ancestor in expression.Ancestors())
            {
                switch (ancestor)
                {
                    case FieldDeclarationSyntax field:
                        return field.Modifiers.Any(SyntaxKind.ConstKeyword);
                    case LocalDeclarationStatementSyntax local:
                        return local.IsConst;
                    case AttributeArgumentSyntax or CaseSwitchLabelSyntax or ConstantPatternSyntax:
                        return true;
                    case EqualsValueClauseSyntax { Parent: ParameterSyntax }:
                        return true;
                    case StatementSyntax or MemberDeclarationSyntax:
                        return false;
                }
            }

            return false;
        }

        // Операнды цепочки a + b + c слева направо (скобки не раскрываются)
        private static List<ExpressionSyntax> GetOperands(BinaryExpressionSyntax chain)
        {
            var operands = new List<ExpressionSyntax>();
            ExpressionSyntax current = chain;
            while (current is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
            {
                operands.Add(binary.Right);
                current = binary.Left;
            }

            operands.Add(current);
            operands.Reverse();
            return operands;
        }

        private static string? GetString(ExpressionSyntax expression)
        {
            return expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.Token.ValueText
                : null;
        }

        // Части пути для Path.Combine: a, "/", b, "/", c -> a, b, c; null - цепочка другого вида
        private static List<string>? GetPathParts(List<ExpressionSyntax> operands, string separator)
        {
            if (operands.Count % 2 == 0)
                return null;

            var parts = new List<string>();
            for (var index = 0; index < operands.Count; index++)
            {
                var operand = operands[index];
                var text = GetString(operand);
                if (index % 2 == 1)
                {
                    if (text != separator)
                        return null;

                    continue;
                }

                if (text != null && IsEdge(text, separator))
                    return null;

                if (!IsString(operand))
                    return null;

                parts.Add(operand.ToString());
            }

            return parts;
        }

        private static bool IsEdge(string text, string separator)
            => text.StartsWith(separator, StringComparison.Ordinal) || text.EndsWith(separator, StringComparison.Ordinal);

        // Строка по синтаксису файла: литерал, интерполяция, вызов ToString(), имя параметра, локальной, поля или
        // свойства с объявленным типом string. Остальное (тип не виден) - замены нет: Path.Combine не примет не строку
        private static bool IsString(ExpressionSyntax operand)
        {
            switch (operand)
            {
                case LiteralExpressionSyntax literal:
                    return literal.IsKind(SyntaxKind.StringLiteralExpression);
                case InterpolatedStringExpressionSyntax:
                    return true;
                case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation:
                    return access.Name.Identifier.Text == "ToString" && invocation.ArgumentList.Arguments.Count == 0;
                case IdentifierNameSyntax name:
                    return IsStringName(name);
                default:
                    return false;
            }
        }

        // Имя - строка, если все его объявления в члене (параметры, локальные, foreach, out var, шаблоны, catch,
        // переменные запроса, в любых блоках и лямбдах) явно string; без объявлений в члене - поле или свойство типа
        // с типом string. var, параметр лямбды без типа и не найденное имя - не строка
        private static bool IsStringName(IdentifierNameSyntax name)
        {
            var text = name.Identifier.Text;
            var member = name.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(
                item => item is not BaseTypeDeclarationSyntax);
            var declarations = member == null ? new List<TypeSyntax?>() : LocalNameTools.FindDeclarationTypes(member, text);
            if (declarations.Count > 0)
                return declarations.All(IsStringType);

            var owner = name.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            return owner != null && IsStringType(FindMember(owner, text));
        }

        private static bool IsStringType(TypeSyntax? type)
            => type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.StringKeyword);

        private static TypeSyntax? FindMember(TypeDeclarationSyntax owner, string text)
        {
            foreach (var member in owner.Members)
            {
                if (member is PropertyDeclarationSyntax property && property.Identifier.Text == text)
                    return property.Type;

                if (member is not FieldDeclarationSyntax field)
                    continue;

                if (field.Declaration.Variables.Any(item => item.Identifier.Text == text))
                    return field.Declaration.Type;
            }

            return null;
        }

        // Path - если в файле есть using System.IO и каждое имя Path в файле - обращение к статическому члену
        // System.IO.Path (Path.Combine, Path.GetFileName...): иначе Path может быть членом типа или базового типа,
        // и замена - global::System.IO.Path
        private static string GetPathName(SyntaxNode root)
        {
            var hasUsing = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Any(directive => directive.Alias == null && directive.Name?.ToString() == "System.IO");
            var paths = root.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken) && token.Text == "Path")
                .ToList();
            bool isClassUse = paths.Count > 0 && paths.All(IsPathClassMember);
            return hasUsing && isClassUse ? "Path" : "global::System.IO.Path";
        }

        private static bool IsPathClassMember(SyntaxToken token)
        {
            return token.Parent is IdentifierNameSyntax name
                && name.Parent is MemberAccessExpressionSyntax access
                && access.Expression == name
                && PathMembers.Contains(access.Name.Identifier.Text);
        }
    }
}
