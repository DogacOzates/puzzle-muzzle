using UnityEngine;
using System.Collections.Generic;

public static class SpriteGenerator
{
    private static Sprite _roundedRect;
    private static readonly Dictionary<int, Sprite> numberSpriteCache = new Dictionary<int, Sprite>();

    public static Sprite RoundedRect
    {
        get
        {
            if (_roundedRect == null)
                _roundedRect = CreateRoundedRect(256, 256, 38);
            return _roundedRect;
        }
    }

    private static Sprite CreateRoundedRect(int w, int h, int r)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[w * h];
        float hw = w * 0.5f, hh = h * 0.5f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = SdfRoundBox(x - hw, y - hh, hw - 0.5f, hh - 0.5f, r);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    private static float SdfRoundBox(float px, float py, float bx, float by, float r)
    {
        float qx = Mathf.Abs(px) - bx + r;
        float qy = Mathf.Abs(py) - by + r;
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude
             + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }

    // --- Number sprites with drop shadow ---

    private static readonly string[][] DigitPatterns = new string[][]
    {
        new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        new[] { " ### ", "#   #", "    #", "  ## ", " #   ", "#    ", "#####" },
        new[] { " ### ", "#   #", "    #", " ### ", "    #", "#   #", " ### " },
        new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
        new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
        new[] { " ### ", "#    ", "#    ", "#### ", "#   #", "#   #", " ### " },
        new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
        new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
        new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
    };

    private const int DigitW = 5;
    private const int DigitH = 7;
    private const int Scale = 12;
    private const int Spacing = 2;
    private const int Pad = 3;

    public static Sprite GetNumberSprite(int number)
    {
        if (numberSpriteCache.TryGetValue(number, out Sprite cached))
            return cached;

        string s = number.ToString();
        int contentW = s.Length * DigitW + (s.Length - 1) * Spacing;
        int texW = (contentW + Pad * 2) * Scale;
        int texH = (DigitH + Pad * 2) * Scale;

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color32[texW * texH];

        // Draw shadow first (offset +1, -1 pixel units)
        int ox = Pad;
        foreach (char c in s)
        {
            FillDigit(px, texW, texH, (ox + 1) * Scale, (Pad - 1) * Scale, c - '0', new Color32(0, 0, 0, 50));
            ox += DigitW + Spacing;
        }

        // Draw white main digit on top
        ox = Pad;
        foreach (char c in s)
        {
            FillDigit(px, texW, texH, ox * Scale, Pad * Scale, c - '0', new Color32(255, 255, 255, 255));
            ox += DigitW + Spacing;
        }

        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), texH);
        numberSpriteCache[number] = sprite;
        return sprite;
    }

    private static void FillDigit(Color32[] pixels, int texW, int texH, int baseX, int baseY, int digit, Color32 col)
    {
        if (digit < 0 || digit > 9) return;
        var pat = DigitPatterns[digit];

        for (int row = 0; row < DigitH; row++)
            for (int col2 = 0; col2 < DigitW; col2++)
                if (pat[row][col2] == '#')
                {
                    int sx = baseX + col2 * Scale;
                    int sy = texH - baseY - (row + 1) * Scale;
                    for (int dy = 0; dy < Scale; dy++)
                        for (int dx = 0; dx < Scale; dx++)
                        {
                            int px2 = sx + dx, py2 = sy + dy;
                            if (px2 >= 0 && px2 < texW && py2 >= 0 && py2 < texH)
                            {
                                int idx = py2 * texW + px2;
                                if (col.a == 255 || pixels[idx].a < col.a)
                                    pixels[idx] = col;
                            }
                        }
                }
    }
}
