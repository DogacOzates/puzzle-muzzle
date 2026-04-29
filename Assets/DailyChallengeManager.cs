using UnityEngine;

public static class DailyChallengeManager
{
    private static string TodayKey => System.DateTime.UtcNow.ToString("yyyyMMdd");

    public static int GetDailyLevelIndex()
    {
        var today = System.DateTime.UtcNow.Date;
        int seed = today.Year * 10000 + today.DayOfYear;
        return new System.Random(seed).Next(0, LevelDatabase.Levels.Length);
    }

    public static bool IsTodayCompleted()
        => PlayerPrefs.GetString("daily.completed", "") == TodayKey;

    public static void MarkTodayCompleted()
    {
        if (IsTodayCompleted()) return;

        string today = TodayKey;
        string yesterday = System.DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");
        string lastCompleted = PlayerPrefs.GetString("daily.lastCompleted", "");

        int streak = (lastCompleted == yesterday)
            ? PlayerPrefs.GetInt("daily.streak", 0) + 1
            : 1;

        PlayerPrefs.SetString("daily.completed", today);
        PlayerPrefs.SetString("daily.lastCompleted", today);
        PlayerPrefs.SetInt("daily.streak", streak);
        PlayerPrefs.Save();
    }

    /// <summary>Returns 0 if the streak is broken (missed more than one day).</summary>
    public static int GetStreak()
    {
        string lastCompleted = PlayerPrefs.GetString("daily.lastCompleted", "");
        if (string.IsNullOrEmpty(lastCompleted)) return 0;

        string today = TodayKey;
        string yesterday = System.DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");

        if (lastCompleted == today || lastCompleted == yesterday)
            return PlayerPrefs.GetInt("daily.streak", 0);

        return 0; // streak broken
    }
}
