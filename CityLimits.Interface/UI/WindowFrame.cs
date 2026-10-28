namespace CityLimits.UI;

/// <summary>
/// The frame every window shares: a unified title bar, macOS-style, over the window's own content.
/// The bar holds a red close button at its left, the title in its middle, and toolbar items, such
/// as the settings window's tabs, at its right; the player moves the window by dragging it. A
/// window can let the player edit its title in place, as the city panel does for the city's name;
/// it is off unless the window turns it on. Clicking the title starts an edit, Enter applies it if
/// the window's check allows it, and Escape or a click elsewhere drops it.
/// </summary>
public sealed class WindowFrame
{
    /// <summary>How tall the title bar is.</summary>
    public const int TitleHeight = 30;

    private const int TitleSize = 16;
    private const int ToolbarSize = 14;
    private const int ProblemSize = 10;
    private const int ToolbarPadding = 10;
    private const int ToolbarGap = 4;
    private const int Margin = 8;
    private const int CloseRadius = 6;
    private const int CloseHit = 20;
    private const int EditWidth = 260;
    private const string Ellipsis = "...";

    private readonly WindowPlacement _placement = new(TitleHeight);
    private readonly TextField _titleField;
    private string _title;
    private IReadOnlyList<string> _toolbar = [];

    // The title as drawn, cut to fit the bar, worked out again only when the title or room changes.
    private string _shownTitle = string.Empty;
    private string? _shownFor;
    private float _shownRoom = -1f;

    /// <summary>Creates a frame with a title of at most the given length.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The length isn't positive.</exception>
    public WindowFrame(string title, int maxTitleLength = 80)
    {
        _title = title ?? string.Empty;
        _titleField = new TextField(_title, maxTitleLength, _ => true, TitleSize);
    }

    /// <summary>What the frame's input in a frame came to.</summary>
    public enum Action
    {
        /// <summary>Nothing the window needs to act on.</summary>
        None,

        /// <summary>The close button was clicked.</summary>
        Close,

        /// <summary>A toolbar item was clicked; <see cref="ClickedTool"/> says which.</summary>
        Toolbar,

        /// <summary>The player entered a new title, which <see cref="Title"/> now holds.</summary>
        Renamed,
    }

    /// <summary>The window's title.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            if (!IsEditingTitle)
            {
                _titleField.Text = _title;
            }
        }
    }

    /// <summary>Whether the player may edit the title in place; off unless the window turns it on.</summary>
    public bool TitleEditable { get; set; }

    /// <summary>
    /// The window's check of an edited title: why it can't be used, or null when it can. Without
    /// one, any title the field takes is used.
    /// </summary>
    public Func<string, string?>? CheckTitle { get; set; }

    /// <summary>Tells whether the player is editing the title. While they are, the keyboard is the title's.</summary>
    public bool IsEditingTitle { get; private set; }

    /// <summary>Why the title being edited can't be used, after the player pressed Enter; null otherwise.</summary>
    public string? TitleProblem { get; private set; }

    /// <summary>The toolbar's items, in the order they run along the bar, the last at its right end.</summary>
    public IReadOnlyList<string> Toolbar
    {
        get => _toolbar;
        set => _toolbar = value ?? [];
    }

    /// <summary>The toolbar item shown as selected, such as the tab that is open, or -1 for none.</summary>
    public int SelectedTool { get; set; } = -1;

    /// <summary>The toolbar item last clicked, when <see cref="Update"/> says <see cref="Action.Toolbar"/>.</summary>
    public int ClickedTool { get; private set; } = -1;

    /// <summary>Tells whether the player is dragging the window.</summary>
    public bool IsDragging => _placement.IsDragging;

    /// <summary>The area under the title bar, where the window draws its own content.</summary>
    public static Rectangle Content(Rectangle bounds) =>
        bounds with { Y = bounds.Y + TitleHeight, Height = Math.Max(0f, bounds.Height - TitleHeight) };

    /// <summary>Where a window of the given size is: where the player left it, or at the start given, kept on screen.</summary>
    public Rectangle Place(Vector2 size, Vector2 start) => _placement.Bounds(size, start);

    /// <summary>
    /// Starts a drag when a press lands on the title bar, but not on its close button, a toolbar
    /// item, or a title that can be edited. Tells whether it did.
    /// </summary>
    public bool BeginDrag(Vector2 point, Rectangle bounds)
    {
        if (OnClose(point, bounds) || ToolAt(point, bounds) >= 0 || (TitleEditable && Raylib.CheckCollisionPointRec(point, TitleBounds(bounds))))
        {
            return false;
        }

        return _placement.BeginDrag(point, bounds);
    }

    /// <summary>Moves the window with the pointer while the button is held, and lets it go when it isn't.</summary>
    public void Drag(Vector2 point, bool held, Vector2 size) => _placement.Drag(point, held, size);

    /// <summary>
    /// For a window nothing else drags: starts a drag on a press on the bar, and moves the window
    /// while the button is held. Tells whether the window is being dragged, when the rest of the
    /// frame's input is the drag's.
    /// </summary>
    public bool FollowPointer(Rectangle bounds)
    {
        var mouse = Raylib.GetMousePosition();
        if (!IsDragging && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            BeginDrag(mouse, bounds);
        }

        if (IsDragging)
        {
            Drag(mouse, Raylib.IsMouseButtonDown(MouseButton.Left), new Vector2(bounds.Width, bounds.Height));
        }

        return IsDragging;
    }

    /// <summary>Tells whether a point is on the close button.</summary>
    public static bool OnClose(Vector2 point, Rectangle bounds) => Raylib.CheckCollisionPointRec(point, CloseBounds(bounds));

    /// <summary>Drops an edit of the title, as closing the window does.</summary>
    public void CancelEdit()
    {
        IsEditingTitle = false;
        TitleProblem = null;
        _titleField.Text = _title;
    }

    /// <summary>
    /// Takes this frame's clicks on the bar, and the typing while the title is edited. Call it
    /// before the window's own input: while <see cref="IsEditingTitle"/> was true at the start of
    /// the frame, the keyboard was the title's, and the window should leave Enter and Escape alone.
    /// </summary>
    public Action Update(Rectangle bounds)
    {
        var mouse = Raylib.GetMousePosition();
        var pressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        if (IsEditingTitle)
        {
            return UpdateEdit(bounds, mouse, pressed);
        }

        if (!pressed)
        {
            return Action.None;
        }

        if (OnClose(mouse, bounds))
        {
            return Action.Close;
        }

        var tool = ToolAt(mouse, bounds);
        if (tool >= 0)
        {
            ClickedTool = tool;
            return Action.Toolbar;
        }

        if (TitleEditable && Raylib.CheckCollisionPointRec(mouse, TitleBounds(bounds)))
        {
            _titleField.Text = _title;
            TitleProblem = null;
            IsEditingTitle = true;
        }

        return Action.None;
    }

    /// <summary>Draws the window's background, border, and title bar; the window draws its content over <see cref="Content"/>.</summary>
    public void Draw(Rectangle bounds)
    {
        var mouse = Raylib.GetMousePosition();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        var bar = bounds with { Height = TitleHeight };
        Raylib.DrawRectangleRec(bar, Theme.TitleBar);
        Raylib.DrawLine((int)bounds.X, (int)(bounds.Y + TitleHeight), (int)(bounds.X + bounds.Width), (int)(bounds.Y + TitleHeight), Theme.PanelBorder);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        // The close button: a red dot, marked with an x while the pointer is over it, as macOS has it.
        var close = CloseBounds(bounds);
        var (centerX, centerY) = ((int)(close.X + (CloseHit / 2)), (int)(close.Y + (CloseHit / 2)));
        Raylib.DrawCircle(centerX, centerY, CloseRadius, Theme.CloseButton);
        if (Raylib.CheckCollisionPointRec(mouse, close))
        {
            Raylib.DrawLine(centerX - 3, centerY - 3, centerX + 3, centerY + 3, Theme.CloseMark);
            Raylib.DrawLine(centerX - 3, centerY + 3, centerX + 3, centerY - 3, Theme.CloseMark);
        }

        for (var i = 0; i < _toolbar.Count; i++)
        {
            var item = ToolBounds(bounds, i);
            if (i == SelectedTool)
            {
                Raylib.DrawRectangleRec(item, Theme.ToolbarSelected);
            }

            var color = i == SelectedTool ? Theme.Accent : Raylib.CheckCollisionPointRec(mouse, item) ? Theme.Text : Theme.TextDim;
            Raylib.DrawText(_toolbar[i], (int)item.X + ToolbarPadding, (int)(item.Y + ((item.Height - ToolbarSize) / 2)), ToolbarSize, color);
        }

        var title = TitleBounds(bounds);
        if (IsEditingTitle)
        {
            _titleField.Draw(title, focused: true);
            if (TitleProblem is not null)
            {
                Raylib.DrawText(TitleProblem, (int)title.X, (int)(bounds.Y + TitleHeight + 2), ProblemSize, Theme.Error);
            }

            return;
        }

        var shown = Shown(title.Width);
        var width = Raylib.MeasureText(shown, TitleSize);
        var titleColor = TitleEditable && Raylib.CheckCollisionPointRec(mouse, title) ? Theme.Accent : Theme.Text;
        Raylib.DrawText(shown, (int)(title.X + ((title.Width - width) / 2)), (int)(bounds.Y + ((TitleHeight - TitleSize) / 2)), TitleSize, titleColor);
    }

    private Action UpdateEdit(Rectangle bounds, Vector2 mouse, bool pressed)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || (pressed && !Raylib.CheckCollisionPointRec(mouse, TitleBounds(bounds))))
        {
            CancelEdit();
            return Action.None;
        }

        if (_titleField.Update())
        {
            TitleProblem = null;
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Enter) && !Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return Action.None;
        }

        var edited = _titleField.Text.Trim();
        TitleProblem = CheckTitle?.Invoke(edited);
        if (TitleProblem is not null)
        {
            return Action.None;
        }

        _title = edited;
        IsEditingTitle = false;
        return Action.Renamed;
    }

    // The title as it fits the room, cut with an ellipsis when it doesn't; kept until either changes.
    private string Shown(float room)
    {
        if (ReferenceEquals(_shownFor, _title) && _shownRoom == room)
        {
            return _shownTitle;
        }

        (_shownFor, _shownRoom) = (_title, room);
        _shownTitle = _title;
        if (Raylib.MeasureText(_title, TitleSize) > room)
        {
            var length = _title.Length;
            while (length > 0 && Raylib.MeasureText(_title[..length] + Ellipsis, TitleSize) > room)
            {
                length--;
            }

            _shownTitle = _title[..length] + Ellipsis;
        }

        return _shownTitle;
    }

    private static Rectangle CloseBounds(Rectangle bounds) =>
        new() { X = bounds.X + Margin - 2, Y = bounds.Y + ((TitleHeight - CloseHit) / 2), Width = CloseHit, Height = CloseHit };

    // The title sits between the close button and the toolbar: centred in the window when it fits
    // there, as macOS has it, and otherwise in all the room between them.
    private Rectangle TitleBounds(Rectangle bounds)
    {
        var left = bounds.X + Margin + CloseHit + Margin;
        var right = _toolbar.Count > 0 ? ToolBounds(bounds, 0).X - Margin : bounds.X + bounds.Width - Margin;
        var between = Math.Max(0f, right - left);
        var side = Math.Max(left - bounds.X, bounds.X + bounds.Width - right);
        var centred = Math.Max(0f, bounds.Width - (side * 2));
        var wanted = IsEditingTitle ? EditWidth : Raylib.MeasureText(_title, TitleSize);
        var (x, room) = centred >= wanted
            ? (bounds.X + ((bounds.Width - centred) / 2), centred)
            : (left, between);
        return new Rectangle { X = x, Y = bounds.Y + 3, Width = room, Height = TitleHeight - 6 };
    }

    private int ToolAt(Vector2 point, Rectangle bounds)
    {
        for (var i = 0; i < _toolbar.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, ToolBounds(bounds, i)))
            {
                return i;
            }
        }

        return -1;
    }

    // The items run leftward from the bar's right end, the last first, as the button row does.
    private Rectangle ToolBounds(Rectangle bounds, int index)
    {
        var right = bounds.X + bounds.Width - Margin;
        for (var i = _toolbar.Count - 1; i >= 0; i--)
        {
            var width = Raylib.MeasureText(_toolbar[i], ToolbarSize) + (ToolbarPadding * 2);
            if (i == index)
            {
                return new Rectangle { X = right - width, Y = bounds.Y + 4, Width = width, Height = TitleHeight - 8 };
            }

            right -= width + ToolbarGap;
        }

        return default;
    }
}
