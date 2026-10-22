using System.Text.Json.Nodes;
using CityLimits.Core.News;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class NewsQueueTests
{
    private static NewsItem Headline(string text) => new(text, NewsKind.Headline);

    private static NewsItem Advisory(string text) => new(text, NewsKind.Advisory);

    private static List<NewsItem> Drain(NewsQueue queue)
    {
        var items = new List<NewsItem>();
        while (queue.TryNext(out var item))
        {
            items.Add(item);
        }

        return items;
    }

    [Fact]
    public void DisastersGoFirstThenAdvisoriesThenHeadlinesEachInTheOrderPosted()
    {
        var queue = new NewsQueue();
        queue.Post(Headline("h1"));
        queue.Post(Advisory("a1"));
        queue.Post(Headline("h2"));
        queue.Post(new NewsItem("d1", NewsKind.Disaster));
        queue.Post(Advisory("a2"));

        Assert.Equal(["d1", "a1", "a2", "h1", "h2"], Drain(queue).Select(item => item.Text));
    }

    [Fact]
    public void ALineAlreadyWaitingIsntQueuedTwice()
    {
        var queue = new NewsQueue();
        queue.Post(Advisory("Autosave failed"));
        queue.Post(Advisory("Autosave failed"));
        queue.Post(Headline("Autosave failed"));

        Assert.Equal(2, queue.Count);
        queue.TryNext(out _);
        queue.Post(Advisory("Autosave failed"));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void AFullQueueDropsItsOldestHeadlineToMakeRoom()
    {
        var queue = new NewsQueue();
        queue.Post(Headline("old"));
        for (var i = 1; i < NewsQueue.Capacity; i++)
        {
            queue.Post(Advisory($"a{i}"));
        }

        queue.Post(Advisory("new"));

        var items = Drain(queue);
        Assert.Equal(NewsQueue.Capacity, items.Count);
        Assert.DoesNotContain(items, item => item.Text == "old");
        Assert.Equal("new", items[^1].Text);
    }

    [Fact]
    public void AFullQueueOfAdvisoriesDropsTheOldest()
    {
        var queue = new NewsQueue();
        for (var i = 0; i <= NewsQueue.Capacity; i++)
        {
            queue.Post(Advisory($"a{i}"));
        }

        var items = Drain(queue);
        Assert.Equal("a1", items[0].Text);
        Assert.Equal($"a{NewsQueue.Capacity}", items[^1].Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ALineWithNoTextIsRefused(string text) =>
        Assert.Throws<ArgumentException>(() => new NewsQueue().Post(Advisory(text)));

    [Fact]
    public void AnEmptyQueueHasNothingNext() => Assert.False(new NewsQueue().TryNext(out _));
}

public class NewsDeskTests
{
    // A city named Testville with the given population, from a save with its census changed, at a
    // count of the given month.
    private static CitySimulation City(int population = 0, int month = 3, int funds = 20_000)
    {
        var city = new CitySimulation(TileMap.FromTerrain(10, 8, new TileKind[80]), 3);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["name"] = "Testville";
        save["census"]!["residentPopulation"] = population;
        save["funds"] = funds;
        var cityTime = (month - 1) * CityCalendar.CountsPerMonth;
        save["cityTime"] = cityTime;
        save["tick"] = cityTime * CityCalendar.StepsPerCount;
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var loaded, out var problem), problem);
        return loaded;
    }

    [Fact]
    public void EveryHeadlineIsAHeadlineWithTheCitysNameFilledIn()
    {
        var desk = new NewsDesk(City(), new Random(1));

        for (var i = 0; i < 200; i++)
        {
            var item = desk.Next();
            Assert.Equal(NewsKind.Headline, item.Kind);
            Assert.False(string.IsNullOrWhiteSpace(item.Text));
            Assert.DoesNotContain("{", item.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryFlavourLineComesOnceBeforeAnyComesAgain()
    {
        // A city in no state worth news, so every line is flavour: in March, with people, at the starting tax and funds.
        var desk = new NewsDesk(City(population: 100), new Random(2));
        var seen = new List<string>();
        while (seen.Count < Headlines.Flavour.Length)
        {
            var text = desk.Next().Text;
            if (Headlines.Flavour.Select(line => line.Replace("{city}", "Testville", StringComparison.Ordinal)).Contains(text))
            {
                seen.Add(text);
            }
        }

        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public void AMilestoneTheCityHadAlreadyPassedIsntNews()
    {
        var desk = new NewsDesk(City(population: 1_500), new Random(3));

        Assert.All(Enumerable.Range(0, 50), _ => Assert.DoesNotContain("Tops", desk.Next().Text, StringComparison.Ordinal));
    }

    [Fact]
    public void ACityThatGrowsPastAMilestoneHearsOfItOnce()
    {
        // A desk made for an empty city, which then has 2,500 people by its next census.
        var city = new CitySimulation(TileMap.FromTerrain(20, 9, new TileKind[180]), 1, funds: 100_000);
        city.Rename("Testville");
        var desk = new NewsDesk(city, new Random(4));
        for (var x = 2; x <= 14; x += 3)
        {
            city.Map.SetState(x, 4, new TileState(TileKind.Grass, TileUse.Residential, development: 5, valueClass: 3, powered: true));
        }

        while (city.Census.CityPopulation < 500)
        {
            city.Tick();
        }

        var passed = Headlines.PopulationMilestones.Last(milestone => city.Census.CityPopulation >= milestone);
        var first = desk.Next().Text;
        Assert.Equal($"Testville Tops {passed:N0} People; Welcome Signs Updated", first);
        Assert.All(Enumerable.Range(0, 50), _ => Assert.DoesNotContain("Tops", desk.Next().Text, StringComparison.Ordinal));
    }

    [Fact]
    public void ANewYearIsNewsInJanuaryButNotTheYearTheDeskStartedIn()
    {
        Assert.All(Enumerable.Range(0, 30), _ => Assert.DoesNotContain("Happy New Year", new NewsDesk(City(month: 1), new Random(5)).Next().Text, StringComparison.Ordinal));

        var city = City(month: 12);
        var desk = new NewsDesk(city, new Random(5));
        while (city.Date.Month != 1)
        {
            city.Tick();
        }

        Assert.Equal("Happy New Year From Testville: Welcome To 1901", desk.Next().Text);
        Assert.All(Enumerable.Range(0, 30), _ => Assert.DoesNotContain("Happy New Year", desk.Next().Text, StringComparison.Ordinal));
    }

    [Fact]
    public void ReactiveLinesComeOnlyWhileTheCityIsInTheirState()
    {
        var broke = Headlines.Reactive.Single(rule => rule.Lines.Any(line => line.Contains("Treasury Runs Dry", StringComparison.Ordinal)));
        var brokeLines = broke.Lines.Select(line => line.Replace("{city}", "Testville", StringComparison.Ordinal)).ToHashSet();

        var solvent = new NewsDesk(City(population: 100), new Random(6));
        Assert.All(Enumerable.Range(0, 200), _ => Assert.DoesNotContain(solvent.Next().Text, brokeLines));

        var inDebt = new NewsDesk(City(population: 100, funds: -500), new Random(6));
        var texts = Enumerable.Range(0, 200).Select(_ => inDebt.Next().Text).ToList();
        Assert.Contains(texts, brokeLines.Contains);

        // Once shown, a reactive line rests before it can come again.
        var indices = texts.Select((text, index) => (text, index)).Where(pair => brokeLines.Contains(pair.text)).Select(pair => pair.index).ToList();
        Assert.All(indices.Zip(indices.Skip(1)), pair => Assert.True(pair.Second - pair.First >= 12, $"shown at {pair.First} and again at {pair.Second}"));
    }

    [Fact]
    public void TheNewsNeverChangesTheCity()
    {
        var city = City(population: 3_000, funds: -10);
        var before = FlatCityFormat.Write(city);
        var desk = new NewsDesk(city, new Random(7));

        for (var i = 0; i < 300; i++)
        {
            desk.Next();
        }

        Assert.Equal(before, FlatCityFormat.Write(city));
    }

    [Fact]
    public void TheHeadlinesAreDistinctAndUseOnlyTheirPlaceholders()
    {
        var all = Headlines.Flavour.Concat(Headlines.Reactive.SelectMany(rule => rule.Lines)).Append(Headlines.NewYear).Append(Headlines.PopulationMilestone).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.All(all, line =>
        {
            var stripped = line.Replace("{city}", "", StringComparison.Ordinal).Replace("{year}", "", StringComparison.Ordinal).Replace("{count}", "", StringComparison.Ordinal);
            Assert.DoesNotContain("{", stripped, StringComparison.Ordinal);
            Assert.DoesNotContain("}", stripped, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheLoadingPhrasesStartWithTheNodAndDontRepeat()
    {
        Assert.Equal("Reticulating splines", LoadingPhrases.All[0]);
        Assert.Equal(LoadingPhrases.All.Length, LoadingPhrases.All.Distinct().Count());
    }
}
