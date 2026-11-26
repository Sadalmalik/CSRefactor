using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Similar
{
    // Единицы L3 одного файла: класс, структура, запись (объявление; части partial - по отдельности). Профиль:
    // - базы: имя без пространства и число аргументов типа (base SyntaxCheck, base IEnumerable`1);
    // - переопределения: override и имя члена (override Analyze(1));
    // - поля: модификаторы и тип (field private static readonly CheckParameter[]);
    // - сигнатуры остальных членов: вид, модификаторы, тип и число параметров (method private static bool(1)).
    // Тела членов и инициализаторы полей - формы L1 для сопоставления. Вложенные типы - отдельные единицы.
    // Тип без поведения (нет члена с телом, кроме конструктора) - данные: их сравнивает набор полей L2
    public static class TypeUnitTool
    {
        private static readonly List<string> ModifierOrder =
        [
            "public",
            "protected",
            "internal",
            "private",
            "file",
            "const",
            "static",
            "abstract",
            "virtual",
            "override",
            "sealed",
            "readonly",
            "volatile",
            "async",
            "extern",
            "unsafe",
            "required",
            "new",
        ];

        public static List<SimilarUnit> Collect(SyntaxTree tree, SimilarOptions options)
        {
            var units = new List<SimilarUnit>();
            foreach (var type in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type is InterfaceDeclarationSyntax)
                    continue;

                if (!HasBehaviour(type))
                    continue;

                var unit = Create(tree, type);
                if (unit.NodeCount >= options.MinTypeMembers)
                    units.Add(unit);
            }

            return units;
        }

        private static TypeUnit Create(SyntaxTree tree, TypeDeclarationSyntax type)
        {
            var bases = new ElementList();
            foreach (var baseType in type.BaseList?.Types ?? default)
                bases.Add("base " + GetSimpleName(baseType.Type));

            var overrides = new ElementList();
            var fields = new ElementList();
            var signatures = new ElementList();
            var members = new List<SimilarUnit>();
            foreach (var member in type.Members)
            {
                if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
                    continue;

                if (member is FieldDeclarationSyntax field)
                {
                    var text = "field " + GetModifiers(field.Modifiers) + Normalize(field.Declaration.Type);
                    for (int i = 0; i < field.Declaration.Variables.Count; i++)
                        fields.Add(text);

                    // инициализатор поля - тоже код: набор параметров проверки, таблица значений
                    if (field.Declaration.Variables.Any(variable => variable.Initializer != null))
                        members.Add(MemberUnitTool.CreateMember(tree, field));
                    continue;
                }

                signatures.Add(GetSignature(member));
                if (member.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    overrides.Add("override " + GetOverrideName(member));

                if (MemberUnitTool.IsUnitNode(member))
                    members.Add(MemberUnitTool.CreateMember(tree, member));
            }

            var profile = new TypeProfile
            {
                Bases = bases.GetFingerprints(),
                Overrides = overrides.GetFingerprints(),
                Fields = fields.GetFingerprints(),
                Signatures = signatures.GetFingerprints(),
            };
            var all = profile.Bases.Concat(profile.Overrides).Concat(profile.Fields).Concat(profile.Signatures)
                .Distinct()
                .ToArray();
            Array.Sort(all);
            return new TypeUnit
            {
                Kind = UnitKind.Type,
                File = tree.FilePath,
                Member = MemberUnitTool.GetMemberName(type),
                StartLine = MemberUnitTool.GetLine(tree, type.SpanStart),
                EndLine = MemberUnitTool.GetLine(tree, type.Span.End),
                NodeCount = fields.Count + signatures.Count,
                Fingerprints = all,
                SpanStart = type.SpanStart,
                SpanEnd = type.Span.End,
                Elements = bases.Texts.Concat(overrides.Texts).Concat(fields.Texts).Concat(signatures.Texts).ToList(),
                Profile = profile,
                Members = members,
            };
        }

        // Член с телом, кроме конструктора и инициализатора поля: метод, свойство с телом, оператор
        private static bool HasBehaviour(TypeDeclarationSyntax type)
        {
            return type.Members.Any(member => member is not (ConstructorDeclarationSyntax or BaseFieldDeclarationSyntax)
                && MemberUnitTool.IsUnitNode(member));
        }

        private static string GetSignature(MemberDeclarationSyntax member)
        {
            var modifiers = GetModifiers(member.Modifiers);
            return member switch
            {
                MethodDeclarationSyntax method
                    => $"method {modifiers}{Normalize(method.ReturnType)}({method.ParameterList.Parameters.Count})",
                ConstructorDeclarationSyntax constructor => $"ctor {modifiers}({constructor.ParameterList.Parameters.Count})",
                PropertyDeclarationSyntax property => $"property {modifiers}{Normalize(property.Type)} "
                    + GetAccessors(property.AccessorList, property.ExpressionBody),
                IndexerDeclarationSyntax indexer => $"indexer {modifiers}{Normalize(indexer.Type)} "
                    + GetAccessors(indexer.AccessorList, indexer.ExpressionBody),
                EventFieldDeclarationSyntax eventField => $"event {modifiers}{Normalize(eventField.Declaration.Type)}",
                EventDeclarationSyntax eventDeclaration => $"event {modifiers}{Normalize(eventDeclaration.Type)}",
                OperatorDeclarationSyntax operatorDeclaration => $"operator {modifiers}{operatorDeclaration.OperatorToken.Text}",
                ConversionOperatorDeclarationSyntax conversion => $"conversion {modifiers}{Normalize(conversion.Type)}",
                DestructorDeclarationSyntax => "destructor",
                _ => member.Kind().ToString(),
            };
        }

        // Имя переопределения: метод - с числом параметров, остальное - по имени
        private static string GetOverrideName(MemberDeclarationSyntax member)
        {
            return member switch
            {
                MethodDeclarationSyntax method => $"{method.Identifier.Text}({method.ParameterList.Parameters.Count})",
                PropertyDeclarationSyntax property => property.Identifier.Text,
                EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.Text,
                EventFieldDeclarationSyntax eventField => string.Join(",", eventField.Declaration.Variables.Select(
                    variable => variable.Identifier.Text)),
                IndexerDeclarationSyntax => "this[]",
                _ => member.Kind().ToString(),
            };
        }

        private static string GetAccessors(AccessorListSyntax? accessors, ArrowExpressionClauseSyntax? expressionBody)
        {
            if (expressionBody != null || accessors == null)
                return "=>";

            return string.Join(" ", accessors.Accessors.Select(accessor => accessor.Keyword.Text));
        }

        // Модификаторы в порядке C# с пробелом в конце: "private static readonly "; partial не входит
        private static string GetModifiers(SyntaxTokenList modifiers)
        {
            var texts = modifiers.Select(token => token.Text)
                .Where(text => text != "partial")
                .OrderBy(text => ModifierOrder.IndexOf(text) is var index && index >= 0 ? index : ModifierOrder.Count)
                .ThenBy(text => text, StringComparer.Ordinal);
            var joined = string.Join(" ", texts);
            return joined.Length > 0 ? joined + " " : "";
        }

        private static string Normalize(TypeSyntax type)
            => string.Concat(type.ToString().Where(symbol => !char.IsWhiteSpace(symbol)));

        // Имя без пространства, с числом аргументов типа: System.Collections.Generic.List<int> -> List`1
        private static string GetSimpleName(TypeSyntax type)
        {
            return type switch
            {
                QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
                AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
                GenericNameSyntax generic => generic.Identifier.Text + "`" + generic.TypeArgumentList.Arguments.Count,
                _ => Normalize(type),
            };
        }

        // Элементы части профиля: повтор элемента получает номер, чтобы совпадение считалось по количеству
        private class ElementList
        {
            private readonly Dictionary<string, int> m_Counts = new();
            private readonly List<string> m_Keys = new();

            public List<string> Texts { get; } = new();

            public int Count
                => Texts.Count;

            public void Add(string text)
            {
                int count = m_Counts.TryGetValue(text, out int previous) ? previous + 1 : 1;
                m_Counts[text] = count;
                Texts.Add(text);
                m_Keys.Add(text + "#" + count);
            }

            public ulong[] GetFingerprints()
            {
                var fingerprints = m_Keys.Select(ShapeHasher.HashText).Distinct().ToArray();
                Array.Sort(fingerprints);
                return fingerprints;
            }
        }
    }
}
