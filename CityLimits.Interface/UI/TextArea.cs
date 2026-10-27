using System.Text;

namespace CityLimits.UI;

/// <summary>
/// A text box of several lines, for a briefing or a description: it takes typed characters while it
/// has focus, Backspace removes the last one, repeating while held, and Enter starts nothing new,
/// since the words wrap to its width on their own. The caret stays at the end. The lines are laid
/// out again only when the text changes, never in a frame. As with <see cref="TextField"/>, only
/// printable ASCII is taken, which is what Raylib's font draws.
/// </summary>
public sealed class TextArea
{
    private const int Padding = 6;
    private const int LineGap = 4;

    private readonly StringBuilder _text = new();
    private readonly int _maxLength;
    private readonly int _fontSize;
    private readonly int _width;
    private List<string> _lines = [];

    /// <summary>Creates a box holding the given text, at most so many characters, wrapped to the width given.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The length, the size, or the width isn't positive.</exception>
    public TextArea(string text, int maxLength, int fontSize, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, Padding * 2);
        _maxLength = maxLength;
        _fontSize = fontSize;
        _width = width;
        Text = text;
    }

    /// <summary>The text in the box.</summary>
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

            Wrap();
        }
    }

    /// <summary>How tall a box showing so many lines is.</summary>
    public int HeightFor(int lines) => (lines * (_fontSize + LineGap)) - LineGap + (Padding * 2);

    /// <summary>Takes this frame's typing, and tells whether the text changed. Call it only for the box that has focus.</summary>
    public bool Update()
    {
        var length = _text.Length;
        var changed = false;
        for (var key = Raylib.GetCharPressed(); key != 0; key = Raylib.GetCharPressed())
        {
            Add((char)key);
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && _text.Length > 0)
        {
            _text.Length--;
            changed = true;
        }

        changed |= _text.Length != length;
        if (changed)
        {
            Wrap();
        }

        return changed;
    }

    /// <summary>
    /// Draws the box in its bounds, with a caret at the end when it has focus. Lines past what the
    /// box can show are left out from the top, so the end being typed stays in view.
    /// </summary>
    public void Draw(Rectangle bounds, bool focused)
    {
        Raylib.DrawRectangleRec(bounds, Theme.Bar);
        Raylib.DrawRectangleLinesEx(bounds, 1f, focused ? Theme.Accent : Theme.PanelBorder);
        var room = Math.Max(1, ((int)bounds.Height - (Padding * 2) + LineGap) / (_fontSize + LineGap));
        var first = Math.Max(0, _lines.Count - room);
        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Padding;
        for (var i = first; i < _lines.Count; i++)
        {
            Raylib.DrawText(_lines[i], x, y, _fontSize, Theme.Text);
            y += _fontSize + LineGap;
        }

        if (focused && (int)(Raylib.GetTime() * 2) % 2 == 0)
        {
            var last = _lines.Count == 0 ? string.Empty : _lines[^1];
            var caretY = (int)bounds.Y + Padding + ((Math.Max(_lines.Count, 1) - 1 - first) * (_fontSize + LineGap));
            Raylib.DrawRectangle(x + Raylib.MeasureText(last, _fontSize) + 2, caretY, 2, _fontSize, Theme.Text);
        }
    }

    private void Wrap() => _lines = _text.Length == 0 ? [] : TextWrap.Lines(_text.ToString(), _fontSize, _width - (Padding * 2) - 4);

    private void Add(char c)
    {
        if (_text.Length < _maxLength && c is >= ' ' and <= '~')
        {
            _text.Append(c);
        }
    }
}
