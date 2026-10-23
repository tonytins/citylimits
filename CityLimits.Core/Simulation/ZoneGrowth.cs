using CityLimits.Core.Randomness;

namespace CityLimits.Core.Simulation;

/// <summary>
/// What happens to a zone when the scan reaches it, as in Micropolis. A zone with people sometimes
/// sends a trip out; a zone with no road along its edge declines. Otherwise, now and then, the
/// city's demand for the zone's kind and the zone's own worth make a score, and a roll against
/// it decides whether the zone grows a step, declines a step, or stays as it is. A zone with no
/// power scores low enough that it never grows.
/// </summary>
/// <remarks>
/// A residential zone grows single houses until its spec's number, then a building once the
/// population density around it is high enough; buildings grow and decline a level at a time, in
/// the land-value class of the moment. One time in four, an empty zone that would grow becomes a
/// hospital or a church instead, when the city needs one. Pollution counts against a residential
/// zone's worth and against the land-value class of homes and shops, and a residential zone doesn't
/// grow at all where it is above 128. A zone with any tile burning can't be redrawn, so it neither
/// grows nor declines a level until the fire is out; when it next does, the holes fire left in it
/// are redrawn too, as Micropolis' zones are. A house never goes in a hole.
/// </remarks>
internal sealed class ZoneGrowth
{
    // A score this low or lower never grows, and this high or higher never declines.
    private const int GrowthFloor = -350;
    private const int DeclineCeiling = 350;

    // The roll a score is measured against runs from -32768 to 32767; this offset makes growth rare.
    private const int RollOffset = 26380;

    // A zone with no power scores this, so it never grows and now and then declines.
    private const int UnpoweredScore = -500;

    // A zone with no road along its edge scores this; industry minds less.
    private const int NoRoadScore = -3000;
    private const int IndustryNoRoadScore = -1000;

    // A residential zone's worth: land value times 32, at most 6000, less 3000.
    private const int MostResidentialWorth = 6000;

    // The density around a residential zone its houses need before it builds.
    private const int DensityForBuilding = 64;

    // Micropolis' residential zones don't grow where pollution is above this.
    private const int MostPollutionForHomes = 128;

    // The outer tiles in the order a house is looked for: across the top, the sides, the bottom.
    private static readonly (int Dx, int Dy)[] Lots =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    ];

    // The order a declining zone loses a house: down each column, left to right.
    private static readonly (int Dx, int Dy)[] HouseLossOrder =
    [
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1), (0, 1),
        (1, -1), (1, 0), (1, 1),
    ];

    private readonly TileMap _map;
    private readonly DeterministicRandom _random;
    private readonly TripPlanner _trips;
    private readonly CityDefinitions _definitions;
    private readonly CityDemand _demand;
    private readonly CityMaps _maps;
    private readonly CommunityBuildings _community;
    private readonly Fires _fires;

    public ZoneGrowth(TileMap map, DeterministicRandom random, TripPlanner trips, CityDefinitions definitions, CityDemand demand, CityMaps maps, CommunityBuildings community, Fires fires)
    {
        _fires = fires;
        _community = community;
        _map = map;
        _random = random;
        _trips = trips;
        _definitions = definitions;
        _demand = demand;
        _maps = maps;
    }

    /// <summary>Decides what happens to the zone centred on a tile, as the scan reaches it.</summary>
    public void Visit(int x, int y)
    {
        var center = _map.StateAt(x, y);
        var spec = _definitions.Zone(center.Use);

        switch (center.Use)
        {
            case TileUse.Residential:
                VisitResidential(x, y, center, spec);
                break;
            case TileUse.Commercial:
                VisitCommercial(x, y, center, spec);
                break;
            default:
                VisitIndustrial(x, y, center, spec);
                break;
        }
    }

    private void VisitResidential(int x, int y, TileState center, ZoneSpec spec)
    {
        var population = spec.PopulationOf(_map, x, y);
        var trip = population > _random.NextInt(0, 36) ? _trips.Drive(_map, _definitions, _random, x, y) : TripOutcome.Arrived;
        if (trip == TripOutcome.NoRoad)
        {
            DeclineResidential(x, y, center, spec, ValueClass(x, y));
            return;
        }

        // An empty zone, with no building, is weighed every time; any other one time in eight.
        if (center.Development > 0 && _random.NextInt(0, 8) != 0)
        {
            return;
        }

        var score = center.Powered ? _demand.Residential + ResidentialWorth(x, y) : UnpoweredScore;
        if (Grows(score))
        {
            // One time in four an empty zone becomes a hospital or a church, if the city needs one.
            if (population == 0 && _random.NextInt(0, 4) == 0)
            {
                _community.TryBuildIn(_map, x, y, _fires);
                return;
            }

            GrowResidential(x, y, center, spec, ValueClass(x, y));
            return;
        }

        if (Declines(score))
        {
            DeclineResidential(x, y, center, spec, ValueClass(x, y));
        }
    }

    private void VisitCommercial(int x, int y, TileState center, ZoneSpec spec)
    {
        var level = center.Development;
        var trip = level > _random.NextInt(0, 6) ? _trips.Drive(_map, _definitions, _random, x, y) : TripOutcome.Arrived;
        if (trip == TripOutcome.NoRoad)
        {
            SetLevel(x, y, center, Math.Max(level - 1, 0), ValueClass(x, y));
            return;
        }

        if (_random.NextInt(0, 8) != 0)
        {
            return;
        }

        var score = center.Powered ? _demand.Commercial + _maps.CommercialRateAt(x, y) : UnpoweredScore;
        if (trip != TripOutcome.NoDestination && Grows(score))
        {
            // Commerce grows only as far as land value can carry it.
            if (level < spec.Levels && level <= _maps.LandValueAt(x, y) / 32)
            {
                SetLevel(x, y, center, level + 1, ValueClass(x, y));
            }

            return;
        }

        if (Declines(score) && level > 0)
        {
            SetLevel(x, y, center, level - 1, ValueClass(x, y));
        }
    }

    private void VisitIndustrial(int x, int y, TileState center, ZoneSpec spec)
    {
        var level = center.Development;
        var trip = level > _random.NextInt(0, 6) ? _trips.Drive(_map, _definitions, _random, x, y) : TripOutcome.Arrived;
        if (trip == TripOutcome.NoRoad)
        {
            SetLevel(x, y, center, Math.Max(level - 1, 0), _random.NextInt(0, 2));
            return;
        }

        if (_random.NextInt(0, 8) != 0)
        {
            return;
        }

        // Industry cares only for demand; its land-value class is a toss of a coin.
        var score = center.Powered ? _demand.Industrial : UnpoweredScore;
        if (Grows(score))
        {
            var valueClass = _random.NextInt(0, 2);
            if (level < spec.Levels)
            {
                SetLevel(x, y, center, level + 1, valueClass);
            }

            return;
        }

        if (Declines(score) && level > 0)
        {
            SetLevel(x, y, center, level - 1, _random.NextInt(0, 2));
        }
    }

    private bool Grows(int score) => score > GrowthFloor && score - RollOffset > Roll();

    private bool Declines(int score) => score < DeclineCeiling && score + RollOffset < Roll();

    private int Roll() => _random.NextInt(0, 0x10000) - 0x8000;

    // Land value, less pollution, times 32, between -3000 and 3000.
    private int ResidentialWorth(int x, int y)
    {
        var value = _maps.LandValueAt(x, y) - _maps.PollutionAt(x, y);
        return Math.Min(Math.Max(value, 0) * 32, MostResidentialWorth) - (MostResidentialWorth / 2);
    }

    // The land-value class a zone's building takes, from land value less pollution: thresholds at
    // 30, 80, and 150.
    private int ValueClass(int x, int y) => (_maps.LandValueAt(x, y) - _maps.PollutionAt(x, y)) switch
    {
        < 30 => 0,
        < 80 => 1,
        < 150 => 2,
        _ => 3,
    };

    private void GrowResidential(int x, int y, TileState center, ZoneSpec spec, int valueClass)
    {
        if (_maps.PollutionAt(x, y) > MostPollutionForHomes)
        {
            return;
        }

        if (center.Development > 0)
        {
            if (center.Development < spec.Levels)
            {
                SetLevel(x, y, center, center.Development + 1, valueClass);
            }

            return;
        }

        if (CountHouses(x, y) < spec.Houses)
        {
            BuildHouse(x, y, valueClass);
            return;
        }

        if (_maps.DensityAt(x, y) > DensityForBuilding && spec.Levels > 0 && !_fires.AnyBurningIn(x, y))
        {
            ClearHouses(x, y);
            SetLevel(x, y, center, 1, valueClass);
        }
    }

    private void DeclineResidential(int x, int y, TileState center, ZoneSpec spec, int valueClass)
    {
        if (center.Development > 1)
        {
            SetLevel(x, y, center, center.Development - 1, valueClass);
            return;
        }

        if (center.Development == 1)
        {
            // The building makes way for a full set of houses again.
            if (!SetLevel(x, y, center, 0, 0))
            {
                return;
            }

            var houses = 0;
            foreach (var (dx, dy) in Lots)
            {
                if (houses++ >= spec.Houses)
                {
                    break;
                }

                SetHouse(x + dx, y + dy, (valueClass * 3) + _random.NextInt(0, 3) + 1);
            }

            return;
        }

        foreach (var (dx, dy) in HouseLossOrder)
        {
            if (_map.StateAt(x + dx, y + dy).HasHouse)
            {
                SetHouse(x + dx, y + dy, 0);
                return;
            }
        }
    }

    // A house goes on the free outer tile with the most roads beside it; a tie goes to a later
    // tile one time in eight.
    private void BuildHouse(int x, int y, int valueClass)
    {
        var best = -1;
        var bestScore = 0;

        for (var i = 0; i < Lots.Length; i++)
        {
            var (lotX, lotY) = (x + Lots[i].Dx, y + Lots[i].Dy);
            var lot = _map.StateAt(lotX, lotY);
            if (lot.HasHouse || lot.Use != TileUse.Residential)
            {
                continue;
            }

            var score = 1 + RoadsBeside(lotX, lotY);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
            else if (score == bestScore && _random.NextInt(0, 8) == 0)
            {
                best = i;
            }
        }

        if (best >= 0)
        {
            SetHouse(x + Lots[best].Dx, y + Lots[best].Dy, (valueClass * 3) + _random.NextInt(0, 3) + 1);
        }
    }

    private int CountHouses(int x, int y)
    {
        var houses = 0;
        foreach (var (dx, dy) in Lots)
        {
            houses += _map.StateAt(x + dx, y + dy).HasHouse ? 1 : 0;
        }

        return houses;
    }

    private void ClearHouses(int x, int y)
    {
        foreach (var (dx, dy) in Lots)
        {
            SetHouse(x + dx, y + dy, 0);
        }
    }

    private void SetHouse(int x, int y, int house)
    {
        var lot = _map.StateAt(x, y);
        _map.SetState(x, y, new TileState(lot.Kind, lot.Use, zonePart: lot.ZonePart, house: house));
    }

    // A zone is redrawn at its new level, holes and all, unless any of it is burning; tells whether it was.
    private bool SetLevel(int x, int y, TileState center, int level, int valueClass)
    {
        if (_fires.AnyBurningIn(x, y))
        {
            return false;
        }

        _fires.RepairHoles(x, y);
        _map.SetState(x, y, new TileState(center.Kind, center.Use, level, valueClass: level > 0 ? valueClass : 0, powered: center.Powered));
        return true;
    }

    private int RoadsBeside(int x, int y) =>
        (_map.StateAtOrDefault(x, y - 1).IsRoad ? 1 : 0) +
        (_map.StateAtOrDefault(x + 1, y).IsRoad ? 1 : 0) +
        (_map.StateAtOrDefault(x, y + 1).IsRoad ? 1 : 0) +
        (_map.StateAtOrDefault(x - 1, y).IsRoad ? 1 : 0);
}
