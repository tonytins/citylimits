/// The ticker's headlines: flavour lines for when nothing is happening, milestones, and lines
/// that react to the state the city is in. `{city}` and `{year}` are filled in when shown.
public enum Headlines {
    public static let flavour = [
        "{city} News Ticker: Moving Left Since It Was Switched On",
        "{city} News Ticker: All The News That Fits On One Line",
        "{city} News Ticker: Read It Before It Goes",
        "{city} News Ticker: Now With Fewer Typos Than Last Week",
        "{city} News Ticker: Faster Than A Newspaper, Slower Than Gossip",
        "{city} News Ticker: If You Missed It, It'll Be Back",
        "Local Man Wins Pie Contest, Loses Appetite",
        "Pigeons Elect New Leader; Statue Declines To Comment",
        "Library Fines Forgiven For Book Returned 40 Years Late",
        "Mayor's Cat Seen Napping In Council Chamber, Again",
        "Garden Club Feud Over Prize Marrow Enters Third Year",
        "Lost Dog Finds Owner, Who Had Also Been Lost",
        "Survey: 8 In 10 Residents Have Opinions About Survey",
        "Town Clock Fixed; Residents Unsure What To Complain About",
        "Bake Sale Raises Record Funds, Crumbs",
        "Local Choir Hits High Note, Window",
        "Squirrel Population Declares Park Its Own",
        "Ice Cream Truck Jingle Stuck In Heads Citywide",
        "Bus Arrives On Time; Passengers Suspicious",
        "Retired Postman Still Walks Route Out Of Habit",
        "Crossword Setter Admits 7 Down Had No Answer",
        "Weather Forecast Correct For Third Day Running",
        "Annual Duck Race Won By Duck",
        "Kite Festival Grounded By Lack Of Wind",
        "Hardware Store Runs Out Of Duct Tape; Repairs Halt",
        "Opinion: Pineapple On Pizza Divides {city} Households",
        "Barber Shop Quartet Splits Over Barber",
        "Lemonade Stand Turns Profit, Hires Younger Brother",
        "Marching Band Practice Marches Into River",
        "Local Inventor Patents Self-Buttering Toast",
        "Chess Club Match Ends In Polite Stalemate",
        "Goldfish Named Employee Of The Month At Pet Shop",
        "{city} Spelling Bee Won On The Word 'Bee'",
        "Night Owls And Early Birds Agree To Disagree",
        "Snail Race Results Expected Sometime Next Week",
        "Museum Unveils Painting, Promptly Hangs It Upside Down",
        "Park Bench Dedicated To 'Whoever Sat Here Most'",
        "Neighbourhood Watch Spots Neighbour, Watches",
        "Grandmother's Secret Recipe Turns Out To Be Store-Bought",
        "Pothole Named After Councillor Who Promised To Fix It",
        "Juggler Drops Act, Then Everything Else",
        "Library Adds Book On How To Return Library Books",
        "Free Concert In Park Draws Crowd, Mosquitoes",
        "Toddler Elected Honorary Mayor For A Day, Bans Naps",
        "Brass Band Plays On Through Rain, Rust",
        "Diner Introduces Breakfast For Dinner; Dinner Fans Outraged",
        "Mystery Of The Missing Garden Gnome Deepens",
        "Garden Gnome Returns With Postcards From Abroad",
        "Fishing Contest Won By Man Who Bought Fish",
        "Roundabout Proves Popular With Drivers Going Nowhere",
        "Ballroom Dance Night Steps On Few Toes",
        "Scientists Confirm Mondays Feel Longer",
        "Dentist Recommends Brushing, Shocking Nobody",
        "Bicycle Built For Two Ridden By Three",
        "Town Historian Discovers {city} Was Here All Along",
        "Shop Sign Misspelling Becomes Beloved Landmark",
        "Cat Show Judges Ignored By Every Contestant",
        "Lighthouse Keeper Retires Inland",
        "Carpenters Strike, Nail It",
        "Psychic Fair Cancelled Due To Unforeseen Circumstances",
        "Model Railway Club Announces Very Small Expansion",
        "Pancake Flipping Record Falls, Pancake Too",
        "Local Parrot Learns Weather Report, Gets Better Ratings",
        "Accordion Sales Up; Neighbours Down",
    ]

    public static let populationMilestones = [500, 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 250_000, 500_000]
    public static let populationMilestone = "{city} Tops {count} People; Welcome Signs Updated"
    public static let newYear = "Happy New Year From {city}: Welcome To {year}"

    /// Lines the city earns by the state it's in, each with the test for that state.
    public static let reactive: [ReactiveHeadline] = [
        ReactiveHeadline(when: { $0.budget.taxRate >= 12 }, lines: [
            "Residents Grumble As Tax Bills Land On Doormats",
            "Tax Rate Tops Dinner Table Conversation In {city}",
            "Businesses Eye Neighbouring Towns As Taxes Bite",
        ]),
        ReactiveHeadline(when: { $0.budget.taxRate <= 4 && $0.census.cityPopulation > 0 }, lines: [
            "Low Taxes Draw Newcomers To {city}",
            "Accountants Bored As Tax Season Passes In Minutes",
        ]),
        ReactiveHeadline(when: { $0.demand.residential > CityDemand.residentialLimit / 2 }, lines: [
            "Housing Shortage: Families Queue For Every New Home",
            "Estate Agents Report Bidding Wars Over Garden Sheds",
            "Newcomers Sleep In Cars As {city} Runs Out Of Homes",
        ]),
        ReactiveHeadline(when: { $0.demand.residential < -CityDemand.residentialLimit / 2 }, lines: [
            "For Sale Signs Outnumber Lawn Gnomes In {city}",
            "Landlords Offer Free Pizza To Fill Empty Flats",
        ]),
        ReactiveHeadline(when: { $0.demand.commercial > CityDemand.businessLimit / 2 }, lines: [
            "Shoppers Travel Out Of Town For Basic Groceries",
            "Entrepreneurs Look For Space To Open Shops In {city}",
        ]),
        ReactiveHeadline(when: { $0.demand.industrial > CityDemand.businessLimit / 2 }, lines: [
            "Factories Seek Land As Workers Wait For Jobs",
            "Job Hunters Outnumber Jobs, Say Unemployment Office Staff",
        ]),
        ReactiveHeadline(when: { $0.treasury.funds < 0 }, lines: [
            "City Hall Checks Sofa Cushions As Treasury Runs Dry",
            "{city} In The Red; Accountants Reach For Red Pens",
        ]),
        ReactiveHeadline(when: { (0..<1_000).contains($0.treasury.funds) }, lines: [
            "Treasury Running Low, Council Cancels Biscuits At Meetings",
        ]),
        ReactiveHeadline(when: { $0.treasury.funds > 100_000 }, lines: [
            "Treasury Overflows; Mayor Considers Gold-Plated Benches",
        ]),
        ReactiveHeadline(when: { $0.traffic.average > 60 || $0.traffic.busiest >= 192 }, lines: [
            "Commuters Swap Recipes While Stuck In Traffic",
            "Rush Hour Now Lasts Most Of The Day, Drivers Say",
            "Car Horn Symphony Performed Nightly On {city} Roads",
        ]),
        ReactiveHeadline(when: { $0.power.unpoweredBuildings > 0 && $0.census.cityPopulation > 0 }, lines: [
            "Residents Read By Candlelight As Power Fails To Reach Homes",
            "Candle Makers Report Record Sales In {city}",
        ]),
        ReactiveHeadline(when: { $0.power.nuclearPlants > 0 }, lines: [
            "Nuclear Plant Workers Glow With Pride",
        ]),
        ReactiveHeadline(when: { $0.services.fireStations > 0 }, lines: [
            "Firefighters Rescue Cat From Tree; Cat Unimpressed",
        ]),
        // Fire risk, hinted at ahead of a fire, as Tony Bark asked after SimCity 3000.
        ReactiveHeadline(when: FireRisk.zonesOutOfReach, lines: [
            "Fire Chief Warns Whole Neighbourhoods Are Beyond Reach Of An Engine",
            "Residents Ask Why The Nearest Fire Station Is A Bus Ride Away",
        ]),
        ReactiveHeadline(when: FireRisk.fundingIsCut, lines: [
            "Firefighters Say Budget Cuts Leave Engines Short Of Crews",
            "Fire Station Holds Bake Sale To Pay For New Hoses",
        ]),
        ReactiveHeadline(when: FireRisk.homesAgainstWoods, lines: [
            "Homeowners By The Woods Worry About Stray Sparks",
            "Fire Chief Urges {city} To Clear Brush Away From Homes",
        ]),
        ReactiveHeadline(when: { $0.services.policeStations > 0 }, lines: [
            "Police Recover Stolen Bicycle, Return It With Fresh Air In Tyres",
        ]),
        ReactiveHeadline(when: { $0.census.cityPopulation == 0 }, lines: [
            "Population Of {city}: Tumbleweeds, Mostly",
            "Town Planners Gaze At Empty Fields, Dream Big",
        ]),
        ReactiveHeadline(when: { [12, 1, 2].contains($0.date.month) }, lines: [
            "Snow Day Declared; Snowball Fights Break Out Downtown",
            "Hot Chocolate Stands Do Brisk Winter Trade",
        ]),
        ReactiveHeadline(when: { [6, 7, 8].contains($0.date.month) }, lines: [
            "Heatwave Melts Ice Cream Faster Than It Can Be Eaten",
            "Summer Fair Opens; Candyfloss Shortage Feared",
        ]),
    ]

    /// Lines for the loading screen.
    public static let loading = [
        "Reticulating splines", "Zoning the zoning board", "Untangling power lines", "Teaching pigeons the bus routes",
        "Counting the potholes twice", "Laying the first brick", "Arguing about where the park goes",
        "Polishing the town hall clock", "Painting lines on the roads", "Persuading the river to stay put",
        "Rounding up the traffic", "Warming up the power plant", "Filing the planning permission",
        "Measuring the land value with a ruler", "Waking the night shift", "Inflating the tax forms",
        "Planting trees, then moving them", "Hiring a very patient mayor",
    ]
}

/// A set of lines the city earns by being in a state.
public struct ReactiveHeadline: Sendable {
    public let when: @Sendable (CitySimulation) -> Bool
    public let lines: [String]
}

/// Hints at fire risk before a fire, after SimCity 3000's advisers. They are news only in a city
/// with people, where fires can start on their own.
public enum FireRisk {
    /// Fire cover a zone needs before it counts as within reach.
    public static let reachingCover = 20
    /// The funding effect below which the fire service counts as cut.
    public static let fundingCut = 700
    /// Tiles of trees around a home that make it a worry.
    public static let woodsAround = 4
    private static let peopleBeforeFireIsNews = 20

    /// Whether more than half the zones are beyond a fire station's reach.
    @Sendable public static func zonesOutOfReach(_ city: CitySimulation) -> Bool {
        guard isNews(city) else { return false }
        var (zones, unreached) = (0, 0)
        for y in 0..<city.map.height {
            for x in 0..<city.map.width where city.map[x, y].isZoneCenter {
                zones += 1
                if city.services.fireCoverage(x, y) < reachingCover { unreached += 1 }
            }
        }
        return unreached * 2 > zones
    }

    @Sendable public static func fundingIsCut(_ city: CitySimulation) -> Bool {
        isNews(city) && city.budget.fireEffect < fundingCut
    }

    /// Whether a residential zone with people in it has woods close around it.
    @Sendable public static func homesAgainstWoods(_ city: CitySimulation) -> Bool {
        guard isNews(city) else { return false }
        let residential = city.definitions.zone(.residential)
        for y in 0..<city.map.height {
            for x in 0..<city.map.width {
                let state = city.map[x, y]
                if state.use == .residential && state.isZoneCenter
                    && residential.population(on: city.map, centerX: x, centerY: y) > 0 && treesAround(city.map, x, y) >= woodsAround
                {
                    return true
                }
            }
        }
        return false
    }

    private static func isNews(_ city: CitySimulation) -> Bool {
        city.randomFires && city.census.cityPopulation > peopleBeforeFireIsNews
    }

    // The tiles of trees in the ring just outside a 3 by 3 zone.
    private static func treesAround(_ map: TileMap, _ x: Int, _ y: Int) -> Int {
        var trees = 0
        for dy in -2...2 {
            for dx in -2...2 where abs(dx) == 2 || abs(dy) == 2 {
                let state = map.state(atOrDefault: x + dx, y + dy)
                if state.use == .none && state.kind == .trees { trees += 1 }
            }
        }
        return trees
    }
}
