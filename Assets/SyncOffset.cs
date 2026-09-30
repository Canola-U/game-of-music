using UnityEngine;

// 싱크 보정값(ms). 이어폰/스피커마다 소리가 늦게 들리는 정도가 달라서 유저가 직접 맞춘다.
// 양수 = 노트가 늦게 옴 (소리가 늦게 들릴 때), 음수 = 노트가 빨리 옴
public static class SyncOffset
{
    public const int step = 1;
    public const int limit = 300;
    const string prefKey = "syncOffsetMs";

    static int? cached;

    public static int Ms
    {
        get
        {
            if (cached == null) cached = PlayerPrefs.GetInt(prefKey, 0);
            return cached.Value;
        }
        set
        {
            int clamped = Mathf.Clamp(value, -limit, limit);
            if (cached == clamped) return;
            cached = clamped;
            PlayerPrefs.SetInt(prefKey, clamped);
            PlayerPrefs.Save();
        }
    }

    public static string Label => Ms == 0 ? "0ms" : (Ms > 0 ? $"+{Ms}ms" : $"{Ms}ms");
}
