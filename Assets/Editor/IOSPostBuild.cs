// IOSPostBuild.cs
// Runs automatically after every Unity iOS build.
// Injects required Info.plist keys and Xcode capabilities.
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

        AddInfoPlistKeys(buildPath);
        AddXcodeCapabilities(buildPath);
        AddRequiredFrameworks(buildPath);
    }

    // ── Info.plist keys ───────────────────────────────────────────────────
    private static void AddInfoPlistKeys(string buildPath)
    {
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

        // Required by Google AdMob SDK — must match AdMob App ID
        if (!root.values.ContainsKey("GADApplicationIdentifier"))
        {
            root.SetString(
                "GADApplicationIdentifier",
                "ca-app-pub-2933494287812005~7293120559"
            );
        }

        plist.WriteToFile(plistPath);
    }

    // ── Xcode Capabilities ────────────────────────────────────────────────
    private static void AddXcodeCapabilities(string buildPath)
    {
        string projPath = PBXProject.GetPBXProjectPath(buildPath);

        // Entitlements file path is relative to the Xcode project root
        const string entitlementsRelPath = "Unity-iPhone/Unity-iPhone.entitlements";

        // GetUnityTargetName() is obsolete; the main app target is always "Unity-iPhone"
        var capManager = new ProjectCapabilityManager(
            projPath,
            entitlementsRelPath,
            "Unity-iPhone"
        );

        // Game Center
        capManager.AddGameCenter();

        // In-App Purchase (no entitlement needed; adds the capability flag)
        capManager.AddInAppPurchase();

        // iCloud Key-Value Storage
        // AddiCloud(cloudKit, kvStorage, iCloudDocument, useSharedContainer, containers)
        capManager.AddiCloud(false, true, false, false, new string[] {});

        capManager.WriteToFile();
    }

    // ── Extra Frameworks ─────────────────────────────────────────────────
    private static void AddRequiredFrameworks(string buildPath)
    {
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string targetGuid = proj.GetUnityMainTargetGuid();

        // AppTrackingTransparency — required for ATT permission dialog (iOS 14+)
        proj.AddFrameworkToProject(targetGuid, "AppTrackingTransparency.framework", false);

        proj.WriteToFile(projPath);
    }
#endif
}
#endif
