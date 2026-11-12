import CityLimitsCore
import Raylib

/// The news ticker, after Tony Bark's mock-up in `docs/design`: a strip of news that scrolls from
/// right to left and never stops. Lines posted to it wait their turn in a `NewsQueue`, disasters
/// and advisories ahead of headlines, and when nothing is waiting a filler line keeps it running.
/// Each line is drawn in its kind's colour; an ordinance's or a neighbour's, in blue, can be
/// clicked.
@MainActor
final class NewsTicker {
    static let gameFontSize: Int32 = 14

    // Pixels a second: slow enough to read a line as it passes.
    private static let speed: Float = 90
    // The space between one line and the next, with a dot in the middle of it.
    private static let spacing: Float = 48
    private static let dotRadius: Float = 2
    // More lines than can ever fit across a screen at once, so the list never grows.
    private static let mostShowing = 32

    private var queue = NewsQueue()
    /// Gives a line whenever nothing is waiting. Without one, the strip waits for news.
    var filler: (() -> NewsItem)?
    private var showing: [(item: NewsItem, x: Float, width: Float)] = []
    private let fontSize: Int32

    /// A ticker with text of the given size; the loading screen's is bigger than the game's.
    init(fontSize: Int32 = NewsTicker.gameFontSize) {
        self.fontSize = fontSize
        showing.reserveCapacity(Self.mostShowing)
    }

    func post(_ item: NewsItem) { queue.post(item) }

    /// Queues a line from the game, such as a save or a warning, in yellow.
    func advise(_ text: String) { queue.post(NewsItem(text, kind: .advisory)) }

    /// Moves the lines on by the time a frame took, across a strip `width` wide, and brings on the
    /// next line once the last is clear of the right edge. An empty ticker starts its first line
    /// at `startShare` of the width, so the strip isn't bare.
    func update(_ elapsed: Float, width: Float, startShare: Float = 1) {
        // A long frame moves the lines on at most a screen, so a stall doesn't skip the news.
        let step = min(Self.speed * max(0, elapsed.isNaN ? 0 : elapsed), width)
        for index in showing.indices { showing[index].x -= step }
        showing.removeAll { $0.x + $0.width < 0 }

        while showing.count < Self.mostShowing {
            let x: Float
            if let last = showing.last {
                x = last.x + last.width + Self.spacing
                if x > width { break }
            } else {
                x = width * min(max(startShare, 0), 1)
            }
            guard let item = queue.next() ?? filler?() else { break }
            showing.append((item, x, Float(MeasureText(item.text, fontSize))))
        }
    }

    /// Draws the strip in `bounds`, which runs the screen's width.
    func draw(in bounds: Rectangle) {
        let textY = Int32(bounds.y + (bounds.height - Float(fontSize)) / 2)
        let middle = bounds.y + bounds.height / 2
        for line in showing {
            DrawText(line.item.text, Int32(bounds.x + line.x), textY, fontSize, Self.color(of: line.item.kind))
            DrawCircleV(Vector2(x: bounds.x + line.x + line.width + Self.spacing / 2, y: middle), Self.dotRadius, Palette.textDim)
        }
    }

    /// The line under `point` that opens something when clicked, on a strip drawn in `bounds`.
    func link(at point: Vector2, in bounds: Rectangle) -> NewsItem? {
        guard CheckCollisionPointRec(point, bounds) else { return nil }
        let x = point.x - bounds.x
        return showing.first { $0.item.link != nil && x >= $0.x && x < $0.x + $0.width }?.item
    }

    private static func color(of kind: NewsKind) -> Color {
        switch kind {
        case .disaster: Palette.newsDisaster
        case .ordinance, .neighbour: Palette.newsOrdinance
        case .advisory: Palette.newsAdvisory
        case .headline: Palette.newsHeadline
        }
    }
}

/// What the ticker says about the city's ordinances and its neighbours.
enum NewsText {
    static func ordinance(_ news: OrdinanceNews, city: String) -> NewsItem {
        let name = news.ordinance.name
        let text = switch news.kind {
        case .petition: "Petition in \(city): residents ask for \(name)"
        case .enacted: "\(name) enacted in \(city)"
        case .repealed: "\(name) repealed in \(city)"
        }
        return NewsItem(text, kind: .ordinance, link: news.ordinance.id)
    }

    static func neighbour(_ news: NeighbourNews, city: String) -> NewsItem {
        let name = news.neighbour.name
        let (power, money) = (Figures.count(news.power), Figures.money(news.money))
        let text = switch (news.kind, news.deal) {
        case (.offer, .cityBuys): "\(name) offers to sell \(city) \(power) power for \(money) a year"
        case (.offer, .citySells): "\(name) offers to buy \(power) of \(city)'s spare power for \(money) a year"
        case (.accepted, _): "\(city) signs a power deal with \(name)"
        case (.declined, _): "\(city) turns down \(name)'s offer"
        case (.lapsed, _): "\(name)'s offer lapses unanswered"
        case (.ended, _): "\(city)'s power deal with \(name) comes to its end"
        case (.broken, _): "\(city) breaks its power deal with \(name)"
        }
        return NewsItem(text, kind: .neighbour, link: news.neighbour.side.name)
    }
}
