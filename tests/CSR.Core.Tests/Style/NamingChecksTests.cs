using CSR.Core.Syntax;
using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    public class NamingChecksTests
    {
#region NameWords

        [TestCase("IsUIVisible", "Is UI Visible")]
        [TestCase("m_isOn", "m is On")]
        [TestCase("MAX_POOL_SIZE", "MAX POOL SIZE")]
        [TestCase("Vector3Pool", "Vector3 Pool")]
        public void NameWords_Split(string name, string expected)
            => Assert.That(string.Join(" ", NameWords.Split(name)), Is.EqualTo(expected));

        [TestCase("MAX_POOL_SIZE", "MaxPoolSize")]
        [TestCase("m_count", "Count")]
        [TestCase("maxSize", "MaxSize")]
        [TestCase("game.Items", "Game.Items")]
        [TestCase("@class", "Class")]
        public void NameWords_ToPascal(string name, string expected)
            => Assert.That(NameWords.ToPascal(name), Is.EqualTo(expected));

#endregion


#region field-prefix

        [Test]
        public void FieldPrefix_Findings()
        {
            const string source = """
                public class Sample
                {
                    public const int MAX_SIZE = 4;
                    private const int MaxCount = 2;
                    private int count;
                    private int m_name;
                    private static int m_Total;
                    public int m_Public;
                    private int m_Value;
                    private static int s_Shared;
                    public int Visible { get; set; }
                    public int m_Property { get; set; }
                }
                """;
            var messages = Run("field-prefix", source).Select(finding => $"{finding.Line}: {finding.Message}").ToList();
            Assert.That(messages, Is.EqualTo(new[]
            {
                "3: 'MAX_SIZE': константа в PascalCase - 'MaxSize'",
                "5: 'count': закрытое поле с префиксом 'm_' - 'm_Count'",
                "6: 'm_name': после префикса - заглавная буква - 'm_Name'",
                "7: 'm_Total': закрытое статическое поле с префиксом 's_' - 's_Total'",
                "8: 'm_Public': поле без префикса 'm_' - 'Public'",
                "12: 'm_Property': свойство без префикса 'm_' - 'Property'",
            }));
        }

        [Test]
        public void FieldPrefix_StaticReadonlyPascal_AndUpperConstants()
        {
            const string source = """
                public class Sample
                {
                    private static readonly int Cache = 1;
                    private static readonly int s_Pool = 1;
                    private const int MaxSize = 4;
                    private const int ID = 1;
                }
                """;
            var findings = Run("field-prefix", source, settings =>
            {
                settings.Set("staticReadonly", "pascal");
                settings.Set("constants", "upper");
            });
            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "'s_Pool': static readonly без префикса 's_' - 'Pool'",
                "'MaxSize': константа в UPPER_CASE - 'MAX_SIZE'",
            }));
        }

        [Test]
        public void FieldPrefix_NoPrefixes_InterfaceAndStruct()
        {
            const string source = """
                public interface IStorage
                {
                    int Count { get; }
                }

                public struct Point
                {
                    private int m_X;
                    private int y;
                }
                """;
            var findings = Run("field-prefix", source, settings => settings.Set("instancePrefix", ""));
            Assert.That(findings.Single().Message, Is.EqualTo("'m_X': закрытое поле без префикса 'm_' - 'X'"));
        }

        [Test]
        public void FieldPrefix_PrefixedAccess_Protected()
        {
            const string source = """
                public class Sample
                {
                    protected int m_Owner;
                    protected internal int count;
                    public int m_Public;
                }
                """;
            var findings = Run(
                "field-prefix",
                source,
                settings => settings.Set("prefixedAccess", new[] { "private", "protected" }));
            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "'count': защищенное поле с префиксом 'm_' - 'm_Count'",
                "'m_Public': поле без префикса 'm_' - 'Public'",
            }));
        }

#endregion


#region bool-name

        [TestCase("private bool m_IsNotActive;", "m_IsNotActive", "Not")]
        [TestCase("public bool NoTarget { get; set; }", "NoTarget", "No")]
        [TestCase("private void Set(bool dontSave) { }", "dontSave", "dont")]
        [TestCase("private void Set() { bool isNonEmpty = true; }", "isNonEmpty", "Non")]
        [TestCase("private void Set(int a) { Read(out bool notFound); }", "notFound", "not")]
        public void BoolName_Negation_Found(string member, string name, string word)
        {
            var finding = Run("bool-name", InClass(member)).Single();
            Assert.That(finding.Message, Is.EqualTo($"bool '{name}': отрицание '{word}' - имя по состоянию"));
        }

        [TestCase("private bool m_IsActive;")]
        [TestCase("private bool m_Notified;")]
        [TestCase("private int m_NotCount;")]
        [TestCase("public override bool IsNotReady => true;")]
        [TestCase("private void Set() { var isNotReady = true; }")]
        [TestCase("private bool m_IsDisplayed;")]
        public void BoolName_NoFinding(string member)
            => Assert.That(Run("bool-name", InClass(member)), Is.Empty);

        [Test]
        public void BoolName_NegationPrefixes_WithIgnoreWords()
        {
            const string member = """
                private bool m_IsDisabled;
                private bool m_IsDisplayed;
                public bool IsUnlocked { get; }
                """;
            var findings = Run("bool-name", InClass(member), settings =>
            {
                settings.Set("negationPrefixes", new[] { "Dis", "Un" });
                settings.Set("ignoreWords", new[] { "Displayed" });
            });
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 6, 8 }));
        }

        [Test]
        public void BoolName_Members_LocalsOnly()
        {
            const string member = """
                private bool m_NotReady;
                private void Set(bool notSaved) { bool notFound = true; }
                """;
            var findings = Run("bool-name", InClass(member), settings => settings.Set("members", new[] { "local" }));
            Assert.That(findings.Single().Message, Does.Contain("notFound"));
        }

#endregion


#region event-name

        [Test]
        public void EventName_Findings()
        {
            const string member = """
                public event Action OnBeforeCreate;
                public event Action OnCreated;
                public event Action Changed;
                public event Action OnWillCreate;
                public Action Execute;
                public override event Action Changing;
                public delegate void OnCreateHandler();
                public delegate void AfterCreateDelegate();
                public delegate void OnlineHandler();
                """;
            var findings = Run("event-name", InClass(member), settings =>
            {
                settings.Set("allowed", new[] { "OnBefore*", "OnAfter*" });
                settings.Set("forbidden", new[] { "OnWill*", "On*ed" });
            });
            Assert.That(findings.Select(finding => $"{finding.Line}: {finding.Message}"), Is.EqualTo(new[]
            {
                "7: событие 'OnCreated': запрещенная форма 'On*ed'",
                "8: событие 'Changed': допустимо OnBefore*, OnAfter*",
                "9: событие 'OnWillCreate': запрещенная форма 'OnWill*'",
                "12: delegate 'OnCreateHandler': без префикса 'On'",
            }));
        }

        [Test]
        public void EventName_Defaults_AnyOnPrefix()
        {
            const string member = """
                public event Action OnChanged;
                public event Action Changed;
                public event Action OnDidLoad;
                """;
            var findings = Run("event-name", InClass(member));
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 7, 8 }));
        }

#endregion


#region type-name

        [Test]
        public void TypeName_InterfaceAndTypeParameters()
        {
            const string source = """
                public interface IStorage { }
                public interface Storage { }
                public interface Index { }
                public class Box<T, TItem, Value> { }
                public class Sample
                {
                    public void Get<Key>() { }
                }
                """;
            var messages = Run("type-name", source).Select(finding => $"{finding.Line}: {finding.Message}");
            Assert.That(messages, Is.EqualTo(new[]
            {
                "2: интерфейс 'Storage': префикс 'I'",
                "3: интерфейс 'Index': префикс 'I'",
                "4: параметр-тип 'Value': префикс 'T'",
                "7: параметр-тип 'Key': префикс 'T'",
            }));
        }

        [Test]
        public void TypeName_DerivedNames()
        {
            const string source = """
                public class SaveCommand : CommandBase { }
                public class Save : CommandBase, IDisposable { }
                public class Load : Core.CommandBase<int> { }
                public class Plain : Other { }
                public class Item : AssetBase { }
                public class AssetItem : AssetBase { }
                """;
            var findings = Run("type-name", source, settings => settings.Set(
                "derivedNames",
                new Dictionary<string, string> { ["CommandBase"] = "*Command", ["Asset*"] = "Asset*" }));
            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "класс 'Save': допустимо *Command",
                "класс 'Load': допустимо *Command",
                "класс 'Item': допустимо Asset*",
            }));
        }

#endregion


#region namespace-name

        [Test]
        public void NamespaceName_RootsWordsDepth()
        {
            const string source = """
                namespace Company.Tools.Editor.Views
                {
                    public class View { }
                }

                namespace Other.Tools
                {
                    public class Tool { }
                }

                namespace Company
                {
                    namespace Units
                    {
                        public class Unit { }
                    }
                }

                public class Global { }
                """;
            var findings = Run("namespace-name", source, settings =>
            {
                settings.Set("roots", new[] { "Company" });
                settings.Set("forbiddenWords", new[] { "Editor" });
                settings.Set("maxDepth", 3);
            });
            Assert.That(findings.Select(finding => $"{finding.Line}: {finding.Message}"), Is.EqualTo(new[]
            {
                "1: пространство 'Company.Tools.Editor.Views': слово 'Editor'",
                "1: пространство 'Company.Tools.Editor.Views': частей 4, предел 3",
                "6: пространство 'Other.Tools' вне корня: Company",
                "19: тип 'Global' вне пространства имен",
            }));
        }

        [Test]
        public void NamespaceName_FileScoped_WordInsidePart()
        {
            const string source = """
                namespace Company.EditorTools;

                public class Tool { }
                """;
            var findings = Run("namespace-name", source, settings => settings.Set("forbiddenWords", new[] { "Editor" }));
            Assert.That(findings.Single().Message, Is.EqualTo("пространство 'Company.EditorTools': слово 'Editor'"));
        }

        [Test]
        public void NamespaceName_Defaults_NoFinding()
            => Assert.That(Run("namespace-name", "namespace A.B.C.D.E.F { public class X { } }\npublic class Y { }"), Is.Empty);

#endregion


#region enum-plural

        [Test]
        public void EnumPlural_FlagsAndSingular()
        {
            const string source = """
                [System.Flags]
                public enum DamageType { None = 0 }
                [Flags]
                public enum DamageTypes { None = 0 }
                [Flags]
                public enum LayerMask { None = 0 }
                public enum Colors { Red }
                public enum Status { Ok }
                public enum Color { Red }
                """;
            var findings = Run("enum-plural", source);
            Assert.That(findings.Select(finding => $"{finding.Line}: {finding.Message}"), Is.EqualTo(new[]
            {
                "2: enum 'DamageType': имя во множественном числе",
                "7: enum 'Colors': имя в единственном числе",
            }));
        }

#endregion


#region collection-name

        [Test]
        public void CollectionName_Findings()
        {
            const string member = """
                private List<int> m_Value;
                private List<int> m_Values;
                private int[] m_Point;
                private Dictionary<string, int> m_ValueById;
                private Dictionary<string, int> m_ValuesByIdDict;
                public IReadOnlyList<string> Item { get; }
                public System.Collections.Generic.HashSet<int> Set { get; }
                private byte[] m_Data;
                private List<int>? m_Status;
                private string m_Name;
                """;
            var findings = Run("collection-name", InClass(member));
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 6, 8, 9, 11, 14 }));
            Assert.That(findings[0].Message, Is.EqualTo("коллекция 'm_Value': имя во множественном числе"));
        }

        [Test]
        public void CollectionName_ArraysOffAndLocals()
        {
            const string member = """
                private int[] m_Point;
                private void Run(List<int> item) { List<int> value = null; }
                """;
            var findings = Run("collection-name", InClass(member), settings =>
            {
                settings.Set("arrays", false);
                settings.Set("members", new[] { "field", "parameter", "local" });
            });
            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "коллекция 'item': имя во множественном числе",
                "коллекция 'value': имя во множественном числе",
            }));
        }

#endregion

        // Члены класса: первая строка members - 6-я строка файла
        private static string InClass(string members)
        {
            var indented = string.Join("\n", members.Split('\n').Select(line => "    " + line));
            return $$"""
                using System;
                using System.Collections.Generic;

                public class Sample
                {
                {{indented}}
                }
                """;
        }
    }
}
