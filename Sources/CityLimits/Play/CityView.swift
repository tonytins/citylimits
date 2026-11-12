import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// The city as the player sees it: the map in a camera that pans and zooms, the stroke being
/// dragged, and what's animated over the map (traffic, fires, explosions, the train, the city's
/// vehicles and menaces, and an earthquake's shake). It reads the city and never changes it,
/// apart from applying a finished stroke.
@MainActor
final class CityView {
    private static let panSpeed: Float = 600
    // Explosions play a frame every other animation step; the few at once share a buffer.
    private static let mostExplosions = 8
    private static let stepsPerExplosionFrame = 2
    // An earthquake shakes the drawing from side to side for a moment; the city isn't moved.
    private static let shakeSeconds: Float = 1.5
    private static let shakePixels: Float = 4
    private static let shakesPerSecond: Float = 18

    let city: CitySimulation
    let camera = ViewportCamera()
    let renderer: MapRenderer
    private let assets: GameAssets
    private var traffic = TrafficAnimation()
    private var fireFrame = 0
    private var explosions: [(x: Int, y: Int, steps: Int)] = []
    private var shakeLeft: Float = 0
    // The train runs at the traffic's pace, on random numbers of its own, so it never changes the city.
    private var train = Train()
    private var trainRandom = SystemRandomNumberGenerator()

    private(set) var stroke: BuildStroke?
    /// What the stroke being dragged would do.
    let preview = BuildPlan()

    init(city: CitySimulation, assets: GameAssets) {
        self.city = city
        self.assets = assets
        renderer = MapRenderer(city: city, sheet: assets.tileSheet)
        explosions.reserveCapacity(Self.mostExplosions)
        camera.resize(to: Vector2(x: Float(GetScreenWidth()), y: Float(GetScreenHeight())))
        let size = Float(MapRenderer.tileSize)
        camera.center(on: Vector2(x: Float(city.map.width) * size / 2, y: Float(city.map.height) * size / 2))
    }

    func unload() { renderer.unload() }

    /// Hears what the city says that the view shows: explosions and earthquakes.
    func show(_ event: CityEvent) {
        switch event {
        case .exploded(let x, let y):
            // A new explosion takes the oldest one's place when the buffer is full.
            if explosions.count == Self.mostExplosions { explosions.removeFirst() }
            explosions.append((x, y, 0))
        case .earthquake:
            shakeLeft = Self.shakeSeconds
        default:
            break
        }
    }

    // MARK: Each frame

    /// Moves the animations on: traffic, fires, explosions, and the train, at the game's speed.
    func animate(_ elapsed: Float, speed: GameSpeed) {
        city.roads.ensureCurrent(city.map)
        let steps = traffic.advance(Double(elapsed), speed: speed)
        for _ in 0..<steps { train.step(map: city.map, population: city.demand.taxablePopulation, random: &trainRandom) }
        fireFrame = (fireFrame + steps) % TileAtlas.fireFrames
        var kept = 0
        for index in explosions.indices {
            explosions[index].steps += steps
            if explosions[index].steps < TileAtlas.explosionFrames * Self.stepsPerExplosionFrame {
                explosions[kept] = explosions[index]
                kept += 1
            }
        }
        explosions.removeLast(explosions.count - kept)
        shakeLeft = max(0, shakeLeft - elapsed)
    }

    /// Fits the camera to the window and the map, and plans the stroke being dragged again, so its
    /// preview follows the city as it changes.
    func update() {
        camera.resize(to: Vector2(x: Float(GetScreenWidth()), y: Float(GetScreenHeight())))
        if let stroke { city.builder.plan(stroke, into: preview) }
        let size = Float(MapRenderer.tileSize)
        camera.constrain(to: Vector2(x: Float(city.map.width) * size, y: Float(city.map.height) * size))
    }

    func pan(_ direction: Vector2, elapsed: Float) {
        camera.pan(by: direction * (Self.panSpeed * elapsed / camera.zoom))
    }

    /// The tile under a point on the screen, or nil off the map.
    func tile(at screen: Vector2) -> (x: Int, y: Int)? {
        let world = camera.worldPoint(forScreenPoint: screen)
        let size = Float(MapRenderer.tileSize)
        let (x, y) = (Int((world.x / size).rounded(.down)), Int((world.y / size).rounded(.down)))
        return city.map.contains(x, y) ? (x, y) : nil
    }

    // MARK: Strokes

    func beginStroke(_ action: BuildAction, at tile: (x: Int, y: Int)) {
        let stroke = BuildStroke(action, tile.x, tile.y)
        self.stroke = stroke
        city.builder.plan(stroke, into: preview)
    }

    func stretchStroke(to tile: (x: Int, y: Int)) {
        stroke?.drag(to: tile.x, tile.y)
    }

    func cancelStroke() { stroke = nil }

    /// Applies the stroke being dragged, and says what came of it.
    func finishStroke() -> (action: BuildAction, result: BuildResult)? {
        guard let stroke else { return nil }
        self.stroke = nil
        return (stroke.action, city.builder.apply(stroke))
    }

    // MARK: Drawing

    /// Redraws what changed in the map's cache. Call before `BeginDrawing`.
    func refresh() { renderer.refresh() }

    func drawMap(hover: (x: Int, y: Int)?) {
        var view = camera.raylibCamera
        if shakeLeft > 0 {
            view.offset.x += sin(shakeLeft * Self.shakesPerSecond * 2 * .pi) * Self.shakePixels * (shakeLeft / Self.shakeSeconds)
        }
        BeginMode2D(view)
        renderer.draw(camera, trafficFrame: traffic.frame, fireFrame: fireFrame)
        drawSprites()
        for explosion in explosions {
            renderer.drawExplosion(explosion.x, explosion.y, frame: explosion.steps / Self.stepsPerExplosionFrame)
        }
        renderer.drawUnpowered(camera, seconds: GetTime())
        if stroke != nil {
            renderer.drawPreview(preview, camera)
        } else {
            renderer.drawHover(camera, tile: hover)
        }
        EndMode2D()
    }

    // The train, then the ship, the monster, the tornado, the plane, and the helicopter, the
    // aircraft above the rest, each centred on where it is but the tornado, whose foot is there,
    // as Micropolis draws them. A missing drawing leaves that one unseen.
    private func drawSprites() {
        func draw(_ texture: Texture2D?, _ x: Int, _ y: Int, foot: Bool = false) {
            guard let texture else { return }
            let top = foot ? y - Int(texture.height) + 8 : y - Int(texture.height) / 2
            DrawTexture(texture, Int32(x - Int(texture.width) / 2), Int32(top), .white)
        }
        if train.isRunning { draw(assets.trainFrame(train.frame), train.x, train.y) }
        let vehicles = city.vehicles, menaces = city.menaces
        if let ship = vehicles.ship { draw(assets.shipFrame(ship.frame), ship.x, ship.y) }
        if let monster = menaces.monster { draw(assets.monsterFrame(monster.frame), monster.x, monster.y) }
        if let tornado = menaces.tornado { draw(assets.tornadoFrame(tornado.frame), tornado.x, tornado.y, foot: true) }
        if let plane = vehicles.plane { draw(assets.planeFrame(plane.frame), plane.x, plane.y) }
        if let helicopter = vehicles.helicopter { draw(assets.helicopterFrame(helicopter.frame), helicopter.x, helicopter.y) }
    }
}
