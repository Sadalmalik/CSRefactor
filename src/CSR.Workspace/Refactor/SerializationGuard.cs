using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Workspace.Refactor
{
    public enum SerializedKind
    {
        // Поле, которое Unity хранит по имени в ассетах и сценах
        UnityField,
        // Метод-сообщение Unity (Update, OnEnable...): Unity зовет его по имени
        UnityMessage,
        // [JsonProperty] без явного имени
        Json,
        // [DataMember] без Name
        DataMember,
        // Атрибут из конфига
        Custom,
    }

    // Причина, по которой имя символа нельзя просто переименовать
    public class SerializedName
    {
        public required SerializedKind Kind { get; init; }

        public required string Reason { get; init; }

        // Можно сохранить старое имя для данных: [FormerlySerializedAs], имя в [JsonProperty] / [DataMember]
        public bool CanMigrate { get; init; }

        // Почему --migrate не поможет
        public string? MigrationProblem { get; init; }

        // Атрибут Unity на автосвойстве ([field: SerializeField]): хранится поле <Имя>k__BackingField
        public bool BackingField { get; init; }

        // Атрибут, в который --migrate дописывает старое имя (Json, DataMember)
        public AttributeData? Attribute { get; init; }
    }

    // Проверка сериализуемых имен до переименования
    public static class SerializationGuard
    {
        private const string SerializeField = "UnityEngine.SerializeField";
        private const string SerializeReference = "UnityEngine.SerializeReference";
        private const string UnityObject = "UnityEngine.Object";
        private const string NonSerialized = "System.NonSerializedAttribute";
        private const string JsonProperty = "Newtonsoft.Json.JsonPropertyAttribute";
        private const string DataMember = "System.Runtime.Serialization.DataMemberAttribute";

        private static readonly HashSet<string> UnityMessages =
        [
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy",
            "OnValidate", "Reset", "OnGUI", "OnApplicationQuit", "OnApplicationFocus", "OnApplicationPause",
            "OnBecameVisible", "OnBecameInvisible", "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
            "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D", "OnTriggerEnter", "OnTriggerExit",
            "OnTriggerStay", "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D", "OnDrawGizmos",
            "OnDrawGizmosSelected", "OnMouseDown", "OnMouseUp", "OnMouseEnter", "OnMouseExit", "OnMouseOver",
            "OnMouseDrag", "OnRenderObject", "OnWillRenderObject", "OnPreRender", "OnPostRender", "OnRenderImage",
            "OnAnimatorMove", "OnAnimatorIK", "OnTransformParentChanged", "OnTransformChildrenChanged",
            "OnRectTransformDimensionsChange", "OnParticleCollision", "OnControllerColliderHit",
        ];

        public static List<SerializedName> Check(ISymbol symbol, IReadOnlyList<string> customAttributes)
        {
            var names = new List<SerializedName>();
            var singleDeclarator = HasSingleDeclarator(symbol);
            var sharedProblem = singleDeclarator ? null : "в объявлении несколько переменных: атрибут попадет на все";
            switch (symbol)
            {
                case IFieldSymbol field when IsUnitySerialized(field):
                    names.Add(new SerializedName
                    {
                        Kind = SerializedKind.UnityField,
                        Reason = "поле сериализует Unity: имя хранится в сценах, префабах и ассетах",
                        CanMigrate = singleDeclarator,
                        MigrationProblem = sharedProblem,
                    });
                    break;
                case IPropertySymbol property when IsUnitySerialized(property):
                    names.Add(new SerializedName
                    {
                        Kind = SerializedKind.UnityField,
                        Reason = $"поле автосвойства сериализует Unity: в ассетах - <{property.Name}>k__BackingField",
                        CanMigrate = true,
                        BackingField = true,
                    });
                    break;
                case IMethodSymbol method when IsUnityMessage(method):
                    names.Add(new SerializedName
                    {
                        Kind = SerializedKind.UnityMessage,
                        Reason = $"{method.Name} - сообщение Unity: движок зовет метод по имени",
                        MigrationProblem = "у сообщений Unity нет переноса имени",
                    });
                    break;
            }

            AddAttributeNames(symbol, names, singleDeclarator, sharedProblem);
            AddCustomNames(symbol, customAttributes, names);
            return names;
        }

        private static void AddAttributeNames(
            ISymbol symbol,
            List<SerializedName> names,
            bool singleDeclarator,
            string? sharedProblem)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                var name = GetName(attribute);
                if (name == JsonProperty && !HasStringArgument(attribute))
                {
                    names.Add(new SerializedName
                    {
                        Kind = SerializedKind.Json,
                        Reason = "[JsonProperty] без имени: в JSON пишется имя члена",
                        CanMigrate = singleDeclarator,
                        MigrationProblem = sharedProblem,
                        Attribute = attribute,
                    });
                }
                else if (name == DataMember && !attribute.NamedArguments.Any(argument => argument.Key == "Name"))
                {
                    names.Add(new SerializedName
                    {
                        Kind = SerializedKind.DataMember,
                        Reason = "[DataMember] без Name: при сериализации пишется имя члена",
                        CanMigrate = singleDeclarator,
                        MigrationProblem = sharedProblem,
                        Attribute = attribute,
                    });
                }
            }
        }

        private static void AddCustomNames(
            ISymbol symbol,
            IReadOnlyList<string> customAttributes,
            List<SerializedName> names)
        {
            if (customAttributes.Count == 0)
                return;

            var attributes = symbol.GetAttributes().AsEnumerable();
            if (symbol is IPropertySymbol property && GetBackingField(property) is { } backing)
                attributes = attributes.Concat(backing.GetAttributes());

            foreach (var attribute in attributes)
            {
                var name = GetName(attribute);
                var configured = customAttributes.FirstOrDefault(custom =>
                    name == custom || name == custom + "Attribute");
                if (configured == null)
                    continue;

                names.Add(new SerializedName
                {
                    Kind = SerializedKind.Custom,
                    Reason = $"атрибут [{configured}] из конфига: имя члена сериализуется",
                    MigrationProblem = "перенос имени для атрибутов из конфига не поддержан",
                });
            }
        }

        // Unity сериализует поле с [SerializeField] / [SerializeReference] и публичное поле экземпляра
        // [Serializable]-типа или наследника UnityEngine.Object (MonoBehaviour, ScriptableObject)
        private static bool IsUnitySerialized(IFieldSymbol field)
        {
            if (field.IsStatic || field.IsConst || HasAttribute(field, NonSerialized))
                return false;

            if (HasUnitySerializeAttribute(field))
                return true;

            if (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly)
                return false;

            return field.ContainingType.IsSerializable || DerivesFrom(field.ContainingType, UnityObject);
        }

        // [field: SerializeField] на автосвойстве
        private static bool IsUnitySerialized(IPropertySymbol property)
            => GetBackingField(property) is { } backing && HasUnitySerializeAttribute(backing);

        private static bool IsUnityMessage(IMethodSymbol method)
            => UnityMessages.Contains(method.Name) && DerivesFrom(method.ContainingType, UnityObject);

        private static bool HasUnitySerializeAttribute(IFieldSymbol field)
            => HasAttribute(field, SerializeField) || HasAttribute(field, SerializeReference);

        public static IFieldSymbol? GetBackingField(IPropertySymbol property)
        {
            return property.ContainingType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
        }

        public static bool DerivesFrom(INamedTypeSymbol? type, string baseName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.ToDisplayString() == baseName)
                    return true;
            }

            return false;
        }

        private static bool HasAttribute(ISymbol symbol, string name)
            => symbol.GetAttributes().Any(attribute => GetName(attribute) == name);

        private static string? GetName(AttributeData attribute)
            => attribute.AttributeClass?.ToDisplayString();

        private static bool HasStringArgument(AttributeData attribute)
            => attribute.ConstructorArguments.Any(argument => argument.Value is string);

        private static bool HasSingleDeclarator(ISymbol symbol)
        {
            if (symbol is not IFieldSymbol)
                return true;

            return symbol.DeclaringSyntaxReferences.All(reference => reference.GetSyntax()
                is not VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Variables.Count: > 1 } });
        }
    }
}
