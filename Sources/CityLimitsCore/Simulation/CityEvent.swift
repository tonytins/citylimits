/// Something the city tells the player, as Micropolis' messages do.
public enum CityMessage: CaseIterable, Sendable {
    case needPower
    case blackouts
    case brownouts
    case highTaxes
    case roadsNeedFunding
    case needFireStation
    case needPoliceStation
    case fireNeedsFunding
    case policeNeedsFunding
    case trafficJams
    case needRail
    case highPollution
    case highCrime
    case fireReported
    case explosionReported
    case floodReported
    case earthquakeReported
    case needStadium
    case needSeaport
    case needAirport
    case meltdownReported
    case planeCrashReported
    case helicopterCrashReported
    case shipwreckReported
    case heavyTrafficReported
    case tornadoReported
    case monsterReported
}

/// A disaster a scenario or the player can set off.
public enum Disaster: CaseIterable, Sendable {
    case fire
    case flood
    case earthquake
    case meltdown
    case planeCrash
    case shipwreck
    case tornado
    case monster
}

/// What happened in the city during a step, for whoever is watching: the game shows it, a
/// headless run logs it, and a test checks it.
public enum CityEvent: Sendable {
    case message(CityMessage)
    /// The year was settled into the treasury.
    case yearSettled(BudgetYear)
    /// Something blew up at a tile, for drawing.
    case exploded(x: Int, y: Int)
    /// The ground shook, for shaking the view.
    case earthquake
    case ordinance(OrdinanceNews)
}
