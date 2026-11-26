using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Статические методы с данными первым параметром: в static class не меньше minMethods методов и не меньше
    // minShare процентов методов принимают первым один и тот же тип; в обычном классе или структуре -
    // не меньше minMethods открытых (не private) статических методов с одним чужим типом первым параметром. Static class, где разные
    // типы данных первым параметром у minDataShare процентов методов, - класс-свалка задач разных типов.
    // Встроенные типы и маски ignoreTypes (библиотечные типы, расширения которых - норма) не считаются
    public class StaticDataMethodsCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("minMethods", 3, "сколько методов с общим первым параметром - находка"),
            CheckParameter.Int("minShare", 60, "доля таких методов в static class, процентов"),
            CheckParameter.Int(
                "minDataShare",
                80,
                "доля методов static class с данными первым параметром разных типов, процентов; 0 - не искать"),
            CheckParameter.Bool("staticClassesOnly", false, "искать только в static class"),
            CheckParameter.Strings(
                "ignoreTypes",
                "маски текста типа первого параметра, которые не считаются",
                "Vector2*",
                "Vector3*",
                "Vector4",
                "Quaternion",
                "Color",
                "Color32",
                "Rect*",
                "Bounds*",
                "Matrix4x4",
                "Transform",
                "GameObject",
                "Component",
                "Object",
                "Type",
                "StringBuilder",
                "Span<*",
                "ReadOnlySpan<*",
                "IEnumerable*",
                "ICollection*",
                "IList*",
                "IReadOnly*",
                "List<*",
                "Dictionary<*",
                "HashSet<*",
                "Scene",
                "BinaryReader",
                "BinaryWriter",
                "Stream",
                "TextReader",
                "TextWriter",
                "*[]"),
        ];

        public override string Name
            => "static-data-methods";

        public override string Description
            => "статические методы с данными первым параметром";

        public override string Question
            => "Перенести методы в сам тип данных (регионами)? Общая математика без привязки к одному типу - норма";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool isStaticOnly = context.Settings.GetBool("staticClassesOnly");
            foreach (var type in context.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type is InterfaceDeclarationSyntax)
                    continue;

                bool isStaticClass = type.Modifiers.Any(SyntaxKind.StaticKeyword);
                if (isStaticClass || !isStaticOnly)
                    CheckType(context, type, isStaticClass);
            }
        }

        private static void CheckType(CheckContext context, TypeDeclarationSyntax type, bool isStaticClass)
        {
            var methods = type.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method => isStaticClass || IsOpenStatic(method))
                .ToList();

            var ignored = NameMask.Get(context.Settings.GetStrings("ignoreTypes"));
            var groups = methods
                .Select(method => (Method: method, Type: GetDataType(method, ignored)))
                .Where(pair => pair.Type != null && pair.Type != type.Identifier.Text)
                .GroupBy(pair => pair.Type!)
                .OrderByDescending(group => group.Count())
                .ToList();

            var minMethods = context.Settings.GetInt("minMethods");
            var largest = groups.FirstOrDefault();
            if (largest != null && largest.Count() >= minMethods
                && (!isStaticClass || largest.Count() * 100 >= context.Settings.GetInt("minShare") * methods.Count))
            {
                var names = largest.Select(pair => pair.Method.Identifier.Text).Distinct().ToList();
                var kind = isStaticClass ? "static class" : "статические методы";
                var message = $"{kind} '{type.Identifier.Text}': {largest.Count()} из {methods.Count} методов "
                    + $"принимают первым '{largest.Key}': {FindingTextTools.JoinNames(names)}";
                context.Report(type.Identifier.Span, message);
                return;
            }

            int dataMethods = groups.Sum(group => group.Count());
            var minDataShare = context.Settings.GetInt("minDataShare");
            if (!isStaticClass || minDataShare <= 0 || groups.Count < 2 || dataMethods < minMethods
                || dataMethods * 100 < minDataShare * methods.Count)
                return;

            var types = groups.Select(group => $"{group.Key} {group.Count()}").ToList();
            var mixed = $"static class '{type.Identifier.Text}': {dataMethods} из {methods.Count} методов "
                + $"принимают данные первым параметром, типов {groups.Count}: {FindingTextTools.JoinNames(types)}";
            context.Report(type.Identifier.Span, mixed);
        }

        // Статический метод обычного типа, видимый снаружи: приватные помощники класса не считаются
        private static bool IsOpenStatic(MethodDeclarationSyntax method)
        {
            var modifiers = method.Modifiers;
            if (!modifiers.Any(SyntaxKind.StaticKeyword))
                return false;

            return modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.InternalKeyword)
                || modifiers.Any(SyntaxKind.ProtectedKeyword);
        }

        // Тип первого параметра без in / ref / this; null - параметра нет или тип не считается
        private static string? GetDataType(MethodDeclarationSyntax method, NameMask ignored)
        {
            var parameters = method.ParameterList.Parameters;
            if (parameters.Count == 0 || parameters[0].Type is not { } type)
                return null;

            if (type is PredefinedTypeSyntax or NullableTypeSyntax { ElementType: PredefinedTypeSyntax })
                return null;

            var text = TypeNames.GetTypeText(type);
            var typeParameters = method.TypeParameterList?.Parameters;
            if (typeParameters != null && typeParameters.Value.Any(parameter => parameter.Identifier.Text == text))
                return null;

            return ignored.IsMatch(text) ? null : text;
        }
    }
}
