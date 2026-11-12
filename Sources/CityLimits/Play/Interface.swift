import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// The game's UIScript documents, in `Resources/Interface`.
@MainActor
enum Interface {
    /// Hudini's standard colours, which the game's own match, with opaque panels so windows that
    /// overlap don't show through each other or the map.
    static let theme = InterfaceTheme(panel: Palette.rgb(26, 30, 38))

    /// The view of a document. A document that's missing or can't be read is reported, and an
    /// empty one of the given size stands in, so the game goes on without that part of the screen.
    static func view(_ name: String, fallbackSize: Vector2) -> UIScriptView {
        do {
            guard let url = Bundle.module.url(forResource: name, withExtension: "uix", subdirectory: "Interface") else {
                throw CocoaError(.fileNoSuchFile)
            }
            let view = UIScriptView(script: try UIScript(contentsOf: url), theme: theme)
            for missing in view.missingImages + view.missingStrings {
                warn("Interface/\(name).uix refers to \(missing), which it doesn't have")
            }
            return view
        } catch {
            warn("Interface/\(name).uix couldn't be read, so it's left out: \(error)")
            return UIScriptView(script: empty(fallbackSize), theme: theme)
        }
    }

    private static func empty(_ size: Vector2) -> UIScript {
        let text = #"<UIScript version="1"><Window width="\#(Int(size.x))" height="\#(Int(size.y))"/></UIScript>"#
        // A document this plain always reads; if it somehow didn't, there'd be nothing to show.
        guard let script = try? UIScript(data: Data(text.utf8)) else { preconditionFailure("an empty UIScript document didn't read") }
        return script
    }
}

/// Writes a warning to the standard error, where the game's other warnings go.
func warn(_ line: String) {
    FileHandle.standardError.write(Data("warning: \(line)\n".utf8))
}

/// How the bar and the windows write money and counts.
enum Figures {
    /// An amount of money, such as $20,000 or -$1,500.
    static func money(_ amount: Int) -> String {
        amount < 0 ? "-$\(NewsDesk.grouped(-amount))" : "$\(NewsDesk.grouped(amount))"
    }

    static func count(_ value: Int) -> String { NewsDesk.grouped(value) }
}
