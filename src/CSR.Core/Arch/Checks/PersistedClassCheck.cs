using CSR.Core.Checks;
using CSR.Core.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Сохраняемый класс (члены с атрибутами из saveAttributes): есть конструкторы, но нет пустого для
    // десериализации (структура и конструктор с атрибутом из constructorAttributes - норма); производное свойство (только get с телом) помечено к сохранению; поле-кэш (cacheNames)
    // без атрибута исключения (ignoreAttributes). По синтаксису одного файла: атрибуты - по имени
    public class PersistedClassCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("saveAttributes", "атрибуты сохранения члена", "JsonProperty", "DataMember"),
            CheckParameter.Strings(
                "ignoreAttributes",
                "атрибуты явного исключения из сохранения",
                "JsonIgnore",
                "NonSerialized",
                "IgnoreDataMember"),
            CheckParameter.Strings(
                "constructorAttributes",
                "атрибуты конструктора десериализации",
                "JsonConstructor"),
            CheckParameter.Strings(
                "cacheNames",
                "маски имен полей-кэшей и производных значений (без m_, _)",
                "*Cache*",
                "*Cached*"),
        ];

        public override string Name
            => "persisted-class";

        public override string Description
            => "сохраняемый класс без пары конструкторов или с несохраняемым состоянием в сохранении";

        public override string Question
            => "Входное состояние сохраняется поштучно, производное исключено явно, "
                + "пустой конструктор для десериализации есть?";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var save = context.Settings.GetStrings("saveAttributes");
            var ignore = context.Settings.GetStrings("ignoreAttributes");
            var caches = NameMask.Get(context.Settings.GetStrings("cacheNames"), true);
            foreach (var type in context.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type is InterfaceDeclarationSyntax || !type.Members.Any(member => HasAttribute(member, save)))
                    continue;

                if (type is not StructDeclarationSyntax)
                    CheckConstructors(context, type);
                foreach (var member in type.Members)
                {
                    if (member is PropertyDeclarationSyntax property && HasAttribute(property, save)
                        && IsDerived(property))
                    {
                        var message = $"производное свойство '{property.Identifier.Text}' сохраняется";
                        context.Report(property.Identifier.Span, message);
                    }

                    if (member is FieldDeclarationSyntax field && IsInstanceField(field)
                        && !HasAttribute(field, ignore))
                        CheckCacheField(context, field, caches);
                }
            }
        }

        private static void CheckConstructors(CheckContext context, TypeDeclarationSyntax type)
        {
            var constructors = type.Members
                .OfType<ConstructorDeclarationSyntax>()
                .Where(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
                .ToList();
            var marked = context.Settings.GetStrings("constructorAttributes");
            bool hasEmpty = constructors.Any(constructor => constructor.ParameterList.Parameters.Count == 0);
            bool hasMarked = constructors.Any(constructor => HasAttribute(constructor, marked));
            if (constructors.Count == 0 || hasEmpty || hasMarked)
                return;

            var name = type.Identifier.Text;
            context.Report(type.Identifier.Span, $"сохраняемый '{name}' без пустого конструктора десериализации");
        }

        private static void CheckCacheField(CheckContext context, FieldDeclarationSyntax field, NameMask caches)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var name = string.Concat(ArchSyntax.SplitWords(variable.Identifier.Text));
                var message = $"поле-кэш '{variable.Identifier.Text}' без явного исключения из сохранения";
                if (caches.IsMatch(name))
                    context.Report(variable.Identifier.Span, message);
            }
        }

        // Свойство только с get и телом: значение вычисляется, а не хранится
        private static bool IsDerived(PropertyDeclarationSyntax property)
        {
            if (property.ExpressionBody != null)
                return true;

            var accessors = property.AccessorList?.Accessors;
            if (accessors == null || accessors.Value.Count != 1)
                return false;

            var getter = accessors.Value[0];
            bool hasBody = getter.Body != null || getter.ExpressionBody != null;
            return getter.IsKind(SyntaxKind.GetAccessorDeclaration) && hasBody;
        }

        private static bool IsInstanceField(FieldDeclarationSyntax field)
            => !field.Modifiers.Any(SyntaxKind.ConstKeyword) && !field.Modifiers.Any(SyntaxKind.StaticKeyword);

        private static bool HasAttribute(MemberDeclarationSyntax member, IReadOnlyList<string> names)
        {
            return member.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => names.Contains(Invocations.GetSimpleName(attribute.Name)));
        }
    }
}
