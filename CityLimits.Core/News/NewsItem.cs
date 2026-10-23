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
}

/// <summary>A line on the news ticker, and what kind of news it is.</summary>
/// <param name="Text">What the line says, with the city's name already filled in.</param>
/// <param name="Kind">What kind of news it is.</param>
public readonly record struct NewsItem(string Text, NewsKind Kind);
