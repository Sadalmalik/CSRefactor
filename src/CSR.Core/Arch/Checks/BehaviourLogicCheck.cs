using CSR.Core.Checks;
using CSR.Core.Syntax;
using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Логика в компоненте сцены: у наследника базового типа (MonoBehaviour) больше maxMethods своих методов -
    // не сообщений движка, не отрисовки, не помеченных атрибутами из ignoreAttributes.
    // Вызовы поиска компонентов у другого объекта (x.GetComponent) в классах, которые не наследуют базовые типы, -
    // логика достает сцену.
    // Наследование видно только напрямую: промежуточные базы проекта добавляются в baseTypes
    public class BehaviourLogicCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("baseTypes", "маски простых имен базовых типов компонента сцены", "MonoBehaviour"),
            CheckParameter.Strings(
                "messages",
                "маски методов-сообщений движка и отрисовки, которые не считаются",
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
                "OnWillRender*",
                "OnPre*",
                "OnPost*",
                "OnRectTransform*",
                "OnCanvas*",
                "OnBeforeTransform*",
                "OnControllerColliderHit",
                "OnJointBreak*",
                "OnSceneGUI",
                "Draw*"),
            CheckParameter.Strings("ignoreAttributes", "методы с этими атрибутами не считаются", "ContextMenu"),
            CheckParameter.Int("maxMethods", 0, "сколько своих методов допустимо"),
            CheckParameter.Strings(
                "lookupMethods",
                "вызовы поиска компонентов, которые ищутся вне компонентов сцены",
                "GetComponent",
                "GetComponents",
                "GetComponentInChildren",
                "GetComponentsInChildren",
                "GetComponentInParent",
                "GetComponentsInParent",
                "TryGetComponent"),
        ];

        public override string Name
            => "behaviour-logic";

        public override string Description
            => "логика в компоненте сцены";

        public override string Question
            => "Это представление, данные сцены, редактор или стенд? "
                + "Если логика - в обычный класс; компонент сцены отдает данные";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var baseTypes = NameMask.Get(context.Settings.GetStrings("baseTypes"));
            foreach (var type in context.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (SyntaxTools.HasBaseType(type, baseTypes))
                    CheckBehaviour(context, type);
                else if (!IsPartWithoutBase(type))
                    CheckLookups(context, type);
            }
        }

        private static void CheckBehaviour(CheckContext context, ClassDeclarationSyntax type)
        {
            var messages = NameMask.Get(context.Settings.GetStrings("messages"));
            var ignoredAttributes = context.Settings.GetStrings("ignoreAttributes");
            var own = type.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method => !messages.IsMatch(method.Identifier.Text))
                .Where(method => !SyntaxTools.HasAttribute(method, ignoredAttributes))
                .Select(method => method.Identifier.Text)
                .ToList();

            var maxMethods = context.Settings.GetInt("maxMethods");
            if (own.Count <= maxMethods)
                return;

            var baseName = type.BaseList!.Types.First().Type.ToString();
            var names = FindingTextTools.JoinNames(own);
            var message = $"{baseName} '{type.Identifier.Text}': своих методов {own.Count} (порог {maxMethods}): {names}";
            context.Report(type.Identifier.Span, message);
        }

        // Поиск компонентов в классе, который сам не компонент сцены; вложенные типы проверяются отдельно
        private static void CheckLookups(CheckContext context, ClassDeclarationSyntax type)
        {
            var lookups = context.Settings.GetStrings("lookupMethods");
            foreach (var invocation in type.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (SyntaxTools.GetEnclosingType(invocation) != type)
                    continue;

                var name = GetMethodName(invocation.Expression);
                if (name != null && lookups.Contains(name))
                    context.Report(invocation.Span, $"'{name}' вне компонента сцены: {FindingTextTools.Shorten(invocation)}");
            }
        }

        // Часть partial-класса без списка баз: база может быть объявлена в другом файле
        private static bool IsPartWithoutBase(ClassDeclarationSyntax type)
            => type.BaseList == null && type.Modifiers.Any(SyntaxKind.PartialKeyword);

        // Имя вызванного метода у другого объекта; вызов без получателя или у this - сам класс компонент
        // через непрямую базу, такой вызов не находка
        private static string? GetMethodName(ExpressionSyntax expression)
        {
            if (expression is not MemberAccessExpressionSyntax access || access.Expression is ThisExpressionSyntax)
                return null;

            return access.Name.Identifier.Text;
        }
    }
}
