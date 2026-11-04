namespace RobotRally.Game;

[Flags]
public enum DriveInput { None = 0, Up = 1, Down = 2, Left = 4, Right = 8 }

/// <summary>One car in a frame. <see cref="Angle"/> is in degrees and unwrapped (it can pass 360) so CSS rotations stay smooth.</summary>
public readonly record struct ArenaCar(string Id, double X, double Y, double Angle);

/// <summary>Two cars bumped at (X, Y). Strength is the closing speed, in arena units per second.</summary>
public readonly record struct ArenaBump(string A, string B, double X, double Y, double Strength);

/// <summary><see cref="Time"/> is the arena's own clock in seconds (it pauses while the arena is parked), so screens
/// can blend smoothly between frames however unevenly the frames arrive.</summary>
public sealed record ArenaFrame(long Seq, double Time, IReadOnlyList<ArenaCar> Cars, IReadOnlyList<ArenaBump> Bumps);

/// <summary>
/// The lobby's bumper-car floor: every racer is a disc that kids push around with the arrow keys.
/// Cars bounce off each other (equal mass, springy) and off the walls. Thread-safe.
/// </summary>
public sealed class LobbyArena(Random? rng = null)
{
    public const double Width = 1600;
    public const double Height = 900;
    public const double Radius = 46;
    public const double Accel = 1500;
    public const double MaxSpeed = 470;
    public const double Drag = 2.4;
    public const double Restitution = 0.9;
    /// <summary>Bumped cars always fly apart at least this fast, so even a gentle tap feels like a bump.</summary>
    public const double MinBounce = 230;
    /// <summary>Closing speed that counts as a bump (sound + pow). Slower contact is just pushing.</summary>
    public const double BumpSpeed = 90;
    public const double WallBounce = 0.55;
    // Walls sit in from the edges so name tags (centered under each car) never get cut off.
    public const double MinX = 100, MaxX = Width - 100, MinY = Radius + 6, MaxY = Height - Radius - 38;
    private const double MaxStep = 1.0 / 60;
    private const double BumpCooldown = 0.3;

    private sealed class Car(string id)
    {
        public string Id { get; } = id;
        public double X, Y, Vx, Vy, Angle, Spin;
        public DriveInput Input;
    }

    private readonly object _gate = new();
    private readonly Random _rng = rng ?? new Random();
    private readonly Dictionary<string, Car> _cars = [];
    private readonly Dictionary<(string, string), double> _lastBump = [];
    private double _clock;
    private long _seq;

    public int Count
    {
        get { lock (_gate) return _cars.Count; }
    }

    /// <summary>Adds cars for new racers (at a free spot) and drops cars for racers who left.</summary>
    public void Sync(IEnumerable<string> ids)
    {
        lock (_gate)
        {
            var keep = ids.ToHashSet();
            foreach (var gone in _cars.Keys.Where(k => !keep.Contains(k)).ToList()) _cars.Remove(gone);
            foreach (var id in keep.Where(k => !_cars.ContainsKey(k)))
            {
                var car = new Car(id) { Angle = _rng.Next(4) * 90 };
                (car.X, car.Y) = FreeSpot();
                _cars[id] = car;
            }
        }
    }

    private (double, double) FreeSpot()
    {
        (double, double) best = (Width / 2, Height / 2);
        var bestGap = -1.0;
        for (var i = 0; i < 40; i++)
        {
            var x = MinX + _rng.NextDouble() * (MaxX - MinX);
            var y = MinY + _rng.NextDouble() * (MaxY - MinY);
            var gap = _cars.Count == 0 ? double.MaxValue : _cars.Values.Min(c => Math.Sqrt((c.X - x) * (c.X - x) + (c.Y - y) * (c.Y - y)));
            if (gap > 3 * Radius) return (x, y);
            if (gap > bestGap) (best, bestGap) = ((x, y), gap);
        }
        return best;
    }

    public void SetInput(string id, DriveInput input)
    {
        lock (_gate)
            if (_cars.TryGetValue(id, out var car)) car.Input = input & (DriveInput.Up | DriveInput.Down | DriveInput.Left | DriveInput.Right);
    }

    /// <summary>Everyone stops (e.g. the demo or the race is starting).</summary>
    public void Halt()
    {
        lock (_gate)
            foreach (var c in _cars.Values)
            {
                c.Vx = c.Vy = c.Spin = 0;
                c.Input = DriveInput.None;
            }
    }

    public ArenaFrame Snapshot()
    {
        lock (_gate) return Frame([]);
    }

    /// <summary>Advances the physics by <paramref name="seconds"/> (in small sub-steps) and returns the new frame.</summary>
    public ArenaFrame Step(double seconds)
    {
        lock (_gate)
        {
            var bumps = new List<ArenaBump>();
            var left = Math.Clamp(seconds, 0, 0.25);
            while (left > 1e-9)
            {
                var dt = Math.Min(MaxStep, left);
                SubStep(dt, bumps);
                left -= dt;
            }
            return Frame(bumps);
        }
    }

    private ArenaFrame Frame(List<ArenaBump> bumps) =>
        new(++_seq, Math.Round(_clock, 4), _cars.Values.Select(c => new ArenaCar(c.Id, Math.Round(c.X, 1), Math.Round(c.Y, 1), Math.Round(c.Angle, 1))).ToList(), bumps);

    private void SubStep(double dt, List<ArenaBump> bumps)
    {
        _clock += dt;
        var cars = _cars.Values.ToList();

        foreach (var c in cars)
        {
            double ax = 0, ay = 0;
            if (c.Input.HasFlag(DriveInput.Left)) ax -= 1;
            if (c.Input.HasFlag(DriveInput.Right)) ax += 1;
            if (c.Input.HasFlag(DriveInput.Up)) ay -= 1;
            if (c.Input.HasFlag(DriveInput.Down)) ay += 1;
            var len = Math.Sqrt(ax * ax + ay * ay);
            if (len > 0)
            {
                c.Vx += ax / len * Accel * dt;
                c.Vy += ay / len * Accel * dt;
                var speed = Math.Sqrt(c.Vx * c.Vx + c.Vy * c.Vy);
                if (speed > MaxSpeed)
                {
                    // Driving can't push past top speed, but a bump can (it just slows down quickly).
                    var cap = Math.Max(MaxSpeed, speed - Accel * dt * 2);
                    c.Vx *= cap / speed;
                    c.Vy *= cap / speed;
                }
            }
            var drag = Math.Max(0, 1 - Drag * dt);
            c.Vx *= drag;
            c.Vy *= drag;
            c.X += c.Vx * dt;
            c.Y += c.Vy * dt;

            // Face the way the kid is steering (or, coasting, the way the car is going), unless still spinning
            // from a bump. Steering wins so that bounces never flip the car back and forth.
            c.Angle += c.Spin * dt;
            c.Spin *= Math.Max(0, 1 - 3 * dt);
            var v = Math.Sqrt(c.Vx * c.Vx + c.Vy * c.Vy);
            double? heading = len > 0 ? Math.Atan2(ay, ax) * 180 / Math.PI
                : v > 40 ? Math.Atan2(c.Vy, c.Vx) * 180 / Math.PI : null;
            if (heading is { } target && Math.Abs(c.Spin) < 90)
            {
                var delta = ((target - c.Angle) % 360 + 540) % 360 - 180;
                c.Angle += delta * Math.Min(1, 10 * dt);
            }

            // Walls: a coasting car bounces off; a car being driven into the wall just stops against it
            // (bouncing while the key is held makes it rattle).
            if (c.X < MinX) { c.X = MinX; if (c.Vx < 0) c.Vx = ax < 0 ? 0 : -c.Vx * WallBounce; }
            if (c.X > MaxX) { c.X = MaxX; if (c.Vx > 0) c.Vx = ax > 0 ? 0 : -c.Vx * WallBounce; }
            if (c.Y < MinY) { c.Y = MinY; if (c.Vy < 0) c.Vy = ay < 0 ? 0 : -c.Vy * WallBounce; }
            if (c.Y > MaxY) { c.Y = MaxY; if (c.Vy > 0) c.Vy = ay > 0 ? 0 : -c.Vy * WallBounce; }
        }

        for (var i = 0; i < cars.Count; i++)
            for (var j = i + 1; j < cars.Count; j++)
                Collide(cars[i], cars[j], bumps);
    }

    private void Collide(Car a, Car b, List<ArenaBump> bumps)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist >= 2 * Radius) return;
        if (dist < 1e-6)
        {
            dx = 1; dy = 0; dist = 1e-6;
        }
        var nx = dx / dist;
        var ny = dy / dist;

        // Push apart so they never overlap.
        var push = (2 * Radius - dist) / 2;
        a.X -= nx * push; a.Y -= ny * push;
        b.X += nx * push; b.Y += ny * push;

        var closing = (a.Vx - b.Vx) * nx + (a.Vy - b.Vy) * ny; // > 0 when moving toward each other
        if (closing <= 0) return;

        // Equal-mass bounce, plus a minimum kick so it feels springy like a bumper car.
        var j = (1 + Restitution) * closing / 2;
        var after = Restitution * closing;
        if (closing >= BumpSpeed && after < MinBounce) j += (MinBounce - after) / 2;
        a.Vx -= j * nx; a.Vy -= j * ny;
        b.Vx += j * nx; b.Vy += j * ny;

        if (closing < BumpSpeed) return;
        // Glancing hits spin the cars a little (direction from the side they were hit on).
        var tangent = (a.Vx - b.Vx) * -ny + (a.Vy - b.Vy) * nx;
        var spin = Math.Clamp(closing * 0.9, 120, 600) * (tangent >= 0 ? 1 : -1);
        a.Spin += spin;
        b.Spin -= spin;

        var key = string.CompareOrdinal(a.Id, b.Id) < 0 ? (a.Id, b.Id) : (b.Id, a.Id);
        if (_lastBump.TryGetValue(key, out var t) && _clock - t < BumpCooldown) return;
        _lastBump[key] = _clock;
        bumps.Add(new ArenaBump(a.Id, b.Id, Math.Round(a.X + nx * Radius, 1), Math.Round(a.Y + ny * Radius, 1), Math.Round(closing)));
    }

    // ---- test helpers ----
    internal void Place(string id, double x, double y, double vx = 0, double vy = 0)
    {
        lock (_gate)
        {
            var c = _cars[id];
            (c.X, c.Y, c.Vx, c.Vy) = (x, y, vx, vy);
        }
    }

    internal (double X, double Y, double Vx, double Vy) StateOf(string id)
    {
        lock (_gate)
        {
            var c = _cars[id];
            return (c.X, c.Y, c.Vx, c.Vy);
        }
    }
}
