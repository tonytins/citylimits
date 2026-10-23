using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// Writes the city's stadiums, seaports, and airports as a part of their own, from format 1.9, as rail
/// is (<see cref="RailCodec"/>): the map holds the bare ground beneath each, and the part lists each
/// one by its centre, with its power and the holes fire, floods, or earthquakes have left in it, and
/// the scan's counts and the demand held back for want of one. A hole is written in the map, and in
/// the fire and flood parts, as the rubble or bare ground it is, so a version that doesn't know these
/// buildings opens the city as if they had never been built, with what was lost from them lying there.
/// </summary>
internal static class FacilityCodec
{
    /// <summary>What a tile's map layer holds: the tile itself, or for one of these buildings, the bare ground beneath it.</summary>
    public static TileState Beneath(TileState state) => TileRules.IsFacility(state.Use) ? new TileState(state.Kind) : state;

    /// <summary>Tells whether a tile is a hole in a stadium, a seaport, or an airport, which the map writes as it is.</summary>
    public static bool IsHoleInFacility(TileMap map, Hazards hazards, int x, int y) =>
        hazards.HoleOwner(x, y) is var (cx, cy) && TileRules.IsFacility(map.StateAt(cx, cy).Use);

    /// <summary>Captures the buildings on the map, with the scan's counts and the demand held back.</summary>
    public static FacilitiesSave Capture(TileMap map, Hazards hazards, Facilities facilities)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(hazards);
        ArgumentNullException.ThrowIfNull(facilities);

        var buildings = new List<FacilitySave>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                if (!state.IsBuildingCenter || !TileRules.IsFacility(state.Use))
                {
                    continue;
                }

                var size = Footprint.SizeOf(state.Use);
                var holes = new List<int>();
                for (var part = 0; part < Footprint.Parts(size); part++)
                {
                    var (dx, dy) = Footprint.OffsetFromCenter(part, size);
                    if (hazards.HoleOwner(x + dx, y + dy) == (x, y))
                    {
                        holes.Add(part);
                    }
                }

                buildings.Add(new FacilitySave { Use = state.Use.ToString(), X = x, Y = y, Powered = state.Powered, Holes = [.. holes] });
            }
        }

        var save = facilities.Capture();
        save.Buildings = [.. buildings];
        return save;
    }

    /// <summary>
    /// Puts a save's buildings on its map, read with what lies beneath them, and its counts and caps
    /// in the city's <paramref name="facilities"/>. Each building must stand whole on the map, its centre
    /// on the map and its tiles on bare ground with nothing on them, but for its holes, which are left
    /// as the map has them: bare ground or rubble. No two buildings may share a tile.
    /// </summary>
    /// <exception cref="InvalidSave">A building is wrong or where it can't be; the message says which and where.</exception>
    public static void Apply(TileMap map, Facilities facilities, FacilitiesSave save)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(facilities);
        ArgumentNullException.ThrowIfNull(save);

        var taken = new HashSet<(int X, int Y)>();
        var buildings = save.Buildings ?? throw new InvalidSave("facilities.buildings: missing.");
        for (var i = 0; i < buildings.Length; i++)
        {
            var at = $"facilities.buildings[{i}]";
            var building = buildings[i] ?? throw new InvalidSave($"{at}: missing.");
            if (!Enum.TryParse<TileUse>(building.Use, ignoreCase: false, out var use) || !TileRules.IsFacility(use) || building.Use != use.ToString())
            {
                throw new InvalidSave($"{at}.use: '{building.Use}' isn't a stadium, a seaport, or an airport (Stadium, Seaport, or Airport).");
            }

            var size = Footprint.SizeOf(use);
            var center = Footprint.CenterPart(size);
            var holes = new HashSet<int>();
            foreach (var hole in building.Holes ?? throw new InvalidSave($"{at}.holes: missing."))
            {
                if (hole < 0 || hole >= Footprint.Parts(size) || hole == center || !holes.Add(hole))
                {
                    throw new InvalidSave($"{at}.holes: {hole} isn't an outer part of a {size} by {size} {use}, or is listed twice.");
                }
            }

            for (var part = 0; part < Footprint.Parts(size); part++)
            {
                var (dx, dy) = Footprint.OffsetFromCenter(part, size);
                var (x, y) = (building.X + dx, building.Y + dy);
                if (!map.Contains(x, y) || !taken.Add((x, y)))
                {
                    throw new InvalidSave($"{at}: the {use} at {building.X}, {building.Y} runs off the map or onto another one at {x}, {y}.");
                }

                var beneath = map.StateAt(x, y);
                var fits = beneath.Use == TileUse.None && (beneath.Kind == TileKind.Grass || (holes.Contains(part) && beneath.Kind == TileKind.Rubble));
                if (!fits)
                {
                    throw new InvalidSave($"{at}: the {use} at {building.X}, {building.Y} can't stand on {beneath.Kind} with {beneath.Use} at {x}, {y}.");
                }

                if (!holes.Contains(part))
                {
                    map.SetState(x, y, new TileState(TileKind.Grass, use, zonePart: part, powered: part == center && building.Powered));
                }
            }
        }

        facilities.Restore(save);
    }
}
