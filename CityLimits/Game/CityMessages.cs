namespace CityLimits.Game;

/// <summary>What the city's messages say to the player, in the game's own words.</summary>
public static class CityMessages
{
    /// <summary>Tells whether a message reports a disaster as it starts, which the ticker shows in red.</summary>
    public static bool IsDisaster(CityMessage message) => message is CityMessage.FireReported or CityMessage.ExplosionReported or CityMessage.FloodReported or CityMessage.EarthquakeReported;

    /// <summary>The line the game shows, and the log writes, for a message.</summary>
    public static string Describe(CityMessage message) => message switch
    {
        CityMessage.NeedPower => "The city needs a power plant",
        CityMessage.Blackouts => "Blackouts: many zones have no power",
        CityMessage.HighTaxes => "Taxes are too high: people and businesses are leaving",
        CityMessage.RoadsNeedFunding => "The roads need funding, or they will crumble",
        CityMessage.NeedFireStation => "The city needs a fire station",
        CityMessage.NeedPoliceStation => "The city needs a police station",
        CityMessage.FireNeedsFunding => "The fire department needs funding",
        CityMessage.PoliceNeedsFunding => "The police department needs funding",
        CityMessage.TrafficJams => "Frequent traffic jams reported",
        CityMessage.NeedRail => "Inadequate rail system",
        CityMessage.HighPollution => "Pollution very high",
        CityMessage.HighCrime => "Crime very high",
        CityMessage.FireReported => "Fire reported!",
        CityMessage.ExplosionReported => "Explosion reported!",
        CityMessage.FloodReported => "Flooding reported!",
        CityMessage.EarthquakeReported => "Major earthquake reported!",
        CityMessage.Brownouts => "Brownouts: the power plants can't keep up, so build another",
        _ => message.ToString(),
    };
}

/// <summary>
/// Decides which of the city's messages to pass on. The city repeats a message every cycle
/// while it holds, a few seconds apart, so a message is passed on when it is new, or when the
/// same one comes again after a year of the city's time.
/// </summary>
public sealed class CityMessageFilter
{
    private CityMessage? _last;
    private long _lastTime;

    /// <summary>Tells whether to pass a message on, and remembers it if so.</summary>
    public bool ShouldPass(CityMessage message, long cityTime)
    {
        if (message == _last && cityTime - _lastTime < CityCalendar.CountsPerYear)
        {
            return false;
        }

        _last = message;
        _lastTime = cityTime;
        return true;
    }
}
