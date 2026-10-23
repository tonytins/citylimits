namespace CityLimits.UI;

/// <summary>Lays words on lines that fit a width, for the windows that show a message or a description.</summary>
public static class TextWrap
{
    /// <summary>
    /// The lines a text takes at a font size within a width: words are laid on a line until the next
    /// one wouldn't fit, and a word wider than the width has a line of its own. It measures with the
    /// window's font, so call it when the text changes, not every frame.
    /// </summary>
    public static List<string> Lines(string text, int fontSize, int width)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var longer = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && Raylib.MeasureText(longer, fontSize) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = longer;
            }
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }
}
