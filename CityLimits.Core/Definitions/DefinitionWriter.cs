using System.Text.Json;

namespace CityLimits.Core.Definitions;

/// <summary>
/// Writes a city's definitions in the definitions format, the one the base game and mods use, so a
/// save carries the definitions the city was made with and reads them back through
/// <see cref="DefinitionLoader"/>, with all its checks. Mods already applied are written out as
/// the numbers they gave, so a saved city doesn't change when the mods folder does.
/// </summary>
internal static class DefinitionWriter
{
    /// <summary>Writes the definitions as a JSON element.</summary>
    public static JsonElement Write(CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("format", DefinitionLoader.Format.ToString());

            writer.WriteStartArray("zones");
            foreach (var zone in definitions.Zones)
            {
                writer.WriteStartObject();
                writer.WriteString("id", zone.Id);
                writer.WriteString("name", zone.Name);
                writer.WriteNumber("price", zone.Price);
                writer.WriteStartArray("populations");
                foreach (var population in zone.Populations)
                {
                    writer.WriteNumberValue(population);
                }

                writer.WriteEndArray();
                writer.WriteNumber("valueClasses", zone.ValueClasses);
                writer.WriteNumber("houses", zone.Houses);
                writer.WriteStartArray("trips");
                foreach (var use in zone.TripsTo)
                {
                    writer.WriteStringValue(definitions.Zone(use).Id);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("plants");
            foreach (var plant in definitions.Plants)
            {
                writer.WriteStartObject();
                writer.WriteString("id", plant.Id);
                writer.WriteString("name", plant.Name);
                writer.WriteNumber("price", plant.Price);
                writer.WriteNumber("capacity", plant.Capacity);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("services");
            foreach (var service in definitions.Services)
            {
                writer.WriteStartObject();
                writer.WriteString("id", service.Id);
                writer.WriteString("name", service.Name);
                writer.WriteNumber("price", service.Price);
                writer.WriteNumber("upkeep", service.Upkeep);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }
}
