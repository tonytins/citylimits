namespace CityLimits.Rendering;

/// <summary>
/// How the window fills the screen. Fullscreen is a borderless window the size of the monitor, as
/// raylib's borderless windowed mode makes it, rather than a change of the monitor's resolution, so
/// switching is quick and leaves the desktop as it was.
/// </summary>
public static class Display
{
    /// <summary>Tells whether the window fills the screen.</summary>
    public static bool IsFullscreen => Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);

    /// <summary>Makes the window fill the screen, or go back to the size it had, when it isn't already as asked.</summary>
    public static void Apply(bool fullscreen)
    {
        if (fullscreen != IsFullscreen)
        {
            Raylib.ToggleBorderlessWindowed();
        }
    }

    /// <summary>
    /// The settings with the window's size as it is now, kept within what the game allows; while the
    /// window fills the screen, the size it goes back to is kept instead of the monitor's.
    /// </summary>
    public static GameSettings WithWindowSize(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsFullscreen)
        {
            return settings;
        }

        var width = Math.Clamp(Raylib.GetScreenWidth(), GameSettings.MinWindowWidth, GameSettings.MaxWindowWidth);
        var height = Math.Clamp(Raylib.GetScreenHeight(), GameSettings.MinWindowHeight, GameSettings.MaxWindowHeight);
        return settings with { WindowWidth = width, WindowHeight = height };
    }
}
