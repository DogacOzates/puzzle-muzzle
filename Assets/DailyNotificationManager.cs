using System;
using System.Collections;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

/// <summary>
/// Schedules a single local push notification 24 hours after the player
/// leaves the app. Cancels it when the player returns.
/// </summary>
public class DailyNotificationManager : MonoBehaviour
{
    private const string NotificationId  = "puzzle_muzzle_daily";
    private const string NotificationId2 = "puzzle_muzzle_streak";
    private const string NotificationId3 = "puzzle_muzzle_engage";

    private static readonly string[] ReminderMessages =
    {
        "Your daily free hint is waiting! 🎁 Come collect it.",
        "Day streak bonus: log in 7 days in a row for 10 free hints! 🔥",
        "A new puzzle is waiting for your brain 🧩",
        "You left a puzzle unsolved… think you can crack it? 💡",
        "Your hint stash is ready — come pick up today's free one! ✨",
        "Challenge someone to a 1v1 puzzle battle today! ⚔️",
        "Don't break your streak! Log in to keep your daily bonus 🔥",
        "Free hint inside — just open the app to claim it 🎯",
        "Brain workout time! New levels are waiting 🧠",
        "Invite a friend and get +5 hints — share your code now! 👫",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitialize()
    {
        if (FindAnyObjectByType<DailyNotificationManager>() != null) return;
        var go = new GameObject("DailyNotificationManager");
        DontDestroyOnLoad(go);
        go.AddComponent<DailyNotificationManager>();
    }

    void Start()
    {
#if UNITY_IOS
        StartCoroutine(RequestPermission());
#endif
    }

#if UNITY_IOS
    private IEnumerator RequestPermission()
    {
        using var req = new AuthorizationRequest(
            AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound,
            registerForRemoteNotifications: false);

        while (!req.IsFinished)
            yield return null;

        // Player is in the app — clear any stale notifications
        CancelPending();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            ScheduleNotification();
        else
            CancelPending();
    }

    private void ScheduleNotification()
    {
        CancelPending();

        // 24 h — daily hint reminder
        Schedule(NotificationId, "Puzzle Muzzle",
            "Your free daily hint is waiting! 🎁 Come collect it.",
            TimeSpan.FromHours(24));

        // 48 h — streak / engagement nudge
        int streak = PlayerPrefs.GetInt("daily.streak", 0);
        string streakBody = streak >= 5
            ? $"You're on a {streak}-day streak! 🔥 Don't break it — log in now."
            : "Log in 7 days in a row for 10 bonus hints! 🔥 Keep your streak going.";
        Schedule(NotificationId2, "Puzzle Muzzle 🔥", streakBody, TimeSpan.FromHours(48));

        // 72 h — re-engagement
        string[] engageMessages = {
            "A puzzle has been waiting 3 days… think you can solve it? 🧩",
            "Invite a friend and get +5 hints free 👫 Share your code now!",
            "Brain workout time! Your hints are piling up 🧠",
        };
        string engageBody = engageMessages[UnityEngine.Random.Range(0, engageMessages.Length)];
        Schedule(NotificationId3, "Puzzle Muzzle", engageBody, TimeSpan.FromHours(72));
    }

    private static void Schedule(string id, string title, string body, TimeSpan delay)
    {
        var notification = new iOSNotification
        {
            Identifier              = id,
            Title                   = title,
            Body                    = body,
            Badge                   = 1,
            ShowInForeground        = false,
            ForegroundPresentationOption = PresentationOption.None,
            CategoryIdentifier      = "reminder",
            ThreadIdentifier        = "daily",
            Trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = delay,
                Repeats      = false,
            },
        };
        iOSNotificationCenter.ScheduleNotification(notification);
    }

    private static void CancelPending()
    {
        iOSNotificationCenter.RemoveScheduledNotification(NotificationId);
        iOSNotificationCenter.RemoveScheduledNotification(NotificationId2);
        iOSNotificationCenter.RemoveScheduledNotification(NotificationId3);
        iOSNotificationCenter.RemoveAllDeliveredNotifications();
        iOSNotificationCenter.ApplicationBadge = 0;
    }
#endif
}
