using System;
using System.Collections.Generic;
using UnityEngine;

public static class LevelGenerator
{
    private struct CampaignConfig
    {
        public int width;
        public int height;
        public int minSegment;
        public int maxSegment;
        public int candidateCount;
        public string tierName;
        public float rectanglePenalty;
        public float densePenalty;
        public float straightPenalty;
        public float turnWeight;
        public float squarePenalty;
        public float lateRectangleBonus;
        public int minBlocked;
        public int maxBlocked;
    }

    private struct SegmentStats
    {
        public int width;
        public int height;
        public int area;
        public int turns;
        public int longestRun;
        public float fillRatio;
        public bool isRectangle;
        public bool isStraight;
        public bool isSquareish;
    }

    private class LevelCandidate
    {
        public List<Vector2Int> path;
        public List<List<Vector2Int>> segments;
        public string signature;
        public string contentFingerprint;
        public float score;
        public List<Vector2Int> blocked;
    }

    public static LevelData[] GenerateCampaign(int generatedLevelCount)
    {
        var levels = new LevelData[generatedLevelCount];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();

        for (int levelNum = 1; levelNum <= generatedLevelCount; levelNum++)
        {
            var rng = new System.Random(levelNum * 7919 + 31);
            CampaignConfig config = GetConfig(levelNum);
            LevelCandidate bestCandidate = null;

            for (int candidateIndex = 0; candidateIndex < config.candidateCount; candidateIndex++)
            {
                var candidateRng = new System.Random(rng.Next());

                // Each candidate gets a fresh blocked layout to maximize diversity
                HashSet<Vector2Int> candidateBlocked = GenerateBlockedCells(config, candidateRng);
                List<Vector2Int> path = HamiltonianPath(config.width, config.height, candidateBlocked, candidateRng);
                if (path == null) continue; // path failed for this blocked layout

                List<List<Vector2Int>> segments = SplitPath(path, config, candidateRng);
                if (segments == null || segments.Count == 0)
                    continue;

                var blockedList = new List<Vector2Int>(candidateBlocked);
                string signature = BuildSignature(config, segments);
                string contentFingerprint = BuildContentFingerprint(config, segments, blockedList);
                float score = ScoreCandidate(path, segments, config, signature, contentFingerprint, recentSignatures, usedSignatures, allFingerprints);

                if (bestCandidate == null || score > bestCandidate.score)
                {
                    bestCandidate = new LevelCandidate
                    {
                        path = path,
                        segments = segments,
                        signature = signature,
                        contentFingerprint = contentFingerprint,
                        score = score,
                        blocked = blockedList
                    };
                }
            }

            if (bestCandidate == null)
            {
                // Fallback: no blocked cells, snake path (always adjacent, always completable)
                var emptyBlocked = new List<Vector2Int>();
                List<Vector2Int> fallbackPath = SnakePath(config.width, config.height);
                // SplitPath can fail if minSegment constraints are unsatisfiable — relax to uniform split
                List<List<Vector2Int>> fallbackSegments = SplitPath(fallbackPath, config, rng);
                if (fallbackSegments == null || fallbackSegments.Count == 0)
                    fallbackSegments = UniformSplit(fallbackPath, config.minSegment);
                bestCandidate = new LevelCandidate
                {
                    path = fallbackPath,
                    segments = fallbackSegments,
                    signature = BuildSignature(config, fallbackSegments),
                    contentFingerprint = BuildContentFingerprint(config, fallbackSegments, emptyBlocked),
                    score = 0f,
                    blocked = emptyBlocked
                };
            }

            // If best candidate is still a duplicate, try extra seeds to find a unique one
            // Use more attempts for harder tiers with blocked cells
            int extraAttempts = config.maxBlocked > 0 ? 100 : 60;
            if (allFingerprints.Contains(bestCandidate.contentFingerprint))
            {
                for (int extra = 0; extra < extraAttempts; extra++)
                {
                    var xRng = new System.Random(levelNum * 7919 + 31 + (extra + 1) * 1013);
                    HashSet<Vector2Int> xBlocked = GenerateBlockedCells(config, xRng);
                    List<Vector2Int> xPath = HamiltonianPath(config.width, config.height, xBlocked, xRng);
                    if (xPath == null) continue;
                    List<List<Vector2Int>> xSegs = SplitPath(xPath, config, xRng);
                    if (xSegs == null || xSegs.Count == 0) continue;
                    var xBlockedList = new List<Vector2Int>(xBlocked);
                    string xFp = BuildContentFingerprint(config, xSegs, xBlockedList);
                    if (!allFingerprints.Contains(xFp))
                    {
                        bestCandidate = new LevelCandidate
                        {
                            path = xPath,
                            segments = xSegs,
                            signature = BuildSignature(config, xSegs),
                            contentFingerprint = xFp,
                            score = float.PositiveInfinity,
                            blocked = xBlockedList
                        };
                        break;
                    }
                }
            }

            levels[levelNum - 1] = BuildLevelData(config, bestCandidate.segments, bestCandidate.blocked);

            recentSignatures.Enqueue(bestCandidate.signature);
            while (recentSignatures.Count > 8)
                recentSignatures.Dequeue();

            if (!usedSignatures.ContainsKey(bestCandidate.signature))
                usedSignatures[bestCandidate.signature] = 0;
            usedSignatures[bestCandidate.signature]++;

            allFingerprints.Add(bestCandidate.contentFingerprint);
        }

        return levels;
    }

    public static LevelData Generate(int levelNum)
    {
        return GenerateCampaign(levelNum)[levelNum - 1];
    }

    private static LevelData BuildLevelData(CampaignConfig config, List<List<Vector2Int>> segments, List<Vector2Int> blocked)
    {
        var numbers = new NumberCellData[segments.Count];
        var solutions = new SolutionPath[segments.Count];

        for (int i = 0; i < segments.Count; i++)
        {
            List<Vector2Int> segment = segments[i];
            Vector2Int target = segment[segment.Count - 1];
            numbers[i] = new NumberCellData(target.x, target.y, segment.Count);

            var coords = new int[segment.Count * 2];
            for (int j = 0; j < segment.Count; j++)
            {
                coords[j * 2] = segment[j].x;
                coords[j * 2 + 1] = segment[j].y;
            }

            solutions[i] = new SolutionPath(coords);
        }

        BlockedCellData[] blockedCells = null;
        if (blocked != null && blocked.Count > 0)
        {
            blockedCells = new BlockedCellData[blocked.Count];
            for (int i = 0; i < blocked.Count; i++)
                blockedCells[i] = new BlockedCellData(blocked[i].x, blocked[i].y);
        }

        return new LevelData(config.tierName, config.width, config.height, numbers, solutions, blockedCells);
    }

    private static CampaignConfig GetConfig(int generatedLevelIndex)
    {
        CampaignConfig config = new CampaignConfig();

        if (generatedLevelIndex <= 14)
        {
            config.width = 4; config.height = 4;
            config.minSegment = 3; config.maxSegment = 5;
            config.candidateCount = 12;
            config.tierName = "Easy";
            config.rectanglePenalty = 3.2f; config.densePenalty = 2.4f;
            config.straightPenalty = 1.8f; config.turnWeight = 1.15f;
            config.squarePenalty = 1.2f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 32)
        {
            SetRectangularBoard(ref config, 4, 5);
            config.minSegment = 3; config.maxSegment = 6;
            config.candidateCount = 14;
            config.tierName = "Easy";
            config.rectanglePenalty = 3.0f; config.densePenalty = 2.2f;
            config.straightPenalty = 1.7f; config.turnWeight = 1.1f;
            config.squarePenalty = 1.0f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 55)
        {
            config.width = 5; config.height = 5;
            config.minSegment = 3; config.maxSegment = 7;
            config.candidateCount = 16;
            config.tierName = "Normal";
            config.rectanglePenalty = 2.7f; config.densePenalty = 2.0f;
            config.straightPenalty = 1.5f; config.turnWeight = 1.05f;
            config.squarePenalty = 0.9f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 85)
        {
            SetRectangularBoard(ref config, 5, 6);
            config.minSegment = 3; config.maxSegment = 8;
            config.candidateCount = 16;
            config.tierName = "Normal";
            config.rectanglePenalty = 2.3f; config.densePenalty = 1.8f;
            config.straightPenalty = 1.35f; config.turnWeight = 1.0f;
            config.squarePenalty = 0.75f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 99)
        {
            // No blocked cells yet — transition to full 6x6 before introducing obstacles
            config.width = 6; config.height = 6;
            config.minSegment = 4; config.maxSegment = 9;
            config.candidateCount = 18;
            config.tierName = "Hard";
            config.rectanglePenalty = 2.0f; config.densePenalty = 1.55f;
            config.straightPenalty = 1.2f; config.turnWeight = 0.95f;
            config.squarePenalty = 0.65f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 140)
        {
            // Blocked cells introduced from level 100 onwards (max 2)
            config.width = 6; config.height = 6;
            config.minSegment = 4; config.maxSegment = 9;
            config.candidateCount = 20;
            config.tierName = "Hard";
            config.rectanglePenalty = 1.8f; config.densePenalty = 1.4f;
            config.straightPenalty = 1.1f; config.turnWeight = 0.92f;
            config.squarePenalty = 0.6f; config.lateRectangleBonus = 0f;
            config.minBlocked = 1; config.maxBlocked = 2;
        }
        else if (generatedLevelIndex <= 180)
        {
            SetRectangularBoard(ref config, 6, 7);
            config.minSegment = 4; config.maxSegment = 10;
            config.candidateCount = 20;
            config.tierName = "Advanced";
            config.rectanglePenalty = 1.5f; config.densePenalty = 1.2f;
            config.straightPenalty = 1.0f; config.turnWeight = 0.88f;
            config.squarePenalty = 0.5f; config.lateRectangleBonus = 0.05f;
            config.minBlocked = 2; config.maxBlocked = 3;
        }
        else if (generatedLevelIndex <= 225)
        {
            SetRectangularBoard(ref config, 6, 8);
            config.minSegment = 4; config.maxSegment = 10;
            config.candidateCount = 22;
            config.tierName = "Expert";
            config.rectanglePenalty = 1.2f; config.densePenalty = 0.95f;
            config.straightPenalty = 0.9f; config.turnWeight = 0.82f;
            config.squarePenalty = 0.4f; config.lateRectangleBonus = 0.15f;
            config.minBlocked = 3; config.maxBlocked = 4;
        }
        else
        {
            SetRectangularBoard(ref config, 6, 9);
            config.minSegment = 5; config.maxSegment = 10;
            config.candidateCount = 24;
            config.tierName = "Master";
            config.rectanglePenalty = 0.9f; config.densePenalty = 0.7f;
            config.straightPenalty = 0.8f; config.turnWeight = 0.72f;
            config.squarePenalty = 0.25f; config.lateRectangleBonus = 0.3f;
            config.minBlocked = 4; config.maxBlocked = 5;
        }

        return config;
    }

    private static void SetRectangularBoard(ref CampaignConfig config, int a, int b)
    {
        config.width = Mathf.Min(a, b);
        config.height = Mathf.Max(a, b);
    }

    private static List<Vector2Int> HamiltonianPath(int width, int height, HashSet<Vector2Int> blocked, System.Random rng)
    {
        int total = width * height - blocked.Count;

        for (int attempt = 0; attempt < 220; attempt++)
        {
            var visited = new bool[height, width];

            // Pre-mark blocked cells as visited so path logic naturally skips them
            foreach (var b in blocked)
                visited[b.y, b.x] = true;

            Vector2Int start = PickStart(width, height, rng, blocked);
            var path = new List<Vector2Int>(total) { start };
            visited[start.y, start.x] = true;

            bool stuck = false;
            while (path.Count < total)
            {
                Vector2Int current = path[path.Count - 1];
                List<Vector2Int> neighbors = Neighbors(current.x, current.y, width, height, visited);
                if (neighbors.Count == 0)
                {
                    stuck = true;
                    break;
                }

                Vector2Int previous = path.Count > 1 ? path[path.Count - 2] : current;
                int dxPrev = current.x - previous.x;
                int dyPrev = current.y - previous.y;

                Vector2Int bestNext = neighbors[0];
                float bestScore = float.PositiveInfinity;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector2Int next = neighbors[i];
                    int onwardMoves = Neighbors(next.x, next.y, width, height, visited).Count;
                    int edgeDistance = Mathf.Min(
                        Mathf.Min(next.x, width - 1 - next.x),
                        Mathf.Min(next.y, height - 1 - next.y));

                    int dx = next.x - current.x;
                    int dy = next.y - current.y;
                    bool changesDirection = path.Count > 1 && (dx != dxPrev || dy != dyPrev);
                    bool nearEnd = total - path.Count <= 8;

                    float score = onwardMoves * 3f;
                    score += nearEnd ? edgeDistance * 0.1f : edgeDistance * 0.35f;
                    score += changesDirection ? -0.7f : 0.45f;
                    score += CountFutureDeadEnds(next, width, height, visited) * 1.3f;
                    score += (float)rng.NextDouble() * 0.35f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestNext = next;
                    }
                }

                path.Add(bestNext);
                visited[bestNext.y, bestNext.x] = true;
            }

            if (!stuck)
                return path;
        }

        // Snake fallback only works without holes (adjacency would break otherwise)
        if (blocked.Count == 0) return SnakePath(width, height);
        return null;
    }

    private static Vector2Int PickStart(int width, int height, System.Random rng, HashSet<Vector2Int> blocked)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int side = rng.Next(4);
            Vector2Int candidate;
            if (side == 0) candidate = new Vector2Int(rng.Next(width), 0);
            else if (side == 1) candidate = new Vector2Int(width - 1, rng.Next(height));
            else if (side == 2) candidate = new Vector2Int(rng.Next(width), height - 1);
            else candidate = new Vector2Int(0, rng.Next(height));

            if (!blocked.Contains(candidate)) return candidate;
        }

        // Fallback: first non-blocked cell
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2Int(x, y);
                if (!blocked.Contains(p)) return p;
            }
        return Vector2Int.zero;
    }

    private static int CountFutureDeadEnds(Vector2Int next, int width, int height, bool[,] visited)
    {
        int deadEnds = 0;
        visited[next.y, next.x] = true;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (visited[y, x])
                    continue;

                int options = 0;
                if (x > 0 && !visited[y, x - 1]) options++;
                if (x < width - 1 && !visited[y, x + 1]) options++;
                if (y > 0 && !visited[y - 1, x]) options++;
                if (y < height - 1 && !visited[y + 1, x]) options++;

                if (options == 0)
                    deadEnds += 3;
                else if (options == 1)
                    deadEnds++;
            }
        }

        visited[next.y, next.x] = false;
        return deadEnds;
    }

    private static List<Vector2Int> SnakePath(int width, int height)
    {
        var path = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++)
        {
            if (y % 2 == 0)
            {
                for (int x = 0; x < width; x++)
                    path.Add(new Vector2Int(x, y));
            }
            else
            {
                for (int x = width - 1; x >= 0; x--)
                    path.Add(new Vector2Int(x, y));
            }
        }
        return path;
    }

    // Last-resort split when SplitPath fails: greedy chunks of minSegment size
    private static List<List<Vector2Int>> UniformSplit(List<Vector2Int> path, int segSize)
    {
        var result = new List<List<Vector2Int>>();
        int i = 0;
        while (i < path.Count)
        {
            int take = Mathf.Min(segSize, path.Count - i);
            result.Add(path.GetRange(i, take));
            i += take;
        }
        return result;
    }

    private static List<List<Vector2Int>> SplitPath(List<Vector2Int> path, CampaignConfig config, System.Random rng)
    {
        var segments = new List<List<Vector2Int>>();
        int index = 0;
        int previousLength = -1;
        string previousShape = string.Empty;

        while (index < path.Count)
        {
            int remaining = path.Count - index;
            int bestLength = -1;
            float bestScore = float.NegativeInfinity;
            string bestShape = string.Empty;

            int maxLength = Mathf.Min(config.maxSegment, remaining);
            for (int length = config.minSegment; length <= maxLength; length++)
            {
                int rest = remaining - length;
                if (!CanSplitRemaining(rest, config.minSegment, config.maxSegment))
                    continue;

                List<Vector2Int> segment = path.GetRange(index, length);
                string shapeType = ClassifySegment(segment);
                float score = ScoreSegment(segment, config);

                if (length == previousLength)
                    score -= 0.9f;
                if (shapeType == previousShape)
                    score -= 0.55f;

                score += Mathf.Min(2, CountTurns(segment)) * 0.15f;
                score += (float)rng.NextDouble() * 0.35f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLength = length;
                    bestShape = shapeType;
                }
            }

            if (bestLength < 0)
                return null;

            List<Vector2Int> chosen = path.GetRange(index, bestLength);
            segments.Add(chosen);
            previousLength = bestLength;
            previousShape = bestShape;
            index += bestLength;
        }

        return segments;
    }

    private static bool CanSplitRemaining(int remaining, int minSegment, int maxSegment)
    {
        if (remaining == 0)
            return true;

        int minParts = Mathf.CeilToInt(remaining / (float)maxSegment);
        int maxParts = remaining / minSegment;
        return minParts <= maxParts;
    }

    private static float ScoreCandidate(
        List<Vector2Int> path,
        List<List<Vector2Int>> segments,
        CampaignConfig config,
        string signature,
        string contentFingerprint,
        Queue<string> recentSignatures,
        Dictionary<string, int> usedSignatures,
        HashSet<string> allFingerprints)
    {
        float score = 0f;
        var lengthCounts = new Dictionary<int, int>();
        var shapeCounts = new Dictionary<string, int>();

        for (int i = 0; i < segments.Count; i++)
        {
            List<Vector2Int> segment = segments[i];
            score += ScoreSegment(segment, config);

            int length = segment.Count;
            if (!lengthCounts.ContainsKey(length))
                lengthCounts[length] = 0;
            lengthCounts[length]++;

            string shape = ClassifySegment(segment);
            if (!shapeCounts.ContainsKey(shape))
                shapeCounts[shape] = 0;
            shapeCounts[shape]++;
        }

        foreach (var pair in lengthCounts)
        {
            if (pair.Value > 2)
                score -= (pair.Value - 2) * 0.45f;
        }

        foreach (var pair in shapeCounts)
        {
            if (pair.Value > 3)
                score -= (pair.Value - 3) * 0.6f;
        }

        score += CountTurns(path) * 0.12f;
        score -= CountLongStraightRuns(path, Mathf.Max(config.width, config.height) - 1) * 0.8f;

        foreach (string recentSignature in recentSignatures)
        {
            if (recentSignature == signature)
                score -= 5f;
            else if (SharePrefix(signature, recentSignature, 12))
                score -= 1.2f;
        }

        if (usedSignatures.TryGetValue(signature, out int usedCount))
            score -= (usedCount + 1) * 3.5f;

        // Hard penalty for exact duplicate puzzles (same number positions + values)
        if (allFingerprints.Contains(contentFingerprint))
            score -= 20f;

        return score;
    }

    private static bool SharePrefix(string a, string b, int prefixLength)
    {
        if (a == null || b == null)
            return false;

        int len = Mathf.Min(prefixLength, Mathf.Min(a.Length, b.Length));
        for (int i = 0; i < len; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return len > 0;
    }

    private static float ScoreSegment(List<Vector2Int> segment, CampaignConfig config)
    {
        SegmentStats stats = GetSegmentStats(segment);
        float score = 0f;

        score += stats.turns * config.turnWeight;
        score += Mathf.Abs(stats.width - stats.height) * 0.22f;
        score -= Mathf.Max(0, stats.longestRun - 2) * 0.28f;

        if (stats.isStraight)
            score -= config.straightPenalty;

        if (stats.isRectangle)
            score -= config.rectanglePenalty;
        else if (stats.fillRatio > 0.8f && Mathf.Min(stats.width, stats.height) > 1)
            score -= config.densePenalty * (stats.fillRatio - 0.8f) * 4f;

        if (stats.isSquareish && stats.isRectangle)
            score -= config.squarePenalty;

        if (config.lateRectangleBonus > 0f && stats.isRectangle && segment.Count >= config.minSegment + 1)
            score += config.lateRectangleBonus;

        if (stats.turns >= 2)
            score += 0.45f;

        return score;
    }

    private static SegmentStats GetSegmentStats(List<Vector2Int> segment)
    {
        SegmentStats stats = new SegmentStats();
        int minX = segment[0].x;
        int maxX = segment[0].x;
        int minY = segment[0].y;
        int maxY = segment[0].y;

        int turns = 0;
        int longestRun = 1;
        int currentRun = 1;

        int dxPrev = 0;
        int dyPrev = 0;
        if (segment.Count > 1)
        {
            dxPrev = segment[1].x - segment[0].x;
            dyPrev = segment[1].y - segment[0].y;
        }

        for (int i = 0; i < segment.Count; i++)
        {
            Vector2Int cell = segment[i];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);

            if (i >= 2)
            {
                int dx = segment[i].x - segment[i - 1].x;
                int dy = segment[i].y - segment[i - 1].y;
                if (dx == dxPrev && dy == dyPrev)
                {
                    currentRun++;
                }
                else
                {
                    turns++;
                    currentRun = 2;
                    dxPrev = dx;
                    dyPrev = dy;
                }

                if (currentRun > longestRun)
                    longestRun = currentRun;
            }
        }

        stats.width = maxX - minX + 1;
        stats.height = maxY - minY + 1;
        stats.area = stats.width * stats.height;
        stats.turns = turns;
        stats.longestRun = longestRun;
        stats.fillRatio = segment.Count / (float)stats.area;
        stats.isRectangle = segment.Count == stats.area && stats.width > 1 && stats.height > 1;
        stats.isStraight = stats.width == 1 || stats.height == 1;
        stats.isSquareish = Mathf.Abs(stats.width - stats.height) <= 1;
        return stats;
    }

    private static int CountTurns(List<Vector2Int> path)
    {
        if (path.Count < 3)
            return 0;

        int turns = 0;
        int dxPrev = path[1].x - path[0].x;
        int dyPrev = path[1].y - path[0].y;

        for (int i = 2; i < path.Count; i++)
        {
            int dx = path[i].x - path[i - 1].x;
            int dy = path[i].y - path[i - 1].y;
            if (dx != dxPrev || dy != dyPrev)
            {
                turns++;
                dxPrev = dx;
                dyPrev = dy;
            }
        }

        return turns;
    }

    private static int CountLongStraightRuns(List<Vector2Int> path, int threshold)
    {
        if (path.Count < 3)
            return 0;

        int longRuns = 0;
        int currentRun = 1;
        int dxPrev = path[1].x - path[0].x;
        int dyPrev = path[1].y - path[0].y;

        for (int i = 2; i < path.Count; i++)
        {
            int dx = path[i].x - path[i - 1].x;
            int dy = path[i].y - path[i - 1].y;
            if (dx == dxPrev && dy == dyPrev)
            {
                currentRun++;
            }
            else
            {
                if (currentRun >= threshold)
                    longRuns++;
                currentRun = 2;
                dxPrev = dx;
                dyPrev = dy;
            }
        }

        if (currentRun >= threshold)
            longRuns++;

        return longRuns;
    }

    private static string BuildContentFingerprint(CampaignConfig config, List<List<Vector2Int>> segments, IList<Vector2Int> blocked)
    {
        if (segments == null || segments.Count == 0)
            return config.width + "x" + config.height + ":empty";

        var parts = new List<string>(segments.Count);
        foreach (var seg in segments)
        {
            Vector2Int target = seg[seg.Count - 1];
            parts.Add(target.x + "," + target.y + "=" + seg.Count);
        }
        parts.Sort(System.StringComparer.Ordinal);

        string blockedStr = "";
        if (blocked != null && blocked.Count > 0)
        {
            var bParts = new List<string>(blocked.Count);
            foreach (var b in blocked) bParts.Add(b.x + "," + b.y);
            bParts.Sort(System.StringComparer.Ordinal);
            blockedStr = "|B:" + string.Join(",", bParts);
        }

        return config.width + "x" + config.height + ":" + string.Join("|", parts) + blockedStr;
    }

    private static HashSet<Vector2Int> GenerateBlockedCells(CampaignConfig config, System.Random rng)
    {
        if (config.maxBlocked == 0) return new HashSet<Vector2Int>();

        int count = config.minBlocked == config.maxBlocked
            ? config.minBlocked
            : rng.Next(config.minBlocked, config.maxBlocked + 1);

        if (count == 0) return new HashSet<Vector2Int>();

        // Prefer interior cells so corners/edges stay accessible
        var interior = new List<Vector2Int>();
        var edge = new List<Vector2Int>();
        for (int y = 0; y < config.height; y++)
            for (int x = 0; x < config.width; x++)
            {
                bool isEdge = x == 0 || x == config.width - 1 || y == 0 || y == config.height - 1;
                if (isEdge) edge.Add(new Vector2Int(x, y));
                else interior.Add(new Vector2Int(x, y));
            }

        Shuffle(interior, rng);
        Shuffle(edge, rng);

        var candidates = new List<Vector2Int>(interior);
        candidates.AddRange(edge);

        var blocked = new HashSet<Vector2Int>();
        foreach (var candidate in candidates)
        {
            if (blocked.Count >= count) break;
            blocked.Add(candidate);
            if (!IsGridConnected(config.width, config.height, blocked))
                blocked.Remove(candidate);
        }

        return blocked;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private static bool IsGridConnected(int width, int height, HashSet<Vector2Int> blocked)
    {
        int total = width * height - blocked.Count;
        if (total <= 1) return true;

        Vector2Int start = default;
        bool found = false;
        for (int y = 0; y < height && !found; y++)
            for (int x = 0; x < width && !found; x++)
            {
                var p = new Vector2Int(x, y);
                if (!blocked.Contains(p)) { start = p; found = true; }
            }

        if (!found) return true;

        var visited = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            int[] ddx = { -1, 1, 0, 0 };
            int[] ddy = { 0, 0, -1, 1 };
            for (int d = 0; d < 4; d++)
            {
                var next = new Vector2Int(cur.x + ddx[d], cur.y + ddy[d]);
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height) continue;
                if (blocked.Contains(next) || visited.Contains(next)) continue;
                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return visited.Count == total;
    }

    private static string BuildSignature(CampaignConfig config, List<List<Vector2Int>> segments)
    {
        var parts = new List<string>();
        parts.Add(config.width + "x" + config.height);

        for (int i = 0; i < segments.Count; i++)
        {
            List<Vector2Int> segment = segments[i];
            parts.Add(segment.Count + ClassifySegment(segment));
        }

        return string.Join("-", parts);
    }

    private static string ClassifySegment(List<Vector2Int> segment)
    {
        SegmentStats stats = GetSegmentStats(segment);
        if (stats.isStraight)
            return "I";
        if (stats.isRectangle && stats.isSquareish)
            return "Q";
        if (stats.isRectangle)
            return "R";
        if (stats.turns == 1)
            return "L";
        if (stats.turns >= 3)
            return "Z";
        if (stats.fillRatio > 0.8f)
            return "B";
        return "C";
    }

    private static List<Vector2Int> Neighbors(int x, int y, int width, int height, bool[,] visited)
    {
        var result = new List<Vector2Int>(4);
        if (x > 0 && !visited[y, x - 1]) result.Add(new Vector2Int(x - 1, y));
        if (x < width - 1 && !visited[y, x + 1]) result.Add(new Vector2Int(x + 1, y));
        if (y > 0 && !visited[y - 1, x]) result.Add(new Vector2Int(x, y - 1));
        if (y < height - 1 && !visited[y + 1, x]) result.Add(new Vector2Int(x, y + 1));
        return result;
    }
}
