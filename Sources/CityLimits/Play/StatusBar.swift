import CityLimitsCore
import Hudini
import Raylib

/// The bar along the bottom of the screen (`Interface/status.uix`), with the news ticker under it:
/// the pause or play button, the month, the speed, the city's name, the demand meter, the funds,
/// and the population, with the frame rate and the help button on the right.
@MainActor
final class StatusBar {
    static let height: Float = 54

    /// What a click on the bar asks for.
    enum Action: Equatable {
        case pausePlay
        case speed(GameSpeed)
        case cityName
        case demand
        case funds
        case help
        case link(NewsItem)
    }

    private static let speeds: [GameSpeed] = [.normal, .fast, .superFast]
    private static let meterBarWidth: Float = 6

    let ticker: NewsTicker
    private let view: UIScriptView

    init() {
        ticker = NewsTicker()
        view = Interface.view("status", fallbackSize: Vector2(x: 1024, y: Self.height))
    }

    var origin: Vector2 { Vector2(x: 0, y: Float(GetScreenHeight()) - Self.height) }

    func contains(_ point: Vector2) -> Bool { point.y >= origin.y }

    /// Fits the bar to the screen, shows the city, under `name` in place of its own when one is
    /// given, moves the ticker on, and answers a click.
    func update(city: CitySimulation, name: String? = nil, speed: GameSpeed, elapsed: Float, takesInput: Bool) -> Action? {
        view.setWindowSize(Vector2(x: Float(GetScreenWidth()), y: Self.height))
        let date = city.date
        view.setText("\(date.month < 10 ? "0" : "")\(date.month)/\(date.year)", named: "date")
        view.setText(name ?? city.name, named: "cityName")
        let funds = city.treasury.funds
        view.setText(Figures.money(funds), named: funds < 0 ? "debt" : "funds")
        view.setHidden(funds < 0, named: "funds")
        view.setHidden(funds >= 0, named: "debt")
        view.setText("\(Figures.count(city.census.cityPopulation)) Pop", named: "population")
        view.setText("\(GetFPS()) FPS", named: "rate")

        if let tickerBounds = bounds("ticker") {
            ticker.update(speed == .paused ? 0 : elapsed, width: tickerBounds.width, startShare: 1.0 / 3)
        }
        guard takesInput else { return nil }

        var action: Action?
        for case .pressed(let name) in view.update(at: origin) {
            switch name {
            case "cityName": action = .cityName
            case "funds", "debt": action = .funds
            default: break
            }
        }
        guard action == nil, IsMouseButtonPressed(.left) else { return action }
        let pointer = GetMousePosition()
        if hits("pausePlay", pointer) { return .pausePlay }
        if let speeds = bounds("speeds"), CheckCollisionPointRec(pointer, speeds) {
            let index = Int((pointer.x - speeds.x) / (speeds.width / Float(Self.speeds.count)))
            return .speed(Self.speeds[min(max(index, 0), Self.speeds.count - 1)])
        }
        if hits("demand", pointer) { return .demand }
        if hits("help", pointer) { return .help }
        if let tickerBounds = bounds("ticker"), let item = ticker.link(at: pointer, in: tickerBounds) { return .link(item) }
        return nil
    }

    private func bounds(_ name: String) -> Rectangle? { view.bounds(named: name, at: origin) }

    private func hits(_ name: String, _ point: Vector2) -> Bool {
        bounds(name).map { CheckCollisionPointRec(point, $0) } ?? false
    }

    // MARK: Drawing

    func draw(city: CitySimulation, speed: GameSpeed, resumeSpeed: GameSpeed) {
        let origin = origin
        DrawRectangleRec(Rectangle(x: 0, y: origin.y, width: Float(GetScreenWidth()), height: Self.height), Palette.bar)
        view.draw(at: origin) { name, bounds in
            switch name {
            case "pausePlay": Self.drawPausePlay(bounds, paused: speed == .paused)
            case "speeds": Self.drawSpeeds(bounds, speed: speed, resumeSpeed: resumeSpeed)
            case "demand": Self.drawDemand(bounds, city.demand)
            case "help": Self.drawHelp(bounds)
            case "ticker": ticker.draw(in: bounds)
            default: break
            }
        }
    }

    // Blue, as a lit speed is, so the button doubles as a sign the city is running.
    private static func drawPausePlay(_ bounds: Rectangle, paused: Bool) {
        let (cx, cy) = (bounds.x + bounds.width / 2, bounds.y + bounds.height / 2)
        if paused {
            DrawTriangle(Vector2(x: cx - 5, y: cy - 7), Vector2(x: cx - 5, y: cy + 7), Vector2(x: cx + 7, y: cy), Palette.speedLit)
        } else {
            DrawRectangleRec(Rectangle(x: cx - 6, y: cy - 7, width: 4, height: 14), Palette.speedLit)
            DrawRectangleRec(Rectangle(x: cx + 2, y: cy - 7, width: 4, height: 14), Palette.speedLit)
        }
    }

    // The arrows light up to the speed, so two lit is Fast. While paused they show, dimmed, the
    // speed play brings back.
    private static func drawSpeeds(_ bounds: Rectangle, speed: GameSpeed, resumeSpeed: GameSpeed) {
        let shown = speed == .paused ? resumeSpeed : speed
        let width = bounds.width / Float(speeds.count)
        let cy = bounds.y + bounds.height / 2
        for (index, each) in speeds.enumerated() {
            let left = bounds.x + Float(index) * width
            let color = each.rawValue <= shown.rawValue ? (speed == .paused ? Palette.speedResume : Palette.speedLit) : Palette.speedUnlit
            // raylib wants a triangle's corners anticlockwise.
            DrawTriangle(Vector2(x: left + 2, y: cy - 6), Vector2(x: left + 2, y: cy + 6), Vector2(x: left + width - 3, y: cy), color)
        }
    }

    // As Micropolis shows demand: each bar rises from the middle when the city wants more of the
    // kind and falls below it when it wants less, as far as its limit allows.
    private static func drawDemand(_ bounds: Rectangle, _ demand: CityDemand) {
        let middle = bounds.y + bounds.height / 2
        DrawLineV(Vector2(x: bounds.x, y: middle), Vector2(x: bounds.x + bounds.width, y: middle), Palette.textDim)
        let bars: [(Int, Int, Color)] = [
            (demand.residential, CityDemand.residentialLimit, Palette.residential),
            (demand.commercial, CityDemand.businessLimit, Palette.commercial),
            (demand.industrial, CityDemand.businessLimit, Palette.industrial),
        ]
        for (index, (value, limit, color)) in bars.enumerated() {
            let length = (Float(min(max(value, -limit), limit)) / Float(limit) * bounds.height / 2).rounded()
            let left = bounds.x + 3 + Float(index) * (meterBarWidth + 2)
            DrawRectangleRec(Rectangle(x: left, y: length >= 0 ? middle - length : middle, width: meterBarWidth, height: abs(length)), color)
        }
    }

    // A green circle with a dark question mark.
    private static func drawHelp(_ bounds: Rectangle) {
        let radius = bounds.width / 2
        DrawCircleV(Vector2(x: bounds.x + radius, y: bounds.y + radius), radius, Palette.helpButton)
        let size: Int32 = 14
        let x = Int32(bounds.x + radius) - MeasureText("?", size) / 2
        let y = Int32(bounds.y + (bounds.height - Float(size)) / 2) + 1
        DrawText("?", x, y, size, Palette.hintText)
    }
}
