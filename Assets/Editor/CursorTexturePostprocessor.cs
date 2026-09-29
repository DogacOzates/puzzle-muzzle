using UnityEditor;
using UnityEngine;

// Auto-configures the recording cursor texture so Unity Recorder captures a
// crisp software cursor. Runs once on import; no manual Inspector tweaking.
public class CursorTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').EndsWith("Resources/icons/cursor_hand.png"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 128;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
