using System.Text.RegularExpressions;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Similar
{
    // Единицы L2 одного файла. Набор параметров - входные параметры (без out) метода, конструктора, оператора,
    // локальной функции; override и явные реализации интерфейса не берутся (набор задан базой). Набор полей -
    // поля экземпляра (без const и static), автосвойства экземпляра и параметры основного конструктора типа.
    // Элемент - тип как написан (без пробелов) и имя без префикса поля в нижнем регистре, после синонимов
    public static class SetUnitTool
    {
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

        public static List<SimilarUnit> Collect(SyntaxTree tree, SimilarOptions options)
        {
            var units = new List<SimilarUnit>();
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var unit = node switch
                {
                    BaseMethodDeclarationSyntax method when options.Kinds.Contains(UnitKind.ParameterSet)
                        && !IsInherited(method) => CreateParameterSet(tree, method, method.ParameterList, options),
                    LocalFunctionStatementSyntax function when options.Kinds.Contains(UnitKind.ParameterSet)
                        => CreateParameterSet(tree, function, function.ParameterList, options),
                    TypeDeclarationSyntax type when options.Kinds.Contains(UnitKind.FieldSet)
                        && type is not InterfaceDeclarationSyntax => CreateFieldSet(tree, type, options),
                    _ => null,
                };
                if (unit != null)
                    units.Add(unit);
            }

            return units;
        }

        // Элемент набора: Vector3 position
        public static string GetElement(TypeSyntax type, string name, SimilarOptions options)
        {
            var normalized = NameWordTools.StripFieldPrefix(name).ToLowerInvariant();
            if (options.Synonyms.TryGetValue(normalized, out string? synonym))
                normalized = synonym;

            return Spaces.Replace(type.ToString(), "") + " " + normalized;
        }

        private static SimilarUnit? CreateParameterSet(
            SyntaxTree tree,
            SyntaxNode owner,
            ParameterListSyntax parameters,
            SimilarOptions options)
        {
            var elements = parameters.Parameters
                .Where(parameter => parameter.Type != null && !parameter.Modifiers.Any(SyntaxKind.OutKeyword))
                .Select(parameter => GetElement(parameter.Type!, parameter.Identifier.Text, options))
                .ToList();
            return CreateSet(tree, UnitKind.ParameterSet, owner, parameters, elements, options);
        }

        private static SimilarUnit? CreateFieldSet(SyntaxTree tree, TypeDeclarationSyntax type, SimilarOptions options)
        {
            var elements = new List<string>();
            if (type.ParameterList != null)
            {
                elements.AddRange(type.ParameterList.Parameters
                    .Where(parameter => parameter.Type != null)
                    .Select(parameter => GetElement(parameter.Type!, parameter.Identifier.Text, options)));
            }

            foreach (var member in type.Members)
            {
                if (member is FieldDeclarationSyntax field && !IsShared(field.Modifiers))
                {
                    var fieldType = field.Declaration.Type;
                    elements.AddRange(field.Declaration.Variables
                        .Select(variable => GetElement(fieldType, variable.Identifier.Text, options)));
                }
                else if (member is PropertyDeclarationSyntax property && IsAutoProperty(property)
                    && !IsShared(property.Modifiers))
                {
                    elements.Add(GetElement(property.Type, property.Identifier.Text, options));
                }
            }

            return CreateSet(tree, UnitKind.FieldSet, type, type, elements, options);
        }

        private static SimilarUnit? CreateSet(
            SyntaxTree tree,
            UnitKind kind,
            SyntaxNode owner,
            SyntaxNode place,
            List<string> elements,
            SimilarOptions options)
        {
            var distinct = elements.Distinct().ToList();
            if (distinct.Count < options.MinShared)
                return null;

            var fingerprints = distinct.Select(ShapeHasher.HashText).Distinct().ToArray();
            Array.Sort(fingerprints);
            return new SimilarUnit
            {
                Kind = kind,
                File = tree.FilePath,
                Member = MemberUnitTool.GetMemberName(owner),
                StartLine = MemberUnitTool.GetLine(tree, owner.SpanStart),
                EndLine = MemberUnitTool.GetLine(tree, place.Span.End),
                NodeCount = distinct.Count,
                Fingerprints = fingerprints,
                SpanStart = place.SpanStart,
                SpanEnd = place.Span.End,
                Elements = distinct,
            };
        }

        private static bool IsInherited(BaseMethodDeclarationSyntax method)
        {
            return method.Modifiers.Any(SyntaxKind.OverrideKeyword)
                || method is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null };
        }

        private static bool IsShared(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.StaticKeyword) || modifiers.Any(SyntaxKind.ConstKeyword);

        private static bool IsAutoProperty(PropertyDeclarationSyntax property)
        {
            return property.ExpressionBody == null && property.AccessorList != null
                && property.AccessorList.Accessors.All(accessor => accessor.Body == null && accessor.ExpressionBody == null);
        }
    }
}
