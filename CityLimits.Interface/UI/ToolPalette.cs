namespace CityLimits.UI;

/// <summary>The tools the player can pick from, and the one that is held.</summary>
public sealed class ToolPalette
{
    private static readonly Tool[] AllTools =
    [
        Tool.Inspect,
        Tool.Road,
        Tool.Residential,
        Tool.Commercial,
        Tool.Industrial,
        Tool.Bulldozer,
        Tool.CoalPowerPlant,
        Tool.NuclearPowerPlant,
        Tool.PowerLine,
        Tool.FireStation,
        Tool.PoliceStation,
        Tool.Park,
        Tool.Rail,
        Tool.Stadium,
        Tool.Seaport,
        Tool.Airport,
    ];

    /// <summary>The tool that is held, or null when none is, as when the game starts.</summary>
    public Tool? Selected { get; private set; }

    /// <summary>The tools in the palette, in the order they are shown.</summary>
    public IReadOnlyList<Tool> Tools => AllTools;

    /// <summary>
    /// Works a tool's button as a switch, as Tony Bark chose: picks the tool up, or puts it down when
    /// it is already held, leaving no tool held. Tells whether the tool is one the palette has.
    /// </summary>
    public bool Toggle(Tool tool)
    {
        if (!AllTools.Contains(tool))
        {
            return false;
        }

        Selected = Selected == tool ? null : tool;
        return true;
    }
}
