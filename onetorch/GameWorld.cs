using System.Numerics;

// 입력/그리기와 분리한 생존 규칙. 좌표는 748 × 540 배경 기준입니다.
sealed class GameWorld
{
    public static readonly Vector2 Center = new(374, 282);
    public static readonly Vector2 ArenaRadii = new(316, 198);
    public const float PlayerRadius = 10;
    public const float EnemyRadius = 11;
    public const float PlayerSpeed = 205;
    public const float MaxEnemySpeed = 300;
    public const float MinimumSpawnDistance = 120;
    public const int MaxEnemies = 28;
    public const float HomingTurnSpeed = 1.65f; // 초당 약 95도. 급회전에는 빈틈이 남습니다.
    private const double Step = 1.0 / 120;
    private readonly Random _random;
    private readonly List<Enemy> _enemies = new();
    private double _accumulator;
    private double _untilSpawn;
    private double _nextWaveIncrease;

    public Vector2 Player { get; private set; }
    public double Elapsed { get; private set; }
    public bool IsDead { get; private set; }
    public int SpawnWaveSerial { get; private set; }
    public IReadOnlyList<Enemy> Enemies => _enemies;
    public float EnemySpeed => Math.Min(MaxEnemySpeed, 180 + (float)Elapsed * 0.95f);
    public double SpawnInterval => Math.Max(0.36, 1.25 - Elapsed * 0.01);
    public int WaveSize => Math.Min(MaxEnemies, 1 + (int)((Elapsed + 1e-8) / 10));

    public GameWorld(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        Reset();
    }

    public void Reset()
    {
        Player = Center;
        Elapsed = 0;
        IsDead = false;
        SpawnWaveSerial = 0;
        _enemies.Clear();
        _accumulator = 0;
        _untilSpawn = 0.7 + _random.NextDouble() * 0.6;
        _nextWaveIncrease = 10;
    }

    public void Advance(double elapsed, Vector2 input)
    {
        if (IsDead || !double.IsFinite(elapsed) || elapsed <= 0) return;
        if (input.LengthSquared() > 1) input = Vector2.Normalize(input);
        // 복귀 직후 긴 프레임으로 순간이동하지 않도록 하고, 충돌은 고정 간격으로 검사합니다.
        _accumulator += Math.Min(elapsed, 0.25);
        while (_accumulator + 1e-9 >= Step && !IsDead)
        {
            Tick((float)Step, input);
            _accumulator -= Step;
        }
    }

    private void Tick(float dt, Vector2 input)
    {
        Vector2 oldPlayer = Player;
        Player = ClampPlayer(Player + input * PlayerSpeed * dt);
        Elapsed += Step;
        _untilSpawn -= Step;
        if (Elapsed + 1e-8 >= _nextWaveIncrease)
        {
            // 10초 경계를 넘는 순간 더 큰 무리를 함께 등장시킵니다.
            _nextWaveIncrease += 10;
            _untilSpawn = 0;
        }
        if (_untilSpawn <= 0)
        {
            int beforeSpawn = _enemies.Count;
            for (int i = 0; i < WaveSize && _enemies.Count < MaxEnemies; i++)
            {
                // 한 마리마다 독립적으로 위치를 뽑아 일정한 각도 패턴을 없앱니다.
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    float angle = (float)(_random.NextDouble() * Math.Tau);
                    Vector2 position = Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle))
                        * (ArenaRadii + new Vector2(14));
                    if (Vector2.Distance(position, Player) < MinimumSpawnDistance
                        || _enemies.Any(e => Vector2.Distance(e.Position, position) < 40)) continue;
                    float speed = Math.Min(MaxEnemySpeed, EnemySpeed * (0.9f + (float)_random.NextDouble() * 0.2f));
                    SpawnThreat(position, speed);
                    break;
                }
            }
            // 실제 출현한 무리당 한 번만 알립니다. 개별 적마다 효과음을 겹치지 않습니다.
            if (_enemies.Count > beforeSpawn) SpawnWaveSerial++;
            _untilSpawn += SpawnInterval * (0.65 + _random.NextDouble() * 0.65);
        }

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _enemies[i];
            Vector2 previous = enemy.Position;
            enemy.Velocity = TurnTowards(enemy.Velocity, Player - enemy.Position, HomingTurnSpeed * dt);
            enemy.Position += enemy.Velocity * dt;
            // 두 객체의 상대 이동 선분을 검사해서 빠르게 스쳐도 충돌을 놓치지 않습니다.
            if (HitsPlayer(previous - oldPlayer, enemy.Position - Player))
            {
                IsDead = true;
                return;
            }
            Vector2 outside = (enemy.Position - Center) / (ArenaRadii + new Vector2(45));
            if (outside.LengthSquared() > 1) _enemies.RemoveAt(i);
        }
    }

    internal void SpawnThreat(Vector2 position, float speed)
    {
        if (_enemies.Count >= MaxEnemies) return;
        Vector2 direction = Player - position;
        if (direction.LengthSquared() < 0.001f) direction = Vector2.UnitX;
        _enemies.Add(new Enemy(position, Vector2.Normalize(direction) * speed));
    }

    internal static Vector2 ClampPlayer(Vector2 position)
    {
        Vector2 radii = ArenaRadii - new Vector2(PlayerRadius);
        Vector2 relative = (position - Center) / radii;
        return relative.LengthSquared() > 1
            ? Center + Vector2.Normalize(relative) * radii : position;
    }

    internal static Vector2 TurnTowards(Vector2 velocity, Vector2 targetDirection, float maxTurn)
    {
        if (targetDirection.LengthSquared() < 1e-8f) return velocity;
        float current = MathF.Atan2(velocity.Y, velocity.X);
        float target = MathF.Atan2(targetDirection.Y, targetDirection.X);
        float difference = MathF.IEEERemainder(target - current, MathF.Tau);
        float angle = current + Math.Clamp(difference, -maxTurn, maxTurn);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * velocity.Length();
    }

    internal static bool HitsPlayer(Vector2 from, Vector2 to)
    {
        Vector2 movement = to - from;
        float lengthSquared = movement.LengthSquared();
        float t = lengthSquared < 1e-8f ? 0 : Math.Clamp(-Vector2.Dot(from, movement) / lengthSquared, 0, 1);
        return (from + movement * t).LengthSquared() <= MathF.Pow(PlayerRadius + EnemyRadius, 2);
    }

    internal sealed class Enemy(Vector2 position, Vector2 velocity)
    {
        public Vector2 Position { get; internal set; } = position;
        public Vector2 Velocity { get; internal set; } = velocity;
    }
}
