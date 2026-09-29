using System.Text.Json;

sealed class PlayerPreferences
{
    public float MusicVolume { get; set; } = 0.9f;
    public float UiVolume { get; set; } = 0.9f;
    public double BestSeconds { get; set; }
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneTorch", "settings.json");

    public static PlayerPreferences Load(string? path = null)
    {
        try
        {
            var preferences = JsonSerializer.Deserialize<PlayerPreferences>(File.ReadAllText(path ?? FilePath)) ?? new();
            preferences.MusicVolume = float.IsFinite(preferences.MusicVolume) ? Math.Clamp(preferences.MusicVolume, 0, 1) : 0.9f;
            preferences.UiVolume = float.IsFinite(preferences.UiVolume) ? Math.Clamp(preferences.UiVolume, 0, 1) : 0.9f;
            preferences.BestSeconds = double.IsFinite(preferences.BestSeconds) ? Math.Max(0, preferences.BestSeconds) : 0;
            return preferences;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public bool Save(string? path = null)
    {
        string target = path ?? FilePath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
            // 중간에 종료돼도 기존 기록을 유지하도록 임시 파일을 완성한 뒤 교체합니다.
            File.WriteAllText(target + ".tmp", JsonSerializer.Serialize(this));
            File.Move(target + ".tmp", target, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
