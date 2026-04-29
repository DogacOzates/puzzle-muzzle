using UnityEngine;
using UnityEngine.SocialPlatforms;

public class GameCenterManager : MonoBehaviour
{
    public static GameCenterManager Instance { get; private set; }

    private bool isAuthenticated;

    // Achievement IDs — must match exactly what is defined in App Store Connect.
    private const string AchievFirstLevel = "com.dogac.puzzlemuzzle.first_level";
    private const string AchievLevel10    = "com.dogac.puzzlemuzzle.level_10";
    private const string AchievLevel50    = "com.dogac.puzzlemuzzle.level_50";
    private const string AchievLevel100   = "com.dogac.puzzlemuzzle.level_100";
    private const string AchievStreak7    = "com.dogac.puzzlemuzzle.streak_7";
    private const string AchievStreak30   = "com.dogac.puzzlemuzzle.streak_30";
    private const string AchievDailyFirst = "com.dogac.puzzlemuzzle.daily_first";
    private const string LeaderboardId    = "com.dogac.puzzlemuzzle.levels_completed";

    void Awake()
    {
        Instance = this;
#if UNITY_IOS && !UNITY_EDITOR
        Social.localUser.Authenticate(success => { isAuthenticated = success; });
#endif
    }

    /// <summary>
    /// Reports achievements and leaderboard score based on total campaign levels completed.
    /// Should only be called for Regular (campaign) mode completions.
    /// </summary>
    public void ReportCampaignLevelCompleted(int highestUnlockedIndex)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!isAuthenticated) return;

        int total = highestUnlockedIndex + 1;
        if (total >= 1)   Social.ReportAchievement(AchievFirstLevel, 100.0, _ => { });
        if (total >= 10)  Social.ReportAchievement(AchievLevel10,    100.0, _ => { });
        if (total >= 50)  Social.ReportAchievement(AchievLevel50,    100.0, _ => { });
        if (total >= 100) Social.ReportAchievement(AchievLevel100,   100.0, _ => { });

        Social.ReportScore(total, LeaderboardId, _ => { });
#endif
    }

    /// <summary>Reports daily challenge achievements based on current streak.</summary>
    public void ReportDailyCompleted(int streak)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!isAuthenticated) return;

        Social.ReportAchievement(AchievDailyFirst, 100.0, _ => { });
        if (streak >= 7)  Social.ReportAchievement(AchievStreak7,  100.0, _ => { });
        if (streak >= 30) Social.ReportAchievement(AchievStreak30, 100.0, _ => { });
#endif
    }

    public void ShowLeaderboard()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!isAuthenticated) return;
        Social.ShowLeaderboardUI();
#endif
    }
}
