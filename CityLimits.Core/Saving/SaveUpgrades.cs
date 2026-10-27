using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// Brings a save from an older minor version up to the current one, a version at a time, each step
/// filling in what its version added, so a city saved by an older CityLimits loads as if it had been
/// saved by this one. Every format reads through here, before the city is restored.
/// </summary>
internal static class SaveUpgrades
{
    /// <summary>The save model's version, which both save formats write.</summary>
    public static readonly FormatVersion Current = new(1, 10);

    /// <summary>Upgrades a save in place, from whatever older 1.x version it is.</summary>
    public static void ToCurrent(CitySave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Format < new FormatVersion(1, 1))
        {
            AddTraffic(save);
        }

        // 1.2 added the parts list, which a save from before simply has no newer parts in.
        if (save.Format < new FormatVersion(1, 3))
        {
            AddRoadTraffic(save);
        }

        if (save.Format < new FormatVersion(1, 4))
        {
            AddRail(save);
        }

        if (save.Format < new FormatVersion(1, 5))
        {
            AddPollutionAndCrime(save);
        }

        if (save.Format < new FormatVersion(1, 6))
        {
            AddFire(save);
        }

        if (save.Format < new FormatVersion(1, 7))
        {
            AddFlood(save);
        }

        // 1.8 added the challenge being played, which a city from before simply isn't.
        if (save.Format < new FormatVersion(1, 9))
        {
            AddFacilities(save);
        }

        if (save.Format < new FormatVersion(1, 10))
        {
            AddFounding(save);
        }

        save.Format = Current;
    }

    // 1.1 added traffic. A city from before has quiet roads, which its trips fill again. A map of a
    // size no save can have is left for the map's own check to refuse.
    private static void AddTraffic(CitySave save)
    {
        if (save.Map is { Width: >= 1 and <= MapCodec.MaxSide, Height: >= 1 and <= MapCodec.MaxSide } map)
        {
            save.Traffic ??= new TrafficSave { Density = new int[TrafficMap.BlocksFor(map.Width, map.Height)] };
        }
    }

    // 1.4 added rail. A city from before has none, and the scan counts none until it is laid.
    private static void AddRail(CitySave save)
    {
        if (save.Map is { Width: >= 1 and <= MapCodec.MaxSide, Height: >= 1 and <= MapCodec.MaxSide } map)
        {
            save.Rail ??= new RailSave { Track = [.. Enumerable.Repeat(new string('.', map.Width), map.Height)], Tiles = 0, BudgetedTiles = 0 };
        }
    }

    // 1.9 added stadiums, seaports, and airports. A city from before has none, and no demand held
    // back for want of one until the city next checks.
    private static void AddFacilities(CitySave save) =>
        save.Facilities ??= new FacilitiesSave
        {
            Stadiums = 0,
            Seaports = 0,
            Airports = 0,
            ResidentialCapped = false,
            CommercialCapped = false,
            IndustrialCapped = false,
            Buildings = [],
        };

    // 1.10 added the year the city was founded. A city from before is counted from 1900, as it was;
    // one that started later can't be told apart from one that has run since.
    private static void AddFounding(CitySave save) =>
        save.Founding ??= new FoundingSave { Year = CityCalendar.StartYear };

    // 1.7 added floods. A city from before has none.
    private static void AddFlood(CitySave save)
    {
        if (save.Map is { Width: >= 1 and <= MapCodec.MaxSide, Height: >= 1 and <= MapCodec.MaxSide } map)
        {
            save.Flood ??= new FloodSave { Tiles = [.. Enumerable.Repeat(new string('.', map.Width), map.Height)], CyclesLeft = 0 };
        }
    }

    // 1.6 added fire. A city from before has none burning, and its buildings are whole.
    private static void AddFire(CitySave save)
    {
        if (save.Map is { Width: >= 1 and <= MapCodec.MaxSide, Height: >= 1 and <= MapCodec.MaxSide } map)
        {
            save.Fire ??= new FireSave { Tiles = [.. Enumerable.Repeat(new string('.', map.Width), map.Height)] };
        }
    }

    // 1.5 added pollution and crime. A city from before has none, which phases 12 and 13 work out
    // within a cycle; until then land value is as it was, without them.
    private static void AddPollutionAndCrime(CitySave save)
    {
        if (save.Map is { Width: >= 1 and <= MapCodec.MaxSide, Height: >= 1 and <= MapCodec.MaxSide } map)
        {
            var blocks = CityMaps.BlocksFor(map.Width, map.Height);
            save.Pollution ??= new BlockMapSave { Blocks = new int[blocks], Average = 0 };
            save.Crime ??= new BlockMapSave { Blocks = new int[blocks], Average = 0 };
        }
    }

    // 1.3 added the traffic each road shows and the year's average. A city from before shows quiet
    // roads, which the scan sets from its traffic within a cycle, and averages nothing until the year ends.
    private static void AddRoadTraffic(CitySave save)
    {
        if (save.Map is { Width: >= 1 and <= MapCodec.MaxSide, Height: >= 1 and <= MapCodec.MaxSide } map)
        {
            save.RoadTraffic ??= new RoadTrafficSave { Levels = [.. Enumerable.Repeat(new string('0', map.Width), map.Height)], Average = 0 };
        }
    }
}
