namespace CityLimits.CommandLine;

/// <summary>The exit codes the CLI returns.</summary>
public static class ExitCodes
{
    /// <summary>The command did what it was asked.</summary>
    public const int Success = 0;

    /// <summary>The command was understood but could not finish.</summary>
    public const int Failure = 1;

    /// <summary>The command line itself was wrong: an unknown command, option, or value.</summary>
    public const int Usage = 2;
}
