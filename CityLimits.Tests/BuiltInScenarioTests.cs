using CityLimits.Core.Scenarios;

namespace CityLimits.Tests;

public class BuiltInScenarioTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Scenarios");

    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var id in new ScenarioCatalog(Folder).ListIds(out _))
        {
            data.Add(id);
        }

        return data;
    }

    [Fact]
    public void TheBuiltInScenarioFilesAreFound() =>
        Assert.True(new ScenarioCatalog(Folder).ListIds(out _).Count > 1, $"No scenario files were found in {Folder}.");

    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryBuiltInScenarioPasses(string id)
    {
        Assert.True(new ScenarioCatalog(Folder).TryLoad(id, out var scenario, out var error), error);

        var report = new ScenarioRunner().Run(scenario, id);

        var failures = report.Checks.Where(c => !c.Passed).Select(c => $"check {c.Index}: expected {c.Expected}, found {c.Actual}")
            .Concat(report.Actions.Where(a => a.Met == false).Select(a => $"action {a.Index}: {a.Status} {a.Reason}"));
        Assert.True(report.Passed, string.Join("; ", failures));
    }
}
