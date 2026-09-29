using System.Numerics;

// 시트에는 한 방향의 모션이 들어 있습니다. 좌우 반전과 기울기로 이동 방향을 표현합니다.
sealed class PlayerAnimation
{
    public const float DeathDuration = 1.0f;
    private double _clipTime;
    private int _row;
    private bool _flipX;
    private float _rotation;

    public readonly record struct Pose(int Row, int Frame, bool FlipX, float Rotation);

    public void Reset()
    {
        _clipTime = 0;
        _row = 0;
        _flipX = false;
        _rotation = 0;
    }

    public void Advance(double dt, Vector2 direction)
    {
        bool moving = direction.LengthSquared() > 0.001f;
        // 세로 이동은 걷기 6칸, 가로/대각선 이동은 달리기 6칸을 모두 사용합니다.
        int row = !moving ? 0 : direction.X == 0 ? 2 : 3;
        if (row != _row) { _clipTime = 0; _row = row; }
        if (moving)
        {
            if (direction.X != 0) _flipX = direction.X < 0;
            float lean = direction.X == 0 ? MathF.PI / 10 : MathF.PI / 12;
            _rotation = Math.Sign(direction.Y) * lean * (_flipX ? -1 : 1);
        }
        _clipTime += Math.Max(0, dt);
    }

    public Pose Current => new(_row, (int)(_clipTime * (_row == 0 ? 6 : 12)) % (_row == 0 ? 4 : 6), _flipX, _rotation);

    public Pose Death(float elapsed)
    {
        // 피격 4칸 후 소멸 10칸을 한 번만 재생합니다. 마지막 프레임에서 멈춥니다.
        return elapsed < 0.24f
            ? new Pose(5, Math.Clamp((int)(elapsed / 0.24f * 4), 0, 3), _flipX, _rotation)
            : new Pose(6, Math.Clamp((int)((elapsed - 0.24f) / (DeathDuration - 0.24f) * 10), 0, 9), _flipX, _rotation);
    }
}
