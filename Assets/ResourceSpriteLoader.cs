using System.Collections.Generic;
using UnityEngine;

public static class ResourceSpriteLoader
{
    private static readonly Dictionary<string, Sprite> TextureSpriteCache = new Dictionary<string, Sprite>();

    public static Sprite LoadSprite(string resourcePath)
    {
        return LoadSprite(resourcePath, new Vector2(0.5f, 0.5f));
    }

    public static Sprite LoadSprite(string resourcePath, Vector2 pivot)
    {
        string cacheKey = resourcePath + "|" + pivot.x + "|" + pivot.y;
        if (TextureSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite))
            return cachedSprite;

        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            pivot,
            100f);

        TextureSpriteCache[cacheKey] = sprite;
        return sprite;
    }
}
