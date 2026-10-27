using System.Text;

namespace CityLimits.UI;

/// <summary>
/// A one-line text box: it takes typed characters while it has focus, and Backspace removes the
/// last one, repeating while held. Raylib's built-in font draws only printable ASCII, so other
/// characters are not taken.
/// </summary>
public sealed class TextField
{
    private const int DefaultFontSize = 20;
    private const int Padding = 6;

    private readonly StringBuilder _text = new();
    private readonly int _maxLength;
    private readonly Func<char, bool> _accepts;
    private readonly int _fontSize;

    /// <summary>
    /// Creates a field holding the given text, taking at most the given number of characters that
    /// the check accepts, drawn at the font size given: 20 unless a denser window asks for less.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The length or the size isn't positive.</exception>
    public TextField(string text, int maxLength, Func<char, bool> accepts, int fontSize = DefaultFontSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        ArgumentNullException.ThrowIfNull(accepts);
        _maxLength = maxLength;
        _accepts = accepts;
        _fontSize = fontSize;
        Text = text;
    }

    /// <summary>The text in the field.</summary>
    public string Text
    {
        get => _text.ToString();
        set
        {
            _text.Clear();
            foreach (var c in value ?? string.Empty)
            {
                Add(c);
            }
        }
    }

    /// <summary>How many characters the field holds, without making its text.</summary>
    public int Length => _text.Length;

    /// <summary>The height a field of the usual size is drawn at.</summary>
    public static int Height => DefaultFontSize + (Padding * 2);

    /// <summary>The height this field is drawn at.</summary>
    public int BoxHeight => _fontSize + (Padding * 2);

    /// <summary>Takes this frame's typing, and tells whether the text changed. Call it only for the field that has focus.</summary>
    public bool Update()
    {
        var length = _text.Length;
        var changed = false;

        // Raylib queues every character typed since the last frame; zero means the queue is empty.
        for (var key = Raylib.GetCharPressed(); key != 0; key = Raylib.GetCharPressed())
        {
            Add((char)key);
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && _text.Length > 0)
        {
            _text.Length--;
            changed = true;
        }

        return changed || _text.Length != length;
    }

    /// <summary>Draws the field in its bounds, with a caret when it has focus.</summary>
    public void Draw(Rectangle bounds, bool focused)
    {
        Raylib.DrawRectangleRec(bounds, Theme.Bar);
        Raylib.DrawRectangleLinesEx(bounds, 1f, focused ? Theme.Accent : Theme.PanelBorder);
        var text = Text;
        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Padding;
        Raylib.DrawText(text, x, y, _fontSize, Theme.Text);

        // The caret blinks twice a second.
        if (focused && (int)(Raylib.GetTime() * 2) % 2 == 0)
        {
            var caretX = x + Raylib.MeasureText(text, _fontSize) + 2;
            Raylib.DrawRectangle(caretX, y, 2, _fontSize, Theme.Text);
        }
    }

    private void Add(char c)
    {
        if (_text.Length < _maxLength && c is >= ' ' and <= '~' && _accepts(c))
        {
            _text.Append(c);
        }
    }
}
