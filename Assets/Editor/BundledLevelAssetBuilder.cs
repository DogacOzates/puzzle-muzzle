using System.IO;
using UnityEditor;
using UnityEngine;

public static class BundledLevelAssetBuilder
{
    private const string OutputDirectory = "Assets/Resources";
    private const string OutputFilePath = OutputDirectory + "/generated_levels.json";

    [MenuItem("Debug/Generate Bundled Levels Asset")]
    public static void GenerateBundledLevelsAsset()
    {
        Directory.CreateDirectory(OutputDirectory);

        LevelDatabase.InvalidateCache();
        string json = LevelDatabase.BuildBundledLevelsJson(false);
        File.WriteAllText(OutputFilePath, json);
        AssetDatabase.Refresh();

        long bytes = new FileInfo(OutputFilePath).Length;
        Debug.Log($"[LevelBundle] Wrote {OutputFilePath} ({bytes / 1024f:F1} KB).");
    }
}
