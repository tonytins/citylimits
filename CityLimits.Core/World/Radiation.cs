namespace CityLimits.Core.World;

/// <summary>
/// The radioactive ground a nuclear plant's meltdown leaves, as Micropolis has it. A radioactive
/// tile has lost what stood there: the map holds bare ground beneath, which is what is left when it
/// decays. Nothing can be built on it, and the bulldozer can't clear it; each time the scan passes
/// a tile, it decays one time in <see cref="DecayChance"/>, so it lasts some 85 years on average.
/// </summary>
public sealed class Radiation
{
    /// <summary>One time in this many, the scan passing a radioactive tile clears it, as in Micropolis.</summary>
    public const int DecayChance = 4096;

    private readonly TileMap _map;
    private readonly bool[] _radioactive;

    /// <summary>Makes the radiation of a map, with none.</summary>
    public Radiation(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _radioactive = new bool[map.Width * map.Height];
    }

    /// <summary>How many tiles are radioactive.</summary>
    public int Count { get; private set; }

    /// <summary>Tells whether a tile is radioactive; a tile off the map never is.</summary>
    public bool IsRadioactive(int x, int y) => _map.Contains(x, y) && _radioactive[(y * _map.Width) + x];

    /// <summary>Makes a tile radioactive: what stood there is lost, and bare ground is left beneath.</summary>
    public void Contaminate(int x, int y)
    {
        if (!_map.Contains(x, y) || IsRadioactive(x, y))
        {
            return;
        }

        _map.SetState(x, y, new TileState(TileKind.Grass));
        _radioactive[(y * _map.Width) + x] = true;
        Count++;
    }

    /// <summary>Lets a tile's radiation decay, leaving the bare ground.</summary>
    public void Decay(int x, int y)
    {
        if (IsRadioactive(x, y))
        {
            _radioactive[(y * _map.Width) + x] = false;
            Count--;
        }
    }

    /// <summary>Sets a tile radioactive as a save had it, without touching what the map holds beneath it.</summary>
    internal void RestoreRadioactive(int x, int y)
    {
        if (!IsRadioactive(x, y))
        {
            _radioactive[(y * _map.Width) + x] = true;
            Count++;
        }
    }
}
