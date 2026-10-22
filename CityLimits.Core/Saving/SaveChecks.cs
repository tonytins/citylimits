namespace CityLimits.Core.Saving;

/// <summary>A save that can't be loaded, with what is wrong and where, for the player to read.</summary>
internal sealed class InvalidSave(string message) : Exception(message);

/// <summary>The checks every part of the simulation makes as it restores its section of a save.</summary>
internal static class SaveChecks
{
    /// <summary>Copies a saved array into the simulation's own, refusing one of the wrong length.</summary>
    /// <exception cref="InvalidSave">The lengths differ.</exception>
    public static void CopyInto(int[]? saved, int[] target, string field)
    {
        if (saved is null || saved.Length != target.Length)
        {
            throw new InvalidSave($"{field}: needs {target.Length} numbers, not {saved?.Length ?? 0}.");
        }

        Array.Copy(saved, target, target.Length);
    }

    /// <summary>Refuses a number outside its range.</summary>
    /// <exception cref="InvalidSave">The number is out of range.</exception>
    public static int InRange(int value, int min, int max, string field) =>
        value >= min && value <= max ? value : throw new InvalidSave($"{field}: needs {min} to {max}, not {value}.");

    /// <summary>Refuses a long number outside its range.</summary>
    /// <exception cref="InvalidSave">The number is out of range.</exception>
    public static long InRange(long value, long min, long max, string field) =>
        value >= min && value <= max ? value : throw new InvalidSave($"{field}: needs {min} to {max}, not {value}.");
}
