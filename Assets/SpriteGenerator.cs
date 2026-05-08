using UnityEngine;
using System.Collections.Generic;

public static class SpriteGenerator
{
    private static Sprite _roundedRect;
    private static Sprite _circle;
    private static Sprite _pentagon;
    private static Sprite _hexagon;
    private static Sprite _flatHexagon;
    private static readonly Dictionary<int, Sprite> numberSpriteCache = new Dictionary<int, Sprite>();
    private static Material _unlitMaterial;

    // Shared Unlit sprite material — ensures cell colors render at their exact linear values
    // regardless of the 2D lighting pipeline / Renderer2D.asset DefaultMaterialType setting.
    public static Material UnlitMaterial
    {
        get
        {
            if (_unlitMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                          ?? Shader.Find("Sprites/Default");
                _unlitMaterial = new Material(shader);
                _unlitMaterial.name = "SpritesUnlit_Shared";
            }
            return _unlitMaterial;
        }
    }

    public static Sprite RoundedRect
    {
        get
        {
            if (_roundedRect == null)
                _roundedRect = CreateRoundedRect(256, 256, 38);
            return _roundedRect;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
                _circle = CreateRoundedRect(256, 256, 127);
            return _circle;
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

    public static Sprite Pentagon
    {
        get
        {
            if (_pentagon == null)
                _pentagon = CreatePentagon();
            return _pentagon;
        }
    }

    // Regular pointy-top hexagon that tiles perfectly in the hex grid.
    // r = 126px in 256×256 texture → apothem = r*cos(30°); CellVisualSize = 2/√3 makes cells touch.
    public static Sprite Hexagon
    {
        get
        {
            if (_hexagon == null)
                _hexagon = CreateHexagon();
            return _hexagon;
        }
    }

    // Flat-top hexagon for column-offset grid (6gen levels).
    // Vertices at 0°, 60°, 120°, 180°, 240°, 300°. Same CVS = 2/√3 as pointy-top.
    public static Sprite FlatHexagon
    {
        get
        {
            if (_flatHexagon == null)
                _flatHexagon = CreateFlatHexagon();
            return _flatHexagon;
        }
    }

    private static Sprite CreateFlatHexagon()
    {
        int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[w * h];
        float cx = w * 0.5f, cy = h * 0.5f;
        float r = 126f;

        // Flat-top hexagon: vertices at 0°, 60°, 120°, 180°, 240°, 300°
        var vx = new float[6];
        var vy = new float[6];
        for (int k = 0; k < 6; k++)
        {
            float angle = k * Mathf.PI / 3f;
            vx[k] = cx + r * Mathf.Cos(angle);
            vy[k] = cy + r * Mathf.Sin(angle);
        }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = PolygonSdf(x, y, vx, vy);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    private static Sprite CreateHexagon()
    {
        int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[w * h];
        float cx = w * 0.5f, cy = h * 0.5f;
        float r = 126f; // slightly under half-texture so AA doesn't clip

        // Pointy-top hexagon: vertices at 90°, 30°, -30°, -90°, -150°, 150°
        var vx = new float[6];
        var vy = new float[6];
        for (int k = 0; k < 6; k++)
        {
            float angle = Mathf.PI / 2f - k * Mathf.PI / 3f;
            vx[k] = cx + r * Mathf.Cos(angle);
            vy[k] = cy + r * Mathf.Sin(angle);
        }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = PolygonSdf(x, y, vx, vy);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    private static Sprite CreatePentagon()
    {
        int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[w * h];
        float cx = w * 0.5f, cy = h * 0.5f;
        float r = 108f;

        // Regular pentagon, pointy top: vertices at angles 90°, 18°, -54°, -126°, -198°
        var vx = new float[5];
        var vy = new float[5];
        for (int k = 0; k < 5; k++)
        {
            float angle = Mathf.PI / 2f - k * 2f * Mathf.PI / 5f;
            vx[k] = cx + r * Mathf.Cos(angle);
            vy[k] = cy + r * Mathf.Sin(angle);
        }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = PolygonSdf(x, y, vx, vy);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    // Returns negative distance inside polygon, positive outside.
    private static float PolygonSdf(float px, float py, float[] vx, float[] vy)
    {
        int n = vx.Length;
        float minDist2 = float.MaxValue;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            // Ray-cast parity for inside test
            if ((vy[i] > py) != (vy[j] > py))
            {
                float t = (py - vy[i]) / (vy[j] - vy[i]);
                if (px < vx[i] + t * (vx[j] - vx[i]))
                    inside = !inside;
            }
            // Squared distance to edge segment
            float ex = vx[j] - vx[i], ey = vy[j] - vy[i];
            float dx = px - vx[i], dy = py - vy[i];
            float t2 = Mathf.Clamp01((dx * ex + dy * ey) / (ex * ex + ey * ey));
            float qx = dx - t2 * ex, qy = dy - t2 * ey;
            minDist2 = Mathf.Min(minDist2, qx * qx + qy * qy);
        }

        float d = Mathf.Sqrt(minDist2);
        return inside ? -d : d;
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
