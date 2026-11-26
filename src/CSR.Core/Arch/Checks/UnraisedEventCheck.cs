using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Событие без отправителя по выбранным проектам.
    // Тип события (наследник базы или интерфейса из eventTypes) нигде не создается: нет new, default(T) и вызова
    // метода из raiseMethods с типом события в аргументе типа (Raise<T>()), - а обработчики есть или нет.
    // Событие C# (event-поле) не поднимается в своем типе (нет вызова и чтения), но на него подписываются.
    // Абстрактные типы событий и базы других типов, override и abstract события не ищутся
    public class UnraisedEventCheck : SolutionCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("eventTypes", "маски баз и интерфейсов типа события", "IEvent", "IEvent<*>"),
            CheckParameter.Strings(
                "raiseMethods",
                "маски методов, которые поднимают событие по аргументу типа",
                "Raise*"),
            CheckParameter.Bool("fieldEvents", true, "искать события C# (event), на которые подписываются"),
        ];

        public override string Name
            => "unraised-event";

        public override string Description
            => "событие без отправителя";

        public override string Question
            => "Отправитель потерян или событие устарело? Устарело - удалить вместе с обработчиками; "
                + "потерян - вернуть отправку";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void AnalyzeSolution(SolutionCheckContext context)
        {
            var eventTypes = NameMask.Get(context.Settings.GetStrings("eventTypes"));
            var raiseMethods = NameMask.Get(context.Settings.GetStrings("raiseMethods"));
            bool checkFields = context.Settings.GetBool("fieldEvents");
            var baseTypes = new HashSet<string>(context.Index.Types
                .Select(declared => declared.Symbol.BaseType)
                .Where(baseType => baseType != null)
                .Select(baseType => SolutionIndex.GetKey(baseType!)));
            foreach (var declared in context.Index.Types)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var type = declared.Symbol;
                if (IsEventType(type, eventTypes) && !baseTypes.Contains(SolutionIndex.GetKey(type)))
                    CheckEventType(context, type, raiseMethods);

                if (!checkFields)
                    continue;

                foreach (var member in type.GetMembers().OfType<IEventSymbol>())
                    CheckFieldEvent(context, member);
            }
        }

        private static bool IsEventType(INamedTypeSymbol type, NameMask eventTypes)
        {
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract || eventTypes.IsEmpty)
                return false;

            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (IsMatch(baseType, eventTypes))
                    return true;
            }

            return type.AllInterfaces.Any(contract => IsMatch(contract, eventTypes));
        }

        private static bool IsMatch(INamedTypeSymbol type, NameMask masks)
        {
            return masks.IsMatch(type.Name)
                || masks.IsMatch(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        private static void CheckEventType(SolutionCheckContext context, INamedTypeSymbol type, NameMask raiseMethods)
        {
            var references = context.Index.GetReferences(type);
            var typeKey = SolutionIndex.GetKey(type);
            if (references.Any(reference => IsRaise(reference, raiseMethods, typeKey))
                || IsCreatedByConstructor(context, type))
                return;

            var location = type.Locations.First(candidate => candidate.IsInSource);
            var handlers = references
                .Where(reference => !SyntaxTools.IsInside(reference.Node, type))
                .Select(GetOwnerName)
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            var message = handlers.Count == 0
                ? $"событие '{type.Name}' никто не поднимает и не обрабатывает"
                : $"событие '{type.Name}' никто не поднимает; обрабатывают: {FindingTextTools.JoinNames(handlers)}";
            context.Report(location, message);
        }

        // Отправка по имени типа: new T(...), new T { }, default(T), Raise<T>(...), фабрика T.Create()
        private static bool IsRaise(SymbolReference reference, NameMask raiseMethods, string typeKey)
        {
            SyntaxNode name = reference.Node;
            while (name.Parent is QualifiedNameSyntax qualified && qualified.Right == name
                || name.Parent is AliasQualifiedNameSyntax)
                name = name.Parent;

            switch (name.Parent)
            {
                case ObjectCreationExpressionSyntax creation when creation.Type == name:
                case DefaultExpressionSyntax:
                    return true;
                case TypeArgumentListSyntax { Parent: GenericNameSyntax method }:
                    return IsInvoked(method) && raiseMethods.IsMatch(method.Identifier.Text);
                case MemberAccessExpressionSyntax access when access.Expression == name:
                    return IsFactoryCall(reference.Model, access, typeKey);
                default:
                    return false;
            }
        }

        // Статический метод типа, который возвращает его экземпляр (или обертку с ним в аргументе типа)
        private static bool IsFactoryCall(SemanticModel model, MemberAccessExpressionSyntax access, string typeKey)
        {
            if (access.Parent is not InvocationExpressionSyntax invocation || invocation.Expression != access)
                return false;

            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { IsStatic: true } method)
                return false;

            var result = method.ReturnType;
            return SolutionIndex.GetKey(result) == typeKey
                || result is INamedTypeSymbol named
                && named.TypeArguments.Any(argument => SolutionIndex.GetKey(argument) == typeKey);
        }

        private static bool IsInvoked(GenericNameSyntax method)
        {
            SyntaxNode expression = method;
            if (method.Parent is MemberAccessExpressionSyntax access && access.Name == method)
                expression = access;

            return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
        }

        // new() с целевым типом: ссылка индекса на конструктор
        private static bool IsCreatedByConstructor(SolutionCheckContext context, INamedTypeSymbol type)
        {
            return type.InstanceConstructors.Any(constructor => context.Index.GetReferences(constructor)
                .Any(reference => reference.Node is ImplicitObjectCreationExpressionSyntax));
        }

        private static void CheckFieldEvent(SolutionCheckContext context, IEventSymbol member)
        {
            if (member.IsOverride || member.IsAbstract || !IsFieldLike(member, context.Cancellation))
                return;

            var references = context.Index.GetReferences(member);
            bool isRaised = references.Any(reference => reference.Access == ReferenceAccess.Read);
            int subscriptions = references.Count(reference => IsSubscription(reference.Node));
            if (isRaised || subscriptions == 0)
                return;

            var location = member.Locations.First(candidate => candidate.IsInSource);
            var name = $"{member.ContainingType.Name}.{member.Name}";
            context.Report(location, $"событие '{name}' нигде не поднимается; подписок: {subscriptions}");
        }

        // event-поле: объявление без add / remove
        private static bool IsFieldLike(IEventSymbol member, CancellationToken cancellation)
        {
            return member.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellation))
                .Any(syntax => syntax is VariableDeclaratorSyntax);
        }

        private static bool IsSubscription(SyntaxNode node)
        {
            SyntaxNode expression = node;
            if (node.Parent is MemberAccessExpressionSyntax access && access.Name == node)
                expression = access;

            return expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression
                && (assignment.IsKind(SyntaxKind.AddAssignmentExpression)
                    || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression));
        }

        // Тип, в котором стоит ссылка; null - вне типов
        private static string? GetOwnerName(SymbolReference reference)
        {
            var declaration = SyntaxTools.GetEnclosingType(reference.Node);
            return declaration?.Identifier.Text;
        }
    }
}
