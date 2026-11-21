// Заглушки внешних библиотек: тестовое решение собирается без Unity и Newtonsoft.Json

namespace UnityEngine
{
    public class Object
    {
    }

    public class Component : Object
    {
    }

    public class MonoBehaviour : Component
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeReference : Attribute
    {
    }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class FormerlySerializedAs : Attribute
    {
        public FormerlySerializedAs(string oldName)
        {
            OldName = oldName;
        }

        public string OldName { get; }
    }
}

namespace Newtonsoft.Json
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class JsonPropertyAttribute : Attribute
    {
        public JsonPropertyAttribute()
        {
        }

        public JsonPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string? PropertyName { get; }

        public int Order { get; set; }
    }
}

namespace Sample.Storage
{
    // Атрибут проекта: имя члена пишется в сохранение
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SavedAttribute : Attribute
    {
    }
}
