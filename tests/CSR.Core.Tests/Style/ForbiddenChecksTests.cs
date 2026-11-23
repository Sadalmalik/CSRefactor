using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    // Запрещенные конструкции: вызовы и типы, корутины, статическое состояние, локальные функции, склейка пути,
    // флаги bool, типы в файле, замыкания и вложенные циклы
    public class ForbiddenChecksTests
    {
#region forbidden-call

        [Test]
        public void ForbiddenCall_MasksAndQualifiedOwner_Found()
        {
            const string source = """
                public class Sample
                {
                    private void Method()
                    {
                        Debug.Log("a");
                        UnityEngine.Debug.LogWarning("b");
                        Gizmos.color = Color.red;
                        Logger.Info("c");
                    }
                }
                """;

            var findings = Run("forbidden-call", source, settings => settings.Set("calls", new Dictionary<string, string>
            {
                ["Debug.Log*"] = "логгер проекта",
                ["Gizmos.*"] = "",
            }));

            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "запрещенный член 'Debug.Log': логгер проекта",
                "запрещенный член 'Debug.LogWarning': логгер проекта",
                "запрещенный член 'Gizmos.color'",
            }));
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 5, 6, 7 }));
        }

        [Test]
        public void ForbiddenCall_PredefinedOwner_Found()
        {
            const string source = "public class Sample { private string M(int a) => string.Format(\"{0}\", a); }";

            var findings = Run("forbidden-call", source, settings => settings.Set("calls", new Dictionary<string, string>
            {
                ["string.Format"] = "интерполяция",
            }));

            Assert.That(findings.Single().Message, Is.EqualTo("запрещенный член 'string.Format': интерполяция"));
        }

        [Test]
        public void ForbiddenCall_NoMasksOrExcludedPath_NotFound()
        {
            const string source = "public class Sample { private void M() => Debug.Log(\"a\"); }";
            Assert.That(Run("forbidden-call", source), Is.Empty);

            var excluded = Run(
                "forbidden-call",
                source,
                settings =>
                {
                    settings.Set("calls", new Dictionary<string, string> { ["Debug.*"] = "" });
                    settings.Set("excludePaths", new[] { "*/Editor/*" });
                },
                "Assets/Editor/Sample.cs");
            Assert.That(excluded, Is.Empty);
        }

#endregion


#region forbidden-type

        [Test]
        public void ForbiddenType_Dynamic_FoundInTypePositions()
        {
            const string source = """
                public class Sample
                {
                    private dynamic m_Value;

                    private object Method(dynamic input, System.Collections.Generic.List<dynamic> items)
                    {
                        var dynamic = 1;
                        return (dynamic)input;
                    }
                }
                """;

            var empty = Run("forbidden-type", source);
            var findings = Run("forbidden-type", source, settings => settings.Set("types", new Dictionary<string, string>
            {
                ["dynamic"] = "строгая типизация",
            }));

            Assert.That(empty, Is.Empty);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 3, 5, 5, 8 }));
            Assert.That(findings[0].Message, Is.EqualTo("запрещенный тип 'dynamic': строгая типизация"));
        }

        [Test]
        public void ForbiddenType_QualifiedAndGenericMasks_Found()
        {
            const string source = """
                public class Sample
                {
                    private System.Collections.ArrayList m_Items = new System.Collections.ArrayList();
                    private Lazy<int> m_Lazy;
                    private object m_Box;
                    private string Name => object.ReferenceEquals(m_Box, null) ? "" : "box";
                }
                """;

            var findings = Run("forbidden-type", source, settings => settings.Set("types", new Dictionary<string, string>
            {
                ["ArrayList"] = "List<T>",
                ["Lazy<*>"] = "",
                ["object"] = "конкретный тип",
            }));

            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "запрещенный тип 'ArrayList': List<T>",
                "запрещенный тип 'ArrayList': List<T>",
                "запрещенный тип 'Lazy<int>'",
                "запрещенный тип 'object': конкретный тип",
            }));
        }

#endregion


#region coroutine

        [Test]
        public void Coroutine_IteratorWithYield_Found()
        {
            const string source = """
                using System.Collections;
                using System.Collections.Generic;

                public class Sample
                {
                    private IEnumerator Fade()
                    {
                        yield return null;
                    }

                    private IEnumerator Wrap() => Fade();

                    private IEnumerable<int> Numbers()
                    {
                        yield return 1;
                    }

                    public IEnumerator GetEnumerator()
                    {
                        yield return 1;
                    }

                    private System.Collections.IEnumerator Nested()
                    {
                        IEnumerable<int> Local()
                        {
                            yield return 1;
                        }

                        return null;
                    }
                }
                """;

            var findings = Run("coroutine", source, settings => settings.Set("use", "асинхронный метод"));

            Assert.That(findings.Single().Message, Is.EqualTo("корутина 'Fade' (IEnumerator): асинхронный метод"));
        }

        [Test]
        public void Coroutine_ReturnTypesWithoutYield_Found()
        {
            const string source = """
                public class Sample
                {
                    private IEnumerator Wrap() => null;

                    private IEnumerable<int> Numbers()
                    {
                        yield return 1;
                    }
                }
                """;

            var findings = Run("coroutine", source, settings =>
            {
                settings.Set("returnTypes", new[] { "IEnumerator", "IEnumerable<*>" });
                settings.Set("requireYield", false);
            });

            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 3, 5 }));
        }

#endregion


#region static-state

        [Test]
        public void StaticState_MutableFieldsAndSingleton_Found()
        {
            const string source = """
                public class Registry
                {
                    public static Registry Instance { get; private set; }
                    private static int s_Count;
                    private static readonly List<int> Items = new();
                    private static readonly int[] Table = { 1 };
                    private static readonly string Name = "a";
                    private const int Max = 3;
                    public static bool IsReady { get; set; }
                    public static int Total => s_Count;
                    public static List<int> Pending { get; } = new();
                    public static event Action Changed;
                    private int m_Value;
                }
                """;

            var messages = Run("static-state", source, settings => settings.Set("use", "сервис"))
                .Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "синглтон 'Instance' типа Registry: сервис",
                "статическое изменяемое поле 's_Count': сервис",
                "статическое поле 'Items' изменяемого типа List<int>: сервис",
                "статическое поле 'Table' изменяемого типа int[]: сервис",
                "статическое свойство 'IsReady' с set: сервис",
                "статическое свойство 'Pending' изменяемого типа List<int>: сервис",
                "статическое событие 'Changed': сервис",
            }));
        }

        [Test]
        public void StaticState_IgnoredAttributesAndNames_NotFound()
        {
            const string source = """
                [Preserve]
                public class Settings
                {
                    private static int s_Count;
                }

                public class Cache
                {
                    [ThreadStatic]
                    private static int s_Depth;
                    private static readonly Cache Empty = new Cache();
                    private static int s_CachedHash;
                }
                """;

            var findings = Run("static-state", source, settings =>
            {
                settings.Set("ignoreAttributes", new[] { "Preserve", "ThreadStatic" });
                settings.Set("ignoreNames", new[] { "s_Cached*" });
                settings.Set("singleton", false);
            });

            Assert.That(findings, Is.Empty);
        }

#endregion


#region local-function

        [Test]
        public void LocalFunction_FoundOutsideExcludedPaths()
        {
            const string source = """
                public class Sample
                {
                    private int Method()
                    {
                        return Twice(2);

                        int Twice(int value) => value * 2;
                    }
                }
                """;

            void Exclude(CSR.Core.Checks.CheckSettings settings)
                => settings.Set("excludePaths", new[] { "*/Editor/*" });

            var runtime = Run("local-function", source, Exclude, "Assets/Code/Sample.cs");
            var editor = Run("local-function", source, Exclude, "Assets/Code/Editor/Sample.cs");
            var topEditor = Run("local-function", source, Exclude, "Editor/Sample.cs");

            Assert.That(runtime.Single().Message, Is.EqualTo("локальная функция 'Twice'"));
            Assert.That(runtime.Single().Line, Is.EqualTo(7));
            Assert.That(editor, Is.Empty);
            Assert.That(topEditor, Is.Empty);
        }

#endregion


#region path-concat

        [Test]
        public void PathConcat_FoundWithoutReplacementByDefault()
        {
            const string source = """
                public class Sample
                {
                    private string Method(string root, string file)
                    {
                        var a = root + "/" + file;
                        var b = root + "-" + file;
                        return a + b;
                    }
                }
                """;

            var finding = Run("path-concat", source).Single();

            Assert.That(finding.Message, Is.EqualTo("склейка пути через \"/\": Path.Combine"));
            Assert.That(finding.Line, Is.EqualTo(5));
            Assert.That(finding.Replacement, Is.Null);
        }

        [Test]
        public void PathConcat_Replace_ChainOnlyWhenUnambiguous()
        {
            const string source = """
                using System.IO;

                public class Sample
                {
                    private string m_Name;

                    private string Method(string root, string folder, string file, int index)
                    {
                        var a = root + "/" + folder + "/" + file;
                        var b = root + "/" + index;
                        var c = root + "/" + "/data";
                        var d = Path.GetFileName(root) + "/" + file;
                        var e = $"{root}" + "/" + m_Name + "/" + file.ToString();
                        return a + b + c + d + e;
                    }
                }
                """;

            var findings = Run("path-concat", source, settings => settings.Set("replace", true));

            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 9, 10, 11, 12, 13 }));
            Assert.That(findings[0].Replacement, Is.EqualTo(new[] { "        var a = Path.Combine(root, folder, file);" }));
            Assert.That(findings.Skip(1).Take(3).Select(finding => finding.Replacement), Is.All.Null);
            Assert.That(
                findings[4].Replacement,
                Is.EqualTo(new[] { "        var e = Path.Combine($\"{root}\", m_Name, file.ToString());" }));
        }

        [Test]
        public void PathConcat_Replace_NotInConstant()
        {
            const string source = """
                using System.IO;

                public class Sample
                {
                    private const string Root = "Build";
                    private const string Cache = Root + "/" + "cache.json";

                    [Obsolete(Root + "/" + "old")]
                    private string Method(string path = Root + "/" + "a") => path;
                }
                """;

            var findings = Run("path-concat", source, settings => settings.Set("replace", true));

            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 6, 8, 9 }));
            Assert.That(findings.Select(finding => finding.Replacement), Is.All.Null);
        }

        // Имя - строка, только если все его объявления в члене явно string; иначе поле типа
        [Test]
        public void PathConcat_Replace_OnlyProvenStrings()
        {
            const string source = """
                using System.IO;
                using System.Linq;

                public class Sample
                {
                    private string root;
                    private string file;
                    private string name;
                    private string folder;

                    private object Method(int[] ids)
                    {
                        var a = ids.Select(file => root + "/" + file);
                        var name = 5;
                        var b = root + "/" + name;
                        foreach (int item in ids)
                        {
                            var c = root + "/" + item;
                        }

                        var d = root + "/" + folder;
                        var e = root + "/" + file;
                        return Path.GetFullPath(d + e);
                    }
                }
                """;

            var findings = Run("path-concat", source, settings => settings.Set("replace", true));

            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 13, 15, 18, 21, 22 }));
            Assert.That(findings.Take(3).Select(finding => finding.Replacement), Is.All.Null);
            Assert.That(findings[3].Replacement, Is.EqualTo(new[] { "        var d = Path.Combine(root, folder);" }));
            Assert.That(findings[4].Replacement, Is.Null);
        }

        // Path в файле - не только класс System.IO.Path (Path.Length у свойства): имя с global::
        [Test]
        public void PathConcat_Replace_GlobalWhenPathIsMember()
        {
            const string source = """
                using System.IO;

                public class Sample : Base
                {
                    private string Method(string file) => Path.Length > 0 ? file + "/" + file : file;
                }
                """;

            var finding = Run("path-concat", source, settings => settings.Set("replace", true)).Single();

            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "    private string Method(string file) => Path.Length > 0 ? global::System.IO.Path.Combine(file, file) : file;",
            }));
        }

        [Test]
        public void PathConcat_Replace_QualifiedWithoutUsing()
        {
            const string source = """
                public class Sample
                {
                    public string Path;

                    private string Method(string file) => Path + "/" + file;
                }
                """;

            var finding = Run("path-concat", source, settings => settings.Set("replace", true)).Single();

            Assert.That(
                finding.Replacement,
                Is.EqualTo(new[] { "    private string Method(string file) => global::System.IO.Path.Combine(Path, file);" }));
        }

#endregion


#region bool-field-count

        [Test]
        public void BoolFieldCount_DerivedClassWithManyFlags_Found()
        {
            const string source = """
                public class Door : MonoBehaviour
                {
                    [SerializeField]
                    private bool m_IsOpen;
                    private bool m_IsLocked, m_IsBroken;
                    private static bool s_Any;
                    private int m_Count;
                }

                public class Panel : Singleton<Panel>
                {
                    private bool m_A;
                    private bool m_B;
                    private bool m_C;
                }

                public class Plain
                {
                    private bool m_A;
                    private bool m_B;
                    private bool m_C;
                }
                """;

            var finding = Run("bool-field-count", source).Single();
            var generic = Run("bool-field-count", source, settings => settings.Set("baseTypes", new[] { "Singleton" }));

            Assert.That(finding.Message, Is.EqualTo("полей bool в 'Door': 3 (m_IsOpen, m_IsLocked, m_IsBroken)"));
            Assert.That(finding.Line, Is.EqualTo(1));
            Assert.That(generic.Single().Message, Does.StartWith("полей bool в 'Panel'"));
        }

#endregion


#region type-per-file

        [Test]
        public void TypePerFile_DerivedTypeWithOthers_FoundWithMoveCommand()
        {
            const string source = """
                namespace Game.Units
                {
                    public class Door : MonoBehaviour
                    {
                    }

                    public struct DoorState
                    {
                    }

                    public enum DoorKind
                    {
                        Wood,
                    }
                }
                """;

            var messages = Run("type-per-file", source, path: "Assets/Door.cs").Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "тип 'DoorState' не один в файле Door.cs: csr move Game.Units.DoorState",
                "тип 'DoorKind' не один в файле Door.cs: csr move Game.Units.DoorKind",
            }));
        }

        [Test]
        public void TypePerFile_NoDerivedTypeOrSingleType_NotFound()
        {
            const string plain = """
                namespace Game;

                public class Helper
                {
                    private class Nested
                    {
                    }
                }

                public struct HelperState
                {
                }
                """;
            const string single = "public class Door : MonoBehaviour { private class Nested { } }";

            const string enums = "public enum Kind { A }\npublic enum Mode { B }\n";

            Assert.That(Run("type-per-file", plain, path: "Helper.cs"), Is.Empty);
            Assert.That(
                Run("type-per-file", enums, settings => settings.Set("baseTypes", Array.Empty<string>()), "Kind.cs")
                    .Single().Message,
                Is.EqualTo("тип 'Mode' не один в файле Kind.cs: csr move Mode"));
            Assert.That(Run("type-per-file", single, path: "Door.cs"), Is.Empty);
            Assert.That(
                Run("type-per-file", plain, settings => settings.Set("baseTypes", Array.Empty<string>()), "Helper.cs")
                    .Single().Message,
                Is.EqualTo("тип 'HelperState' не один в файле Helper.cs: csr move Game.HelperState"));
        }

#endregion


#region closure-in-method

        [Test]
        public void ClosureInMethod_CapturedLocalsInUpdate_Found()
        {
            const string source = """
                public class Sample
                {
                    private void Update()
                    {
                        var limit = 3;
                        m_Items.RemoveAll(item => item.Value > limit);
                        m_Items.ForEach(item => item.Tick());
                        m_Items.Sort(static (a, b) => a.Value - b.Value);
                        Run(() => this.Stop());
                        const int Max = 5;
                        Run(() => Log(nameof(limit), Max));
                        Run(() => Run(() => Use(limit)));
                    }

                    private void Start()
                    {
                        var limit = 3;
                        m_Items.RemoveAll(item => item.Value > limit);
                    }
                }
                """;

            var plain = Run("closure-in-method", source);
            var withThis = Run("closure-in-method", source, settings => settings.Set("captureThis", true));

            Assert.That(plain.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "лямбда в 'Update' захватывает limit",
                "лямбда в 'Update' захватывает limit",
            }));
            Assert.That(plain.Select(finding => finding.Line), Is.EqualTo(new[] { 6, 12 }));
            Assert.That(withThis.Select(finding => finding.Line), Is.EqualTo(new[] { 6, 9, 12 }));
        }

#endregion


#region nested-loop

        [Test]
        public void NestedLoop_IndependentCollections_Found()
        {
            const string source = """
                public class Sample
                {
                    private void Method()
                    {
                        foreach (var unit in m_Units)
                        {
                            foreach (var other in m_Units)
                                Touch(unit, other);

                            foreach (var part in unit.Parts)
                                Touch(unit, part);

                            var cells = GetCells(unit);
                            for (var index = 0; index < cells.Count; index++)
                                Touch(unit, cells[index]);
                        }

                        for (var row = 0; row < m_Rows.Count; row++)
                        {
                            for (var column = 0; column < m_Columns.Length; column++)
                                Touch(row, column);
                        }

                        foreach (var unit in m_Units)
                            Run(() => { foreach (var other in m_Units) Touch(unit, other); });

                        while (m_Queue.Count > 0)
                        {
                            foreach (var direction in m_Directions)
                                Touch(m_Queue.Dequeue(), direction);
                        }

                        foreach (var unit in m_Units)
                        {
                            foreach (var offset in s_Offsets)
                                Touch(unit, offset);
                        }
                    }
                }
                """;

            var findings = Run("nested-loop", source, settings => settings.Set("ignoreCollections", new[] { "s_*" }));

            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "вложенный цикл по 'm_Units' внутри цикла по 'm_Units': O(N²)",
                "вложенный цикл по 'm_Columns' внутри цикла for: O(N²)",
            }));
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 7, 20 }));
        }

#endregion
    }
}
