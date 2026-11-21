using static CSR.Core.Tests.Arch.ArchTestCode;

namespace CSR.Core.Tests.Arch
{
    public class ApiChecksTests
    {
#region index-api

        [Test]
        public void IndexApi_IndexParametersAndMarker_Found()
        {
            const string source = """
                public class Grid
                {
                    public const int NoNode = -1;

                    public bool TryFindPath(int node, bool isFast, out int area, out int cell)
                    {
                        area = 0;
                        cell = 0;
                        return true;
                    }

                    internal int FindNearestCell(float x)
                    {
                        return -1;
                    }

                    public int CellIndex { get; }
                }
                """;
            var findings = Run("index-api", source);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 3, 5, 12, 17 }));
            Assert.That(findings[0].Message, Is.EqualTo("NoNode: маркер NoNode = -1"));
            Assert.That(
                findings[1].Message,
                Is.EqualTo("TryFindPath: параметр int node; параметр int area; параметр int cell; out целых: 2"));
            Assert.That(findings[2].Message, Is.EqualTo("FindNearestCell: результат int FindNearestCell; return -1"));
            Assert.That(findings[3].Message, Is.EqualTo("CellIndex: свойство int CellIndex"));
        }

        [Test]
        public void IndexApi_PrivateOverrideAndOtherNames_NoFinding()
        {
            const string source = """
                public class Grid : Base
                {
                    private const int NoNode = -1;

                    public int NodeCount { get; }

                    public Vector3 GetPoint(int index) => default;

                    public override int GetCell(int cell) => cell;

                    private int FindCell(int cell) => -1;
                }
                """;
            Assert.That(Run("index-api", source), Is.Empty);
        }

        [Test]
        public void IndexApi_Options_NamesAndOutInts()
        {
            const string source = """
                public interface IPath
                {
                    void Split(int segment, out int first, out int second);
                }
                """;
            Assert.That(Run("index-api", source, settings => settings.Set("minOutInts", 0)), Is.Empty);
            var finding = Run("index-api", source, settings => settings.Set("names", new[] { "segment" })).Single();
            Assert.That(finding.Message, Is.EqualTo("Split: параметр int segment; out целых: 2"));
        }

#endregion


#region static-data-methods

        [Test]
        public void StaticData_StaticClassSameFirstType_Found()
        {
            const string source = """
                public static class SurfaceRouter
                {
                    public static bool TryFindRoute(in Surface surface, int from, int to) => true;
                    public static float GetLength(Surface surface) => 0;
                    public static void Draw(this Surface surface) { }
                    public static float Clamp(float value) => value;
                }
                """;
            var finding = Run("static-data-methods", source).Single();
            Assert.That(finding.Line, Is.EqualTo(1));
            const string message = "static class 'SurfaceRouter': 3 из 4 методов принимают первым 'Surface': "
                + "TryFindRoute, GetLength, Draw";
            Assert.That(finding.Message, Is.EqualTo(message));
        }

        [Test]
        public void StaticData_StaticMethodsInClass_Found()
        {
            const string source = """
                public class Builder
                {
                    public static Vector3 GetCellPoint(in Surface surface, int cell) => default;
                    public static Vector3 GetBlockPoint(Surface surface, int block) => default;
                    internal static Vector3 GetNormal(Surface surface, int block) => default;
                    private static Vector3 GetEdge(Surface surface, int block) => default;
                    public static Builder Create(Builder source) => source;
                }
                """;
            var finding = Run("static-data-methods", source).Single();
            Assert.That(finding.Message, Does.StartWith("статические методы 'Builder': 3 из 4 методов"));
            var staticOnly = Run("static-data-methods", source, settings => settings.Set("staticClassesOnly", true));
            Assert.That(staticOnly, Is.Empty);
        }

        [Test]
        public void StaticData_MixedDataTypes_Found()
        {
            const string source = """
                public static class Helper
                {
                    public static bool IsAgile(Entity entity) => true;
                    public static bool IsSprinting(Entity entity) => true;
                    public static bool HasEnter(Route route) => true;
                    public static bool IsInZone(Route route, Entity entity) => true;
                    public static bool CanFall(Unit unit) => true;
                }
                """;
            var finding = Run("static-data-methods", source).Single();
            const string message = "static class 'Helper': 5 из 5 методов принимают данные первым параметром, "
                + "типов 3: Entity 2, Route 2, Unit 1";
            Assert.That(finding.Message, Is.EqualTo(message));
            Assert.That(Run("static-data-methods", source, settings => settings.Set("minDataShare", 0)), Is.Empty);
        }

        [Test]
        public void StaticData_LibraryTypesAndMath_NoFinding()
        {
            const string source = """
                public static class VectorExtensions
                {
                    public static Vector3 Flat(this Vector3 v) => v;
                    public static Vector3 Up(this Vector3 v) => v;
                    public static Vector3 Down(Vector3 v) => v;
                    public static T First<T>(T a, T b) => a;
                    public static T Second<T>(T a, T b) => b;
                    public static T Third<T>(T a, T b) => b;
                    public static float Lerp(float a, float b, float t) => a;
                }

                public static class Routes
                {
                    public static bool A(Surface surface) => true;
                    public static bool B(Surface surface) => true;
                    public static bool C(Surface surface) => true;
                    public static bool D(int x) => true;
                    public static bool E(int x) => true;
                    public static bool F(int x) => true;
                }
                """;
            Assert.That(Run("static-data-methods", source), Is.Empty);
        }

#endregion


#region tuple-type

        [Test]
        public void TupleType_Signature_FoundWithArchCheck()
        {
            var source = """
                public class Sample
                {
                    public (float angle, bool isBlocked) CalculateDirection(float lastAngle)
                    {
                        var pair = (1, 2);
                        return default;
                    }
                }
                """;
            var finding = Run("tuple-type", source).Single();
            Assert.That(finding.Check, Is.EqualTo("tuple-type"));
            Assert.That(finding.Line, Is.EqualTo(3));
        }

#endregion
    }
}
