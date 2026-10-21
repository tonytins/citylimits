namespace CityLimits.Game;

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
    ];

    /// <summary>The tool that is held when the game starts.</summary>
    public Tool Selected { get; private set; } = Tool.Inspect;

    /// <summary>The tools in the palette, in the order they are shown.</summary>
    public IReadOnlyList<Tool> Tools => AllTools;

    /// <summary>Selects a tool and tells whether it is one the player holds.</summary>
    public bool Select(Tool tool)
    {
        if (!AllTools.Contains(tool))
        {
            return false;
        }

        Selected = tool;
        return true;
    }

    /// <summary>Selects the next tool in the palette.</summary>
    public void SelectNext() => Selected = AllTools[(Array.IndexOf(AllTools, Selected) + 1) % AllTools.Length];

    /// <summary>Selects the previous tool in the palette.</summary>
    public void SelectPrevious()
    {
        var index = Array.IndexOf(AllTools, Selected) - 1;
        Selected = AllTools[(index + AllTools.Length) % AllTools.Length];
    }
}
