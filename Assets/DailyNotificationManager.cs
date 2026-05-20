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
    private const string NotificationId = "puzzle_muzzle_daily";

    private static readonly string[] ReminderMessages =
    {
        "Bugünkü bulmacalarını çözdün mü? 🧩",
        "Bir sonraki bölüm seni bekliyor! 🎯",
        "Günlük beyin egzersizi vakti 🧠",
        "Yeni bölümler seni bekliyor ✨",
        "Kafanı biraz çalıştır! 💡",
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

        string body = ReminderMessages[UnityEngine.Random.Range(0, ReminderMessages.Length)];

        var notification = new iOSNotification
        {
            Identifier              = NotificationId,
            Title                   = "Puzzle Muzzle",
            Body                    = body,
            Badge                   = 1,
            ShowInForeground        = false,
            ForegroundPresentationOption = PresentationOption.None,
            CategoryIdentifier      = "reminder",
            ThreadIdentifier        = "daily",
            Trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = TimeSpan.FromHours(24),
                Repeats      = false,
            },
        };

        iOSNotificationCenter.ScheduleNotification(notification);
    }

    private static void CancelPending()
    {
        iOSNotificationCenter.RemoveScheduledNotification(NotificationId);
        iOSNotificationCenter.RemoveAllDeliveredNotifications();
        iOSNotificationCenter.ApplicationBadge = 0;
    }
#endif
}
