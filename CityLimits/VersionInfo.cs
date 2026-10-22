using System.Text;

namespace CityLimits;

internal struct VersionInfo
{
    /// <summary>
    /// The name of the operating system.
    /// </summary>
    public const string Name = "CityLimits";

    /// <summary>
    /// The version from GitInfo.txt: on this backport branch, the release it backports and a revision,
    /// such as 0.3.102.1, the revision being what GitInfo reads as the label.
    /// </summary>
    public const string Version = $"{ThisAssembly.Git.BaseVersion.Major}.{ThisAssembly.Git.BaseVersion.Minor}.{ThisAssembly.Git.BaseVersion.Patch}.{ThisAssembly.Git.SemVer.Label}";

    /// <summary>
    /// The name, version, and build number as shown to the user, such as <c>CityLimits 0.1.116 (Build 123456)</c>.
    /// </summary>
    public static string DisplayVersion => $"{Name} {Version} (Build {BuildNumber})";

    /// <summary>
    /// A build number of up to six digits, derived from the commit hash.
    /// </summary>
    /// <remarks>
    /// The first four characters of the hash are read as a little-endian integer and reduced modulo 1,000,000.
    /// The number identifies a build but doesn't increase over time. It's 0 when the hash is unavailable.
    /// </remarks>
    public static uint BuildNumber
    {
        get
        {
            var commit = Encoding.UTF8.GetBytes(ThisAssembly.Git.Commit);
            return commit.Length < sizeof(uint) ? 0 : BitConverter.ToUInt32(commit, 0) % 1_000_000;
        }
    }
}