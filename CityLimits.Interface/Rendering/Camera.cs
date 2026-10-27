namespace CityLimits.Rendering;

/// <summary>The view of the map: where it is centred, how far it is zoomed in, and how the two spaces relate.</summary>
public sealed class Camera
{
    /// <summary>The smallest zoom the camera allows.</summary>
    public const float MinZoom = 0.25f;

    /// <summary>The largest zoom the camera allows.</summary>
    public const float MaxZoom = 4f;

    private const float ZoomStep = 0.15f;

    private Vector2 _target;

    /// <summary>Creates a camera looking at the middle of the world.</summary>
    public Camera(float zoom = 1f) => Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

    /// <summary>The world point at the middle of the screen.</summary>
    public Vector2 Target => _target;

    /// <summary>How much the world is magnified.</summary>
    public float Zoom { get; private set; }

    /// <summary>The size of the screen, in pixels.</summary>
    public Vector2 ScreenSize { get; private set; } = new(1280, 720);

    /// <summary>Tells the camera how big the screen is.</summary>
    public void SetScreenSize(int width, int height) => ScreenSize = new Vector2(width, height);

    /// <summary>Points the camera at a place in the world.</summary>
    public void CenterOn(Vector2 worldPoint) => _target = worldPoint;

    /// <summary>Sets how near the camera looks, within its limits.</summary>
    public void SetZoom(float zoom) => Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

    /// <summary>Moves the camera by a distance in world units.</summary>
    public void Pan(Vector2 worldDelta) => _target += worldDelta;

    /// <summary>Zooms in or out, keeping the world point under the given screen point still.</summary>
    public void ZoomAt(Vector2 screenPoint, float wheelDelta)
    {
        if (wheelDelta == 0f)
        {
            return;
        }

        var before = ScreenToWorld(screenPoint);
        var factor = 1f + (wheelDelta * ZoomStep);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var after = ScreenToWorld(screenPoint);
        _target += before - after;
    }

    /// <summary>Keeps the view over the map, or centred on it when the map is smaller than the view.</summary>
    public void ClampTo(float worldWidth, float worldHeight)
    {
        var halfWidth = ScreenSize.X / (2f * Zoom);
        var halfHeight = ScreenSize.Y / (2f * Zoom);

        _target.X = worldWidth <= halfWidth * 2f ? worldWidth / 2f : Math.Clamp(_target.X, halfWidth, worldWidth - halfWidth);
        _target.Y = worldHeight <= halfHeight * 2f ? worldHeight / 2f : Math.Clamp(_target.Y, halfHeight, worldHeight - halfHeight);
    }

    /// <summary>Converts a world point to a screen point.</summary>
    public Vector2 WorldToScreen(Vector2 world) => ((world - _target) * Zoom) + (ScreenSize / 2f);

    /// <summary>Converts a screen point to a world point.</summary>
    public Vector2 ScreenToWorld(Vector2 screen) => ((screen - (ScreenSize / 2f)) / Zoom) + _target;

    /// <summary>Builds the camera the renderer draws with.</summary>
    public Camera2D ToRaylib() => new()
    {
        Offset = ScreenSize / 2f,
        Target = _target,
        Rotation = 0f,
        Zoom = Zoom,
    };
}
