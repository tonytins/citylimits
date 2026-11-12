import CityLimitsCore
import Hudini
import Raylib

/// The city panel, a modal window that pauses the city: its title is the city's name, edited in
/// place, and its tabs show the city (with the challenge it's played as, if any), its ordinances,
/// and its neighbours. What it can't do itself, such as asking before a deal is broken or showing
/// an offer, it asks the session for through `onBreakDeal` and `onShowOffer`.
@MainActor
final class CityPanelWindow {
    enum Tab: String, CaseIterable {
        case city = "cityTab"
        case ordinances = "ordinancesTab"
        case neighbours = "neighboursTab"
    }

    private static let narrowWidth: Float = 420
    private static let wideWidth: Float = 720
    private static let cityHeight: Float = 226
    private static let challengeHeight: Float = 380
    private static let neighboursHeight: Float = 226
    private static let ordinanceRow: Float = 26
    private static let noteSeconds = 2.0
    private static let mostEvents = 5

    private static let cityControls = [
        "renameNote", "renamed", "date", "population", "breakdown", "demandLabel", "residential", "commercial", "industrial",
        "funds", "fundsWarning", "thisYear", "thisYearWarning", "lastYear", "lastYearWarning", "seed", "copySeed",
    ]
    private static let challengeControls = [
        "challengeLabel", "challengeName", "ends", "state", "stateWarning", "stateWon", "eventsLabel", "events",
        "goalsLabel", "goals", "goalStates",
    ]
    private static let neighbourControls = Side.allCases.flatMap { ["neighbour\($0.rawValue)", "links\($0.rawValue)", "deal\($0.rawValue)", "break\($0.rawValue)"] } + ["offer"]

    let frame: WindowFrame
    private let view = Interface.view("citypanel", fallbackSize: Vector2(x: narrowWidth, y: cityHeight))
    private let city: CitySimulation
    private var tab = Tab.city
    private var renamedAt = -Double.infinity
    private var copiedAt = -Double.infinity

    /// Asked to break the deal with a side, which the session confirms first.
    var onBreakDeal: ((Side) -> Void)?
    /// Asked to show the offer standing.
    var onShowOffer: (() -> Void)?

    init(city: CitySimulation) {
        self.city = city
        frame = WindowFrame(view: view)
        frame.allowsTitleEditing = true
        frame.validateTitle = { name in
            CitySimulation.isValidName(name) ? nil : "A city's name needs 1 to \(CitySimulation.maxNameLength) characters"
        }
    }

    /// Whether the player is typing the city's name, so keys shouldn't reach the game.
    var isTyping: Bool { frame.isEditingTitle }

    func open(_ tab: Tab = .city, in windows: WindowManager) {
        frame.cancelEdit()
        view.setText(city.name, named: "cityName")
        renamedAt = -.infinity
        copiedAt = -.infinity
        show(tab)
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            handle(event, in: windows)
        }
    }

    private func handle(_ event: WindowFrame.Event, in windows: WindowManager) {
        switch event {
        case .closeRequested:
            windows.close(frame)
        case .view(.submitted(control: "cityName", let name)):
            if city.rename(name) {
                renamedAt = GetTime()
            }
            view.setText(city.name, named: "cityName")
        case .view(.pressed(control: let name)):
            if let picked = Tab(rawValue: name) {
                show(picked)
            } else if name == "copySeed" {
                SetClipboardText(String(city.seed))
                copiedAt = GetTime()
            } else if name == "offer" {
                onShowOffer?()
            } else if name.hasPrefix("break"), let index = Int(name.dropFirst(5)), let side = Side(rawValue: index) {
                onBreakDeal?(side)
            }
        default:
            break
        }
    }

    private func show(_ chosen: Tab) {
        tab = chosen
        for each in Tab.allCases { view.setSelected(each == chosen, named: each.rawValue) }
        let hasChallenge = city.challenge != nil
        for name in Self.cityControls { view.setHidden(chosen != .city, named: name) }
        for name in Self.challengeControls { view.setHidden(chosen != .city || !hasChallenge, named: name) }
        for name in Self.neighbourControls { view.setHidden(chosen != .neighbours, named: name) }
        view.setHidden(chosen != .ordinances, named: "ordinances")
        view.setHidden(chosen != .ordinances, named: "ordinanceNote")

        switch chosen {
        case .city:
            view.setWindowSize(Vector2(
                x: hasChallenge ? Self.wideWidth : Self.narrowWidth, y: hasChallenge ? Self.challengeHeight : Self.cityHeight))
        case .ordinances:
            // The rows run as far as the city's ordinances go, mods' included, with the note under them.
            let rows = Float(city.ordinances.all.count) * Self.ordinanceRow
            view.setBounds(Rectangle(x: 12, y: 8, width: Self.narrowWidth - 24, height: rows), named: "ordinances")
            view.setBounds(Rectangle(x: 12, y: 8 + rows + 8, width: Self.narrowWidth - 24, height: 10), named: "ordinanceNote")
            view.setWindowSize(Vector2(x: Self.narrowWidth, y: 8 + rows + 30))
        case .neighbours:
            view.setWindowSize(Vector2(x: Self.narrowWidth, y: Self.neighboursHeight))
        }
        refresh()
    }

    /// Shows the city as it stands. Call each frame while it's open.
    func refresh() {
        switch tab {
        case .city:
            showCity()
            if let challenge = city.challenge { showChallenge(challenge) }
        case .ordinances:
            showOrdinanceNote()
        case .neighbours:
            showNeighbours()
        }
    }

    // MARK: City

    private func showCity() {
        let now = GetTime()
        let renamed = now - renamedAt < Self.noteSeconds
        view.setHidden(!renamed || tab != .city, named: "renamed")
        view.setHidden(renamed || tab != .city, named: "renameNote")
        view.setText(
            frame.isEditingTitle ? "Press Enter to rename the city, or Escape to leave it" : "Click the name above to rename the city",
            named: "renameNote")

        let date = city.date
        let age = date.year - city.foundedYear
        let ageText = age == 0 ? "founded this year" : age == 1 ? "1 year old" : "\(age) years old"
        view.setText("\(date.month < 10 ? "0" : "")\(date.month)/\(date.year), \(ageText)", named: "date")
        let census = city.census
        view.setText("Population \(Figures.count(census.cityPopulation))", named: "population")
        view.setText(
            "Residents \(Figures.count(census.residentPopulation))   Commerce \(Figures.count(census.commercialPopulation))   Industry \(Figures.count(census.industrialPopulation))",
            named: "breakdown")
        view.setText("R \(DemandWindow.signed(city.demand.residential))", named: "residential")
        view.setText("C \(DemandWindow.signed(city.demand.commercial))", named: "commercial")
        view.setText("I \(DemandWindow.signed(city.demand.industrial))", named: "industrial")

        let funds = city.treasury.funds
        view.show("Funds \(Figures.money(funds)), tax \(city.budget.taxRate)%", named: "funds", warning: funds < 0)
        let cashFlow = city.estimateBudget().cashFlow
        view.show("This year, as it stands: \(Self.signedMoney(cashFlow))", named: "thisYear", warning: cashFlow < 0)
        if let last = city.budget.lastYear {
            view.show("Last year: \(Self.signedMoney(last.cashFlow))", named: "lastYear", warning: last.cashFlow < 0)
        } else {
            view.show("Last year: not settled yet", named: "lastYear", warning: false)
        }

        view.setText("Seed \(city.seed)", named: "seed")
        view.setText(now - copiedAt < Self.noteSeconds ? "copied" : "click to copy", named: "copySeed")
    }

    // The challenge the city is played as: its name, its end, how it stands, the disasters still
    // to come, and its goals, each with what the city measures now.
    private func showChallenge(_ challenge: Challenge) {
        view.setText(challenge.name, named: "challengeName")
        view.setText("Ends \(challenge.ends)", named: "ends")
        let state = challenge.won
        view.setText("Under way", named: "state")
        view.setText("Lost", named: "stateWarning")
        view.setText("Won", named: "stateWon")
        view.setHidden(state != nil, named: "state")
        view.setHidden(state != false, named: "stateWarning")
        view.setHidden(state != true, named: "stateWon")

        let upcoming = challenge.upcomingEvents
        var events = upcoming.prefix(Self.mostEvents).map { "\($0.disaster), \($0.date)" }
        if upcoming.count > Self.mostEvents { events.append("and \(upcoming.count - Self.mostEvents) more") }
        view.setText(events.isEmpty ? "None" : events.joined(separator: "\n"), named: "events")

        view.setText("Goals by \(challenge.ends)", named: "goalsLabel")
        var goals: [String] = [], states: [String] = []
        for goal in challenge.goals {
            let report = ScenarioRunner.judge(goal, in: city)
            let shown = goal.measure == .funds ? Figures.money(report.found) : Figures.count(report.found)
            let verdict = report.met ? "met" : state == nil ? "not yet" : "missed"
            goals.append(goal.text)
            states.append("\(shown), \(verdict)")
        }
        view.setText(goals.joined(separator: "\n"), named: "goals")
        view.setText(states.joined(separator: "\n"), named: "goalStates")
    }

    private static func signedMoney(_ amount: Int) -> String { amount > 0 ? "+" + Figures.money(amount) : Figures.money(amount) }

    // MARK: Ordinances

    private func ordinanceRow(at point: Vector2) -> Int? {
        guard let region = regionBounds() else { return nil }
        guard CheckCollisionPointRec(point, region) else { return nil }
        let row = Int((point.y - region.y) / Self.ordinanceRow)
        return city.ordinances.all.indices.contains(row) ? row : nil
    }

    private func regionBounds() -> Rectangle? {
        guard let windows = frameBounds else { return nil }
        let content = WindowFrame.content(in: windows)
        return view.bounds(named: "ordinances", at: Vector2(x: content.x, y: content.y))
    }

    // Where the window was last drawn, for finding its region.
    private var frameBounds: Rectangle?

    /// Toggles the ordinance under a press, on the Ordinances tab. Call while it's the front
    /// window, after the window manager's update.
    func handleOrdinances(isFront: Bool) {
        guard isFront, tab == .ordinances, IsMouseButtonPressed(.left), let row = ordinanceRow(at: GetMousePosition()) else { return }
        let ordinance = city.ordinances.all[row]
        _ = city.setOrdinance(ordinance.id, enacted: !city.ordinances.isEnacted(ordinance.id))
    }

    private func showOrdinanceNote() {
        let pointer = GetMousePosition()
        if let row = ordinanceRow(at: pointer) {
            view.setText(Self.effects(of: city.ordinances.all[row]), named: "ordinanceNote")
            return
        }
        let population = city.census.cityPopulation
        let (income, cost) = city.ordinances.yearlyMoney(population: population)
        view.setText(
            city.ordinances.enacted.isEmpty
                ? "Each earns or costs that a year for every 1,000 people. Click one to enact it"
                : "Those enacted earn \(Figures.money(income)) and cost \(Figures.money(cost)) a year at \(Figures.count(population)) people",
            named: "ordinanceNote")
    }

    private static func effects(of ordinance: OrdinanceSpec) -> String {
        guard !ordinance.effects.isEmpty else { return "\(ordinance.name): nothing but its money" }
        let order: [OrdinanceEffect] = [
            .residentialDemand, .commercialDemand, .industrialDemand, .crime, .industrialPollution, .fires, .powerCapacity, .traffic,
        ]
        let parts = order.compactMap { effect in ordinance.effects[effect].map { (effect, $0) } }.map { effect, amount in
            let signed = amount > 0 ? "+\(amount)" : "\(amount)"
            return switch effect {
            case .residentialDemand: "residential demand \(signed)"
            case .commercialDemand: "commercial demand \(signed)"
            case .industrialDemand: "industrial demand \(signed)"
            case .crime: "crime \(signed)"
            case .industrialPollution: "industry's pollution \(signed)%"
            case .fires: "fires breaking out \(signed)%"
            case .powerCapacity: "power plants' capacity \(signed)%"
            case .traffic: "traffic \(signed)%"
            }
        }
        return "\(ordinance.name): " + parts.joined(separator: ", ")
    }

    /// Draws the window and its ordinance rows, which the document leaves to the game.
    func draw(in bounds: Rectangle) {
        frameBounds = bounds
        frame.draw(in: bounds)
        guard tab == .ordinances, let region = regionBounds() else { return }
        let hovered = ordinanceRow(at: GetMousePosition())
        let theme = Interface.theme
        for (index, ordinance) in city.ordinances.all.enumerated() {
            let row = Rectangle(x: region.x, y: region.y + Float(index) * Self.ordinanceRow, width: region.width, height: Self.ordinanceRow - 4)
            let enacted = city.ordinances.isEnacted(ordinance.id)
            if enacted { DrawRectangleRec(row, theme.selectedItem) }
            DrawRectangleLinesEx(row, 1, enacted ? Palette.accent : index == hovered ? Palette.textDim : Palette.panelBorder)
            let textY = Int32(row.y + (row.height - 14) / 2)
            DrawText(ordinance.name, Int32(row.x) + 8, textY, 14, enacted ? Palette.accent : Palette.text)
            let money = "\(Self.signedMoney(ordinance.money)) per 1,000"
            DrawText(money, Int32(row.x + row.width) - 8 - MeasureText(money, 12), textY + 2, 12, Palette.textDim)
        }
    }

    // MARK: Neighbours

    private func showNeighbours() {
        let neighbours = city.neighbours
        let population = city.census.cityPopulation
        for side in Side.allCases {
            let index = side.rawValue
            let neighbour = neighbours.neighbour(side)
            view.setText(
                "\(neighbour.name), to the \(side.name): \(Figures.count(neighbour.population(beside: population))) people", named: "neighbour\(index)")
            let connections = neighbours.connections(to: side)
            let names = [(Connections.road, "road"), (.rail, "rail"), (.power, "power")].filter { connections.contains($0.0) }.map(\.1)
            var links = names.isEmpty ? "Not connected" : names.joined(separator: ", ")
            links = links.prefix(1).uppercased() + links.dropFirst()
            let deal = neighbours.deal(with: side)
            if let deal {
                let verb = deal.kind == .cityBuys ? "Sells you" : "Buys"
                let ends = CityCalendar.date(at: deal.endStep / CityCalendar.stepsPerCount)
                view.setText("\(links). \(verb) \(Figures.count(deal.power)) power for \(Figures.money(deal.money)) a year to \(ends)", named: "deal\(index)")
            } else {
                view.setText(links, named: "links\(index)")
            }
            view.setHidden(deal == nil, named: "deal\(index)")
            view.setHidden(deal != nil, named: "links\(index)")
            view.setHidden(deal == nil, named: "break\(index)")
        }
        if let offer = neighbours.offer {
            let until = CityCalendar.date(at: offer.expiresStep / CityCalendar.stepsPerCount)
            view.setText("\(neighbours.neighbour(offer.side).name) offers a deal until \(until): click here to answer", named: "offer")
        } else {
            view.setText("Build a road, rail, or power line off an edge to connect", named: "offer")
        }
        view.setDisabled(neighbours.offer == nil, named: "offer")
    }
}

/// A neighbour's offer of a power deal, in a modal window over whatever opened it.
@MainActor
final class DealWindow {
    let frame: WindowFrame
    private let view = Interface.view("deal", fallbackSize: Vector2(x: 420, y: 200))
    private let city: CitySimulation

    init(city: CitySimulation) {
        self.city = city
        frame = WindowFrame(view: view, title: "A deal")
    }

    /// Opens the offer standing, if there is one.
    func open(in windows: WindowManager) {
        let neighbours = city.neighbours
        guard let offer = neighbours.offer else { return }
        let neighbour = neighbours.neighbour(offer.side)
        let (name, cityName) = (neighbour.name, city.name)
        let people = Figures.count(neighbour.population(beside: city.census.cityPopulation))
        let what = offer.kind == .cityBuys
            ? "\(name) offers to sell \(cityName) \(Figures.count(offer.power)) tiles of power, for \(Figures.money(offer.money)) a year."
            : "\(name) offers to buy \(Figures.count(offer.power)) tiles of \(cityName)'s spare power, for \(Figures.money(offer.money)) a year."
        let until = CityCalendar.date(at: offer.expiresStep / CityCalendar.stepsPerCount)
        frame.title = "A deal from \(name)"
        view.setWrapped(
            "\(name), a city of \(people) to the \(offer.side.name), sends word. \(what) "
                + "The deal would run \(offer.years) years, while the power line to \(name) stands. "
                + "Breaking it would cost a year's money, and \(name) wouldn't deal again for \(CityNeighbours.grudgeYears) years. "
                + "The offer stands until \(until).",
            named: "message")
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .view(.pressed(control: "accept")):
                _ = city.acceptOffer()
                windows.close(frame)
            case .view(.pressed(control: "decline")):
                _ = city.declineOffer()
                windows.close(frame)
            case .closeRequested:
                // Closing leaves the offer standing, to answer later from the city panel.
                windows.close(frame)
            default:
                break
            }
        }
    }
}
