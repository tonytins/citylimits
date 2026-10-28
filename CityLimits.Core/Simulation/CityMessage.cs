namespace CityLimits.Core.Simulation;

/// <summary>Something the city tells the player, as Micropolis' messages do.</summary>
public enum CityMessage
{
    /// <summary>There are more than 10 zones and no power plant.</summary>
    NeedPower,

    /// <summary>Fewer than 7 in 10 of the zones and plants have power.</summary>
    Blackouts,

    /// <summary>The plants ran out of capacity before power reached everything connected to them.</summary>
    Brownouts,

    /// <summary>The tax rate is above 12%.</summary>
    HighTaxes,

    /// <summary>The roads are funded too little to keep them up, and there are more than 30 of them.</summary>
    RoadsNeedFunding,

    /// <summary>The city has more than 60 people and no fire station.</summary>
    NeedFireStation,

    /// <summary>The city has more than 60 people and no police station.</summary>
    NeedPoliceStation,

    /// <summary>The fire stations are funded below 70%, in a city of more than 20 people.</summary>
    FireNeedsFunding,

    /// <summary>The police stations are funded below 70%, in a city of more than 20 people.</summary>
    PoliceNeedsFunding,

    /// <summary>The year's traffic average is above 60.</summary>
    TrafficJams,

    /// <summary>The city has more than 50 zones and fewer rail tiles than zones.</summary>
    NeedRail,

    /// <summary>The city's average pollution is above 60.</summary>
    HighPollution,

    /// <summary>The city's average crime is above 100.</summary>
    HighCrime,

    /// <summary>A fire has broken out.</summary>
    FireReported,

    /// <summary>A burning building has exploded.</summary>
    ExplosionReported,

    /// <summary>A flood has risen from the water onto the land.</summary>
    FloodReported,

    /// <summary>An earthquake has struck.</summary>
    EarthquakeReported,

    /// <summary>The city has more than 500 residential units and no stadium, so residential demand is held at zero.</summary>
    NeedStadium,

    /// <summary>The city has more than 70 industrial units and no seaport, so industrial demand is held at zero.</summary>
    NeedSeaport,

    /// <summary>The city has more than 100 commercial units and no airport, so commercial demand is held at zero.</summary>
    NeedAirport,

    /// <summary>A nuclear plant has melted down.</summary>
    MeltdownReported,

    /// <summary>The plane has crashed.</summary>
    PlaneCrashReported,

    /// <summary>The helicopter has crashed.</summary>
    HelicopterCrashReported,

    /// <summary>The ship has run aground.</summary>
    ShipwreckReported,

    /// <summary>The helicopter has seen heavy traffic.</summary>
    HeavyTrafficReported,
}
