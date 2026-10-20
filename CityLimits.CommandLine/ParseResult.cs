namespace CityLimits.CommandLine;

/// <summary>The result of parsing CLI arguments.</summary>
public sealed record ParseResult
{
    /// <summary>Whether parsing was successful.</summary>
    public bool Success { get; init; }

    /// <summary>The error message if parsing failed.</summary>
    public string? Error { get; init; }

    /// <summary>The parsed context to execute.</summary>
    public SubcommandContext? Context { get; init; }

    /// <summary>Creates a successful parse result.</summary>
    public static ParseResult Ok(SubcommandContext context) =>
        new() { Success = true, Context = context };

    /// <summary>Creates a failed parse result.</summary>
    public static ParseResult Fail(string error) =>
        new() { Success = false, Error = error };
}
