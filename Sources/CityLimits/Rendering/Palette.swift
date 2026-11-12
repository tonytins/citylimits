import CityLimitsCore
import Raylib

/// Every colour the game draws with, in one place. The flat tile colours are only for a missing
/// tile sheet.
enum Palette {
    static func rgb(_ r: UInt8, _ g: UInt8, _ b: UInt8, _ a: UInt8 = 255) -> Color { Color(r: r, g: g, b: b, a: a) }

    static let background = rgb(12, 14, 18)
    static let splash = rgb(78, 95, 187)
    static let grid = rgb(255, 255, 255, 28)
    static let hover = rgb(255, 235, 120)
    static let error = rgb(232, 106, 96)
    static let accent = rgb(240, 190, 90)

    static let panel = rgb(22, 26, 32, 225)
    static let panelBorder = rgb(72, 80, 92)
    static let titleBar = rgb(38, 43, 52, 245)
    static let text = rgb(228, 232, 238)
    static let textDim = rgb(150, 158, 170)
    static let bar = rgb(0, 0, 0)
    static let speedLit = rgb(62, 110, 190)
    static let speedResume = rgb(46, 70, 110)
    static let speedUnlit = rgb(217, 217, 217)
    static let helpButton = rgb(74, 138, 66)

    static let residential = rgb(74, 138, 66)
    static let commercial = rgb(62, 110, 190)
    static let industrial = rgb(198, 156, 58)

    static let hintBackground = rgb(250, 215, 80)
    static let hintText = rgb(0, 0, 0)

    static let newsHeadline = rgb(240, 240, 240)
    static let newsAdvisory = rgb(250, 210, 60)
    static let newsDisaster = rgb(235, 70, 60)
    static let newsOrdinance = rgb(110, 170, 250)

    static let overviewPowered = rgb(240, 70, 50)
    static let overviewUnpowered = rgb(110, 120, 230)
    static let overviewLine = rgb(190, 190, 190)
    /// Low to high, for the overview's maps of land value, pollution, crime, and the rest.
    static let overlayBands = [rgb(80, 140, 200), rgb(90, 200, 110), rgb(240, 210, 60), rgb(235, 80, 50)]

    static let flood = rgb(60, 110, 200)
    static let radiation = rgb(200, 230, 40)
    static let fire = rgb(255, 110, 20)
    static let unpowered = rgb(250, 220, 40)

    static func ground(_ kind: TileKind) -> Color {
        switch kind {
        case .grass: rgb(92, 132, 74)
        case .water: rgb(48, 96, 148)
        case .trees: rgb(44, 96, 56)
        case .rubble: rgb(128, 126, 120)
        }
    }

    static func tile(_ state: TileState) -> Color {
        if state.isRail { return rgb(120, 96, 70) }
        if state.isRoad { return rgb(66, 66, 70) }
        switch state.use {
        case .none: return ground(state.kind)
        case .powerLine: return rgb(210, 190, 80)
        case .park: return rgb(70, 160, 70)
        case .hospital, .church: return rgb(220, 220, 220)
        case .fireStation: return rgb(200, 60, 50)
        case .policeStation: return rgb(60, 80, 190)
        case .residential: return residential
        case .commercial: return commercial
        case .industrial: return industrial
        case .coalPowerPlant, .nuclearPowerPlant: return rgb(150, 150, 160)
        case .stadium, .seaport, .airport: return rgb(180, 170, 120)
        default: return ground(state.kind)
        }
    }
}
