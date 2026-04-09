using System;
using System.Collections.Generic;
using UnityEngine;

public static class LevelGenerator
{
    public static LevelData Generate(int levelNum)
    {
        var rng = new System.Random(levelNum * 7919 + 31);

        int w, h, minSeg, maxSeg;
        string tierName;
        GetConfig(levelNum, rng, out w, out h, out minSeg, out maxSeg, out tierName);

        var path = HamiltonianPath(w, h, rng);
        var segments = SplitPath(path, minSeg, maxSeg, rng);

        var numbers = new NumberCellData[segments.Count];
        var solutions = new SolutionPath[segments.Count];

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var target = seg[seg.Count - 1];
            numbers[i] = new NumberCellData(target.x, target.y, seg.Count);

            var coords = new int[seg.Count * 2];
            for (int j = 0; j < seg.Count; j++)
            {
                coords[j * 2] = seg[j].x;
                coords[j * 2 + 1] = seg[j].y;
            }
            solutions[i] = new SolutionPath(coords);
        }

        return new LevelData(tierName, w, h, numbers, solutions);
    }

    // Difficulty tiers (called with n = 1..148):
    // 1-10   Easy      4x4   segs 3-6
    // 11-25  Normal    4x5   segs 3-7
    // 26-45  Normal    5x5   segs 3-8
    // 46-65  Hard      5x6   segs 3-9
    // 66-90  Hard      6x6   segs 4-10
    // 91-110 Advanced  6x7   segs 4-12
    // 111-135 Advanced 7x7   segs 4-12
    // 136-148 Expert   7x7   segs 5-14
    static void GetConfig(int n, System.Random rng,
        out int w, out int h, out int minS, out int maxS, out string name)
    {
        if      (n <= 10)  { w=4; h=4; minS=3; maxS=6; name="Easy"; }
        else if (n <= 25)
        {
            if (rng.Next(2)==0) { w=4; h=5; } else { w=5; h=4; }
            minS=3; maxS=7; name="Normal";
        }
        else if (n <= 45)  { w=5; h=5; minS=3; maxS=8; name="Normal"; }
        else if (n <= 65)
        {
            if (rng.Next(2)==0) { w=5; h=6; } else { w=6; h=5; }
            minS=3; maxS=9; name="Hard";
        }
        else if (n <= 90)  { w=6; h=6; minS=4; maxS=10; name="Hard"; }
        else if (n <= 110)
        {
            if (rng.Next(2)==0) { w=6; h=7; } else { w=7; h=6; }
            minS=4; maxS=12; name="Advanced";
        }
        else if (n <= 135) { w=7; h=7; minS=4; maxS=12; name="Advanced"; }
        else               { w=7; h=7; minS=5; maxS=14; name="Expert"; }
    }

    // Warnsdorf heuristic with random tie-breaking
    static List<Vector2Int> HamiltonianPath(int w, int h, System.Random rng)
    {
        int total = w * h;

        for (int attempt = 0; attempt < 150; attempt++)
        {
            var visited = new bool[h, w];
            int sx = rng.Next(w), sy = rng.Next(h);
            var path = new List<Vector2Int>(total) { new Vector2Int(sx, sy) };
            visited[sy, sx] = true;

            bool stuck = false;
            while (path.Count < total)
            {
                var c = path[path.Count - 1];
                var nbrs = Neighbors(c.x, c.y, w, h, visited);
                if (nbrs.Count == 0) { stuck = true; break; }

                int bestDeg = int.MaxValue;
                var best = new List<Vector2Int>(4);

                for (int i = 0; i < nbrs.Count; i++)
                {
                    int deg = Neighbors(nbrs[i].x, nbrs[i].y, w, h, visited).Count;
                    if (deg < bestDeg)
                    {
                        bestDeg = deg;
                        best.Clear();
                        best.Add(nbrs[i]);
                    }
                    else if (deg == bestDeg)
                    {
                        best.Add(nbrs[i]);
                    }
                }

                var next = best[rng.Next(best.Count)];
                path.Add(next);
                visited[next.y, next.x] = true;
            }

            if (!stuck) return path;
        }

        return SnakePath(w, h);
    }

    static List<Vector2Int> SnakePath(int w, int h)
    {
        var path = new List<Vector2Int>(w * h);
        for (int y = 0; y < h; y++)
        {
            if (y % 2 == 0)
                for (int x = 0; x < w; x++) path.Add(new Vector2Int(x, y));
            else
                for (int x = w - 1; x >= 0; x--) path.Add(new Vector2Int(x, y));
        }
        return path;
    }

    static List<List<Vector2Int>> SplitPath(
        List<Vector2Int> path, int minS, int maxS, System.Random rng)
    {
        var segs = new List<List<Vector2Int>>();
        int i = 0, n = path.Count;

        while (i < n)
        {
            int rem = n - i;
            if (rem <= maxS)
            {
                if (rem >= minS || segs.Count == 0)
                    segs.Add(new List<Vector2Int>(path.GetRange(i, rem)));
                else
                    segs[segs.Count - 1].AddRange(path.GetRange(i, rem));
                break;
            }

            int top = Math.Min(maxS, rem - minS);
            if (top < minS)
            {
                segs.Add(new List<Vector2Int>(path.GetRange(i, rem)));
                break;
            }

            int len = rng.Next(minS, top + 1);
            segs.Add(new List<Vector2Int>(path.GetRange(i, len)));
            i += len;
        }

        return segs;
    }

    static List<Vector2Int> Neighbors(int x, int y, int w, int h, bool[,] vis)
    {
        var r = new List<Vector2Int>(4);
        if (x > 0   && !vis[y, x-1]) r.Add(new Vector2Int(x-1, y));
        if (x < w-1 && !vis[y, x+1]) r.Add(new Vector2Int(x+1, y));
        if (y > 0   && !vis[y-1, x]) r.Add(new Vector2Int(x, y-1));
        if (y < h-1 && !vis[y+1, x]) r.Add(new Vector2Int(x, y+1));
        return r;
    }
}
