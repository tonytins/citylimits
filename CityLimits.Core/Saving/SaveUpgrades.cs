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
    public static readonly FormatVersion Current = new(1, 1);

    /// <summary>Upgrades a save in place, from whatever older 1.x version it is.</summary>
    public static void ToCurrent(CitySave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Format < new FormatVersion(1, 1))
        {
            AddTraffic(save);
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
}
