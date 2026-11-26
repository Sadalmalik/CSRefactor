using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Мертвый код по выбранным проектам: член без ссылок, поле или свойство, которое только записывается,
    // параметр, который не читается. Не ищутся: override, virtual, abstract, реализации интерфейсов, сообщения движка
    // и члены с атрибутами из ignoreAttributes (тесты, вызов движком), члены перечислений, конструкторы.
    // Данные (члены с атрибутами из dataAttributes и поля типа с таким атрибутом) заполняет загрузка или инспектор:
    // для них ищется только запись без чтения в коде - мертвые данные в сохранении, удаление с миграцией.
    // и операторы. Видимость - accessibility: public по умолчанию не ищется (потребители могут быть вне решения)
    public class DeadMemberCheck : SolutionCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "accessibility",
                "видимость членов: private, protected, internal, public",
                "private",
                "internal"),
            CheckParameter.Bool("writeOnly", true, "искать поля и свойства, которые только записываются"),
            CheckParameter.Bool("parameters", true, "искать параметры, которые не читаются"),
            CheckParameter.Strings(
                "ignoreNames",
                "маски имен членов, вызываемых снаружи (сообщения движка)",
                "Awake",
                "Start",
                "Update",
                "FixedUpdate",
                "LateUpdate",
                "OnEnable",
                "OnDisable",
                "OnDestroy",
                "OnValidate",
                "Reset",
                "OnGUI",
                "OnDrawGizmos*",
                "OnTrigger*",
                "OnCollision*",
                "OnMouse*",
                "OnApplication*",
                "OnBecame*",
                "OnAnimator*",
                "OnParticle*",
                "OnTransform*",
                "OnRender*",
                "OnBefore*",
                "OnAfter*",
                "GetEnumerator",
                "Dispose",
                "Deconstruct",
                "Main"),
            CheckParameter.Strings(
                "dataAttributes",
                "атрибуты сериализации: ищется только запись без чтения",
                "SerializeField",
                "Serializable",
                "SerializeReference",
                "JsonProperty",
                "DataMember"),
            CheckParameter.Strings(
                "ignoreAttributes",
                "члены и типы с этими атрибутами не ищутся",
                "Test*",
                "SetUp",
                "TearDown",
                "OneTime*",
                "MenuItem",
                "ContextMenu",
                "RuntimeInitializeOnLoadMethod",
                "InitializeOnLoad*",
                "UsedImplicitly",
                "Preserve",
                "DllImport",
                "Obsolete"),
        ];

        public override string Name
            => "dead-member";

        public override string Description
            => "мертвый код";

        public override string Question
            => "Удалить вместе с местами записи, если код в задаче; если вне задачи - записать отдельной задачей";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void AnalyzeSolution(SolutionCheckContext context)
        {
            var accessibility = context.Settings.GetStrings("accessibility");
            var ignoredNames = NameMask.Get(context.Settings.GetStrings("ignoreNames"));
            var ignoredAttributes = NameMask.Get(context.Settings.GetStrings("ignoreAttributes"));
            var dataAttributes = NameMask.Get(context.Settings.GetStrings("dataAttributes"));
            foreach (var declared in context.Index.Types)
            {
                var type = declared.Symbol;
                if (type.TypeKind is TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate
                    || HasAttribute(type, ignoredAttributes))
                    continue;

                bool isSerialized = HasAttribute(type, dataAttributes);

                foreach (var member in type.GetMembers())
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    if (member.IsImplicitlyDeclared || !member.Locations.Any(location => location.IsInSource))
                        continue;

                    bool isData = (isSerialized && member is IFieldSymbol) || HasAttribute(member, dataAttributes);
                    if (IsCandidate(member, accessibility, ignoredNames, ignoredAttributes))
                        CheckMember(context, member, isData);

                    if (member is IMethodSymbol method && context.Settings.GetBool("parameters")
                        && IsParameterCandidate(context, method, ignoredNames, ignoredAttributes))
                        CheckParameters(context, method);
                }
            }
        }

        private static void CheckMember(SolutionCheckContext context, ISymbol member, bool isData)
        {
            var references = context.Index.GetReferences(member)
                .Where(reference => !SyntaxParts.IsInside(reference.Node, member))
                .ToList();
            var name = $"{member.ContainingType.Name}.{member.Name}";
            var location = member.Locations.First(candidate => candidate.IsInSource);
            if (references.Count == 0 && !isData)
            {
                context.Report(location, $"{GetKind(member)} '{name}' не используется");
                return;
            }

            bool isValue = member is IFieldSymbol or IPropertySymbol;
            if (!isValue || references.Count == 0 || !context.Settings.GetBool("writeOnly")
                || references.Any(reference => reference.IsRead))
                return;

            var saved = isData ? " (сохраняется: удаление - с миграцией)" : "";
            context.Report(location, $"{GetKind(member)} '{name}' только записывается: мест {references.Count}{saved}");
        }

        private static void CheckParameters(SolutionCheckContext context, IMethodSymbol method)
        {
            foreach (var parameter in method.Parameters)
            {
                if (parameter.Name.StartsWith("_", StringComparison.Ordinal) || parameter.IsThis
                    || parameter.RefKind == RefKind.Out)
                    continue;

                if (context.Index.GetReferences(parameter).Any(reference => reference.IsRead))
                    continue;

                var location = parameter.Locations.FirstOrDefault(candidate => candidate.IsInSource);
                var owner = $"{method.ContainingType.Name}.{method.Name}";
                if (location != null)
                    context.Report(location, $"параметр '{parameter.Name}' метода '{owner}' не читается");
            }
        }

        private static bool IsCandidate(
            ISymbol member,
            IReadOnlyList<string> accessibility,
            NameMask ignoredNames,
            NameMask ignoredAttributes)
        {
            if (!accessibility.Contains(GetAccessibility(member)) || ignoredNames.IsMatch(member.Name)
                || HasAttribute(member, ignoredAttributes) || IsExternallyBound(member))
                return false;

            return member switch
            {
                IMethodSymbol method => method.MethodKind == MethodKind.Ordinary && !method.IsPartialDefinition,
                IFieldSymbol field => !field.IsImplicitlyDeclared && field.AssociatedSymbol == null,
                IPropertySymbol property => !property.IsIndexer,
                IEventSymbol => true,
                _ => false,
            };
        }

        // Параметры обычного метода с телом, который вызывается напрямую (не группа методов, не обработчик)
        private static bool IsParameterCandidate(
            SolutionCheckContext context,
            IMethodSymbol method,
            NameMask ignoredNames,
            NameMask ignoredAttributes)
        {
            if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor) || IsExternallyBound(method)
                || ignoredNames.IsMatch(method.Name) || HasAttribute(method, ignoredAttributes) || method.IsExtern)
                return false;

            bool hasBody = method.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(context.Cancellation))
                .OfType<BaseMethodDeclarationSyntax>()
                .Any(declaration => declaration.Body != null || declaration.ExpressionBody != null);
            if (!hasBody)
                return false;

            // Метод без вызовов - уже находка сам; конструктор зовут и через base() / this(), их индекс не видит
            var references = context.Index.GetReferences(method);
            if (method.MethodKind == MethodKind.Ordinary && references.Count == 0)
                return false;

            return references.All(reference => reference.IsInvocation);
        }

        // Член, который вызывается через базу или интерфейс: override, virtual, abstract, реализация интерфейса
        private static bool IsExternallyBound(ISymbol member)
        {
            if (member.IsOverride || member.IsVirtual || member.IsAbstract)
                return true;

            var type = member.ContainingType;
            return type.AllInterfaces
                .SelectMany(contract => contract.GetMembers())
                .Any(contractMember => SymbolEqualityComparer.Default.Equals(
                    type.FindImplementationForInterfaceMember(contractMember), member));
        }

        private static bool HasAttribute(ISymbol symbol, NameMask attributes)
        {
            var own = symbol.GetAttributes();
            var backing = symbol is IPropertySymbol property
                ? property.ContainingType.GetMembers().OfType<IFieldSymbol>()
                    .Where(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property))
                    .SelectMany(field => field.GetAttributes())
                : Enumerable.Empty<AttributeData>();

            return own.Concat(backing).Any(attribute => attribute.AttributeClass != null
                && attributes.IsMatch(TypeNames.TrimAttributeSuffix(attribute.AttributeClass.Name)));
        }

        private static string GetAccessibility(ISymbol member)
        {
            return member.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Protected or Accessibility.ProtectedOrInternal or Accessibility.ProtectedAndInternal
                    => "protected",
                _ => "private",
            };
        }

        private static string GetKind(ISymbol member)
        {
            return member switch
            {
                IMethodSymbol => "метод",
                IFieldSymbol => "поле",
                IPropertySymbol => "свойство",
                _ => "событие",
            };
        }
    }
}
