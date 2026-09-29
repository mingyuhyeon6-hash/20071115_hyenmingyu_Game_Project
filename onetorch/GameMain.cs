using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

class GameMain : G2AppBase
{
    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;

    private enum Scene { Opening, Playing, Dying, GameOver }
    private Scene _scene = Scene.Opening;
    private readonly GameWorld _world = new();
    private readonly PlayerAnimation _playerAnimation = new();
    private readonly PlayerPreferences _preferences = PlayerPreferences.Load();
    private G2Texture _bgTexture = null!, _openingTexture = null!, _playerTexture = null!;
    private G2Texture _enemyTexture = null!, _torchBarTexture = null!;
    private G2Font _labelFont = null!, _timeFont = null!, _headingFont = null!, _centerFont = null!;
    private ID2D1SolidColorBrush _brush = null!;
    private G2AudioSound? _clickSound, _startSound, _spawnSound, _backgroundMusic;
    private static readonly Rect SettingsButton = new(861, 16, 88, 36);
    private static readonly Rect CloseSettingsButton = new(360, 370, 240, 44);
    private static readonly Rect RetryButton = new(314, 318, 332, 44);
    private static readonly Rect TitleButton = new(314, 376, 332, 36);
    private static readonly Rect MusicSlider = new(300, 246, 360, 24);
    private static readonly Rect UiSlider = new(300, 313, 360, 24);
    private static readonly Color4 TextColor = new(0.94f, 0.91f, 0.82f, 1);
    private static readonly Color4 Amber = new(1, 0.65f, 0.2f, 1);
    private static readonly Color4 Muted = new(0.62f, 0.65f, 0.70f, 1);
    private bool _settingsOpen, _newRecord, _saveFailed;
    private int _dragSlider;
    private float _deathTime, _clickRemaining, _spawnSoundCooldown;
    private Vector2 _clickPosition;
    private PointF _mouse;

    protected override void Initialize()
    {
        ClearColor = new Color4(0.02f, 0.02f, 0.03f, 1);
        _openingTexture = new G2Texture("resource/image/title.png");
        _bgTexture = new G2Texture("resource/background/map.png");
        _playerTexture = new G2Texture("resource/character/PLAYER/ONE-TORCH-Character.png");
        _enemyTexture = new G2Texture("resource/character/ENEMY/MONSTER.png");
        _torchBarTexture = new G2Texture("resource/ui/torch-bar-transparent.png");
        _labelFont = new G2Font("Malgun Gothic", 14, FontWeight.Normal);
        _timeFont = new G2Font("Arial", 24);
        _headingFont = new G2Font("Malgun Gothic", 32, textAlignment: TextAlignment.Center);
        _centerFont = new G2Font("Malgun Gothic", 16, textAlignment: TextAlignment.Center,
            paragraphAlignment: ParagraphAlignment.Center);
        _brush = RenderTarget.CreateSolidColorBrush(TextColor);
        if (G2AudioContext.Instance?.Audio != null)
        {
            _clickSound = new G2AudioSound("resource/music/click.wav");
            _startSound = new G2AudioSound("resource/music/start.wav");
            _spawnSound = new G2AudioSound("resource/music/monster-spawn.wav");
            _backgroundMusic = new G2AudioSound("resource/music/dark-ambient.wav");
            ApplyVolume();
            _backgroundMusic.Play(true);
        }
    }

    protected override void Update()
    {
        float dt = (float)Math.Min(DeltaTime, 0.1);
        _mouse = Input.MousePosition;
        _clickRemaining = Math.Max(0, _clickRemaining - dt);
        // 다른 창으로 전환한 동안에는 생존 시간과 적이 멈춥니다.
        if (!IsActive)
        {
            if (_scene == Scene.Playing) _settingsOpen = true;
            if (_dragSlider != 0) SavePreferences();
            _dragSlider = 0;
            return;
        }
        bool clicked = Input.IsButtonDown(MouseButtons.Left)
            && _mouse.X >= 0 && _mouse.X < ScreenSize.Width
            && _mouse.Y >= 0 && _mouse.Y < ScreenSize.Height;
        if (clicked)
        {
            _clickPosition = new Vector2(_mouse.X, _mouse.Y);
            _clickRemaining = 0.3f;
        }
        if (Input.IsKeyDown(Keys.Escape) || (_scene != Scene.Opening && clicked && Hover(SettingsButton)))
        {
            _settingsOpen = !_settingsOpen;
            _dragSlider = 0;
            _clickSound?.Play();
            if (!_settingsOpen) SavePreferences();
            return;
        }
        if (_settingsOpen)
        {
            UpdateSettings(clicked);
            return;
        }
        if (_scene == Scene.Opening)
        {
            if (clicked || Input.IsKeyDown(Keys.Space))
            {
                _startSound?.Play();
                StartGame();
            }
        }
        else if (_scene == Scene.Playing)
        {
            Vector2 movement = new(
                (Held(Keys.D) || Held(Keys.Right) ? 1 : 0) - (Held(Keys.A) || Held(Keys.Left) ? 1 : 0),
                (Held(Keys.S) || Held(Keys.Down) ? 1 : 0) - (Held(Keys.W) || Held(Keys.Up) ? 1 : 0));
            int previousWave = _world.SpawnWaveSerial;
            _world.Advance(DeltaTime, movement);
            _spawnSoundCooldown = Math.Max(0, _spawnSoundCooldown - dt);
            if (!_world.IsDead && _world.SpawnWaveSerial != previousWave && _spawnSoundCooldown <= 0)
            {
                _spawnSound?.Play();
                _spawnSoundCooldown = 0.45f;
            }
            _playerAnimation.Advance(dt, movement);
            if (clicked) _clickSound?.Play();
            if (_world.IsDead)
            {
                _scene = Scene.Dying;
                _deathTime = 0;
                _newRecord = _world.Elapsed > _preferences.BestSeconds;
                _preferences.BestSeconds = Math.Max(_preferences.BestSeconds, _world.Elapsed);
                SavePreferences();
            }
        }
        else if (_scene == Scene.Dying)
        {
            _deathTime += dt;
            if (_deathTime >= PlayerAnimation.DeathDuration) _scene = Scene.GameOver;
        }
        else if (_scene == Scene.GameOver)
        {
            if ((clicked && Hover(RetryButton)) || Input.IsKeyDown(Keys.R) || Input.IsKeyDown(Keys.Space))
            {
                _startSound?.Play();
                StartGame();
            }
            else if (clicked && Hover(TitleButton))
            {
                _clickSound?.Play();
                _scene = Scene.Opening;
            }
            else if (clicked) _clickSound?.Play();
        }
    }

    private void StartGame()
    {
        _world.Reset();
        _playerAnimation.Reset();
        _scene = Scene.Playing;
        _settingsOpen = false;
        _deathTime = 0;
        _spawnSoundCooldown = 0;
        _spawnSound?.Stop();
        _newRecord = false;
    }

    private bool Held(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);
    private bool Hover(Rect rectangle) => rectangle.Contains(_mouse.X, _mouse.Y);

    private void UpdateSettings(bool clicked)
    {
        if (clicked && Hover(CloseSettingsButton))
        {
            _settingsOpen = false;
            _dragSlider = 0;
            SavePreferences();
            _clickSound?.Play();
            return;
        }
        if (clicked)
        {
            if (Hover(MusicSlider)) _dragSlider = 1;
            else if (Hover(UiSlider)) _dragSlider = 2;
        }
        if (_dragSlider != 0 && (Input.IsButtonDown(MouseButtons.Left) || Input.IsButtonPress(MouseButtons.Left)))
        {
            Rect track = _dragSlider == 1 ? MusicSlider : UiSlider;
            float value = Math.Clamp((_mouse.X - track.X) / track.Width, 0, 1);
            if (_dragSlider == 1) _preferences.MusicVolume = value;
            else _preferences.UiVolume = value;
            ApplyVolume();
        }
        if (_dragSlider != 0 && !Input.IsButtonDown(MouseButtons.Left) && !Input.IsButtonPress(MouseButtons.Left))
        {
            // 효과음은 바를 놓았을 때 변경된 음량으로 미리 들려줍니다.
            if (_dragSlider == 2) _clickSound?.Play();
            _dragSlider = 0;
            SavePreferences();
        }
    }

    private void ApplyVolume()
    {
        // 원본 파일의 낮은 음량을 보정합니다. 기본 90%에서 BGM 2.7배, UI 3.6배입니다.
        _backgroundMusic?.SetVolume(_preferences.MusicVolume * 3);
        _clickSound?.SetVolume(_preferences.UiVolume * 4);
        _startSound?.SetVolume(_preferences.UiVolume * 4);
        // 스폰음은 배경에 섞일 정도로만 재생하며 효과음 음소거도 함께 적용합니다.
        _spawnSound?.SetVolume(_preferences.UiVolume);
    }

    private void SavePreferences() => _saveFailed = !_preferences.Save();

    protected override void Render()
    {
        if (_scene == Scene.Opening)
        {
            _openingTexture.Draw(new Rect(0, 0, 960, 540), new Rect(0, 0, 960, 540));
            _centerFont.DrawText("아무 곳이나 클릭하여 시작 · WASD / 방향키 이동", new Rect(180, 508, 600, 26), TextColor);
        }
        else RenderPlay();

        if (_scene == Scene.GameOver) RenderGameOver();
        if (_scene != Scene.Opening) DrawButton(SettingsButton, "설정 · ESC");
        if (_settingsOpen) RenderSettings();
        RenderClickEffect();
    }

    private void RenderPlay()
    {
        var screenTransform = RenderTarget.Transform;
        RenderTarget.Transform = Matrix3x2.CreateTranslation(106, 0) * screenTransform;
        _bgTexture.Draw();
        // 맵은 원본 그림에서 타원으로 보이므로 바닥 모양에 맞춰 이동 경계를 잡습니다.
        _brush.Color = new Color4(0.85f, 0.66f, 0.35f, 0.24f);
        RenderTarget.DrawEllipse(new Ellipse(GameWorld.Center, GameWorld.ArenaRadii.X, GameWorld.ArenaRadii.Y), _brush, 1);
        RenderTarget.PushAxisAlignedClip(new Rect(0, 68, 748, 472), AntialiasMode.PerPrimitive);
        foreach (var enemy in _world.Enemies)
        {
            Vector2 direction = Vector2.Normalize(enemy.Velocity);
            // 출현 전 예고 대신 이미 움직이는 적의 짧은 잔상만 표시합니다.
            _brush.Color = new Color4(1, 0.36f, 0.24f, 0.55f);
            RenderTarget.DrawLine(enemy.Position - direction * 30, enemy.Position, _brush, 2);
            DrawCharacter(_enemyTexture, enemy.Position, (int)(_world.Elapsed * 12) % 6, 2);
        }
        float light = _scene == Scene.Playing ? 1 : Math.Max(0, 1 - _deathTime / PlayerAnimation.DeathDuration);
        // 중심에 가까운 작은 원을 겹쳐 횃불 빛을 표현합니다.
        for (int ring = 5; ring >= 1; ring--)
        {
            _brush.Color = new Color4(1, 0.5f, 0.1f, 0.024f * light);
            RenderTarget.FillEllipse(new Ellipse(_world.Player, ring * 15, ring * 15), _brush);
        }
        if (_scene == Scene.Playing) DrawPlayer(_playerAnimation.Current);
        else DrawPlayer(_playerAnimation.Death(_deathTime));
        RenderTarget.PopAxisAlignedClip();

        Fill(new Rect(0, 0, 748, 66), new Color4(0.02f, 0.025f, 0.035f, 0.94f));
        _torchBarTexture.Draw(new Rect(22, 18, 280, 30.65f), new Rect(80, 235, 2010, 220), _world.IsDead ? 0.25f : 1);
        _labelFont.DrawText("생존 시간", new Rect(330, 5, 180, 22), TextColor);
        _timeFont.DrawText(FormatTime(_world.Elapsed), new Rect(330, 28, 190, 32), TextColor);
        _labelFont.DrawText("최고 기록", new Rect(554, 5, 175, 22), TextColor);
        _timeFont.DrawText(FormatTime(Math.Max(_preferences.BestSeconds, _world.Elapsed)), new Rect(554, 28, 185, 32), Amber);
        RenderTarget.Transform = screenTransform;
    }

    private void RenderGameOver()
    {
        Fill(new Rect(0, 0, 960, 540), new Color4(0, 0, 0, 0.65f));
        Fill(new Rect(274, 136, 412, 302), new Color4(0.055f, 0.06f, 0.075f, 0.98f));
        Outline(new Rect(274, 136, 412, 302), new Color4(0.45f, 0.3f, 0.14f, 1));
        _headingFont.DrawText("GAME OVER", new Rect(284, 157, 392, 48), TextColor);
        _centerFont.DrawText(_newRecord ? "새로운 최고 기록!" : "횃불이 꺼졌습니다", new Rect(284, 204, 392, 28), Amber);
        _centerFont.DrawText($"생존  {FormatTime(_world.Elapsed)}    /    최고  {FormatTime(_preferences.BestSeconds)}",
            new Rect(284, 252, 392, 36), TextColor);
        DrawButton(RetryButton, "다시 도전 · R / SPACE");
        DrawButton(TitleButton, "타이틀로");
    }

    private void RenderSettings()
    {
        Fill(new Rect(0, 0, 960, 540), new Color4(0, 0, 0, 0.68f));
        Fill(new Rect(250, 112, 460, 336), new Color4(0.055f, 0.06f, 0.075f, 0.99f));
        Outline(new Rect(250, 112, 460, 336), new Color4(0.45f, 0.3f, 0.14f, 1));
        _headingFont.DrawText("사운드 설정", new Rect(270, 130, 420, 45), TextColor);
        _centerFont.DrawText(_scene == Scene.Playing ? "일시정지 · 바를 드래그해 음량을 조절하세요" : "바를 드래그해 음량을 조절하세요",
            new Rect(270, 179, 420, 28), Muted);
        RenderSlider(MusicSlider, "배경음", _preferences.MusicVolume);
        RenderSlider(UiSlider, "효과음 (UI · 몬스터)", _preferences.UiVolume);
        DrawButton(CloseSettingsButton, _scene == Scene.Playing ? "계속하기 · ESC" : "닫기 · ESC");
        if (_saveFailed) _centerFont.DrawText("설정을 저장하지 못했습니다", new Rect(270, 416, 420, 24), Amber);
        else if (_backgroundMusic == null) _centerFont.DrawText("오디오 출력 장치가 없습니다", new Rect(270, 416, 420, 24), Muted);
    }

    private void RenderSlider(Rect bounds, string label, float value)
    {
        _labelFont.DrawText(label, new Rect(bounds.X, bounds.Y - 25, 240, 24), TextColor);
        _labelFont.DrawText($"{Math.Round(value * 100):0}%", new Rect(bounds.X + bounds.Width - 44, bounds.Y - 25, 64, 24), Amber);
        float y = bounds.Y + bounds.Height / 2;
        Fill(new Rect(bounds.X, y - 3, bounds.Width, 6), new Color4(0.2f, 0.22f, 0.26f, 1));
        if (value > 0) Fill(new Rect(bounds.X, y - 3, bounds.Width * value, 6), Amber);
        _brush.Color = TextColor;
        RenderTarget.FillEllipse(new Ellipse(new Vector2(bounds.X + bounds.Width * value, y), 8, 8), _brush);
    }

    private void DrawButton(Rect bounds, string text)
    {
        bool hovered = Hover(bounds);
        Fill(bounds, hovered ? new Color4(0.25f, 0.17f, 0.09f, 0.98f) : new Color4(0.09f, 0.1f, 0.12f, 0.95f));
        Outline(bounds, hovered ? Amber : new Color4(0.4f, 0.32f, 0.22f, 1));
        _centerFont.DrawText(text, bounds, TextColor);
    }

    private void Fill(Rect bounds, Color4 color)
    {
        _brush.Color = color;
        RenderTarget.FillRectangle(bounds, _brush);
    }

    private void Outline(Rect bounds, Color4 color, float width = 1)
    {
        _brush.Color = color;
        RenderTarget.DrawRectangle(bounds, _brush, width);
    }

    private void RenderClickEffect()
    {
        if (_clickRemaining <= 0) return;
        float progress = 1 - _clickRemaining / 0.3f;
        float radius = 5 + progress * 22;
        _brush.Color = new Color4(1, 0.65f, 0.2f, 1 - progress);
        RenderTarget.DrawEllipse(new Ellipse(_clickPosition, radius, radius), _brush, 2);
    }

    private static void DrawCharacter(G2Texture texture, Vector2 position, int frame, int row, float opacity = 1)
    {
        texture.Draw(new Rect(position.X - 48, position.Y - 48, 96, 96),
            new Rect(frame * 64, row * 64, 64, 64), opacity, BitmapInterpolationMode.NearestNeighbor);
    }

    private void DrawPlayer(PlayerAnimation.Pose pose)
    {
        var mapTransform = RenderTarget.Transform;
        // 캐릭터 중심을 기준으로 변환하며 맵과 UI에는 영향을 주지 않습니다.
        RenderTarget.Transform = Matrix3x2.CreateTranslation(-_world.Player)
            * Matrix3x2.CreateScale(pose.FlipX ? -1 : 1, 1)
            * Matrix3x2.CreateRotation(pose.Rotation)
            * Matrix3x2.CreateTranslation(_world.Player) * mapTransform;
        DrawCharacter(_playerTexture, _world.Player, pose.Frame, pose.Row);
        RenderTarget.Transform = mapTransform;
    }

    private static string FormatTime(double seconds)
    {
        int tenths = (int)(seconds * 10);
        return $"{tenths / 600:00}:{tenths / 10 % 60:00}.{tenths % 10}";
    }

    public override void Dispose()
    {
        _backgroundMusic?.Dispose();
        _startSound?.Dispose();
        _spawnSound?.Dispose();
        _clickSound?.Dispose();
        _brush?.Dispose();
        _openingTexture?.Dispose();
        _bgTexture?.Dispose();
        _playerTexture?.Dispose();
        _enemyTexture?.Dispose();
        _torchBarTexture?.Dispose();
        _labelFont?.Dispose();
        _timeFont?.Dispose();
        _headingFont?.Dispose();
        _centerFont?.Dispose();
        base.Dispose();
    }
}
