import CityLimitsCore
import Hudini
import Raylib

extension UIScriptView {
    /// Shows `text` in the control called `name`, or in its red twin, `name` + "Warning", hiding
    /// the other, since a document's colours are fixed by its styles.
    func show(_ text: String, named name: String, warning: Bool) {
        let shown = warning ? name + "Warning" : name
        setText(text, named: shown)
        setHidden(warning, named: name)
        setHidden(!warning, named: name + "Warning")
    }

    /// Wraps `text` to the width of the control called `name`, at the size its style gives,
    /// keeping its own line breaks.
    func setWrapped(_ text: String, named name: String, fontSize: Int? = nil) {
        let width = Int(bounds(named: name, at: .zero)?.width ?? 300)
        let size = fontSize ?? theme.textSizes.body
        let lines = text.split(separator: "\n", omittingEmptySubsequences: false).flatMap { line in
            line.isEmpty ? [""] : TextWrapper.lines(for: String(line), fontSize: size, maximumWidth: width)
        }
        setText(lines.joined(separator: "\n"), named: name)
    }
}

/// Closes or minimizes a window that asks, as most of the game's windows want.
@MainActor
func closeOrMinimize(_ event: WindowFrame.Event, _ frame: WindowFrame, in windows: WindowManager) {
    switch event {
    case .closeRequested: windows.close(frame)
    case .minimizeRequested: windows.minimize(frame)
    default: break
    }
}

/// A yes-or-no question in a modal window that dims what's behind it. One window serves every
/// question: whoever asks passes the answers' labels and what to do with the answer.
@MainActor
final class ConfirmWindow {
    let frame: WindowFrame
    private let view = Interface.view("confirm", fallbackSize: Vector2(x: 380, y: 132))
    private var answer: ((Bool) -> Void)?

    init() {
        frame = WindowFrame(view: view)
        // A question needs an answer, so the window closes only through one of its buttons.
        frame.showsCloseButton = false
    }

    func ask(
        _ title: String, _ question: String, yes: String = "Yes", no: String = "No",
        in windows: WindowManager, then answer: @escaping (Bool) -> Void
    ) {
        frame.title = title
        view.setWrapped(question, named: "message")
        view.setText(yes, named: "yes")
        view.setText(no, named: "no")
        self.answer = answer
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .view(.pressed(control: "yes")): finish(true, in: windows)
            case .view(.pressed(control: "no")): finish(false, in: windows)
            default: break
            }
        }
    }

    private func finish(_ isYes: Bool, in windows: WindowManager) {
        windows.close(frame)
        let answer = self.answer
        self.answer = nil
        answer?(isYes)
    }
}

/// Something the player reads before going on, in a modal window that dims what's behind it.
@MainActor
final class NoticeWindow {
    let frame: WindowFrame
    private let view = Interface.view("notice", fallbackSize: Vector2(x: 380, y: 200))

    init() {
        frame = WindowFrame(view: view)
    }

    func show(_ title: String, _ message: String, in windows: WindowManager) {
        frame.title = title
        view.setWrapped(message, named: "message")
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .view(.pressed(control: "ok")), .closeRequested: windows.close(frame)
            default: break
            }
        }
    }
}

/// The budget: taxes and funding, in a modal window that pauses the city. It opens by itself
/// when a year needs attention, saying why.
@MainActor
final class BudgetWindow {
    static let fundingStep = 10

    let frame: WindowFrame
    private let view = Interface.view("budget", fallbackSize: Vector2(x: 460, y: 380))
    private let city: CitySimulation

    init(city: CitySimulation) {
        self.city = city
        frame = WindowFrame(view: view, title: "Budget")
    }

    /// Opens the budget, with the reason it opened by itself, if it did.
    func open(reason: String? = nil, in windows: WindowManager) {
        view.setText(reason ?? "", named: "reason")
        show()
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .view(.pressed(control: "continue")), .closeRequested: windows.close(frame)
            case .view(.pressed(control: let name)): change(name)
            default: break
            }
        }
    }

    // The tax rate moves a percent a press; funding moves a tenth. Each stops at its ends.
    private func change(_ button: String) {
        let budget = city.budget
        func step(_ level: Int, _ direction: Int) -> Int {
            min(max(level + direction * Self.fundingStep, 0), CityBudget.fullFunding)
        }
        let (roads, fire, police) = (budget.roadFunding, budget.fireFunding, budget.policeFunding)
        switch button {
        case "taxDown": _ = budget.setTaxRate(max(budget.taxRate - 1, 0))
        case "taxUp": _ = budget.setTaxRate(min(budget.taxRate + 1, CityBudget.maxTaxRate))
        case "roadsDown": _ = budget.setFunding(roads: step(roads, -1), fire: fire, police: police)
        case "roadsUp": _ = budget.setFunding(roads: step(roads, 1), fire: fire, police: police)
        case "fireDown": _ = budget.setFunding(roads: roads, fire: step(fire, -1), police: police)
        case "fireUp": _ = budget.setFunding(roads: roads, fire: step(fire, 1), police: police)
        case "policeDown": _ = budget.setFunding(roads: roads, fire: fire, police: step(police, -1))
        case "policeUp": _ = budget.setFunding(roads: roads, fire: fire, police: step(police, 1))
        default: return
        }
        show()
    }

    /// Shows the figures as they stand. Call while it's open, as the funds can change under it.
    func show() {
        let budget = city.budget
        let funds = city.treasury.funds
        view.show(Figures.money(funds), named: "funds", warning: funds < 0)
        let estimate = city.estimateBudget()
        view.setText("Taxes at \(budget.taxRate)%", named: "tax")
        view.setText("collects \(Figures.money(estimate.taxIncome))", named: "taxDetail")
        view.setText("Roads at \(budget.roadFunding)%", named: "roads")
        view.setText("asks \(Figures.money(estimate.roadRequest)), pays \(Figures.money(estimate.roadFunded))", named: "roadsDetail")
        view.setText("Fire at \(budget.fireFunding)%", named: "fire")
        view.setText("asks \(Figures.money(estimate.fireRequest)), pays \(Figures.money(estimate.fireFunded))", named: "fireDetail")
        view.setText("Police at \(budget.policeFunding)%", named: "police")
        view.setText("asks \(Figures.money(estimate.policeRequest)), pays \(Figures.money(estimate.policeFunded))", named: "policeDetail")
        view.setText(
            "Ordinances earn \(Figures.money(estimate.ordinanceIncome)), cost \(Figures.money(estimate.ordinanceCost))", named: "ordinances")
        view.setText("Deals with neighbours earn \(Figures.money(estimate.dealIncome)), cost \(Figures.money(estimate.dealCost))", named: "deals")
        let cut = estimate.cut ? ", funding cut: the money runs out" : ""
        view.show("Cash flow \(Figures.money(estimate.cashFlow))\(cut)", named: "cashFlow", warning: estimate.needsAttention)
        if let last = budget.lastYear {
            view.show(
                "Collected \(Figures.money(last.income)), paid \(Figures.money(last.expenses)), cash flow \(Figures.money(last.cashFlow))",
                named: "lastYear", warning: last.needsAttention)
        } else {
            view.show("The city hasn't settled a year yet", named: "lastYear", warning: false)
        }
    }

    /// Why a settled year opens the budget by itself, or nil when it doesn't need to: upkeep came
    /// to more than the taxes, or the money ran out and funding was cut.
    static func reason(for year: BudgetYear) -> String? {
        guard year.needsAttention else { return nil }
        return year.cut
            ? "The money ran out, so funding was cut"
            : "Upkeep came to \(Figures.money(year.expenses)), more than the \(Figures.money(year.taxIncome)) in taxes"
    }
}

/// Demand over time, in a window that stays open beside the map and minimizes.
@MainActor
final class DemandWindow {
    let frame: WindowFrame
    private let view = Interface.view("demand", fallbackSize: Vector2(x: 420, y: 300))
    private let city: CitySimulation

    init(city: CitySimulation) {
        self.city = city
        frame = WindowFrame(view: view, title: "Demand")
        frame.showsMinimizeButton = true
    }

    func open(in windows: WindowManager) {
        windows.open(frame) { [unowned self, unowned windows] event in closeOrMinimize(event, frame, in: windows) }
    }

    /// Shows the demand as it stands, and draws the chart into the document's region.
    func show() {
        let demand = city.demand, census = city.census
        view.setText(Self.span(demand.historyCount), named: "span")
        view.setText("R \(Self.signed(demand.residential))", named: "residential")
        view.setText("C \(Self.signed(demand.commercial))", named: "commercial")
        view.setText("I \(Self.signed(demand.industrial))", named: "industrial")
        view.setText(
            "Last move from the economy: R \(Self.signed(demand.residentialPull)), C \(Self.signed(demand.commercialPull)), I \(Self.signed(demand.industrialPull))",
            named: "pulls")
        view.setText("Taxes at \(city.budget.taxRate)% push every demand by \(Self.signed(demand.taxPushApplied))", named: "taxes")
        view.setText("Jobs per resident \(Self.twoPlaces(demand.jobsPerResident))   Workers per job \(Self.twoPlaces(demand.workersPerJob))", named: "ratios")
        let caps = city.facilities.caps
        view.show(Self.heldBack(caps), named: "heldBack", warning: !caps.isEmpty)
        view.setText("Population \(Figures.count(census.cityPopulation))", named: "population")
        view.setText(
            "Residents \(Figures.count(census.residentPopulation))   Commerce \(Figures.count(census.commercialPopulation))   Industry \(Figures.count(census.industrialPopulation))",
            named: "breakdown")
    }

    /// Draws the chart into its region, over the window drawn in `window`. Hudini's frames draw
    /// their views with no way into a region, so the session calls this right after the frame.
    func drawChart(window: Rectangle) {
        let content = WindowFrame.content(in: window)
        guard let chart = view.bounds(named: "chart", at: Vector2(x: content.x, y: content.y)) else { return }
        let demand = city.demand
        DrawRectangleRec(chart, Palette.bar)
        let middle = chart.y + chart.height / 2
        DrawLineV(Vector2(x: chart.x, y: middle), Vector2(x: chart.x + chart.width, y: middle), Palette.panelBorder)
        let count = demand.historyCount
        guard count >= 2 else {
            let waiting = "The chart starts after the next update"
            DrawText(waiting, Int32(chart.x + (chart.width - Float(MeasureText(waiting, 10))) / 2), Int32(middle) - 14, 10, Palette.textDim)
            return
        }
        // Three plain lines, oldest on the left, each against its own limit, so the middle is no
        // demand either way.
        let lines: [([Int], Int, Color)] = [
            (demand.residentialHistory, CityDemand.residentialLimit, Palette.residential),
            (demand.commercialHistory, CityDemand.businessLimit, Palette.commercial),
            (demand.industrialHistory, CityDemand.businessLimit, Palette.industrial),
        ]
        let step = chart.width / Float(count - 1)
        for (history, limit, color) in lines {
            func point(_ ago: Int) -> Vector2 {
                let share = Float(min(max(history[ago], -limit), limit)) / Float(limit)
                return Vector2(x: chart.x + Float(count - 1 - ago) * step, y: middle - share * (chart.height / 2 - 2))
            }
            var previous = point(count - 1)
            for ago in stride(from: count - 2, through: 0, by: -1) {
                let next = point(ago)
                DrawLineEx(previous, next, 2, color)
                previous = next
            }
        }
    }

    private static func span(_ updates: Int) -> String {
        let months = max(updates - 1, 0) / 2
        switch months {
        case ..<1: return "Under a month of history"
        case 1: return "The last month"
        case ..<24: return "The last \(months) months"
        default: return "The last \(months / 12) years"
        }
    }

    private static func heldBack(_ caps: DemandCaps) -> String {
        guard !caps.isEmpty else { return "Nothing is holding demand back" }
        var wants: [String] = []
        if caps.contains(.residential) { wants.append("R wants a stadium") }
        if caps.contains(.commercial) { wants.append("C wants an airport") }
        if caps.contains(.industrial) { wants.append("I wants a seaport") }
        return "Held at zero: " + wants.joined(separator: ", ")
    }

    static func signed(_ value: Int) -> String { value > 0 ? "+\(Figures.count(value))" : Figures.count(value) }

    private static func twoPlaces(_ value: Double) -> String {
        let hundredths = Int((value * 100).rounded())
        let fraction = abs(hundredths % 100)
        return "\(hundredths / 100).\(fraction < 10 ? "0" : "")\(fraction)"
    }
}

/// What each key and press does, and how a scenario is made, in a window that stays open beside
/// the map and minimizes.
@MainActor
final class HelpWindow {
    let frame: WindowFrame
    private let view = Interface.view("help", fallbackSize: Vector2(x: 520, y: 420))
    private let keyControls: [String]

    init() {
        frame = WindowFrame(view: view, title: "Help")
        frame.showsMinimizeButton = true
        let view = self.view
        keyControls = (0..<64).flatMap { ["row\($0)", "key\($0)", "action\($0)"] }.filter { view.bounds(named: $0, at: .zero) != nil }
        show("keysTab")
    }

    private func show(_ tab: String) {
        view.setSelected(tab == "keysTab", named: "keysTab")
        view.setSelected(tab == "editingTab", named: "editingTab")
        for name in keyControls { view.setHidden(tab != "keysTab", named: name) }
        view.setHidden(tab != "editingTab", named: "editing")
    }

    /// Opens help, or closes it when it's already open.
    func toggle(in windows: WindowManager) {
        if windows.isOpen(frame), !windows.isMinimized(frame) {
            windows.close(frame)
            return
        }
        windows.open(frame) { [unowned self, unowned windows] event in
            if case .view(.pressed(control: let tab)) = event {
                show(tab)
            } else {
                closeOrMinimize(event, frame, in: windows)
            }
        }
    }
}
