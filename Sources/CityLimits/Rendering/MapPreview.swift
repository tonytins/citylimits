import CityLimitsCore
import Raylib

/// A map drawn small in flat colours, as the overview's City mode draws it, for the windows that
/// show a city before it's opened. It's drawn into a texture once per map; without the texture,
/// or without a map, it says why in place of the picture.
@MainActor
final class MapPreview {
    private static let pixelsPerTile = 2

    private var texture: RenderTexture2D?
    private var waiting: TileMap?
    private(set) var note: String?

    /// Shows a map, drawn before the next frame.
    func show(_ map: TileMap) {
        waiting = map
        note = nil
    }

    /// Shows a line in place of a map.
    func show(note: String) {
        waiting = nil
        self.note = note
        if let texture { UnloadRenderTexture(texture) }
        texture = nil
    }

    /// Draws the map waiting, if any. Call before `BeginDrawing`.
    func refresh() {
        guard let map = waiting else { return }
        waiting = nil
        let (width, height) = (Int32(map.width * Self.pixelsPerTile), Int32(map.height * Self.pixelsPerTile))
        if let current = texture, current.texture.width != width || current.texture.height != height {
            UnloadRenderTexture(current)
            texture = nil
        }
        if texture == nil {
            let made = LoadRenderTexture(width, height)
            guard IsRenderTextureValid(made) else {
                note = "The map couldn't be drawn"
                return
            }
            texture = made
        }
        guard let texture else { return }
        BeginTextureMode(texture)
        ClearBackground(.blank)
        let pixels = Float(Self.pixelsPerTile)
        for y in 0..<map.height {
            for x in 0..<map.width {
                DrawRectangleRec(Rectangle(x: Float(x) * pixels, y: Float(y) * pixels, width: pixels, height: pixels), Palette.tile(map[x, y]))
            }
        }
        EndTextureMode()
    }

    /// Draws the map as big as fits `bounds`, keeping its shape, or the note in its place.
    func draw(in bounds: Rectangle) {
        DrawRectangleRec(bounds, Palette.bar)
        guard note == nil, let texture else {
            let line = note ?? ""
            let width = Float(MeasureText(line, 12))
            DrawText(line, Int32(bounds.x + (bounds.width - width) / 2), Int32(bounds.y + bounds.height / 2 - 6), 12, Palette.textDim)
            return
        }
        let (width, height) = (Float(texture.texture.width), Float(texture.texture.height))
        let scale = min(bounds.width / width, bounds.height / height)
        let target = Rectangle(
            x: bounds.x + (bounds.width - width * scale) / 2, y: bounds.y + (bounds.height - height * scale) / 2,
            width: width * scale, height: height * scale)
        // A render texture is stored upside down, so it's drawn with a negative height.
        DrawTexturePro(texture.texture, Rectangle(x: 0, y: 0, width: width, height: -height), target, .zero, 0, .white)
    }

    /// Releases the texture. Call before `CloseWindow`.
    func unload() {
        if let texture { UnloadRenderTexture(texture) }
        texture = nil
    }
}
