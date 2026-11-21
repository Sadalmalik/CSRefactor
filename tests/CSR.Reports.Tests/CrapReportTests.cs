using System.Text.Json;
using CSR.Core.Config;
using CSR.Core.Metrics;
using CSR.Reports.Metrics;

namespace CSR.Reports.Tests
{
    public class CrapReportTests
    {
        private const string Source = """
            namespace Space
            {
                class Sample
                {
                    int Branchy(bool a, bool b)
                    {
                        if (a)
                            return 1;
                        if (b && a)
                            return 2;
                        return 3;
                    }

                    void Simple() { }
                }
            }
            """;

        private static string CoverageXml(string path)
        {
            return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <CoverageSession>
                  <Modules>
                    <Module>
                      <Files>
                        <File uid="1" fullPath="{path}" />
                      </Files>
                      <Classes>
                        <Class>
                          <Methods>
                            <Method>
                              <SequencePoints>
                                <SequencePoint vc="1" sl="7" fileid="1" />
                                <SequencePoint vc="1" sl="8" fileid="1" />
                                <SequencePoint vc="1" sl="9" fileid="1" />
                                <SequencePoint vc="0" sl="10" fileid="1" />
                                <SequencePoint vc="3" sl="14" fileid="1" />
                              </SequencePoints>
                            </Method>
                          </Methods>
                        </Class>
                      </Classes>
                    </Module>
                  </Modules>
                </CoverageSession>
                """;
        }

        private static FileMetrics Measure()
            => MetricsCalculator.Calculate(Source, "Sample.cs", new ProjectOptions().ParseOptions);

        [Test]
        public void OpenCover_ReadsSequencePointsByFile()
        {
            var path = Path.GetFullPath("Sample.cs");
            var coverage = OpenCoverReader.Read(CoverageXml(path));
            var file = coverage[OpenCoverReader.NormalizePath(path)];

            Assert.That(file.GetPercent(5, 12), Is.EqualTo(75.0));
            Assert.That(file.GetPercent(14, 14), Is.EqualTo(100.0));
        }

        [Test]
        public void Edn_CrapperFormatAndOrder()
        {
            var path = Path.GetFullPath("Sample.cs");
            var metrics = Measure();
            Crap.Apply(metrics, OpenCoverReader.Read(CoverageXml(path))[OpenCoverReader.NormalizePath(path)]);

            var edn = CrapEdnWriter.Write([metrics]);
            Assert.That(edn, Is.EqualTo(
                "{:entries [\n"
                + "  {:name \"Branchy\", :namespace \"Space.Sample\", :complexity 4, :coverage 75.0, :crap 4.25}\n"
                + "  {:name \"Simple\", :namespace \"Space.Sample\", :complexity 1, :coverage 100.0, :crap 1.0}\n"
                + "]}\n"));
        }

        [Test]
        public void Edn_WithoutCoverage_NilAndComplexityOrder()
        {
            var edn = CrapEdnWriter.Write([Measure()]);
            Assert.That(edn, Does.StartWith(
                "{:entries [\n  {:name \"Branchy\", :namespace \"Space.Sample\", :complexity 4, :coverage nil, :crap nil}"));
            Assert.That(CrapEdnWriter.Write([]), Is.EqualTo("{:entries []}\n"));
        }

        [Test]
        public void Reports_CrapTopInJsonAndMarkdown()
        {
            var metrics = Measure();
            Crap.Apply(metrics, null);
            var report = MetricsReport.Build([metrics], new MetricsOptions());

            using var document = JsonDocument.Parse(MetricsJsonWriter.Write(report, includeItems: true));
            var first = document.RootElement.GetProperty("crap")[0];
            Assert.That(first.GetProperty("name").GetString(), Is.EqualTo("Sample.Branchy"));
            Assert.That(first.GetProperty("value").GetDouble(), Is.EqualTo(20.0));

            var markdown = MetricsMarkdownWriter.Write(report);
            Assert.That(markdown, Does.Contain("## CRAP"));
            Assert.That(markdown, Does.Contain("| 20 | 0 | 4 | `Sample.Branchy` | `Sample.cs:5` |"));
        }
    }
}
