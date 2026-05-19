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

    [MenuItem("Debug/Regenerate Triangle Levels Only")]
    public static void RegenerateTriangleLevelsAsset()
    {
        Directory.CreateDirectory(OutputDirectory);
        string existing = File.Exists(OutputFilePath) ? File.ReadAllText(OutputFilePath) : "";

        LevelDatabase.InvalidateCache();
        string json = LevelDatabase.RebuildTriangleLevelsJson(existing);
        File.WriteAllText(OutputFilePath, json);
        AssetDatabase.Refresh();

        long bytes = new FileInfo(OutputFilePath).Length;
        Debug.Log($"[LevelBundle] Triangle levels rebuilt. Wrote {OutputFilePath} ({bytes / 1024f:F1} KB).");
    }
}
