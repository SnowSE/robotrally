namespace RobotRally.Game;

public enum BaseKind { Car, Motorcycle, Turtle, Plane, Rocket }

public sealed record AddOnInfo(string Id, string Name, string Emoji);

public sealed record BaseInfo(BaseKind Kind, string Name, string Emoji, IReadOnlyList<AddOnInfo> AddOns);

public sealed record CharacterLook(BaseKind Base, string Color, IReadOnlyList<string> AddOns)
{
    public bool Has(string addOn) => AddOns.Contains(addOn);
    public static CharacterLook Default => new(BaseKind.Car, Characters.Colors[0], []);
}

public static class Characters
{
    public static IReadOnlyList<string> Colors { get; } =
        ["#ff4d6d", "#ff9f1c", "#ffd60a", "#38d66b", "#2ec4ff", "#4361ee", "#9b5de5", "#ff6ad5"];

    public static IReadOnlyList<BaseInfo> Bases { get; } =
    [
        new(BaseKind.Car, "Race Car", "🏎️", [
            new("spoiler", "Spoiler", "🪽"),
            new("flames", "Flames", "🔥"),
            new("stripes", "Racing Stripes", "🏁"),
            new("siren", "Siren Light", "🚨"),
        ]),
        new(BaseKind.Motorcycle, "Motorcycle", "🏍️", [
            new("sidecar", "Sidecar", "🛺"),
            new("streamers", "Streamers", "🎏"),
            new("helmet", "Cool Helmet", "⛑️"),
            new("exhaust", "Exhaust Fire", "💥"),
        ]),
        new(BaseKind.Turtle, "Turtle", "🐢", [
            new("shades", "Sunglasses", "🕶️"),
            new("partyhat", "Party Hat", "🥳"),
            new("spikes", "Shell Spikes", "🦔"),
            new("bowtie", "Bow Tie", "🎀"),
        ]),
        new(BaseKind.Plane, "Airplane", "✈️", [
            new("propeller", "Propeller", "🌀"),
            new("banner", "Banner", "🏳️"),
            new("smoke", "Smoke Trail", "☁️"),
            new("stars", "Wing Stars", "⭐"),
        ]),
        new(BaseKind.Rocket, "Rocket", "🚀", [
            new("fins", "Big Fins", "🦈"),
            new("flame", "Flame Trail", "🔥"),
            new("astronaut", "Astronaut", "👩‍🚀"),
            new("antenna", "Antenna", "📡"),
        ]),
    ];

    public static BaseInfo Info(BaseKind kind) => Bases.First(b => b.Kind == kind);

    public static bool IsValid(CharacterLook look) =>
        Colors.Contains(look.Color)
        && look.AddOns.Distinct().Count() == look.AddOns.Count
        && look.AddOns.All(a => Info(look.Base).AddOns.Any(x => x.Id == a));
}

public static class Nicknames
{
    private static readonly string[] Adjectives =
    [
        "Zippy", "Speedy", "Giggly", "Turbo", "Sparkly", "Bouncy", "Mighty", "Sneaky", "Jolly", "Cosmic",
        "Wobbly", "Fuzzy", "Snappy", "Dizzy", "Lucky", "Super", "Happy", "Silly", "Brave", "Zappy",
    ];

    private static readonly string[] Nouns =
    [
        "Octopus", "Penguin", "Taco", "Pickle", "Llama", "Dragon", "Banana", "Panda", "Waffle", "Narwhal",
        "Comet", "Noodle", "Gecko", "Muffin", "Otter", "Yeti", "Robot", "Pancake", "Koala", "Unicorn",
    ];

    public static string Random(Random rng, ISet<string> taken)
    {
        for (var i = 0; i < 200; i++)
        {
            var name = $"{Adjectives[rng.Next(Adjectives.Length)]} {Nouns[rng.Next(Nouns.Length)]}";
            if (!taken.Contains(name)) return name;
        }
        // 400 combos and at most 24 players: practically unreachable, but stay unique anyway.
        var n = 2;
        while (taken.Contains($"Racer {n}")) n++;
        return $"Racer {n}";
    }

    public static bool IsGenerated(string name) =>
        (name.Split(' ') is [var a, var b] && Adjectives.Contains(a) && Nouns.Contains(b))
        || (name.StartsWith("Racer ") && int.TryParse(name[6..], out _));
}
