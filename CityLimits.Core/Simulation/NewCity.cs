using System.Globalization;

namespace CityLimits.Core.Simulation;

/// <summary>
/// What the player asks for when starting a city: its name, and the seed its map grows from, or
/// none, when the game picks one.
/// </summary>
public sealed record NewCity
{
    /// <summary>The city's name, trimmed.</summary>
    public required string Name { get; init; }

    /// <summary>The seed the player gave, or null to let the game pick one.</summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Reads the New Game form: a name a city can have (<see cref="CitySimulation.IsValidName"/>),
    /// and a seed that is blank or a whole number. Says what is wrong with the first field that
    /// is, so the form can show it.
    /// </summary>
    public static bool TryRead(string? name, string? seedText, out NewCity? city, out string? problem)
    {
        city = null;
        if (!CitySimulation.IsValidName(name))
        {
            problem = $"A city's name needs 1 to {CitySimulation.MaxNameLength} characters";
            return false;
        }

        int? seed = null;
        var trimmedSeed = seedText?.Trim();
        if (!string.IsNullOrEmpty(trimmedSeed))
        {
            if (!int.TryParse(trimmedSeed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                problem = $"The seed is a whole number from {int.MinValue:N0} to {int.MaxValue:N0}, or blank for any";
                return false;
            }

            seed = parsed;
        }

        city = new NewCity { Name = name!.Trim(), Seed = seed };
        problem = null;
        return true;
    }
}
