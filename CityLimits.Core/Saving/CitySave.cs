using System.Text.Json;

namespace CityLimits.Core.Saving;

// The save model: everything a city needs to carry on exactly where it was, the cycle's half-done
// figures included, so a city saved at any step, paused or not, loads back and runs on tick for tick
// as if it had never stopped. It is also the shape of the flat JSON format, so its names are the
// file's. Each part of the simulation captures and restores its own section.

/// <summary>A saved city, as the flat format holds it.</summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class CitySave
{
    /// <summary>The schema that describes the file, for editors; the game ignores it when reading.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public required FormatVersion Format { get; set; }

    public required string Name { get; set; }

    public required int Seed { get; set; }

    /// <summary>The simulation's generator state, as 16 hexadecimal digits.</summary>
    public required string Random { get; set; }

    public required long Tick { get; set; }

    public required long CityTime { get; set; }

    public required int Funds { get; set; }

    /// <summary>The definitions the city runs on, in the definitions format a mod uses.</summary>
    public required JsonElement Definitions { get; set; }

    public required ModSave[] Mods { get; set; }

    public required MapSave Map { get; set; }

    public required BudgetSave Budget { get; set; }

    public required CensusSave Census { get; set; }

    public required DemandSave Demand { get; set; }

    public required PowerSave Power { get; set; }

    public required ServicesSave Services { get; set; }

    public required CommunitySave Community { get; set; }

    public required CityMapsSave CityMaps { get; set; }

    public required CycleSave Cycle { get; set; }

    /// <summary>How busy the roads are; from format 1.1, and empty for a city upgraded from 1.0.</summary>
    public TrafficSave? Traffic { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class ModSave
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public string? Version { get; set; }
}

/// <summary>
/// The map as rows of characters, one string per row, a layer for each part of a tile, so a save
/// can be read and compared: the ground, the use, and the part of a building's footprint. What
/// only a building's centre or a house's tile holds is listed beside them.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class MapSave
{
    public required int Width { get; set; }

    public required int Height { get; set; }

    public required string[] Ground { get; set; }

    public required string[] Use { get; set; }

    public required string[] Part { get; set; }

    public required BuildingSave[] Buildings { get; set; }

    public required HouseSave[] Houses { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class BuildingSave
{
    public required int X { get; set; }

    public required int Y { get; set; }

    public int Development { get; set; }

    public int ValueClass { get; set; }

    public bool Powered { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class HouseSave
{
    public required int X { get; set; }

    public required int Y { get; set; }

    public required int House { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class BudgetSave
{
    public required int TaxRate { get; set; }

    public required int RoadFunding { get; set; }

    public required int FireFunding { get; set; }

    public required int PoliceFunding { get; set; }

    public required int TaxEffect { get; set; }

    public required int RoadEffect { get; set; }

    public required int FireEffect { get; set; }

    public required int PoliceEffect { get; set; }

    public required long TaxFund { get; set; }

    public required long RoadFund { get; set; }

    public required long FireFund { get; set; }

    public required long PoliceFund { get; set; }

    public required bool Cut { get; set; }

    public BudgetYearSave? LastYear { get; set; }

    public BudgetYearSave? Estimated { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class BudgetYearSave
{
    public required int TaxRate { get; set; }

    public required int TaxIncome { get; set; }

    public required int RoadRequest { get; set; }

    public required int RoadFunded { get; set; }

    public required int FireRequest { get; set; }

    public required int FireFunded { get; set; }

    public required int PoliceRequest { get; set; }

    public required int PoliceFunded { get; set; }

    public required bool Cut { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class CensusSave
{
    public required int Residential { get; set; }

    public required int Commercial { get; set; }

    public required int Industrial { get; set; }

    public required int ResidentPopulation { get; set; }

    public required int CommercialPopulation { get; set; }

    public required int IndustrialPopulation { get; set; }

    public required int[] ResidentialHistory { get; set; }

    public required int[] CommercialHistory { get; set; }

    public required int[] IndustrialHistory { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class DemandSave
{
    public required int Residential { get; set; }

    public required int Commercial { get; set; }

    public required int Industrial { get; set; }

    public required int TaxablePopulation { get; set; }

    public required int ResidentialPull { get; set; }

    public required int CommercialPull { get; set; }

    public required int IndustrialPull { get; set; }

    public required int TaxPushApplied { get; set; }

    public required double JobsPerResident { get; set; }

    public required double WorkersPerJob { get; set; }

    public required int HistoryCount { get; set; }

    public required int[] ResidentialHistory { get; set; }

    public required int[] CommercialHistory { get; set; }

    public required int[] IndustrialHistory { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class PowerSave
{
    /// <summary>The plants the scan has passed this cycle, as x and y pairs, in the order passed.</summary>
    public required int[] Plants { get; set; }

    /// <summary>What the last trace powered, as rows of 0 and 1; empty before the first trace.</summary>
    public required string[] Powered { get; set; }

    public required int CoalPlants { get; set; }

    public required int NuclearPlants { get; set; }

    public required int Zones { get; set; }

    public required int PoweredBuildings { get; set; }

    public required int UnpoweredBuildings { get; set; }

    public required int Capacity { get; set; }

    public required int PoweredTiles { get; set; }

    public required bool BrownedOut { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class ServicesSave
{
    public required int FireStations { get; set; }

    public required int PoliceStations { get; set; }

    public required int[] FireRaw { get; set; }

    public required int[] PoliceRaw { get; set; }

    public required int[] Fire { get; set; }

    public required int[] Police { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class CommunitySave
{
    public required int Hospitals { get; set; }

    public required int Churches { get; set; }

    public required int HospitalNeed { get; set; }

    public required int ChurchNeed { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class CityMapsSave
{
    public required int CenterX { get; set; }

    public required int CenterY { get; set; }

    public required int LandValueAverage { get; set; }

    public required int[] LandValue { get; set; }

    public required int[] Density { get; set; }

    public required int[] Terrain { get; set; }

    public required int[] CommercialRate { get; set; }
}

/// <summary>What the city counts as its scan goes, and what the last count set aside for the budget.</summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class CycleSave
{
    public required int RoadTiles { get; set; }

    public required int BudgetedRoadTiles { get; set; }

    public required int BudgetedFireStations { get; set; }

    public required int BudgetedPoliceStations { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class TrafficSave
{
    /// <summary>Each 2 by 2 block's traffic, row by row.</summary>
    public required int[] Density { get; set; }
}
