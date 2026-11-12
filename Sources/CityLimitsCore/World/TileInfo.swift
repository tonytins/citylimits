extension TileKind {
    /// The ground's name, as the player sees it.
    public var displayName: String {
        switch self {
        case .grass: "Grass"
        case .water: "Water"
        case .trees: "Trees"
        case .rubble: "Rubble"
        }
    }

    /// What the player can do with the ground.
    public var buildAdvice: String {
        switch self {
        case .water: "only a bridge, a tunnel, or a power line can cross it"
        case .trees, .rubble: "cleared for $1 a tile when built on"
        case .grass: "ready to build on"
        }
    }
}

extension TileUse {
    /// The name of what is built, as the player sees it, when no spec names it.
    public var displayName: String {
        switch self {
        case .none: "Empty"
        case .road: "Road"
        case .residential: "Residential zone"
        case .commercial: "Commercial zone"
        case .industrial: "Industrial zone"
        case .coalPowerPlant: "Coal power plant"
        case .nuclearPowerPlant: "Nuclear power plant"
        case .powerLine: "Power line"
        case .roadAcrossPowerLine, .roadDownPowerLine: "Road and power line"
        case .fireStation: "Fire station"
        case .policeStation: "Police station"
        case .park: "Park"
        case .hospital: "Hospital"
        case .church: "Church"
        case .rail: "Rail"
        case .railAcrossRoad, .railDownRoad: "Rail and road"
        case .railAcrossPowerLine, .railDownPowerLine: "Rail and power line"
        case .stadium: "Stadium"
        case .seaport: "Seaport"
        case .airport: "Airport"
        }
    }

    /// A short name for tight spaces, in lower case.
    public var shortName: String {
        switch self {
        case .none: "empty"
        case .coalPowerPlant: "coal plant"
        case .nuclearPowerPlant: "nuclear plant"
        case .roadAcrossPowerLine, .roadDownPowerLine: "road and line"
        case .railAcrossPowerLine, .railDownPowerLine: "rail and line"
        case .residential, .commercial, .industrial: String(displayName.dropLast(" zone".count)).lowercased()
        default: displayName.lowercased()
        }
    }
}

extension TileState {
    /// What the player can do with the tile.
    public var buildAdvice: String {
        switch use {
        case .road: isBridge ? "a bridge, carries traffic" : "carries traffic"
        case .residential, .commercial, .industrial:
            development == 0 ? "not developed yet" : "developed to level \(development)"
        case .coalPowerPlant, .nuclearPowerPlant, .fireStation, .policeStation, .stadium, .seaport, .airport:
            "bulldoze its centre to clear it"
        case .hospital, .church: "the city built it on a residential zone"
        case .park: "the bulldozer clears it for $1"
        case .powerLine: kind == .water ? "laid under water" : "strung on poles"
        case .roadAcrossPowerLine, .roadDownPowerLine: "carries traffic across the line"
        case .rail: isTunnel ? "a tunnel, carries trips under water" : "carries trips"
        case .railAcrossRoad, .railDownRoad: "a level crossing, carries trips both ways"
        case .railAcrossPowerLine, .railDownPowerLine: "carries trips under the line"
        case .none: kind.buildAdvice
        }
    }
}

extension TileMap {
    /// A line for the tile readout: what is on a tile and what the player can do with it.
    public func describe(_ x: Int, _ y: Int, definitions: CityDefinitions, hazards: Hazards? = nil) -> String {
        if let hazards {
            if hazards.isBurning(x, y) { return "On fire - it burns out to rubble, sooner near a fire station" }
            if hazards.isFlooded(x, y) { return "Flooded - the water drains to bare ground in time" }
            if hazards.isRadioactive(x, y) {
                return "Radioactive - nothing can be built here until it decays, which can take decades"
            }
            if let owner = hazards.holeOwner(x, y) {
                let use = self[owner.x, owner.y].use
                let name = use.displayName.lowercased()
                return use.isZone
                    ? "Damaged - part of a \(name), rebuilt as it grows"
                    : "Damaged - part of a \(name), repaired in time once it is clear ground"
            }
        }

        let state = self[x, y]
        if state.use == .park {
            return Parks.isWooded(x, y) ? "Park - woods that make the land around them worth more" : "Park - a fountain"
        }
        if state.use == .none {
            return "\(state.kind.displayName) - \(state.kind.buildAdvice)"
        }
        guard state.isZoned else {
            return "\(definitions.name(of: state.use)) - \(state.buildAdvice)"
        }

        let spec = definitions.zone(state.use)
        let center = centerOf(x, y)
        let centerState = self[center.x, center.y]
        let population = spec.population(on: self, centerX: center.x, centerY: center.y)
        let growth =
            if centerState.development > 0 {
                "level \(centerState.development) of \(spec.levels), population \(population)"
            } else if population > 0 {
                population == 1 ? "1 house" : "\(population) houses"
            } else {
                "not developed yet"
            }
        return centerState.powered ? "\(spec.name) - \(growth)" : "\(spec.name) - \(growth), no power"
    }
}
