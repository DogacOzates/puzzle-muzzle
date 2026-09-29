using UnityEngine;

// Swaps the OS cursor for the hand pointer used in recorded ad videos.
// ForceSoftware ensures Unity Recorder captures the cursor in its frames
// (hardware cursors are drawn by the OS and skipped by the render capture).
// iOS builds skip this — touch input has no cursor.
public static class CursorOverride
{
#if UNITY_EDITOR || UNITY_STANDALONE
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Texture2D cursor = Resources.Load<Texture2D>("icons/cursor_hand");
        if (cursor == null) return;

        // Fingertip hot-spot: ~40% X, ~8% Y matches the pointing-finger sprite.
        Vector2 hotspot = new Vector2(cursor.width * 0.40f, cursor.height * 0.08f);
        Cursor.SetCursor(cursor, hotspot, CursorMode.ForceSoftware);
    }
#endif
}
