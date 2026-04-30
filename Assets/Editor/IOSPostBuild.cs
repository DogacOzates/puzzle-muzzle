// IOSPostBuild.cs
// Runs automatically after every Unity iOS build.
// Injects required Info.plist keys that Unity doesn't expose in PlayerSettings UI.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

public static class IOSPostBuild
{
#if UNITY_IOS
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        PlistElementDict root = plist.root;

        // Required for AdMob / App Tracking Transparency (ATT) — iOS 14.5+
        if (!root.values.ContainsKey("NSUserTrackingUsageDescription"))
        {
            root.SetString(
                "NSUserTrackingUsageDescription",
                "This identifier will be used to deliver personalized ads to you."
            );
        }

        plist.WriteToFile(plistPath);
    }
#endif
}
#endif
