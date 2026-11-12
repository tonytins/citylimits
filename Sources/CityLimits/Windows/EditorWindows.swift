import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// A scenario's details: its name, start, end, money, briefing, goals, and disasters, in a modal
/// window. Nothing changes until Apply, which hands the details to `onApply`; that returns the
/// problem, if any, which the window shows and stays open for.
@MainActor
final class ScenarioDetailsWindow {
    static let mostRows = 5
    private static let hint = "Blank repeat fields strike once; a blank week is the month's first. Nothing changes until Apply."

    let frame: WindowFrame
    private let view = Interface.view("scenariodetails", fallbackSize: Vector2(x: 780, y: 470))
    private var goals: [GoalMeasure] = []
    private var events: [Disaster] = []
    private var canMoveStart = true
    private var note = hint
    var onApply: ((ScenarioDetails) -> String?)?

    init() {
        frame = WindowFrame(view: view, title: "Scenario")
    }

    /// Whether a field has the keyboard, so Escape and the game's keys leave it alone.
    var isTyping: Bool {
        let fields = ["name", "start", "ends", "funds"] + (0..<Self.mostRows).flatMap {
            ["goalLeast\($0)", "goalMost\($0)", "goalText\($0)", "eventAt\($0)", "eventWeek\($0)", "eventEvery\($0)", "eventTimes\($0)"]
        }
        return fields.contains { view.textField(named: $0)?.isFocused == true } || view.textArea(named: "description")?.isFocused == true
    }

    /// Opens a scenario's details. A saved city's start and money are its own once it has run.
    func open(_ scenario: Scenario, canMoveStart: Bool, in windows: WindowManager) {
        let details = ScenarioDetails(of: scenario)
        self.canMoveStart = canMoveStart
        view.setText(details.name, named: "name")
        view.setText(String(details.startYear), named: "start")
        view.setText(ScenarioDate(year: details.ends.year, month: details.ends.month).description, named: "ends")
        view.setText(String(details.funds), named: "funds")
        view.setText(details.description ?? "", named: "description")
        let setByCity = scenario.cityFile != nil && !canMoveStart
        view.setText("\(scenario.date(at: 0)), set by its city", named: "startSet")
        for name in ["start", "funds"] { view.setHidden(setByCity, named: name) }
        for name in ["startSet", "fundsSet"] { view.setHidden(!setByCity, named: name) }

        goals = []
        for (row, goal) in details.goals.prefix(Self.mostRows).enumerated() {
            goals.append(goal.measure)
            view.setText(goal.atLeast.map(String.init) ?? "", named: "goalLeast\(row)")
            view.setText(goal.atMost.map(String.init) ?? "", named: "goalMost\(row)")
            view.setText(goal.text ?? "", named: "goalText\(row)")
        }
        events = []
        for (row, event) in details.events.prefix(Self.mostRows).enumerated() {
            events.append(event.disaster)
            view.setText(ScenarioDate(year: event.at.year, month: event.at.month).description, named: "eventAt\(row)")
            view.setText(event.at.week == 1 ? "" : String(event.at.week), named: "eventWeek\(row)")
            view.setText(event.repeatWeeks.map(String.init) ?? "", named: "eventEvery\(row)")
            view.setText(event.repeatTimes.map(String.init) ?? "", named: "eventTimes\(row)")
        }
        note = details.goals.count > Self.mostRows || details.events.count > Self.mostRows
            ? "Only the first \(Self.mostRows) goals and disasters can be edited here; Apply would drop the rest."
            : Self.hint
        show(problem: nil)
        showRows()
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            handle(event, in: windows)
        }
    }

    private func handle(_ event: WindowFrame.Event, in windows: WindowManager) {
        switch event {
        case .closeRequested, .view(.pressed(control: "cancel")):
            windows.close(frame)
        case .view(.pressed(control: "apply")):
            apply(in: windows)
        case .view(.pressed(control: "addGoal")) where goals.count < Self.mostRows:
            goals.append(.population)
            clearGoal(goals.count - 1)
            showRows()
        case .view(.pressed(control: "addEvent")) where events.count < Self.mostRows:
            events.append(.fire)
            clearEvent(events.count - 1)
            showRows()
        case .view(.pressed(control: let name)):
            if let row = Self.row(name, "goalMeasure"), goals.indices.contains(row) {
                goals[row] = Self.next(goals[row])
            } else if let row = Self.row(name, "eventDisaster"), events.indices.contains(row) {
                events[row] = Self.next(events[row])
            } else if let row = Self.row(name, "goalRemove"), goals.indices.contains(row) {
                removeGoal(row)
            } else if let row = Self.row(name, "eventRemove"), events.indices.contains(row) {
                removeEvent(row)
            }
            showRows()
        default:
            break
        }
    }

    private static func row(_ name: String, _ prefix: String) -> Int? {
        name.hasPrefix(prefix) ? Int(name.dropFirst(prefix.count)) : nil
    }

    // A button that picks a measure or a disaster moves on to the next with each press.
    private static func next<Value: CaseIterable & Equatable>(_ value: Value) -> Value {
        let all = Array(Value.allCases)
        let index = all.firstIndex(of: value) ?? 0
        return all[(index + 1) % all.count]
    }

    private func clearGoal(_ row: Int) {
        for field in ["goalLeast", "goalMost", "goalText"] { view.setText("", named: "\(field)\(row)") }
    }

    private func clearEvent(_ row: Int) {
        for field in ["eventAt", "eventWeek", "eventEvery", "eventTimes"] { view.setText("", named: "\(field)\(row)") }
    }

    // A removed row's fields move up, so the rows on screen stay in their order.
    private func removeGoal(_ row: Int) {
        for later in row..<(goals.count - 1) {
            for field in ["goalLeast", "goalMost", "goalText"] {
                view.setText(view.text(named: "\(field)\(later + 1)") ?? "", named: "\(field)\(later)")
            }
        }
        goals.remove(at: row)
        clearGoal(goals.count)
    }

    private func removeEvent(_ row: Int) {
        for later in row..<(events.count - 1) {
            for field in ["eventAt", "eventWeek", "eventEvery", "eventTimes"] {
                view.setText(view.text(named: "\(field)\(later + 1)") ?? "", named: "\(field)\(later)")
            }
        }
        events.remove(at: row)
        clearEvent(events.count)
    }

    private func showRows() {
        for row in 0..<Self.mostRows {
            let hasGoal = row < goals.count
            for field in ["goalMeasure", "goalLeast", "goalMost", "goalText", "goalRemove"] { view.setHidden(!hasGoal, named: "\(field)\(row)") }
            if hasGoal { view.setText(Self.measureName(goals[row]), named: "goalMeasure\(row)") }
            let hasEvent = row < events.count
            for field in ["eventAt", "eventWeek", "eventDisaster", "eventEvery", "eventTimes", "eventRemove"] {
                view.setHidden(!hasEvent, named: "\(field)\(row)")
            }
            if hasEvent { view.setText(Self.disasterName(events[row]), named: "eventDisaster\(row)") }
        }
        view.setDisabled(goals.count >= Self.mostRows, named: "addGoal")
        view.setDisabled(events.count >= Self.mostRows, named: "addEvent")
        view.setText(goals.count >= Self.mostRows ? "\(Self.mostRows) at most" : "+ Add", named: "addGoal")
        view.setText(events.count >= Self.mostRows ? "\(Self.mostRows) at most" : "+ Add", named: "addEvent")
    }

    private static func measureName(_ measure: GoalMeasure) -> String {
        switch measure {
        case .population: "Population"
        case .funds: "Funds"
        case .pollution: "Pollution"
        case .crime: "Crime"
        case .traffic: "Traffic"
        }
    }

    private static func disasterName(_ disaster: Disaster) -> String { disaster.title }

    private func show(problem: String?) {
        view.setText(note, named: "note")
        view.setText(problem ?? "", named: "problem")
        view.setHidden(problem != nil, named: "note")
        view.setHidden(problem == nil, named: "problem")
    }

    private func field(_ name: String) -> String { (view.text(named: name) ?? "").trimmingCharacters(in: .whitespaces) }

    // A whole number, nil for a blank field, or a problem for anything else.
    private func number(_ name: String) -> Int?? {
        let text = field(name)
        if text.isEmpty { return .some(nil) }
        return Int(text).map { .some($0) }
    }

    private func apply(in windows: WindowManager) {
        guard let details = readDetails() else { return }
        if let problem = onApply?(details) {
            show(problem: Self.reworded(problem))
            return
        }
        windows.close(frame)
    }

    // The fields as details, or nil after showing what's wrong with them.
    private func readDetails() -> ScenarioDetails? {
        guard let start = Int(field("start")) else {
            show(problem: "Starts: needs a year, such as 1900.")
            return nil
        }
        guard let ends = ScenarioDate.parse(field("ends")) else {
            show(problem: "Ends: needs a month, written as 1910-01.")
            return nil
        }
        guard let funds = Int(field("funds")) else {
            show(problem: "Funds: needs a sum of money, such as 20000.")
            return nil
        }
        var goalDetails: [GoalDetails] = []
        for (row, measure) in goals.enumerated() {
            guard case .some(let atLeast) = number("goalLeast\(row)"), case .some(let atMost) = number("goalMost\(row)") else {
                show(problem: "Goal \(row + 1): at least and at most need whole numbers, or nothing.")
                return nil
            }
            let text = field("goalText\(row)")
            goalDetails.append(GoalDetails(measure: measure, atLeast: atLeast, atMost: atMost, text: text.isEmpty ? nil : text))
        }
        var eventDetails: [EventDetails] = []
        for (row, disaster) in events.enumerated() {
            guard case .some(let week) = number("eventWeek\(row)"), let at = ScenarioDate.parse(field("eventAt\(row)"), week: week ?? 1) else {
                show(problem: "Disaster \(row + 1): needs a month, written as 1906-07, and a week from 1 to 4, or nothing for the first.")
                return nil
            }
            guard case .some(let every) = number("eventEvery\(row)"), case .some(let times) = number("eventTimes\(row)"), (every == nil) == (times == nil) else {
                show(problem: "Disaster \(row + 1): a repeat needs both its weeks and its times, or neither, to strike once.")
                return nil
            }
            eventDetails.append(EventDetails(at: at, disaster: disaster, repeatWeeks: every, repeatTimes: times))
        }
        let description = view.textArea(named: "description")?.text.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return ScenarioDetails(
            name: field("name"), description: description.isEmpty ? nil : description, startYear: start, ends: ends, funds: funds,
            goals: goalDetails, events: eventDetails)
    }

    // A file's place in a problem, as the window names it: "goals[1].text" is "Goal 2, text".
    private static func reworded(_ problem: String) -> String {
        var text = problem
        for (list, word) in [("goals", "Goal"), ("events", "Disaster")] {
            while let open = text.range(of: "\(list)["), let close = text.range(of: "]", range: open.upperBound..<text.endIndex),
                let index = Int(text[open.upperBound..<close.lowerBound])
            {
                var end = close.upperBound
                var joiner = ""
                if end < text.endIndex, text[end] == "." {
                    end = text.index(after: end)
                    joiner = ", "
                }
                text.replaceSubrange(open.lowerBound..<end, with: "\(word) \(index + 1)\(joiner)")
            }
        }
        return text
    }
}

/// The menu Escape opens over a scenario being edited. What each choice does is the editor's.
@MainActor
final class EditorMenuWindow {
    enum Choice: String {
        case save, reset, startOver, settings, mainMenu, quit
    }

    let frame: WindowFrame
    private let view = Interface.view("editormenu", fallbackSize: Vector2(x: 220, y: 282))
    var onChoice: ((Choice) -> Void)?

    init() {
        frame = WindowFrame(view: view, title: "Menu")
    }

    /// Opens the menu; Reset the test is offered only while a test plays.
    func open(testing: Bool, in windows: WindowManager) {
        view.setDisabled(!testing, named: "reset")
        view.setText(testing ? "Testing" : "Editing", named: "paused")
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .closeRequested, .view(.pressed(control: "resume")):
                windows.close(frame)
            case .view(.pressed(control: let name)):
                guard let choice = Choice(rawValue: name) else { return }
                windows.close(frame)
                onChoice?(choice)
            default:
                break
            }
        }
    }
}
