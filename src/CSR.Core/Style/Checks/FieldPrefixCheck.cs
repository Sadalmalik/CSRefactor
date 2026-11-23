using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Префиксы полей и регистр констант. Поле с доступом из prefixedAccess (по умолчанию закрытое): экземпляра -
    // instancePrefix, статическое - staticPrefix (static readonly - по staticReadonly); остальные поля и свойства -
    // без префиксов; константа - по constants. Замены нет: переименование задевает другие файлы (csr rename)
    public class FieldPrefixCheck : SyntaxCheck
    {
        private const string Pascal = "pascal";
        private const string Upper = "upper";
        private const string AsStatic = "static";
        private const string Private = "private";
        private const string Protected = "protected";
        private const string Internal = "internal";
        private const string Public = "public";

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Text("instancePrefix", "m_", "префикс закрытого поля экземпляра; пусто - без префикса"),
            CheckParameter.Text("staticPrefix", "s_", "префикс закрытого статического поля; пусто - без префикса"),
            CheckParameter.Choice(
                "staticReadonly",
                "закрытое static readonly: static - префикс staticPrefix, pascal - без префикса, как константа",
                AsStatic,
                Pascal),
            CheckParameter.Choice(
                "constants",
                "регистр константы: pascal - MaxSize, upper - MAX_SIZE, any - не проверять",
                Pascal,
                Upper,
                "any"),
            CheckParameter.Strings(
                "prefixedAccess",
                "доступ полей с префиксами: private (и без модификатора), protected, internal, public",
                Private),
            CheckParameter.Bool("pascalAfterPrefix", true, "после префикса - заглавная буква (m_Name, не m_name)"),
            CheckParameter.Bool("properties", true, "свойства без префиксов полей"),
        ];

        public override string Name
            => "field-prefix";

        public override string Description
            => "префиксы полей и регистр констант";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var rules = new Rules(context.Settings);
            foreach (var node in context.Root.DescendantNodes())
            {
                if (node is FieldDeclarationSyntax field && IsInType(field))
                    CheckField(context, rules, field);
                else if (rules.CheckProperties && node is PropertyDeclarationSyntax property && IsInType(property))
                    CheckUnprefixed(context, rules, property.Identifier, "свойство");
            }
        }

        private static void CheckField(CheckContext context, Rules rules, FieldDeclarationSyntax field)
        {
            var modifiers = field.Modifiers;
            bool isConst = modifiers.Any(SyntaxKind.ConstKeyword);
            bool isStatic = modifiers.Any(SyntaxKind.StaticKeyword);
            bool isReadonly = modifiers.Any(SyntaxKind.ReadOnlyKeyword);
            var access = GetAccess(modifiers);
            foreach (var variable in field.Declaration.Variables)
            {
                var identifier = variable.Identifier;
                if (isConst)
                    CheckConstant(context, rules, identifier);
                else if (!rules.PrefixedAccess.Contains(access))
                    CheckUnprefixed(context, rules, identifier, "поле");
                else if (isStatic && isReadonly && !rules.IsStaticReadonlyPrefixed)
                    CheckUnprefixed(context, rules, identifier, "static readonly");
                else
                    CheckPrefixed(context, rules, identifier, access, isStatic);
            }
        }

        private static void CheckPrefixed(
            CheckContext context,
            Rules rules,
            SyntaxToken identifier,
            string access,
            bool isStatic)
        {
            var name = identifier.Text;
            var prefix = isStatic ? rules.StaticPrefix : rules.InstancePrefix;
            var kind = GetAccessWord(access) + (isStatic ? " статическое поле" : " поле");
            var expected = prefix + NameWords.ToPascal(name);
            if (prefix.Length == 0)
            {
                CheckUnprefixed(context, rules, identifier, kind);
                return;
            }

            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
            {
                context.Report(identifier.Span, $"'{name}': {kind} с префиксом '{prefix}' - '{expected}'");
                return;
            }

            if (rules.IsPascalAfterPrefix && !char.IsUpper(name[prefix.Length]))
                context.Report(identifier.Span, $"'{name}': после префикса - заглавная буква - '{expected}'");
        }

        // Член без префикса полей: имя не начинается с instancePrefix, staticPrefix или '_'
        private static void CheckUnprefixed(CheckContext context, Rules rules, SyntaxToken identifier, string kind)
        {
            var name = identifier.Text;
            var prefix = rules.FindPrefix(name);
            if (prefix == null)
                return;

            context.Report(identifier.Span, $"'{name}': {kind} без префикса '{prefix}' - '{NameWords.ToPascal(name)}'");
        }

        private static void CheckConstant(CheckContext context, Rules rules, SyntaxToken identifier)
        {
            var name = identifier.Text;
            if (rules.Constants == Pascal && !IsPascal(name))
                context.Report(identifier.Span, $"'{name}': константа в PascalCase - '{NameWords.ToPascal(name)}'");
            else if (rules.Constants == Upper && name.Any(char.IsLower))
                context.Report(identifier.Span, $"'{name}': константа в UPPER_CASE - '{NameWords.ToUpperSnake(name)}'");
        }

        // PascalCase: с заглавной, без '_', не UPPER_CASE (короткая аббревиатура UI, ID - допустима)
        private static bool IsPascal(string name)
        {
            if (!char.IsUpper(name[0]) || name.Contains("_"))
                return false;

            return name.Any(char.IsLower) || name.Count(char.IsLetter) <= 3;
        }

        private static bool IsInType(MemberDeclarationSyntax member)
            => member.Parent is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;

        // Доступ поля для prefixedAccess: без модификатора - private, protected internal и private protected - protected
        private static string GetAccess(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(SyntaxKind.PublicKeyword))
                return Public;

            if (modifiers.Any(SyntaxKind.ProtectedKeyword))
                return Protected;

            return modifiers.Any(SyntaxKind.InternalKeyword) || modifiers.Any(SyntaxKind.FileKeyword) ? Internal : Private;
        }

        private static string GetAccessWord(string access)
        {
            return access switch
            {
                Protected => "защищенное",
                Internal => "внутреннее",
                Public => "открытое",
                _ => "закрытое",
            };
        }

        // Значения параметров одного прогона
        private class Rules
        {
            public Rules(CheckSettings settings)
            {
                InstancePrefix = settings.GetString("instancePrefix");
                StaticPrefix = settings.GetString("staticPrefix");
                IsStaticReadonlyPrefixed = settings.GetString("staticReadonly") == AsStatic;
                Constants = settings.GetString("constants");
                IsPascalAfterPrefix = settings.GetBool("pascalAfterPrefix");
                CheckProperties = settings.GetBool("properties");
                PrefixedAccess = settings.GetStrings("prefixedAccess");
            }

            public string InstancePrefix { get; }

            public string StaticPrefix { get; }

            public bool IsStaticReadonlyPrefixed { get; }

            public string Constants { get; }

            public bool IsPascalAfterPrefix { get; }

            public bool CheckProperties { get; }

            public IReadOnlyList<string> PrefixedAccess { get; }

            // Префикс полей в начале имени: instancePrefix, staticPrefix или общий вид (m_, k_, _)
            public string? FindPrefix(string name)
            {
                if (IsPrefixed(name, InstancePrefix))
                    return InstancePrefix;

                if (IsPrefixed(name, StaticPrefix))
                    return StaticPrefix;

                int length = name.Length - NameWords.StripFieldPrefix(name).Length;
                return length > 0 ? name.Substring(0, length) : null;
            }

            private static bool IsPrefixed(string name, string prefix)
                => prefix.Length > 0 && name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal);
        }
    }
}
