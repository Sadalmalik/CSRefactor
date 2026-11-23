using CSR.Core.Arch;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Имя события по маскам: подходит под forbidden или не подходит ни под одну из allowed (пустой allowed -
    // любое имя). Имя своего delegate не начинается с delegatePrefixes. Поля Action и Func - не события, не
    // проверяются; override и явные реализации интерфейса - тоже (имя задано базой)
    public class EventNameCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("allowed", "маски допустимых имен события; пусто - любое имя", "On*"),
            CheckParameter.Strings("forbidden", "маски запрещенных имен события", "OnWill*", "OnDid*"),
            CheckParameter.Strings("delegatePrefixes", "запрещенные префиксы имени delegate (перед заглавной буквой)", "On"),
        ];

        public override string Name
            => "event-name";

        public override string Description
            => "имя события и delegate";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var allowed = context.Settings.GetStrings("allowed");
            var forbidden = context.Settings.GetStrings("forbidden");
            var delegatePrefixes = context.Settings.GetStrings("delegatePrefixes");
            foreach (var node in context.Root.DescendantNodes())
            {
                if (node is DelegateDeclarationSyntax declaration)
                {
                    CheckDelegate(context, declaration.Identifier, delegatePrefixes);
                    continue;
                }

                foreach (var identifier in GetEventNames(node))
                    CheckEvent(context, identifier, allowed, forbidden);
            }
        }

        private static void CheckEvent(
            CheckContext context,
            SyntaxToken identifier,
            IReadOnlyList<string> allowed,
            IReadOnlyList<string> forbidden)
        {
            var name = identifier.Text;
            var mask = forbidden.FirstOrDefault(candidate => NameMask.Get([candidate]).IsMatch(name));
            if (mask != null)
            {
                context.Report(identifier.Span, $"событие '{name}': запрещенная форма '{mask}'");
                return;
            }

            if (allowed.Count > 0 && !NameMask.Get(allowed).IsMatch(name))
                context.Report(identifier.Span, $"событие '{name}': допустимо {string.Join(", ", allowed)}");
        }

        private static void CheckDelegate(CheckContext context, SyntaxToken identifier, IReadOnlyList<string> prefixes)
        {
            var name = identifier.Text;
            var prefix = prefixes.FirstOrDefault(candidate => name.Length > candidate.Length
                && name.StartsWith(candidate, StringComparison.Ordinal)
                && !char.IsLower(name[candidate.Length]));
            if (prefix != null)
                context.Report(identifier.Span, $"delegate '{name}': без префикса '{prefix}'");
        }

        private static IEnumerable<SyntaxToken> GetEventNames(SyntaxNode node)
        {
            return node switch
            {
                EventFieldDeclarationSyntax field when !IsInherited(field.Modifiers)
                    => field.Declaration.Variables.Select(variable => variable.Identifier),
                EventDeclarationSyntax declaration when !IsInherited(declaration.Modifiers)
                    && declaration.ExplicitInterfaceSpecifier == null => [declaration.Identifier],
                _ => [],
            };
        }

        private static bool IsInherited(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.OverrideKeyword);
    }
}
