using System.Numerics;

int passed = 0;
void Check(string name, Action action)
{
    action();
    Console.WriteLine($"PASS {name}");
    passed++;
}
void Expect(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Advance(GameWorld world, double seconds, Vector2 direction, int fps = 120)
{
    for (int frame = 0; frame < (int)Math.Round(seconds * fps); frame++) world.Advance(1.0 / fps, direction);
}

Check("Equal travel speed on diagonals and axes", () =>
{
    var a = new GameWorld(1); var b = new GameWorld(1);
    Advance(a, 0.5, Vector2.UnitX); Advance(b, 0.5, Vector2.One);
    Expect(Math.Abs(Vector2.Distance(a.Player, GameWorld.Center) - Vector2.Distance(b.Player, GameWorld.Center)) < 0.02, "Diagonal boost");
});
Check("Player stays within the arena on every edge", () =>
{
    for (int angle = 0; angle < 360; angle++)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        Vector2 clamped = GameWorld.ClampPlayer(GameWorld.Center + direction * 10000);
        var normalized = (clamped - GameWorld.Center) / (GameWorld.ArenaRadii - new Vector2(GameWorld.PlayerRadius));
        Expect(normalized.LengthSquared() <= 1.00001, "Escaped arena");
    }
});
Check("30 FPS and 120 FPS produce the same travel and clock", () =>
{
    var a = new GameWorld(1); var b = new GameWorld(1);
    Advance(a, 1, Vector2.UnitX, 30); Advance(b, 1, Vector2.UnitX, 120);
    Expect(Vector2.Distance(a.Player, b.Player) < 0.001 && Math.Abs(a.Elapsed - b.Elapsed) < 1e-8, "Frame-dependent simulation");
});
Check("Enemies move immediately and steer toward the moving player", () =>
{
    var world = new GameWorld(4);
    var spawn = GameWorld.Center + new Vector2(300, 0);
    world.SpawnThreat(spawn, 100);
    var enemy = world.Enemies[0]; var velocity = enemy.Velocity;
    world.Advance(1.0 / 120, -Vector2.UnitY);
    Expect(enemy.Position.X < spawn.X && !world.IsDead, "Enemy did not move immediately");
    Advance(world, 0.5, -Vector2.UnitY);
    Expect(enemy.Position.X < spawn.X && enemy.Velocity.Y < -1 && enemy.Velocity != velocity,
        "Enemy did not track player movement");
    Expect(Math.Abs(enemy.Velocity.Length() - 100) < 0.01, "Homing changed speed");
});
Check("Homing turns smoothly, uses the shortest turn, and preserves speed", () =>
{
    float turnLimit = GameWorld.HomingTurnSpeed / 120;
    Vector2 turned = GameWorld.TurnTowards(new Vector2(300, 0), Vector2.UnitY, turnLimit);
    Expect(Math.Abs(MathF.Atan2(turned.Y, turned.X) - turnLimit) < 1e-6f, "Instant or excessive steering");
    Expect(Math.Abs(turned.Length() - 300) < 0.001, "Speed changed during steering");
    float a = 179 * MathF.PI / 180, b = -179 * MathF.PI / 180;
    turned = GameWorld.TurnTowards(new Vector2(MathF.Cos(a), MathF.Sin(a)) * 200,
        new Vector2(MathF.Cos(b), MathF.Sin(b)), 0.01f);
    float delta = MathF.IEEERemainder(MathF.Atan2(turned.Y, turned.X) - a, MathF.Tau);
    Expect(delta > 0 && delta <= 0.01001f, "Turn crossed angle seam the long way");
    Expect(GameWorld.TurnTowards(new Vector2(100, 0), Vector2.Zero, turnLimit) == new Vector2(100, 0), "Zero target broke steering");
});
Check("Swept collision catches crossings and permits near misses", () =>
{
    Expect(GameWorld.HitsPlayer(new(-100, 0), new(100, 0)), "Missed high speed crossing");
    Expect(!GameWorld.HitsPlayer(new(-100, 22), new(100, 22)), "Unfair near miss collision");
});
Check("One contact ends the game; death freezes clock; retry clears threats", () =>
{
    var world = new GameWorld(2);
    world.SpawnThreat(GameWorld.Center + new Vector2(100, 0), 100);
    Advance(world, 2, Vector2.Zero);
    Expect(world.IsDead, "Did not die");
    double time = world.Elapsed; var position = world.Player;
    Advance(world, 2, Vector2.One);
    Expect(world.Elapsed == time && world.Player == position, "Dead world kept running");
    world.Reset();
    Expect(!world.IsDead && world.Elapsed == 0 && world.Enemies.Count == 0 && world.Player == GameWorld.Center
        && world.WaveSize == 1 && world.SpawnWaveSerial == 0, "Retry failed");
});
Check("Enemies outside the far edge are removed", () =>
{
    var world = new GameWorld(3);
    world.SpawnThreat(GameWorld.Center + new Vector2(330, 0), 100);
    var enemy = world.Enemies[0];
    enemy.Position = GameWorld.Center - new Vector2(370, 0);
    world.Advance(1.0 / 120, Vector2.Zero);
    Expect(world.Enemies.Count == 0, "Enemy was retained offscreen");
});
Check("Difficulty and threat count remain capped after several minutes", () =>
{
    var world = new GameWorld(3);
    for (int i = 0; i < 1600; i++)
    {
        foreach (var enemy in world.Enemies) enemy.Position = GameWorld.Center + new Vector2(1000, 0);
        world.Advance(0.25, Vector2.Zero);
        Expect(world.Enemies.Count <= GameWorld.MaxEnemies, "Too many threats");
    }
    Expect(world.EnemySpeed == 300 && world.SpawnInterval == 0.36 && world.WaveSize == GameWorld.MaxEnemies, "Difficulty did not settle at cap");
    for (int i = 0; i < 100; i++) world.SpawnThreat(GameWorld.Center + new Vector2(300, 0), 200);
    Expect(world.Enemies.Count == GameWorld.MaxEnemies, "Manual spawn exceeded threat cap");
});
Check("Every ten seconds spawns a larger simultaneous group with one cue per wave", () =>
{
    var world = new GameWorld(7);
    for (int tick = 1; tick <= 3600; tick++)
    {
        foreach (var enemy in world.Enemies) enemy.Position = GameWorld.Center + new Vector2(1000, 0);
        int previousSerial = world.SpawnWaveSerial;
        world.Advance(1.0 / 120, Vector2.Zero);
        int expected = 1 + tick / 1200;
        Expect(world.WaveSize == expected, "Wave size changed at the wrong time");
        int cueCount = world.SpawnWaveSerial - previousSerial;
        Expect(cueCount is 0 or 1, "Multiple cues for one simultaneous wave");
        if (tick % 1200 == 0)
        {
            Expect(world.Enemies.Count == expected && cueCount == 1,
                $"At {tick / 120}s expected {expected} simultaneous monsters and one cue");
        }
        if (world.Enemies.Count > 0) Expect(cueCount == 1, "Spawn cue missing");
    }
    world.Reset();
    Expect(world.WaveSize == 1 && world.SpawnWaveSerial == 0, "Restart retained difficulty or cue");
});
Check("Spawn locations and intervals vary, with no fixed angle pairing or point-blank hits", () =>
{
    var world = new GameWorld(42);
    var observed = new HashSet<GameWorld.Enemy>();
    var sectors = new HashSet<int>();
    var intervals = new HashSet<int>();
    double lastWave = -1;
    int pairs = 0;
    for (int i = 0; i < 120 * 100; i++)
    {
        foreach (var enemy in world.Enemies) enemy.Position = GameWorld.Center + new Vector2(1000, 0);
        world.Advance(1.0 / 120, Vector2.UnitX);
        var arrivals = world.Enemies.Where(e => observed.Add(e)).ToArray();
        if (arrivals.Length == 0) continue;
        if (lastWave >= 0) intervals.Add((int)Math.Round((world.Elapsed - lastWave) * 1000));
        lastWave = world.Elapsed;
        var angles = new List<float>();
        foreach (var enemy in arrivals)
        {
            var spawn = enemy.Position - enemy.Velocity / 120;
            var unit = (spawn - GameWorld.Center) / (GameWorld.ArenaRadii + new Vector2(14));
            float angle = MathF.Atan2(unit.Y, unit.X);
            sectors.Add((int)((angle + MathF.PI) / MathF.Tau * 8) % 8);
            angles.Add(angle);
            Expect(Math.Abs(unit.LengthSquared() - 1) < 0.001, "Spawn not on arena edge");
            Expect(Vector2.Distance(spawn, world.Player) >= GameWorld.MinimumSpawnDistance - 0.01f, "Unavoidable point-blank spawn");
            Expect(enemy.Velocity.Length() >= 162 && enemy.Velocity.Length() <= 300.01, "Speed outside new range");
        }
        if (angles.Count > 1)
        {
            float gap = Math.Abs(MathF.IEEERemainder(angles[1] - angles[0], MathF.Tau));
            if (Math.Abs(gap - 2.39996f) > 0.02) pairs++;
        }
    }
    Expect(!world.IsDead && observed.Count > 50 && sectors.Count == 8 && intervals.Count > 10 && pairs > 10,
        "Predictable or insufficient spawn variation");
});
Check("Player animation uses all walk/run frames and changes facing in eight directions", () =>
{
    var animation = new PlayerAnimation();
    var poses = new HashSet<(bool, float)>();
    foreach (var direction in new[] { new Vector2(1,0), new(1,1), new(0,1), new(-1,1), new(-1,0), new(-1,-1), new(0,-1), new(1,-1) })
    {
        animation.Advance(0.01, direction);
        poses.Add((animation.Current.FlipX, animation.Current.Rotation));
    }
    Expect(poses.Count == 8, "Directional poses not distinct");
    foreach (var direction in new[] { Vector2.UnitX, Vector2.UnitY })
    {
        animation.Reset();
        var frames = new HashSet<int>();
        for (int i = 0; i < 60; i++) { animation.Advance(1.0 / 60, direction); frames.Add(animation.Current.Frame); }
        Expect(frames.SetEquals(Enumerable.Range(0, 6)), "Missing walk/run frames");
    }
    animation.Advance(0.1, -Vector2.UnitX);
    animation.Advance(0.1, Vector2.Zero);
    Expect(animation.Current.Row == 0 && animation.Current.FlipX, "Idle lost previous facing");
    Expect(animation.Death(0.1f).Row == 5 && animation.Death(0.5f).Row == 6
        && animation.Death(5).Frame == 9, "Death clips did not play or clamp");
    animation.Reset();
    Expect(animation.Current.Row == 0 && animation.Current.Frame == 0 && !animation.Current.FlipX, "Animation reset failed");
});
Check("Settings and best record round trip; malformed files fall back safely", () =>
{
    string path = Path.Combine(AppContext.BaseDirectory, "check-data", "settings.json");
    var preferences = new PlayerPreferences { MusicVolume = 0, UiVolume = 1, BestSeconds = 92.3 };
    Expect(preferences.Save(path), "Save failed");
    var loaded = PlayerPreferences.Load(path);
    Expect(loaded.MusicVolume == 0 && loaded.UiVolume == 1 && loaded.BestSeconds == 92.3, "Settings lost");
    File.WriteAllText(path, "{invalid}");
    loaded = PlayerPreferences.Load(path);
    Expect(loaded.MusicVolume == 0.9f && loaded.BestSeconds == 0, "Bad file did not recover");
    File.WriteAllText(path, "{\"MusicVolume\":-2,\"UiVolume\":8,\"BestSeconds\":-1}");
    loaded = PlayerPreferences.Load(path);
    Expect(loaded.MusicVolume == 0 && loaded.UiVolume == 1 && loaded.BestSeconds == 0, "Out-of-range values not clamped");
});
Console.WriteLine($"{passed} checks passed.");
