using CityLimits.Core.Saving;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class ScenarioWriterTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Scenarios");

    public static TheoryData<string> Ids() => BuiltInScenarioTests.Ids();

    private static Scenario Read(string json, string? folder = null)
    {
        Assert.True(ScenarioLoader.TryParse(json, folder, out var scenario, out var error), error);
        return scenario;
    }

    // What a run of a scenario comes to, which two scenarios that are the same share.
    private static string Outcome(Scenario scenario)
    {
        var report = new ScenarioRunner().Run(scenario, "test");
        return ScenarioReportJson.Serialize(report with { Scenario = report.Scenario with { Source = "test" } });
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryBuiltInScenarioWritesAndReadsBackTheSame(string id)
    {
        Assert.True(new ScenarioCatalog(Folder).TryLoad(id, out var scenario, out var error), error);

        var written = ScenarioWriter.Write(scenario, Folder);
        var read = Read(written, Folder);

        // Written again, it reads the same, and it runs to the same end.
        Assert.Equal(written, ScenarioWriter.Write(read, Folder));
        Assert.Equal((scenario.Name, scenario.Seed, scenario.Ticks, scenario.StartStep), (read.Name, read.Seed, read.Ticks, read.StartStep));
        Assert.Equal(Outcome(scenario), Outcome(read));
    }

    [Fact]
    public void AFileIsWrittenAsTheNewestFormatWithOnlyWhatIsntGiven()
    {
        var scenario = Read("""
            { "format": "2.0", "name": "W", "seed": 4, "map": { "width": 32, "height": 32 }, "start": 1950, "ends": "1952-01",
              "goals": [ { "measure": "Population", "atLeast": 500 }, { "measure": "Funds", "atLeast": 0, "text": "Stay out of debt" } ],
              "events": [ { "at": "1950-03", "week": 2, "disaster": "Fire", "repeat": { "weeks": 4, "times": 3 } } ] }
            """);

        var written = ScenarioWriter.Write(scenario, Folder);

        Assert.Contains("\"format\": \"3.0\"", written);
        Assert.Contains("scenario.v3.0.schema.json", written);
        Assert.Contains("\"start\": 1950", written);
        Assert.Contains("\"week\": 2", written);
        Assert.Contains("\"text\": \"Stay out of debt\"", written);
        Assert.DoesNotContain("Have a population", written);
        Assert.DoesNotContain("disasters", written);
        Assert.Equal(new EventRepeat(4, 3), Read(written).Events[0].Repeat);
    }

    [Fact]
    public void ASavedCityIsNamedFromTheScenariosFolder()
    {
        var folder = Directory.CreateTempSubdirectory("citylimits-writer").FullName;
        try
        {
            var city = new CitySimulation(Core.World.TileMap.FromTerrain(20, 20, new Core.World.TileKind[400]), 3, startYear: 1930);
            Assert.True(CityFiles.TryWrite(city, Path.Combine(folder, "town.fcl"), out var problem), problem);
            var scenario = Read("""{ "format": "2.0", "name": "C", "city": "town.fcl", "ends": "1935-01" }""", folder);

            var written = ScenarioWriter.Write(scenario, folder);

            Assert.Contains("\"city\": \"town.fcl\"", written);
            Assert.DoesNotContain("\"seed\"", written);
            Assert.Equal(scenario.CityFile, Read(written, folder).CityFile);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ATimeBetweenTheCalendarsWeeksIsntWritten()
    {
        // Format 1 counts in ticks, which needn't fall on a week.
        var scenario = Read("""
            { "format": "1.3", "name": "T", "seed": 1, "map": { "width": 16, "height": 16 }, "ticks": 64,
              "actions": [ { "tick": 5, "tool": "Road", "from": { "x": 1, "y": 1 } } ] }
            """);

        var problem = Assert.Throws<InvalidDataException>(() => ScenarioWriter.Write(scenario, Folder));
        Assert.Equal("actions[0]: tick 5 falls between the calendar's weeks, so it can't be written as a date.", problem.Message);
    }

    [Fact]
    public void AFileFor04Is22WithTheSameFields()
    {
        var scenario = Read("""
            { "format": "3.0", "name": "W", "seed": 4, "map": { "width": 32, "height": 32 }, "ends": "1902-01",
              "events": [ { "at": "1900-03", "disaster": "Fire", "repeat": { "weeks": 4, "times": 3 } } ] }
            """);

        var written = ScenarioWriter.Write(scenario, Folder, legacy: true);

        Assert.Contains("\"format\": \"2.2\"", written);
        Assert.Contains("scenario.v2.2.schema.json", written);
        Assert.Equal(ScenarioWriter.Write(scenario, Folder).Replace("3.0", "2.2"), written);
    }
}
