namespace CityLimits.UI;

/// <summary>
/// A window the player moves by dragging its title bar, such as the budget, the demand panel, or
/// the city panel. Each keeps its place with its <see cref="WindowFrame"/>, and the game drags
/// any of them the same way: a press that starts a drag makes it the window being dragged, and it
/// follows the pointer until the button is let go.
/// </summary>
public interface IMovableWindow
{
    /// <summary>Where the window is on the screen.</summary>
    Rectangle Bounds { get; }

    /// <summary>Tells whether the player is dragging the window.</summary>
    bool IsDragging { get; }

    /// <summary>Starts a drag when a press lands on the window's title bar. Tells whether it did.</summary>
    bool BeginDrag(Vector2 point);

    /// <summary>Moves the window with the pointer while the button is held, and lets it go when it isn't.</summary>
    void Drag(Vector2 point, bool held);
}
