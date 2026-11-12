import CityLimitsCore
import Raylib

/// The tool palette in the top-left corner: Micropolis' icons in its rows, with the name and
/// price of the tool under the pointer, or the one held, above them.
@MainActor
final class ToolPaletteView {
    private static let fontSize: Int32 = 14
    private static let lineHeight: Float = 18
    private static let padding: Float = 8
    private static let gap: Float = 2
    private static let top: Float = 8
    private static let missingIconSize: Float = 34

    private let assets: GameAssets
    private let definitions: CityDefinitions
    private(set) var bounds = Rectangle()
    private var buttons: [(tool: Tool, bounds: Rectangle)] = []
    /// The tool held, or nil for none.
    private(set) var selected: Tool?

    init(assets: GameAssets, definitions: CityDefinitions) {
        self.assets = assets
        self.definitions = definitions
        layOut()
    }

    private func iconSize(_ tool: Tool) -> (width: Float, height: Float) {
        guard let icon = assets.icon(tool, selected: false) else { return (Self.missingIconSize, Self.missingIconSize) }
        return (Float(icon.width), Float(icon.height))
    }

    private func rowWidth(_ row: [Tool]) -> Float {
        row.reduce(-Self.gap) { $0 + iconSize($1).width + Self.gap }
    }

    // The panel is as wide as its widest row of buttons, or its widest header.
    private func layOut() {
        var widest = Float(MeasureText("pick one below", Self.fontSize))
        for row in Tool.rows {
            widest = max(widest, rowWidth(row))
            for tool in row {
                widest = max(widest, Float(MeasureText(tool.name, Self.fontSize)), Float(MeasureText(tool.price(definitions), Self.fontSize)))
            }
        }
        var y = Self.top + Self.padding + 2 * Self.lineHeight + Self.padding
        for row in Tool.rows {
            var x = Self.padding * 2 + (widest - rowWidth(row)) / 2
            var height: Float = 0
            for tool in row {
                let size = iconSize(tool)
                buttons.append((tool, Rectangle(x: x, y: y, width: size.width, height: size.height)))
                x += size.width + Self.gap
                height = max(height, size.height)
            }
            y += height + Self.gap
        }
        bounds = Rectangle(x: Self.padding, y: Self.top, width: widest + Self.padding * 2, height: y - Self.top + Self.padding - Self.gap)
    }

    func contains(_ point: Vector2) -> Bool { CheckCollisionPointRec(point, bounds) }

    func tool(at point: Vector2) -> Tool? {
        buttons.first { CheckCollisionPointRec(point, $0.bounds) }?.tool
    }

    /// Picks a tool, or puts it down when it's already held.
    func toggle(_ tool: Tool) {
        selected = selected == tool ? nil : tool
    }

    func draw(pointer: Vector2) {
        DrawRectangleRec(bounds, Palette.panel)
        DrawRectangleLinesEx(bounds, 1, Palette.panelBorder)
        let (headerX, headerY) = (Int32(bounds.x + Self.padding), Int32(bounds.y + Self.padding))
        if let shown = tool(at: pointer) ?? selected {
            DrawText(shown.name, headerX, headerY, Self.fontSize, Palette.text)
            DrawText(shown.price(definitions), headerX, headerY + Int32(Self.lineHeight), Self.fontSize, Palette.textDim)
        } else {
            DrawText("No tool", headerX, headerY, Self.fontSize, Palette.text)
            DrawText("pick one below", headerX, headerY + Int32(Self.lineHeight), Self.fontSize, Palette.textDim)
        }
        for (tool, button) in buttons {
            // The held tool shows its highlighted icon, as Micropolis' pressed button does.
            let isSelected = tool == selected
            if let icon = assets.icon(tool, selected: isSelected) {
                DrawTexture(icon, Int32(button.x), Int32(button.y), .white)
            } else {
                // A missing icon keeps the tool's label, so the button still says what it is.
                DrawRectangleLinesEx(button, 1, Palette.textDim)
                DrawText(tool.shortName, Int32(button.x) + 3, Int32(button.y) + 3, 10, Palette.textDim)
                if isSelected { DrawRectangleLinesEx(button, 2, Palette.accent) }
            }
        }
    }
}

/// A hint beside the pointer, in black on yellow, of one line or two. It stays on the screen,
/// moving left or up from the pointer when an edge is near.
@MainActor
enum PointerHint {
    private static let fontSize: Int32 = 14
    private static let padding: Int32 = 6
    private static let line: Int32 = 18
    private static let margin: Int32 = 3

    static func draw(at pointer: Vector2, _ text: String, _ second: String? = nil, bottom: Int32) {
        guard !text.isEmpty else { return }
        let lines: Int32 = second == nil ? 1 : 2
        let textWidth = max(MeasureText(text, fontSize), second.map { MeasureText($0, fontSize) } ?? 0)
        let (width, height) = (textWidth + padding * 2, lines * line + margin * 2)
        var (x, y) = (Int32(pointer.x) + 16, Int32(pointer.y) + 16)
        if x + width > GetScreenWidth() { x = Int32(pointer.x) - 8 - width }
        if y + height > bottom { y = Int32(pointer.y) - 8 - height }
        DrawRectangle(x, y, width, height, Palette.hintBackground)
        let textY = y + margin + (line - fontSize) / 2
        DrawText(text, x + padding, textY, fontSize, Palette.hintText)
        if let second { DrawText(second, x + padding, textY + line, fontSize, Palette.hintText) }
    }
}
