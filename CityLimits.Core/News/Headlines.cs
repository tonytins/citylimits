using CityLimits.Core.Simulation;

namespace CityLimits.Core.News;

/// <summary>
/// The base game's headlines, written for CityLimits after the news tickers of later city
/// builders: flavour from the city's own world, and lines that react to how the city is doing.
/// <c>{city}</c> in a line is the city's name, and <c>{year}</c> the year.
/// </summary>
public static class Headlines
{
    /// <summary>Flavour: things that happen in any city, whatever state it is in.</summary>
    public static readonly string[] Flavour =
    [
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
    ];

    /// <summary>When the city's population first passes a milestone in a session: the milestones, in order.</summary>
    public static readonly int[] PopulationMilestones = [500, 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 250_000, 500_000];

    /// <summary>The line for a population milestone; <c>{count}</c> is the milestone.</summary>
    public const string PopulationMilestone = "{city} Tops {count} People; Welcome Signs Updated";

    /// <summary>The line for a new year, shown in January.</summary>
    public const string NewYear = "Happy New Year From {city}: Welcome To {year}";

    /// <summary>The lines that react to the city: each with what makes it news.</summary>
    public static readonly ReactiveHeadline[] Reactive =
    [
        new(city => city.Budget.TaxRate >= 12,
        [
            "Residents Grumble As Tax Bills Land On Doormats",
            "Tax Rate Tops Dinner Table Conversation In {city}",
            "Businesses Eye Neighbouring Towns As Taxes Bite",
        ]),
        new(city => city.Budget.TaxRate <= 4 && city.Census.CityPopulation > 0,
        [
            "Low Taxes Draw Newcomers To {city}",
            "Accountants Bored As Tax Season Passes In Minutes",
        ]),
        new(city => city.Demand.Residential > CityDemand.ResidentialLimit / 2,
        [
            "Housing Shortage: Families Queue For Every New Home",
            "Estate Agents Report Bidding Wars Over Garden Sheds",
            "Newcomers Sleep In Cars As {city} Runs Out Of Homes",
        ]),
        new(city => city.Demand.Residential < -CityDemand.ResidentialLimit / 2,
        [
            "For Sale Signs Outnumber Lawn Gnomes In {city}",
            "Landlords Offer Free Pizza To Fill Empty Flats",
        ]),
        new(city => city.Demand.Commercial > CityDemand.BusinessLimit / 2,
        [
            "Shoppers Travel Out Of Town For Basic Groceries",
            "Entrepreneurs Look For Space To Open Shops In {city}",
        ]),
        new(city => city.Demand.Industrial > CityDemand.BusinessLimit / 2,
        [
            "Factories Seek Land As Workers Wait For Jobs",
            "Job Hunters Outnumber Jobs, Say Unemployment Office Staff",
        ]),
        new(city => city.Treasury.Funds < 0,
        [
            "City Hall Checks Sofa Cushions As Treasury Runs Dry",
            "{city} In The Red; Accountants Reach For Red Pens",
        ]),
        new(city => city.Treasury.Funds is >= 0 and < 1_000,
        [
            "Treasury Running Low, Council Cancels Biscuits At Meetings",
        ]),
        new(city => city.Treasury.Funds > 100_000,
        [
            "Treasury Overflows; Mayor Considers Gold-Plated Benches",
        ]),
        new(city => city.Traffic.Average > 60 || city.Traffic.Busiest >= 192,
        [
            "Commuters Swap Recipes While Stuck In Traffic",
            "Rush Hour Now Lasts Most Of The Day, Drivers Say",
            "Car Horn Symphony Performed Nightly On {city} Roads",
        ]),
        new(city => city.Power.UnpoweredBuildings > 0 && city.Census.CityPopulation > 0,
        [
            "Residents Read By Candlelight As Power Fails To Reach Homes",
            "Candle Makers Report Record Sales In {city}",
        ]),
        new(city => city.Power.NuclearPlants > 0,
        [
            "Nuclear Plant Workers Glow With Pride",
        ]),
        new(city => city.Services.FireStations > 0,
        [
            "Firefighters Rescue Cat From Tree; Cat Unimpressed",
        ]),

        // Fire risk, hinted at ahead of a fire, as Tony Bark asked after SimCity 3000.
        new(FireRisk.ZonesOutOfReach,
        [
            "Fire Chief Warns Whole Neighbourhoods Are Beyond Reach Of An Engine",
            "Residents Ask Why The Nearest Fire Station Is A Bus Ride Away",
        ]),
        new(FireRisk.FundingIsCut,
        [
            "Firefighters Say Budget Cuts Leave Engines Short Of Crews",
            "Fire Station Holds Bake Sale To Pay For New Hoses",
        ]),
        new(FireRisk.HomesAgainstWoods,
        [
            "Homeowners By The Woods Worry About Stray Sparks",
            "Fire Chief Urges {city} To Clear Brush Away From Homes",
        ]),
        new(city => city.Services.PoliceStations > 0,
        [
            "Police Recover Stolen Bicycle, Return It With Fresh Air In Tyres",
        ]),
        new(city => city.Census.CityPopulation == 0,
        [
            "Population Of {city}: Tumbleweeds, Mostly",
            "Town Planners Gaze At Empty Fields, Dream Big",
        ]),
        new(city => city.Date.Month is 12 or 1 or 2,
        [
            "Snow Day Declared; Snowball Fights Break Out Downtown",
            "Hot Chocolate Stands Do Brisk Winter Trade",
        ]),
        new(city => city.Date.Month is 6 or 7 or 8,
        [
            "Heatwave Melts Ice Cream Faster Than It Can Be Eaten",
            "Summer Fair Opens; Candyfloss Shortage Feared",
        ]),
    ];
}

/// <summary>Headlines that become news when something about the city holds.</summary>
/// <param name="When">Whether the city is in the state the lines report.</param>
/// <param name="Lines">The lines to choose from.</param>
public sealed record ReactiveHeadline(Func<CitySimulation, bool> When, string[] Lines);
