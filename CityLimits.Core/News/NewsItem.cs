namespace CityLimits.Core.News;

/// <summary>What a line on the news ticker is, which decides its colour and its place in the queue.</summary>
public enum NewsKind
{
    /// <summary>A headline from the city's own world, shown in white: flavour, or news of how the city is doing.</summary>
    Headline,

    /// <summary>The game or the city's advisers telling the player something, shown in yellow: saves, warnings, and the city's messages.</summary>
    Advisory,

    /// <summary>A disaster, shown in red the moment it starts, such as a fire or an explosion.</summary>
    Disaster,

    /// <summary>
    /// News of an ordinance, shown in blue, as Tony Bark chose: what the city asks for, and what was
    /// enacted or repealed. A click on it opens the city's ordinances.
    /// </summary>
    Ordinance,

    /// <summary>
    /// News of a neighbour, shown in blue as an ordinance's is: an offer, and what came of a deal. A
    /// click on it opens the offer standing, or the city's neighbours.
    /// </summary>
    Neighbour,
}

/// <summary>A line on the news ticker, and what kind of news it is.</summary>
/// <param name="Text">What the line says, with the city's name already filled in.</param>
/// <param name="Kind">What kind of news it is.</param>
/// <param name="Link">What a click on the line opens: an ordinance's id, or a neighbour's side; null for a line that opens nothing.</param>
public readonly record struct NewsItem(string Text, NewsKind Kind, string? Link = null);
