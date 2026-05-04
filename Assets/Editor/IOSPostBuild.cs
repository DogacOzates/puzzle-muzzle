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

        // Lock orientation to Portrait only on both iPhone and iPad.
        // Unity may generate PortraitUpsideDown in the iPad plist even when disabled —
        // override explicitly to prevent the app appearing upside-down on iPad.
        var iPhoneOrientations = root.CreateArray("UISupportedInterfaceOrientations");
        iPhoneOrientations.AddString("UIInterfaceOrientationPortrait");

        var iPadOrientations = root.CreateArray("UISupportedInterfaceOrientations~ipad");
        iPadOrientations.AddString("UIInterfaceOrientationPortrait");

        // Require full screen on iPad (disables Slide Over / Split View).
        // Without this, iPadOS may force landscape when entering multitasking.
        root.SetBoolean("UIRequiresFullScreen", true);

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

        string mainTarget      = proj.GetUnityMainTargetGuid();
        string frameworkTarget = proj.GetUnityFrameworkTargetGuid();

        // AppTrackingTransparency — required for ATT permission dialog (iOS 14+)
        proj.AddFrameworkToProject(frameworkTarget, "AppTrackingTransparency.framework", false);

        // AdMob static libraries are compiled for arm64 (device) only.
        // Exclude arm64 from simulator builds to prevent linker errors.
        // Ads simply won't load on simulator — this is expected behaviour.
        const string excludedArchs = "EXCLUDED_ARCHS[sdk=iphonesimulator*]";
        proj.SetBuildProperty(mainTarget,      excludedArchs, "arm64");
        proj.SetBuildProperty(frameworkTarget, excludedArchs, "arm64");

        proj.WriteToFile(projPath);
    }
#endif
}
#endif
