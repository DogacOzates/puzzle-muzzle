using System;
using System.Collections.Generic;
using UnityEngine;

public static class LevelGenerator
{
    public static bool UseBundledBuildOptimizations { get; set; }

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

    private struct TriangleRemovableWindow
    {
        public int startIndex;
        public int endIndex;
        public int interiorCount;
    }

    private static readonly Vector2[] TriangleBlockedAnchorTemplates =
    {
        new Vector2(0.18f, 0.20f),
        new Vector2(0.78f, 0.20f),
        new Vector2(0.28f, 0.52f),
        new Vector2(0.72f, 0.58f),
        new Vector2(0.42f, 0.34f),
        new Vector2(0.58f, 0.78f),
        new Vector2(0.18f, 0.78f),
        new Vector2(0.82f, 0.82f)
    };

    public static LevelData[] GenerateCampaign(int generatedLevelCount)
        => GenerateCampaign(0, generatedLevelCount);

    public static LevelData[] GenerateCampaign(int startIndex, int generatedLevelCount)
    {
        var levels = new LevelData[generatedLevelCount];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        // Tier-local value-set balancing: prevents same number combos dominating a tier
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        // Recent region fingerprints: prevents endpoints always clustering in same bands
        var recentRegionSets = new Queue<string>();

        for (int localIndex = 0; localIndex < generatedLevelCount; localIndex++)
        {
            int generatedLevelIndex = startIndex + localIndex;
            int levelNum = generatedLevelIndex + 1;
            var rng = new System.Random(levelNum * 7919 + 31);
            CampaignConfig config = GetConfig(levelNum);
            LevelCandidate bestCandidate = null;
            bool fastBundledMode = UseBundledBuildOptimizations;
            int candidateAttempts = fastBundledMode ? Mathf.Min(config.candidateCount, 8) : config.candidateCount;

            for (int candidateIndex = 0; candidateIndex < candidateAttempts; candidateIndex++)
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
                float score = ScoreCandidate(path, segments, config, signature, contentFingerprint, recentSignatures, usedSignatures, allFingerprints, usedValueSetsByTier, recentRegionSets);

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
            int extraAttempts = fastBundledMode
                ? (config.maxBlocked > 0 ? 10 : 6)
                : (config.maxBlocked > 0 ? 100 : 60);
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

            levels[localIndex] = BuildLevelData(config, bestCandidate.segments, bestCandidate.blocked);

            recentSignatures.Enqueue(bestCandidate.signature);
            while (recentSignatures.Count > 15)
                recentSignatures.Dequeue();

            if (!usedSignatures.ContainsKey(bestCandidate.signature))
                usedSignatures[bestCandidate.signature] = 0;
            usedSignatures[bestCandidate.signature]++;

            // Track tier-local value-set usage
            string chosenValueSet = BuildValueSetFingerprint(bestCandidate.segments);
            if (!usedValueSetsByTier.ContainsKey(config.tierName))
                usedValueSetsByTier[config.tierName] = new Dictionary<string, int>();
            if (!usedValueSetsByTier[config.tierName].ContainsKey(chosenValueSet))
                usedValueSetsByTier[config.tierName][chosenValueSet] = 0;
            usedValueSetsByTier[config.tierName][chosenValueSet]++;

            // Track recent region fingerprint
            string chosenRegion = BuildRegionFingerprint(bestCandidate.segments, config);
            recentRegionSets.Enqueue(chosenRegion);
            while (recentRegionSets.Count > 10)
                recentRegionSets.Dequeue();

            allFingerprints.Add(bestCandidate.contentFingerprint);
        }

        return levels;
    }

    public static LevelData Generate(int levelNum)
    {
        return GenerateCampaign(levelNum - 1, 1)[0];
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
            config.candidateCount = 22;
            config.tierName = "Easy";
            config.rectanglePenalty = 3.2f; config.densePenalty = 2.4f;
            config.straightPenalty = 1.8f; config.turnWeight = 1.15f;
            config.squarePenalty = 1.2f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 32)
        {
            SetRectangularBoard(ref config, 4, 5);
            config.minSegment = 3; config.maxSegment = 7;
            config.candidateCount = 26;
            config.tierName = "Easy";
            config.rectanglePenalty = 3.0f; config.densePenalty = 2.2f;
            config.straightPenalty = 1.7f; config.turnWeight = 1.1f;
            config.squarePenalty = 1.0f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 55)
        {
            config.width = 5; config.height = 5;
            config.minSegment = 3; config.maxSegment = 8;
            config.candidateCount = 28;
            config.tierName = "Normal";
            config.rectanglePenalty = 2.7f; config.densePenalty = 2.0f;
            config.straightPenalty = 1.5f; config.turnWeight = 1.05f;
            config.squarePenalty = 0.9f; config.lateRectangleBonus = 0f;
            config.minBlocked = 0; config.maxBlocked = 0;
        }
        else if (generatedLevelIndex <= 85)
        {
            SetRectangularBoard(ref config, 5, 6);
            config.minSegment = 3; config.maxSegment = 9;
            config.candidateCount = 28;
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
            config.candidateCount = 26;
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
            config.candidateCount = 28;
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
            config.candidateCount = 26;
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
            config.candidateCount = 28;
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
            config.candidateCount = 30;
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

    private static List<Vector2Int> HamiltonianPath(int width, int height, HashSet<Vector2Int> blocked, System.Random rng, bool hexMode = false, bool colHexMode = false, bool triMode = false, int maxAttempts = 220)
    {
        int total = width * height - blocked.Count;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
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
                List<Vector2Int> neighbors = triMode
                    ? TriangleNeighbors(current.x, current.y, width, height, visited)
                    : (colHexMode
                        ? HexColNeighbors(current.x, current.y, width, height, visited)
                        : (hexMode
                            ? HexNeighbors(current.x, current.y, width, height, visited)
                            : Neighbors(current.x, current.y, width, height, visited)));
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
                    int onwardMoves = triMode
                        ? TriangleNeighbors(next.x, next.y, width, height, visited).Count
                        : (colHexMode
                            ? HexColNeighbors(next.x, next.y, width, height, visited).Count
                            : (hexMode
                                ? HexNeighbors(next.x, next.y, width, height, visited).Count
                                : Neighbors(next.x, next.y, width, height, visited).Count));
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
                    score += (triMode
                        ? CountTriangleFutureDeadEnds(next, width, height, visited)
                        : (colHexMode
                            ? CountColHexFutureDeadEnds(next, width, height, visited)
                            : (hexMode
                                ? CountHexFutureDeadEnds(next, width, height, visited)
                                : CountFutureDeadEnds(next, width, height, visited)))) * 1.3f;
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
        if (blocked.Count == 0) return triMode ? TriangleSnakePath(width, height) : SnakePath(width, height);
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

            // Collect all valid (length, score, shape) candidates
            int maxLength = Mathf.Min(config.maxSegment, remaining);
            var choices = new List<(int length, float score, string shape)>();

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
                // Increased randomness so diverse lengths can win
                score += (float)rng.NextDouble() * 0.55f;

                choices.Add((length, score, shapeType));
            }

            if (choices.Count == 0)
                return null;

            // Sort descending by score, then sample from top 3 probabilistically
            choices.Sort((a, b) => b.score.CompareTo(a.score));
            double sel = rng.NextDouble();
            int pick = 0;
            if (choices.Count >= 2 && sel > 0.60) pick = 1;
            if (choices.Count >= 3 && sel > 0.85) pick = 2;

            var chosen_item = choices[pick];
            List<Vector2Int> chosen = path.GetRange(index, chosen_item.length);
            segments.Add(chosen);
            previousLength = chosen_item.length;
            previousShape = chosen_item.shape;
            index += chosen_item.length;
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
        HashSet<string> allFingerprints,
        Dictionary<string, Dictionary<string, int>> usedValueSetsByTier,
        Queue<string> recentRegionSets)
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

        // Tier-local value-set balancing: penalize overused number combinations within this tier
        string myValueSet = BuildValueSetFingerprint(segments);
        if (usedValueSetsByTier.TryGetValue(config.tierName, out var tierSets) &&
            tierSets.TryGetValue(myValueSet, out int vsCount))
            score -= (vsCount + 1) * 3.0f;

        // Region diversity: penalize if endpoints cluster in same bands as recent levels
        string myRegion = BuildRegionFingerprint(segments, config);
        int regionMatches = 0;
        foreach (string rrs in recentRegionSets)
            if (rrs == myRegion) regionMatches++;
        score -= regionMatches * 2.0f;

        // Spatial spread bonus: reward levels where number cells are far apart
        score += ComputeSpreadScore(segments, config) * 2.0f;

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

    private static string BuildBlockedLayoutFingerprint(IList<Vector2Int> blocked)
    {
        if (blocked == null || blocked.Count == 0)
            return string.Empty;

        var parts = new List<string>(blocked.Count);
        foreach (var cell in blocked)
            parts.Add(cell.x + "," + cell.y);
        parts.Sort(System.StringComparer.Ordinal);
        return string.Join("|", parts);
    }

    private static string BuildBlockedRegionFingerprint(CampaignConfig config, IList<Vector2Int> blocked)
    {
        if (blocked == null || blocked.Count == 0)
            return string.Empty;

        var parts = new List<string>(blocked.Count);
        foreach (var cell in blocked)
        {
            string xBand = cell.x < config.width / 3f ? "L" : (cell.x < 2f * config.width / 3f ? "C" : "R");
            string yBand = cell.y < config.height / 3f ? "B" : (cell.y < 2f * config.height / 3f ? "M" : "T");
            bool isEdge = cell.x == 0 || cell.x == config.width - 1 || cell.y == 0 || cell.y == config.height - 1;
            parts.Add(xBand + yBand + (isEdge ? "E" : "I"));
        }

        parts.Sort(System.StringComparer.Ordinal);
        return string.Join("|", parts);
    }

    private static HashSet<Vector2Int> GenerateBlockedCells(CampaignConfig config, System.Random rng, bool hexMode = false, bool colHexMode = false, bool triMode = false)
    {
        if (config.maxBlocked == 0) return new HashSet<Vector2Int>();

        int count = config.minBlocked == config.maxBlocked
            ? config.minBlocked
            : rng.Next(config.minBlocked, config.maxBlocked + 1);

        if (count == 0) return new HashSet<Vector2Int>();
        return GenerateBlockedCellsExact(config, count, rng, hexMode, colHexMode, triMode);
    }

    private static HashSet<Vector2Int> GenerateBlockedCellsExact(
        CampaignConfig config,
        int count,
        System.Random rng,
        bool hexMode = false,
        bool colHexMode = false,
        bool triMode = false)
    {
        if (count <= 0)
            return new HashSet<Vector2Int>();

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
        int[] minDistancePasses = triMode ? new[] { 5, 4, 3, 2, 1, 0 } : new[] { 3, 2, 1, 0 };
        foreach (int minDistance in minDistancePasses)
        {
            foreach (var candidate in candidates)
            {
                if (blocked.Count >= count) break;
                if (blocked.Contains(candidate))
                    continue;
                if (minDistance > 0 && IsTooCloseToBlocked(candidate, blocked, minDistance))
                    continue;

                blocked.Add(candidate);
                if (!IsGridConnected(config.width, config.height, blocked, hexMode, colHexMode, triMode))
                    blocked.Remove(candidate);
            }

            if (blocked.Count >= count)
                break;
        }

        return blocked;
    }

    private static bool IsTooCloseToBlocked(Vector2Int candidate, HashSet<Vector2Int> blocked, int minDistance)
    {
        foreach (var existing in blocked)
        {
            int manhattan = Mathf.Abs(candidate.x - existing.x) + Mathf.Abs(candidate.y - existing.y);
            if (manhattan < minDistance)
                return true;
        }

        return false;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private static bool IsGridConnected(int width, int height, HashSet<Vector2Int> blocked, bool hexMode = false, bool colHexMode = false, bool triMode = false)
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
            var neighbors = new List<Vector2Int>(6);
            neighbors.Add(new Vector2Int(cur.x - 1, cur.y));
            neighbors.Add(new Vector2Int(cur.x + 1, cur.y));
            if (colHexMode)
            {
                // 6-way column-offset neighbors (odd columns shifted down 0.5)
                neighbors.Add(new Vector2Int(cur.x, cur.y - 1));
                neighbors.Add(new Vector2Int(cur.x, cur.y + 1));
                if (cur.x % 2 == 0) {
                    neighbors.Add(new Vector2Int(cur.x - 1, cur.y - 1));
                    neighbors.Add(new Vector2Int(cur.x + 1, cur.y - 1));
                } else {
                    neighbors.Add(new Vector2Int(cur.x - 1, cur.y + 1));
                    neighbors.Add(new Vector2Int(cur.x + 1, cur.y + 1));
                }
            }
            else if (hexMode)
            {
                // 6-way offset-grid neighbors (odd rows shifted +0.5 in X)
                if (cur.y % 2 == 0) {
                    neighbors.Add(new Vector2Int(cur.x - 1, cur.y - 1));
                    neighbors.Add(new Vector2Int(cur.x,     cur.y - 1));
                    neighbors.Add(new Vector2Int(cur.x - 1, cur.y + 1));
                    neighbors.Add(new Vector2Int(cur.x,     cur.y + 1));
                } else {
                    neighbors.Add(new Vector2Int(cur.x,     cur.y - 1));
                    neighbors.Add(new Vector2Int(cur.x + 1, cur.y - 1));
                    neighbors.Add(new Vector2Int(cur.x,     cur.y + 1));
                    neighbors.Add(new Vector2Int(cur.x + 1, cur.y + 1));
                }
            }
            else if (triMode)
            {
                // Triangle: only one vertical neighbor (▲ connects up, ▽ connects down)
                bool isUp = (cur.x + cur.y) % 2 == 0;
                neighbors.Add(new Vector2Int(cur.x, cur.y + (isUp ? 1 : -1)));
            }
            else
            {
                neighbors.Add(new Vector2Int(cur.x, cur.y - 1));
                neighbors.Add(new Vector2Int(cur.x, cur.y + 1));
            }
            foreach (var next in neighbors)
            {
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

    private static string BuildValueSetFingerprint(List<List<Vector2Int>> segments)
    {
        var sizes = new List<int>(segments.Count);
        foreach (var seg in segments) sizes.Add(seg.Count);
        sizes.Sort();
        return string.Join(",", sizes);
    }

    // Divide the grid into 3×2 bands (left/center/right × bottom/top)
    private static string BuildRegionFingerprint(List<List<Vector2Int>> segments, CampaignConfig config)
    {
        var bands = new List<string>(segments.Count);
        foreach (var seg in segments)
        {
            var ep = seg[seg.Count - 1];
            string bx = ep.x < config.width / 3f ? "L" : (ep.x < 2f * config.width / 3f ? "C" : "R");
            string by = ep.y < config.height / 2f ? "B" : "T";
            bands.Add(bx + by);
        }
        bands.Sort();
        return string.Join(",", bands);
    }

    // Reward levels where number-cell endpoints are spread far apart
    private static float ComputeSpreadScore(List<List<Vector2Int>> segments, CampaignConfig config)
    {
        if (segments.Count <= 1) return 0f;

        var endpoints = new List<Vector2Int>(segments.Count);
        foreach (var seg in segments) endpoints.Add(seg[seg.Count - 1]);

        float totalDist = 0f;
        int pairs = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < endpoints.Count; i++)
            for (int j = i + 1; j < endpoints.Count; j++)
            {
                float d = Mathf.Abs(endpoints[i].x - endpoints[j].x)
                        + Mathf.Abs(endpoints[i].y - endpoints[j].y);
                totalDist += d;
                pairs++;
                if (d < minDist) minDist = d;
            }

        float avgDist = pairs > 0 ? totalDist / pairs : 0f;
        float maxPossible = config.width + config.height - 2f;
        // Weight both average distance and minimum (to avoid all-corners vs. all-center)
        return (avgDist / (maxPossible + 0.01f)) * 0.7f + (minDist / (maxPossible + 0.01f)) * 0.3f;
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

    // --- Pentagon campaign ---

    // 6-directional neighbors for offset hex grid (odd rows shifted +0.5 in X)
    private static List<Vector2Int> HexNeighbors(int x, int y, int width, int height, bool[,] visited)
    {
        var result = new List<Vector2Int>(6);
        // Left / right (same for all rows)
        if (x > 0 && !visited[y, x - 1]) result.Add(new Vector2Int(x - 1, y));
        if (x < width - 1 && !visited[y, x + 1]) result.Add(new Vector2Int(x + 1, y));
        // Diagonal neighbors depend on row parity
        if (y % 2 == 0) // even row: diagonals at (x-1,y±1) and (x,y±1)
        {
            if (y > 0) { if (x > 0 && !visited[y-1, x-1]) result.Add(new Vector2Int(x-1, y-1)); if (!visited[y-1, x]) result.Add(new Vector2Int(x, y-1)); }
            if (y < height-1) { if (x > 0 && !visited[y+1, x-1]) result.Add(new Vector2Int(x-1, y+1)); if (!visited[y+1, x]) result.Add(new Vector2Int(x, y+1)); }
        }
        else // odd row: diagonals at (x,y±1) and (x+1,y±1)
        {
            if (y > 0) { if (!visited[y-1, x]) result.Add(new Vector2Int(x, y-1)); if (x < width-1 && !visited[y-1, x+1]) result.Add(new Vector2Int(x+1, y-1)); }
            if (y < height-1) { if (!visited[y+1, x]) result.Add(new Vector2Int(x, y+1)); if (x < width-1 && !visited[y+1, x+1]) result.Add(new Vector2Int(x+1, y+1)); }
        }
        return result;
    }

    private static int CountHexFutureDeadEnds(Vector2Int next, int width, int height, bool[,] visited)
    {
        int deadEnds = 0;
        visited[next.y, next.x] = true;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (visited[y, x]) continue;
                // Count using temporary visited (not modifying array, just passing current state)
                int options = 0;
                // Same row
                if (x > 0 && !visited[y, x-1]) options++;
                if (x < width-1 && !visited[y, x+1]) options++;
                // Diagonals
                if (y % 2 == 0) {
                    if (y > 0) { if (x > 0 && !visited[y-1, x-1]) options++; if (!visited[y-1, x]) options++; }
                    if (y < height-1) { if (x > 0 && !visited[y+1, x-1]) options++; if (!visited[y+1, x]) options++; }
                } else {
                    if (y > 0) { if (!visited[y-1, x]) options++; if (x < width-1 && !visited[y-1, x+1]) options++; }
                    if (y < height-1) { if (!visited[y+1, x]) options++; if (x < width-1 && !visited[y+1, x+1]) options++; }
                }
                if (options == 0) deadEnds += 3;
                else if (options == 1) deadEnds++;
            }
        visited[next.y, next.x] = false;
        return deadEnds;
    }

    // 6-directional neighbors for flat-top column-offset hex (odd columns shifted down 0.5)
    private static List<Vector2Int> HexColNeighbors(int x, int y, int width, int height, bool[,] visited)
    {
        var result = new List<Vector2Int>(6);
        // Same column: up and down
        if (y > 0 && !visited[y - 1, x]) result.Add(new Vector2Int(x, y - 1));
        if (y < height - 1 && !visited[y + 1, x]) result.Add(new Vector2Int(x, y + 1));
        // Adjacent columns: (x±1, y) always; parity determines the second row
        if (x % 2 == 0) // even col: cross neighbors at y and y-1
        {
            if (x > 0)
            {
                if (!visited[y, x - 1]) result.Add(new Vector2Int(x - 1, y));
                if (y > 0 && !visited[y - 1, x - 1]) result.Add(new Vector2Int(x - 1, y - 1));
            }
            if (x < width - 1)
            {
                if (!visited[y, x + 1]) result.Add(new Vector2Int(x + 1, y));
                if (y > 0 && !visited[y - 1, x + 1]) result.Add(new Vector2Int(x + 1, y - 1));
            }
        }
        else // odd col: cross neighbors at y and y+1
        {
            if (x > 0)
            {
                if (!visited[y, x - 1]) result.Add(new Vector2Int(x - 1, y));
                if (y < height - 1 && !visited[y + 1, x - 1]) result.Add(new Vector2Int(x - 1, y + 1));
            }
            if (x < width - 1)
            {
                if (!visited[y, x + 1]) result.Add(new Vector2Int(x + 1, y));
                if (y < height - 1 && !visited[y + 1, x + 1]) result.Add(new Vector2Int(x + 1, y + 1));
            }
        }
        return result;
    }

    private static int CountColHexFutureDeadEnds(Vector2Int next, int width, int height, bool[,] visited)
    {
        int deadEnds = 0;
        visited[next.y, next.x] = true;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (visited[y, x]) continue;
                int options = 0;
                if (y > 0 && !visited[y - 1, x]) options++;
                if (y < height - 1 && !visited[y + 1, x]) options++;
                if (x % 2 == 0)
                {
                    if (x > 0)        { if (!visited[y, x - 1]) options++; if (y > 0 && !visited[y - 1, x - 1]) options++; }
                    if (x < width - 1) { if (!visited[y, x + 1]) options++; if (y > 0 && !visited[y - 1, x + 1]) options++; }
                }
                else
                {
                    if (x > 0)        { if (!visited[y, x - 1]) options++; if (y < height - 1 && !visited[y + 1, x - 1]) options++; }
                    if (x < width - 1) { if (!visited[y, x + 1]) options++; if (y < height - 1 && !visited[y + 1, x + 1]) options++; }
                }
                if (options == 0) deadEnds += 3;
                else if (options == 1) deadEnds++;
            }
        visited[next.y, next.x] = false;
        return deadEnds;
    }

    // 3-directional neighbors for equilateral triangle grid.
    // ▲ cells (isUp=(x+y)%2==0): horizontal left/right + one cell directly BELOW (y+1).
    // ▽ cells (isDown=(x+y)%2==1): horizontal left/right + one cell directly ABOVE (y-1).
    private static List<Vector2Int> TriangleNeighbors(int x, int y, int width, int height, bool[,] visited)
    {
        var result = new List<Vector2Int>(3);
        if (x > 0 && !visited[y, x - 1]) result.Add(new Vector2Int(x - 1, y));
        if (x < width - 1 && !visited[y, x + 1]) result.Add(new Vector2Int(x + 1, y));
        bool isUp = (x + y) % 2 == 0;
        int vy = y + (isUp ? 1 : -1);
        if (vy >= 0 && vy < height && !visited[vy, x]) result.Add(new Vector2Int(x, vy));
        return result;
    }

    private static int CountTriangleFutureDeadEnds(Vector2Int next, int width, int height, bool[,] visited)
    {
        int deadEnds = 0;
        visited[next.y, next.x] = true;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (visited[y, x]) continue;
                int options = 0;
                if (x > 0 && !visited[y, x - 1]) options++;
                if (x < width - 1 && !visited[y, x + 1]) options++;
                bool isUp = (x + y) % 2 == 0;
                int vy = y + (isUp ? 1 : -1);
                if (vy >= 0 && vy < height && !visited[vy, x]) options++;
                if (options == 0) deadEnds += 3;
                else if (options == 1) deadEnds++;
            }
        visited[next.y, next.x] = false;
        return deadEnds;
    }

    // Snake fallback path for triangle grids.
    // Prefer a row snake on even widths; otherwise use a column snake on even heights.
    private static List<Vector2Int> TriangleSnakePath(int width, int height)
    {
        if (width % 2 != 0 && height % 2 == 0)
            return TriangleColumnSnakePath(width, height);

        var path = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++)
        {
            if (y % 2 == 0)
                for (int x = width - 1; x >= 0; x--) path.Add(new Vector2Int(x, y));
            else
                for (int x = 0; x < width; x++) path.Add(new Vector2Int(x, y));
        }
        return path;
    }

    private static List<Vector2Int> TriangleColumnSnakePath(int width, int height)
    {
        var path = new List<Vector2Int>(width * height);
        for (int x = 0; x < width; x++)
        {
            if (x % 2 == 0)
            {
                for (int y = 0; y < height; y++)
                    path.Add(new Vector2Int(x, y));
            }
            else
            {
                for (int y = height - 1; y >= 0; y--)
                    path.Add(new Vector2Int(x, y));
            }
        }
        return path;
    }

    private static List<Vector2Int> TransformTrianglePathVariant(List<Vector2Int> sourcePath, int width, int height, int variantIndex)
    {
        var transformed = new List<Vector2Int>(sourcePath.Count);
        bool mirrorX = (variantIndex & 1) != 0;
        bool mirrorY = (variantIndex & 2) != 0;
        bool reverse = (variantIndex & 4) != 0;

        for (int i = 0; i < sourcePath.Count; i++)
        {
            Vector2Int cell = sourcePath[i];
            int x = mirrorX ? (width - 1 - cell.x) : cell.x;
            int y = mirrorY ? (height - 1 - cell.y) : cell.y;
            transformed.Add(new Vector2Int(x, y));
        }

        if (reverse)
            transformed.Reverse();

        return transformed;
    }

    private static bool AreTriangleCellsAdjacent(Vector2Int a, Vector2Int b, int width, int height)
    {
        if (a.x == b.x && a.y == b.y)
            return false;

        if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1)
        {
            if (a.y == b.y)
                return true;

            bool isUp = (a.x + a.y) % 2 == 0;
            int verticalNeighborY = a.y + (isUp ? 1 : -1);
            return a.x == b.x && b.y == verticalNeighborY;
        }

        return false;
    }

    private static List<TriangleRemovableWindow> FindTriangleRemovableWindows(
        List<Vector2Int> path,
        int width,
        int height,
        int blockedLength,
        int frontTrim,
        int backTrim)
    {
        var windows = new List<TriangleRemovableWindow>();
        int playableEndExclusive = path.Count - backTrim;

        for (int start = frontTrim + 1; start + blockedLength < playableEndExclusive; start++)
        {
            int end = start + blockedLength - 1;
            if (!AreTriangleCellsAdjacent(path[start - 1], path[end + 1], width, height))
                continue;

            int interiorCount = 0;
            for (int i = start; i <= end; i++)
            {
                Vector2Int cell = path[i];
                if (cell.x > 0 && cell.x < width - 1 && cell.y > 0 && cell.y < height - 1)
                    interiorCount++;
            }

            windows.Add(new TriangleRemovableWindow
            {
                startIndex = start,
                endIndex = end,
                interiorCount = interiorCount
            });
        }

        return windows;
    }

    private static bool TryBuildInteriorTriangleFallback(
        List<Vector2Int> fullPath,
        int levelIndex,
        CampaignConfig config,
        System.Random rng,
        int desiredBlocked,
        out List<Vector2Int> playablePath,
        out List<Vector2Int> blocked)
    {
        TriangleRemovableWindow? bestWindow = null;
        int bestFrontTrim = 0;
        int bestBackTrim = 0;
        float bestScore = float.NegativeInfinity;
        playablePath = null;
        blocked = null;

        for (int interiorBlockedLength = Mathf.Min(desiredBlocked, 8); interiorBlockedLength >= 2; interiorBlockedLength--)
        {
            if ((interiorBlockedLength & 1) != 0)
                continue;

            int edgeTrimCount = desiredBlocked - interiorBlockedLength;
            if (edgeTrimCount < 0)
                continue;

            for (int frontTrim = 0; frontTrim <= edgeTrimCount; frontTrim++)
            {
                int backTrim = edgeTrimCount - frontTrim;
                List<TriangleRemovableWindow> windows = FindTriangleRemovableWindows(
                    fullPath,
                    config.width,
                    config.height,
                    interiorBlockedLength,
                    frontTrim,
                    backTrim);

                if (windows.Count == 0)
                    continue;

                foreach (TriangleRemovableWindow window in windows)
                {
                    float center = (window.startIndex + window.endIndex) * 0.5f;
                    float centerDistance = Mathf.Abs(center - (fullPath.Count - 1) * 0.5f);
                    float score = window.interiorCount * 100f
                        - edgeTrimCount * 18f
                        - centerDistance * 0.12f
                        + (float)rng.NextDouble() * 0.5f;

                    if (bestWindow == null || score > bestScore)
                    {
                        bestWindow = window;
                        bestFrontTrim = frontTrim;
                        bestBackTrim = backTrim;
                        bestScore = score;
                    }
                }
            }
        }

        if (bestWindow == null)
            return false;

        TriangleRemovableWindow chosenWindow = bestWindow.Value;
        int playableEndExclusive = fullPath.Count - bestBackTrim;
        blocked = new List<Vector2Int>(desiredBlocked);

        for (int i = 0; i < bestFrontTrim; i++)
            blocked.Add(fullPath[i]);

        for (int i = chosenWindow.startIndex; i <= chosenWindow.endIndex; i++)
            blocked.Add(fullPath[i]);

        for (int i = playableEndExclusive; i < fullPath.Count; i++)
            blocked.Add(fullPath[i]);

        playablePath = new List<Vector2Int>(fullPath.Count - blocked.Count);
        for (int i = bestFrontTrim; i < chosenWindow.startIndex; i++)
            playablePath.Add(fullPath[i]);
        for (int i = chosenWindow.endIndex + 1; i < playableEndExclusive; i++)
            playablePath.Add(fullPath[i]);

        return playablePath.Count >= config.minSegment;
    }

    private static float ScoreTriangleWindowCombination(List<TriangleRemovableWindow> windows, int edgeTrimCount)
    {
        if (windows == null || windows.Count == 0)
            return float.NegativeInfinity;

        float score = windows.Count * 28f - edgeTrimCount * 16f;
        for (int i = 0; i < windows.Count; i++)
        {
            TriangleRemovableWindow window = windows[i];
            int length = window.endIndex - window.startIndex + 1;
            float center = (window.startIndex + window.endIndex) * 0.5f;
            score += window.interiorCount * 90f;
            score += length == 1 ? 18f : length == 2 ? 10f : length == 3 ? 3f : -4f;
            if (window.interiorCount == 0)
                score -= 22f;

            for (int j = i + 1; j < windows.Count; j++)
            {
                TriangleRemovableWindow other = windows[j];
                float otherCenter = (other.startIndex + other.endIndex) * 0.5f;
                score += Mathf.Abs(center - otherCenter) * 0.25f;
            }
        }

        return score;
    }

    private static void SearchTriangleWindowCombination(
        List<TriangleRemovableWindow> windows,
        int nextIndex,
        int remainingLength,
        int minGap,
        List<TriangleRemovableWindow> current,
        ref List<TriangleRemovableWindow> best,
        ref float bestScore,
        int edgeTrimCount)
    {
        if (remainingLength == 0)
        {
            float score = ScoreTriangleWindowCombination(current, edgeTrimCount);
            if (score > bestScore)
            {
                bestScore = score;
                best = new List<TriangleRemovableWindow>(current);
            }
            return;
        }

        for (int i = nextIndex; i < windows.Count; i++)
        {
            TriangleRemovableWindow window = windows[i];
            int length = window.endIndex - window.startIndex + 1;
            if (length > remainingLength)
                continue;

            bool overlaps = false;
            for (int j = 0; j < current.Count; j++)
            {
                TriangleRemovableWindow chosen = current[j];
                if (window.startIndex <= chosen.endIndex + minGap &&
                    window.endIndex >= chosen.startIndex - minGap)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            current.Add(window);
            SearchTriangleWindowCombination(
                windows,
                i + 1,
                remainingLength - length,
                minGap,
                current,
                ref best,
                ref bestScore,
                edgeTrimCount);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static bool TryBuildDistributedTriangleWindows(
        List<Vector2Int> fullPath,
        CampaignConfig config,
        int desiredBlocked,
        out List<Vector2Int> playablePath,
        out List<Vector2Int> blocked)
    {
        playablePath = null;
        blocked = null;
        List<TriangleRemovableWindow> bestWindows = null;
        int bestFrontTrim = 0;
        int bestBackTrim = 0;
        float bestScore = float.NegativeInfinity;

        int[] edgeTrimOptions = desiredBlocked % 2 == 0 ? new[] { 0 } : new[] { 1 };
        foreach (int edgeTrimCount in edgeTrimOptions)
        {
            int interiorBlockedLength = desiredBlocked - edgeTrimCount;
            if (interiorBlockedLength < 2)
                continue;

            for (int frontTrim = 0; frontTrim <= edgeTrimCount; frontTrim++)
            {
                int backTrim = edgeTrimCount - frontTrim;
                var windows = new List<TriangleRemovableWindow>();
                for (int length = 1; length <= Mathf.Min(interiorBlockedLength, 4); length++)
                    windows.AddRange(FindTriangleRemovableWindows(fullPath, config.width, config.height, length, frontTrim, backTrim));

                windows.Sort((a, b) =>
                {
                    int cmp = a.startIndex.CompareTo(b.startIndex);
                    return cmp != 0 ? cmp : a.endIndex.CompareTo(b.endIndex);
                });

                List<TriangleRemovableWindow> candidateWindows = null;
                float candidateScore = float.NegativeInfinity;
                SearchTriangleWindowCombination(
                    windows,
                    0,
                    interiorBlockedLength,
                    2,
                    new List<TriangleRemovableWindow>(),
                    ref candidateWindows,
                    ref candidateScore,
                    edgeTrimCount);

                if (candidateWindows != null && candidateScore > bestScore)
                {
                    bestWindows = candidateWindows;
                    bestFrontTrim = frontTrim;
                    bestBackTrim = backTrim;
                    bestScore = candidateScore;
                }
            }
        }

        if (bestWindows == null)
            return false;

        var blockedIndices = new HashSet<int>();
        for (int i = 0; i < bestFrontTrim; i++)
            blockedIndices.Add(i);
        for (int i = fullPath.Count - bestBackTrim; i < fullPath.Count; i++)
            blockedIndices.Add(i);
        foreach (TriangleRemovableWindow window in bestWindows)
            for (int i = window.startIndex; i <= window.endIndex; i++)
                blockedIndices.Add(i);

        blocked = new List<Vector2Int>(blockedIndices.Count);
        playablePath = new List<Vector2Int>(fullPath.Count - blockedIndices.Count);
        for (int i = 0; i < fullPath.Count; i++)
        {
            if (blockedIndices.Contains(i))
                blocked.Add(fullPath[i]);
            else
                playablePath.Add(fullPath[i]);
        }

        return blocked.Count == desiredBlocked && playablePath.Count >= config.minSegment;
    }

    private static LevelCandidate BuildTriangleBundledCandidate(int levelIndex, CampaignConfig config)
    {
        int desiredBlocked = Mathf.Clamp(
            config.minBlocked + (levelIndex % Mathf.Max(1, config.maxBlocked - config.minBlocked + 1)),
            config.minBlocked,
            config.maxBlocked);

        LevelCandidate bestCandidate = null;
        for (int variantOffset = 0; variantOffset < 8; variantOffset++)
        {
            int variantIndex = (levelIndex * 5 + config.maxBlocked + variantOffset * 3) & 7;
            var rng = new System.Random((levelIndex + 9000) * 6151 + variantOffset * 197);
            List<Vector2Int> fullPath = HamiltonianPath(
                config.width,
                config.height,
                new HashSet<Vector2Int>(),
                rng,
                triMode: true,
                maxAttempts: 160);
            if (fullPath == null)
            {
                fullPath = TransformTrianglePathVariant(
                    TriangleSnakePath(config.width, config.height),
                    config.width,
                    config.height,
                    variantIndex);
            }

            desiredBlocked = Mathf.Min(desiredBlocked, Mathf.Max(0, fullPath.Count - config.minSegment));
            if (!TryBuildDistributedTriangleWindows(fullPath, config, desiredBlocked, out List<Vector2Int> playablePath, out List<Vector2Int> blocked))
                continue;

            List<List<Vector2Int>> segments = SplitPath(playablePath, config, rng)
                ?? UniformSplit(playablePath, config.minSegment);
            if (segments == null || segments.Count == 0)
                continue;

            float score = ComputeTriangleBlockedScatterScore(blocked, config) + CountTurns(playablePath) * 0.12f;
            if (bestCandidate == null || score > bestCandidate.score)
            {
                bestCandidate = new LevelCandidate
                {
                    path = playablePath,
                    segments = segments,
                    signature = BuildSignature(config, segments),
                    contentFingerprint = BuildContentFingerprint(config, segments, blocked),
                    score = score,
                    blocked = blocked
                };
            }
        }

        return bestCandidate;
    }

    private static float ComputeTriangleBlockedScatterScore(IList<Vector2Int> blocked, CampaignConfig config)
    {
        if (blocked == null || blocked.Count == 0)
            return 0f;

        float score = 0f;
        var usedXBands = new HashSet<int>();
        var usedYBands = new HashSet<int>();
        for (int i = 0; i < blocked.Count; i++)
        {
            Vector2Int a = blocked[i];
            int edgeDistance = Mathf.Min(
                Mathf.Min(a.x, config.width - 1 - a.x),
                Mathf.Min(a.y, config.height - 1 - a.y));
            score += edgeDistance * 3.2f;
            usedXBands.Add(Mathf.Min(2, (a.x * 3) / Mathf.Max(1, config.width)));
            usedYBands.Add(Mathf.Min(2, (a.y * 3) / Mathf.Max(1, config.height)));

            for (int j = i + 1; j < blocked.Count; j++)
            {
                Vector2Int b = blocked[j];
                int manhattan = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
                score += Mathf.Min(manhattan, 8) * 0.9f;

                if (AreTriangleCellsAdjacent(a, b, config.width, config.height))
                    score -= 15f;
                else if (manhattan == 2)
                    score -= 3f;

                if (Mathf.Abs(a.x - b.x) <= 1 && Mathf.Abs(a.y - b.y) <= 1)
                    score -= 4f;

                if (a.x == b.x || a.y == b.y)
                    score -= 1.75f;
            }
        }

        score += usedXBands.Count * 1.4f;
        score += usedYBands.Count * 1.1f;
        return score;
    }

    private static float ComputeTriangleBlockedAnchorScore(IList<Vector2Int> blocked, CampaignConfig config, int levelIndex)
    {
        if (blocked == null || blocked.Count == 0)
            return 0f;

        int anchorCount = Mathf.Min(blocked.Count, 4);
        List<Vector2> anchors = BuildTriangleBlockedAnchors(config, anchorCount, levelIndex, 0);

        float score = 0f;
        foreach (Vector2Int cell in blocked)
        {
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < anchors.Count; i++)
            {
                float distance = Mathf.Abs(cell.x - anchors[i].x) + Mathf.Abs(cell.y - anchors[i].y);
                if (distance < bestDistance)
                    bestDistance = distance;
            }

            score += Mathf.Max(0f, 6f - bestDistance) * 2.2f;
        }

        return score;
    }

    private static List<Vector2> BuildTriangleBlockedAnchors(CampaignConfig config, int count, int levelIndex, int variantSeed)
    {
        var anchors = new List<Vector2>(count);
        if (count <= 0)
            return anchors;

        int interiorMaxX = Mathf.Max(1, config.width - 2);
        int interiorMaxY = Mathf.Max(1, config.height - 2);
        int offset = Mathf.Abs(levelIndex * 3 + variantSeed * 5) % TriangleBlockedAnchorTemplates.Length;
        bool mirrorX = ((levelIndex + variantSeed) & 1) != 0;
        bool mirrorY = ((levelIndex + variantSeed) & 2) != 0;

        for (int i = 0; i < count; i++)
        {
            Vector2 normalized = TriangleBlockedAnchorTemplates[(offset + i * 2) % TriangleBlockedAnchorTemplates.Length];
            float nx = mirrorX ? 1f - normalized.x : normalized.x;
            float ny = mirrorY ? 1f - normalized.y : normalized.y;
            float x = Mathf.Clamp(1f + nx * (interiorMaxX - 1), 1f, interiorMaxX);
            float y = Mathf.Clamp(1f + ny * (interiorMaxY - 1), 1f, interiorMaxY);
            anchors.Add(new Vector2(x, y));
        }

        return anchors;
    }

    private static float ScoreTriangleBlockedAnchorCandidate(
        Vector2Int cell,
        Vector2 anchor,
        HashSet<Vector2Int> blocked,
        CampaignConfig config)
    {
        int edgeDistance = Mathf.Min(
            Mathf.Min(cell.x, config.width - 1 - cell.x),
            Mathf.Min(cell.y, config.height - 1 - cell.y));
        float score = edgeDistance * 6f;
        score -= (Mathf.Abs(cell.x - anchor.x) + Mathf.Abs(cell.y - anchor.y)) * 2.4f;

        foreach (Vector2Int existing in blocked)
        {
            if (AreTriangleCellsAdjacent(cell, existing, config.width, config.height))
                return float.NegativeInfinity;

            int dx = Mathf.Abs(cell.x - existing.x);
            int dy = Mathf.Abs(cell.y - existing.y);
            if (dx <= 1 && dy <= 1)
                return float.NegativeInfinity;

            int manhattan = dx + dy;
            score += Mathf.Min(manhattan, 8) * 0.8f;

            if (cell.x == existing.x || cell.y == existing.y)
                score -= 1.5f;
        }

        return score;
    }

    private static HashSet<Vector2Int> GenerateAnchoredTriangleBlockedCellsExact(
        CampaignConfig config,
        int count,
        int levelIndex,
        int variantSeed,
        System.Random rng)
    {
        var blocked = new HashSet<Vector2Int>();
        if (count <= 0)
            return blocked;

        var allCells = new List<Vector2Int>(config.width * config.height);
        for (int y = 0; y < config.height; y++)
            for (int x = 0; x < config.width; x++)
                allCells.Add(new Vector2Int(x, y));
        Shuffle(allCells, rng);

        List<Vector2> anchors = BuildTriangleBlockedAnchors(config, count, levelIndex, variantSeed);
        foreach (Vector2 anchor in anchors)
        {
            Vector2Int bestCell = default;
            float bestScore = float.NegativeInfinity;
            bool found = false;

            foreach (Vector2Int candidate in allCells)
            {
                if (blocked.Contains(candidate))
                    continue;

                float score = ScoreTriangleBlockedAnchorCandidate(candidate, anchor, blocked, config);
                if (score <= bestScore)
                    continue;

                blocked.Add(candidate);
                if (!IsGridConnected(config.width, config.height, blocked, triMode: true))
                {
                    blocked.Remove(candidate);
                    continue;
                }

                blocked.Remove(candidate);
                bestCell = candidate;
                bestScore = score;
                found = true;
            }

            if (!found)
                continue;

            blocked.Add(bestCell);
        }

        if (blocked.Count < count)
        {
            foreach (Vector2Int candidate in allCells)
            {
                if (blocked.Count >= count)
                    break;
                if (blocked.Contains(candidate))
                    continue;

                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < anchors.Count; i++)
                    bestScore = Mathf.Max(bestScore, ScoreTriangleBlockedAnchorCandidate(candidate, anchors[i], blocked, config));
                if (float.IsNegativeInfinity(bestScore))
                    continue;

                blocked.Add(candidate);
                if (!IsGridConnected(config.width, config.height, blocked, triMode: true))
                    blocked.Remove(candidate);
            }
        }

        return blocked;
    }

    public static LevelData[] GeneratePentagonCampaign(int count)
        => GeneratePentagonCampaign(0, count);

    public static LevelData[] GeneratePentagonCampaign(int startIndex, int count)
    {
        var levels = new LevelData[count];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        var recentRegionSets = new Queue<string>();

        for (int localIndex = 0; localIndex < count; localIndex++)
        {
            int i = startIndex + localIndex;
            var rng = new System.Random((i + 500) * 7919 + 11);
            CampaignConfig config = GetPentagonConfig(i);
            LevelCandidate bestCandidate = null;
            bool fastBundledMode = UseBundledBuildOptimizations;
            int candidateAttempts = fastBundledMode ? Mathf.Min(config.candidateCount, 8) : config.candidateCount;

            for (int ci = 0; ci < candidateAttempts; ci++)
            {
                var candidateRng = new System.Random(rng.Next());
                HashSet<Vector2Int> blocked = GenerateBlockedCells(config, candidateRng, colHexMode: true);
                List<Vector2Int> path = HamiltonianPath(config.width, config.height, blocked, candidateRng, colHexMode: true);
                if (path == null) continue;
                List<List<Vector2Int>> segments = SplitPath(path, config, candidateRng);
                if (segments == null || segments.Count == 0) continue;

                var blockedList = new List<Vector2Int>(blocked);
                string signature = BuildSignature(config, segments);
                string contentFingerprint = BuildContentFingerprint(config, segments, blockedList);
                float score = ScoreCandidate(path, segments, config, signature, contentFingerprint,
                    recentSignatures, usedSignatures, allFingerprints, usedValueSetsByTier, recentRegionSets);

                if (bestCandidate == null || score > bestCandidate.score)
                {
                    bestCandidate = new LevelCandidate
                    {
                        path = path, segments = segments,
                        signature = signature, contentFingerprint = contentFingerprint,
                        score = score, blocked = blockedList
                    };
                }
            }

            if (bestCandidate == null)
            {
                var emptyBlocked = new List<Vector2Int>();
                List<Vector2Int> fallbackPath = SnakePath(config.width, config.height);
                List<List<Vector2Int>> fallbackSegs = SplitPath(fallbackPath, config, rng)
                    ?? UniformSplit(fallbackPath, config.minSegment);
                bestCandidate = new LevelCandidate
                {
                    path = fallbackPath, segments = fallbackSegs,
                    signature = BuildSignature(config, fallbackSegs),
                    contentFingerprint = BuildContentFingerprint(config, fallbackSegs, emptyBlocked),
                    score = 0f, blocked = emptyBlocked
                };
            }

            // Extra de-dup loop — avoid exact duplicate puzzles
            int extraAttempts = fastBundledMode
                ? (config.maxBlocked > 0 ? 10 : 6)
                : (config.maxBlocked > 0 ? 100 : 60);
            if (allFingerprints.Contains(bestCandidate.contentFingerprint))
            {
                for (int extra = 0; extra < extraAttempts; extra++)
                {
                    var xRng = new System.Random((i + 500) * 7919 + 11 + (extra + 1) * 1013);
                    HashSet<Vector2Int> xBlocked = GenerateBlockedCells(config, xRng, colHexMode: true);
                    List<Vector2Int> xPath = HamiltonianPath(config.width, config.height, xBlocked, xRng, colHexMode: true);
                    if (xPath == null) continue;
                    List<List<Vector2Int>> xSegs = SplitPath(xPath, config, xRng);
                    if (xSegs == null || xSegs.Count == 0) continue;
                    var xBlockedList = new List<Vector2Int>(xBlocked);
                    string xFp = BuildContentFingerprint(config, xSegs, xBlockedList);
                    if (!allFingerprints.Contains(xFp))
                    {
                        bestCandidate = new LevelCandidate
                        {
                            path = xPath, segments = xSegs,
                            signature = BuildSignature(config, xSegs),
                            contentFingerprint = xFp,
                            score = float.PositiveInfinity, blocked = xBlockedList
                        };
                        break;
                    }
                }
            }

            LevelData ld = BuildLevelData(config, bestCandidate.segments, bestCandidate.blocked);
            ld.cellShape = CellShape.Pentagon;
            levels[localIndex] = ld;

            recentSignatures.Enqueue(bestCandidate.signature);
            while (recentSignatures.Count > 15) recentSignatures.Dequeue();

            if (!usedSignatures.ContainsKey(bestCandidate.signature))
                usedSignatures[bestCandidate.signature] = 0;
            usedSignatures[bestCandidate.signature]++;

            string chosenValueSet = BuildValueSetFingerprint(bestCandidate.segments);
            if (!usedValueSetsByTier.ContainsKey(config.tierName))
                usedValueSetsByTier[config.tierName] = new Dictionary<string, int>();
            if (!usedValueSetsByTier[config.tierName].ContainsKey(chosenValueSet))
                usedValueSetsByTier[config.tierName][chosenValueSet] = 0;
            usedValueSetsByTier[config.tierName][chosenValueSet]++;

            string chosenRegion = BuildRegionFingerprint(bestCandidate.segments, config);
            recentRegionSets.Enqueue(chosenRegion);
            while (recentRegionSets.Count > 10) recentRegionSets.Dequeue();

            allFingerprints.Add(bestCandidate.contentFingerprint);
        }

        return levels;
    }

    private static CampaignConfig GetPentagonConfig(int idx)
    {
        CampaignConfig c = new CampaignConfig();

        if (idx < 25)          // Tier 1: 5×5, Easy (levels 301-325)
        {
            c.width = 5; c.height = 5;
            c.minSegment = 3; c.maxSegment = 7; c.candidateCount = 24;
            c.tierName = "Hex Easy";
            c.rectanglePenalty = 3.2f; c.densePenalty = 2.4f;
            c.straightPenalty = 1.8f; c.turnWeight = 1.15f;
            c.squarePenalty = 1.2f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 60)     // Tier 2: 5×6, Easy+ (326-360)
        {
            SetRectangularBoard(ref c, 5, 6);
            c.minSegment = 3; c.maxSegment = 8; c.candidateCount = 26;
            c.tierName = "Hex Easy";
            c.rectanglePenalty = 2.9f; c.densePenalty = 2.2f;
            c.straightPenalty = 1.7f; c.turnWeight = 1.1f;
            c.squarePenalty = 1.0f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 95)     // Tier 3: 6×6, Normal (361-395)
        {
            c.width = 6; c.height = 6;
            c.minSegment = 4; c.maxSegment = 9; c.candidateCount = 26;
            c.tierName = "Hex Normal";
            c.rectanglePenalty = 2.5f; c.densePenalty = 1.9f;
            c.straightPenalty = 1.5f; c.turnWeight = 1.05f;
            c.squarePenalty = 0.85f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 135)    // Tier 4: 6×7, Normal+ (396-435)
        {
            SetRectangularBoard(ref c, 6, 7);
            c.minSegment = 4; c.maxSegment = 9; c.candidateCount = 26;
            c.tierName = "Hex Normal";
            c.rectanglePenalty = 2.1f; c.densePenalty = 1.6f;
            c.straightPenalty = 1.3f; c.turnWeight = 0.98f;
            c.squarePenalty = 0.7f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 1;
        }
        else if (idx < 170)    // Tier 5: 6×8, Hard (436-470)
        {
            SetRectangularBoard(ref c, 6, 8);
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 28;
            c.tierName = "Hex Hard";
            c.rectanglePenalty = 1.8f; c.densePenalty = 1.4f;
            c.straightPenalty = 1.1f; c.turnWeight = 0.92f;
            c.squarePenalty = 0.6f; c.lateRectangleBonus = 0.05f;
            c.minBlocked = 1; c.maxBlocked = 2;
        }
        else if (idx < 210)    // Tier 6: 6×9, Hard+ (471-510)
        {
            SetRectangularBoard(ref c, 6, 9);
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 28;
            c.tierName = "Hex Hard";
            c.rectanglePenalty = 1.5f; c.densePenalty = 1.2f;
            c.straightPenalty = 1.0f; c.turnWeight = 0.87f;
            c.squarePenalty = 0.5f; c.lateRectangleBonus = 0.1f;
            c.minBlocked = 2; c.maxBlocked = 3;
        }
        else if (idx < 245)    // Tier 7: 6×10, Advanced (511-545)
        {
            SetRectangularBoard(ref c, 6, 10);
            c.minSegment = 5; c.maxSegment = 11; c.candidateCount = 28;
            c.tierName = "Hex Advanced";
            c.rectanglePenalty = 1.2f; c.densePenalty = 0.95f;
            c.straightPenalty = 0.9f; c.turnWeight = 0.82f;
            c.squarePenalty = 0.4f; c.lateRectangleBonus = 0.15f;
            c.minBlocked = 2; c.maxBlocked = 3;
        }
        else if (idx < 275)    // Tier 8: 6×11, Expert (546-575)
        {
            SetRectangularBoard(ref c, 6, 11);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 30;
            c.tierName = "Hex Expert";
            c.rectanglePenalty = 1.0f; c.densePenalty = 0.75f;
            c.straightPenalty = 0.85f; c.turnWeight = 0.77f;
            c.squarePenalty = 0.3f; c.lateRectangleBonus = 0.2f;
            c.minBlocked = 3; c.maxBlocked = 4;
        }
        else                   // Tier 9: 6×12, Master (576-600)
        {
            SetRectangularBoard(ref c, 6, 12);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 32;
            c.tierName = "Hex Master";
            c.rectanglePenalty = 0.8f; c.densePenalty = 0.6f;
            c.straightPenalty = 0.75f; c.turnWeight = 0.7f;
            c.squarePenalty = 0.2f; c.lateRectangleBonus = 0.3f;
            c.minBlocked = 5; c.maxBlocked = 6;
        }

        return c;
    }

    // --- Hexagon campaign (flat-top column-offset, 6gen) ---

    public static LevelData[] GenerateHexagonCampaign(int count)
        => GenerateHexagonCampaign(0, count);

    public static LevelData[] GenerateHexagonCampaign(int startIndex, int count)
    {
        var levels = new LevelData[count];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        var recentRegionSets = new Queue<string>();

        for (int localIndex = 0; localIndex < count; localIndex++)
        {
            int i = startIndex + localIndex;
            var rng = new System.Random((i + 1000) * 7919 + 17);
            CampaignConfig config = GetHexagonConfig(i);
            LevelCandidate bestCandidate = null;
            bool fastBundledMode = UseBundledBuildOptimizations;
            int candidateAttempts = fastBundledMode ? Mathf.Min(config.candidateCount, 8) : config.candidateCount;

            for (int ci = 0; ci < candidateAttempts; ci++)
            {
                var candidateRng = new System.Random(rng.Next());
                HashSet<Vector2Int> blocked = GenerateBlockedCells(config, candidateRng, hexMode: false, colHexMode: true);
                List<Vector2Int> path = HamiltonianPath(config.width, config.height, blocked, candidateRng, hexMode: false, colHexMode: true);
                if (path == null) continue;
                List<List<Vector2Int>> segments = SplitPath(path, config, candidateRng);
                if (segments == null || segments.Count == 0) continue;

                var blockedList = new List<Vector2Int>(blocked);
                string signature = BuildSignature(config, segments);
                string contentFingerprint = BuildContentFingerprint(config, segments, blockedList);
                float score = ScoreCandidate(path, segments, config, signature, contentFingerprint,
                    recentSignatures, usedSignatures, allFingerprints, usedValueSetsByTier, recentRegionSets);

                if (bestCandidate == null || score > bestCandidate.score)
                {
                    bestCandidate = new LevelCandidate
                    {
                        path = path, segments = segments,
                        signature = signature, contentFingerprint = contentFingerprint,
                        score = score, blocked = blockedList
                    };
                }
            }

            if (bestCandidate == null)
            {
                var emptyBlocked = new List<Vector2Int>();
                List<Vector2Int> fallbackPath = SnakePath(config.width, config.height);
                List<List<Vector2Int>> fallbackSegs = SplitPath(fallbackPath, config, rng)
                    ?? UniformSplit(fallbackPath, config.minSegment);
                bestCandidate = new LevelCandidate
                {
                    path = fallbackPath, segments = fallbackSegs,
                    signature = BuildSignature(config, fallbackSegs),
                    contentFingerprint = BuildContentFingerprint(config, fallbackSegs, emptyBlocked),
                    score = 0f, blocked = emptyBlocked
                };
            }

            int extraAttempts = fastBundledMode
                ? (config.maxBlocked > 0 ? 10 : 6)
                : (config.maxBlocked > 0 ? 100 : 60);
            if (allFingerprints.Contains(bestCandidate.contentFingerprint))
            {
                for (int extra = 0; extra < extraAttempts; extra++)
                {
                    var xRng = new System.Random((i + 1000) * 7919 + 17 + (extra + 1) * 1013);
                    HashSet<Vector2Int> xBlocked = GenerateBlockedCells(config, xRng, hexMode: false, colHexMode: true);
                    List<Vector2Int> xPath = HamiltonianPath(config.width, config.height, xBlocked, xRng, hexMode: false, colHexMode: true);
                    if (xPath == null) continue;
                    List<List<Vector2Int>> xSegs = SplitPath(xPath, config, xRng);
                    if (xSegs == null || xSegs.Count == 0) continue;
                    var xBlockedList = new List<Vector2Int>(xBlocked);
                    string xFp = BuildContentFingerprint(config, xSegs, xBlockedList);
                    if (!allFingerprints.Contains(xFp))
                    {
                        bestCandidate = new LevelCandidate
                        {
                            path = xPath, segments = xSegs,
                            signature = BuildSignature(config, xSegs),
                            contentFingerprint = xFp,
                            score = float.PositiveInfinity, blocked = xBlockedList
                        };
                        break;
                    }
                }
            }

            LevelData ld = BuildLevelData(config, bestCandidate.segments, bestCandidate.blocked);
            ld.cellShape = CellShape.Hexagon;
            levels[localIndex] = ld;

            recentSignatures.Enqueue(bestCandidate.signature);
            while (recentSignatures.Count > 15) recentSignatures.Dequeue();

            if (!usedSignatures.ContainsKey(bestCandidate.signature))
                usedSignatures[bestCandidate.signature] = 0;
            usedSignatures[bestCandidate.signature]++;

            string chosenValueSet = BuildValueSetFingerprint(bestCandidate.segments);
            if (!usedValueSetsByTier.ContainsKey(config.tierName))
                usedValueSetsByTier[config.tierName] = new Dictionary<string, int>();
            if (!usedValueSetsByTier[config.tierName].ContainsKey(chosenValueSet))
                usedValueSetsByTier[config.tierName][chosenValueSet] = 0;
            usedValueSetsByTier[config.tierName][chosenValueSet]++;

            string chosenRegion = BuildRegionFingerprint(bestCandidate.segments, config);
            recentRegionSets.Enqueue(chosenRegion);
            while (recentRegionSets.Count > 10) recentRegionSets.Dequeue();

            allFingerprints.Add(bestCandidate.contentFingerprint);
        }

        return levels;
    }

    private static CampaignConfig GetHexagonConfig(int idx)
    {
        CampaignConfig c = new CampaignConfig();

        if (idx < 20)          // Tier 1:  4×4  Intro  (601-620)
        {
            c.width = 4; c.height = 4;
            c.minSegment = 2; c.maxSegment = 6; c.candidateCount = 20;
            c.tierName = "6gen Intro";
            c.rectanglePenalty = 3.5f; c.densePenalty = 2.5f;
            c.straightPenalty = 2.0f; c.turnWeight = 1.2f;
            c.squarePenalty = 1.5f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 40)     // Tier 2:  4×5  Easy   (621-640)
        {
            SetRectangularBoard(ref c, 4, 5);
            c.minSegment = 2; c.maxSegment = 7; c.candidateCount = 22;
            c.tierName = "6gen Easy";
            c.rectanglePenalty = 3.2f; c.densePenalty = 2.3f;
            c.straightPenalty = 1.9f; c.turnWeight = 1.15f;
            c.squarePenalty = 1.3f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 65)     // Tier 3:  5×5  Easy+  (641-665)
        {
            c.width = 5; c.height = 5;
            c.minSegment = 3; c.maxSegment = 7; c.candidateCount = 24;
            c.tierName = "6gen Easy";
            c.rectanglePenalty = 3.0f; c.densePenalty = 2.1f;
            c.straightPenalty = 1.8f; c.turnWeight = 1.1f;
            c.squarePenalty = 1.1f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 90)     // Tier 4:  5×6  Normal (666-690)
        {
            SetRectangularBoard(ref c, 5, 6);
            c.minSegment = 3; c.maxSegment = 8; c.candidateCount = 24;
            c.tierName = "6gen Normal";
            c.rectanglePenalty = 2.6f; c.densePenalty = 1.9f;
            c.straightPenalty = 1.6f; c.turnWeight = 1.05f;
            c.squarePenalty = 0.9f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 1;
        }
        else if (idx < 120)    // Tier 5:  5×7  Normal+(691-720)
        {
            SetRectangularBoard(ref c, 5, 7);
            c.minSegment = 3; c.maxSegment = 9; c.candidateCount = 26;
            c.tierName = "6gen Normal";
            c.rectanglePenalty = 2.2f; c.densePenalty = 1.7f;
            c.straightPenalty = 1.4f; c.turnWeight = 1.0f;
            c.squarePenalty = 0.75f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 2;
        }
        else if (idx < 150)    // Tier 6:  6×7  Hard   (721-750)
        {
            SetRectangularBoard(ref c, 6, 7);
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 26;
            c.tierName = "6gen Hard";
            c.rectanglePenalty = 1.9f; c.densePenalty = 1.5f;
            c.straightPenalty = 1.2f; c.turnWeight = 0.95f;
            c.squarePenalty = 0.65f; c.lateRectangleBonus = 0.05f;
            c.minBlocked = 1; c.maxBlocked = 3;
        }
        else if (idx < 185)    // Tier 7:  6×8  Hard+  (751-785)
        {
            SetRectangularBoard(ref c, 6, 8);
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 28;
            c.tierName = "6gen Hard";
            c.rectanglePenalty = 1.6f; c.densePenalty = 1.3f;
            c.straightPenalty = 1.1f; c.turnWeight = 0.9f;
            c.squarePenalty = 0.55f; c.lateRectangleBonus = 0.1f;
            c.minBlocked = 2; c.maxBlocked = 4;
        }
        else if (idx < 220)    // Tier 8:  7×8  Advanced(786-820)
        {
            SetRectangularBoard(ref c, 7, 8);
            c.minSegment = 4; c.maxSegment = 11; c.candidateCount = 28;
            c.tierName = "6gen Advanced";
            c.rectanglePenalty = 1.3f; c.densePenalty = 1.0f;
            c.straightPenalty = 0.95f; c.turnWeight = 0.85f;
            c.squarePenalty = 0.45f; c.lateRectangleBonus = 0.15f;
            c.minBlocked = 3; c.maxBlocked = 5;
        }
        else if (idx < 260)    // Tier 9:  7×9  Expert (821-860)
        {
            SetRectangularBoard(ref c, 7, 9);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 30;
            c.tierName = "6gen Expert";
            c.rectanglePenalty = 1.0f; c.densePenalty = 0.8f;
            c.straightPenalty = 0.85f; c.turnWeight = 0.78f;
            c.squarePenalty = 0.35f; c.lateRectangleBonus = 0.2f;
            c.minBlocked = 4; c.maxBlocked = 6;
        }
        else                   // Tier 10: 7×10 Master (861-900)
        {
            SetRectangularBoard(ref c, 7, 10);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 32;
            c.tierName = "6gen Master";
            c.rectanglePenalty = 0.8f; c.densePenalty = 0.6f;
            c.straightPenalty = 0.75f; c.turnWeight = 0.7f;
            c.squarePenalty = 0.25f; c.lateRectangleBonus = 0.3f;
            c.minBlocked = 5; c.maxBlocked = 7;
        }

        return c;
    }

    // --- 3gen campaign (triangle grid — equilateral triangles tile perfectly) ---

    public static LevelData[] GenerateThreeGenCampaign(int count)
        => GenerateThreeGenCampaign(0, count);

    public static LevelData[] GenerateThreeGenCampaign(int startIndex, int count)
    {
        var levels = new LevelData[count];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        var recentRegionSets = new Queue<string>();
        var usedBlockedLayouts = new Dictionary<string, int>();
        var usedBlockedRegions = new Dictionary<string, int>();
        bool fastSingleLevelRequest = count == 1;

        for (int localIndex = 0; localIndex < count; localIndex++)
        {
            int i = startIndex + localIndex;
            var rng = new System.Random((i + 3000) * 7331 + 41);
            CampaignConfig config = GetThreeGenConfig(i);
            bool fastBundledMode = UseBundledBuildOptimizations;
            bool fastBundledSingleLevel = fastBundledMode && fastSingleLevelRequest;
            bool prioritizeBlockedReliability = fastSingleLevelRequest && config.maxBlocked > 0 && !fastBundledMode;
            bool hardBlockedTier = config.maxBlocked >= 4;
            int pathAttemptLimit = prioritizeBlockedReliability ? 220 : (fastBundledMode ? (hardBlockedTier ? 220 : (config.maxBlocked > 0 ? 112 : 64)) : (fastSingleLevelRequest ? 48 : 220));
            int initialCandidateAttempts = prioritizeBlockedReliability ? config.candidateCount : (fastBundledMode ? (hardBlockedTier ? Mathf.Min(config.candidateCount, 28) : (config.maxBlocked > 0 ? Mathf.Min(config.candidateCount, 14) : Mathf.Min(config.candidateCount, 8))) : (fastSingleLevelRequest ? Mathf.Min(config.candidateCount, 10) : config.candidateCount));
            int blockedRecoveryAttempts = prioritizeBlockedReliability ? config.candidateCount * 24 : (fastBundledMode ? (hardBlockedTier ? Mathf.Max(config.candidateCount * 12, 96) : Mathf.Max(config.candidateCount * 6, 30)) : (fastSingleLevelRequest ? Mathf.Max(config.candidateCount * 4, 28) : config.candidateCount * 24));
            int easierBlockedAttempts = prioritizeBlockedReliability ? Mathf.Max(config.candidateCount * 18, 160) : (fastBundledMode ? (hardBlockedTier ? Mathf.Max(config.candidateCount * 10, 80) : Mathf.Max(config.candidateCount * 4, 24)) : (fastSingleLevelRequest ? Mathf.Max(config.candidateCount * 3, 24) : Mathf.Max(config.candidateCount * 18, 160)));
            int guaranteedBlockedAttempts = prioritizeBlockedReliability ? Mathf.Max(config.candidateCount * 28, 240) : (fastBundledMode ? (hardBlockedTier ? Mathf.Max(config.candidateCount * 14, 120) : Mathf.Max(config.candidateCount * 6, 40)) : (fastSingleLevelRequest ? Mathf.Max(config.candidateCount * 4, 32) : Mathf.Max(config.candidateCount * 28, 240)));
            LevelCandidate bestCandidate = null;

            if (fastBundledSingleLevel && config.maxBlocked > 0)
                bestCandidate = BuildTriangleBundledCandidate(i, config);

            if (bestCandidate == null)
            {
                bestCandidate = TryGenerateThreeGenCandidate(
                    i,
                    config,
                    initialCandidateAttempts,
                    recentSignatures,
                    usedSignatures,
                    allFingerprints,
                    usedValueSetsByTier,
                    recentRegionSets,
                    pathAttemptLimit,
                    usedBlockedLayouts,
                    usedBlockedRegions);
            }

            if (bestCandidate == null && config.maxBlocked > 0)
            {
                bestCandidate = TryGenerateThreeGenCandidate(
                    i,
                    config,
                    blockedRecoveryAttempts,
                    recentSignatures,
                    usedSignatures,
                    allFingerprints,
                    usedValueSetsByTier,
                    recentRegionSets,
                    pathAttemptLimit,
                    usedBlockedLayouts,
                    usedBlockedRegions);
            }

            if (bestCandidate == null && config.maxBlocked > 1)
            {
                for (int forcedBlocked = config.maxBlocked - 1; forcedBlocked >= 1 && bestCandidate == null; forcedBlocked--)
                {
                    CampaignConfig easierBlockedConfig = config;
                    easierBlockedConfig.minBlocked = forcedBlocked;
                    easierBlockedConfig.maxBlocked = forcedBlocked;
                    easierBlockedConfig.candidateCount = easierBlockedAttempts;
                        bestCandidate = TryGenerateThreeGenCandidate(
                            i,
                            easierBlockedConfig,
                            easierBlockedConfig.candidateCount,
                            recentSignatures,
                            usedSignatures,
                            allFingerprints,
                            usedValueSetsByTier,
                            recentRegionSets,
                            pathAttemptLimit,
                            usedBlockedLayouts,
                            usedBlockedRegions);
                }
            }

            if (bestCandidate == null && config.maxBlocked > 0)
            {
                CampaignConfig guaranteedBlockedConfig = config;
                guaranteedBlockedConfig.minBlocked = 1;
                guaranteedBlockedConfig.maxBlocked = 1;
                guaranteedBlockedConfig.candidateCount = guaranteedBlockedAttempts;
                bestCandidate = TryGenerateThreeGenCandidate(
                    i,
                    guaranteedBlockedConfig,
                    guaranteedBlockedConfig.candidateCount,
                    recentSignatures,
                    usedSignatures,
                    allFingerprints,
                    usedValueSetsByTier,
                    recentRegionSets,
                    pathAttemptLimit,
                    usedBlockedLayouts,
                    usedBlockedRegions);
            }

            if (bestCandidate == null)
            {
                bestCandidate = BuildTriangleFallbackCandidate(i, config, rng);
            }

            string bestBlockedLayout = BuildBlockedLayoutFingerprint(bestCandidate.blocked);
            int bestBlockedLayoutUses = 0;
            if (!string.IsNullOrEmpty(bestBlockedLayout))
                usedBlockedLayouts.TryGetValue(bestBlockedLayout, out bestBlockedLayoutUses);
            string bestBlockedRegion = BuildBlockedRegionFingerprint(config, bestCandidate.blocked);
            int bestBlockedRegionUses = 0;
            if (!string.IsNullOrEmpty(bestBlockedRegion))
                usedBlockedRegions.TryGetValue(bestBlockedRegion, out bestBlockedRegionUses);

            int extraAttempts = fastBundledMode
                ? (config.maxBlocked > 0 ? 8 : 5)
                : prioritizeBlockedReliability
                ? 100
                : (fastSingleLevelRequest
                ? (config.maxBlocked > 0 ? 12 : 8)
                : (config.maxBlocked > 0 ? 100 : 60));
            if (allFingerprints.Contains(bestCandidate.contentFingerprint) || bestBlockedLayoutUses > 0)
            {
                for (int extra = 0; extra < extraAttempts; extra++)
                {
                    var xRng = new System.Random((i + 3000) * 7331 + 41 + (extra + 1) * 997);
                    int xBlockedCount = config.minBlocked == config.maxBlocked
                        ? config.minBlocked
                        : xRng.Next(config.minBlocked, config.maxBlocked + 1);
                    HashSet<Vector2Int> xBlocked = GenerateAnchoredTriangleBlockedCellsExact(config, xBlockedCount, i, extra + 101, xRng);
                    if (xBlocked.Count != xBlockedCount)
                        xBlocked = GenerateBlockedCellsExact(config, xBlockedCount, xRng, triMode: true);
                    if (config.maxBlocked > 0 && xBlocked.Count == 0) continue;
                    List<Vector2Int> xPath = HamiltonianPath(config.width, config.height, xBlocked, xRng, triMode: true, maxAttempts: pathAttemptLimit);
                    if (xPath == null) continue;
                    List<List<Vector2Int>> xSegs = SplitPath(xPath, config, xRng);
                    if (xSegs == null || xSegs.Count == 0) continue;
                    var xBlockedList = new List<Vector2Int>(xBlocked);
                    string xFp = BuildContentFingerprint(config, xSegs, xBlockedList);
                    if (!allFingerprints.Contains(xFp))
                    {
                        string xBlockedLayout = BuildBlockedLayoutFingerprint(xBlockedList);
                        int xBlockedLayoutUses = 0;
                        if (!string.IsNullOrEmpty(xBlockedLayout))
                            usedBlockedLayouts.TryGetValue(xBlockedLayout, out xBlockedLayoutUses);
                        string xBlockedRegion = BuildBlockedRegionFingerprint(config, xBlockedList);
                        int xBlockedRegionUses = 0;
                        if (!string.IsNullOrEmpty(xBlockedRegion))
                            usedBlockedRegions.TryGetValue(xBlockedRegion, out xBlockedRegionUses);

                        if (xBlockedLayoutUses > bestBlockedLayoutUses)
                            continue;
                        if (xBlockedLayoutUses == bestBlockedLayoutUses && xBlockedRegionUses > bestBlockedRegionUses)
                            continue;

                        bestCandidate = new LevelCandidate
                        {
                            path = xPath, segments = xSegs,
                            signature = BuildSignature(config, xSegs),
                            contentFingerprint = xFp,
                            score = float.PositiveInfinity, blocked = xBlockedList
                        };
                        bestBlockedLayout = xBlockedLayout;
                        bestBlockedLayoutUses = xBlockedLayoutUses;
                        bestBlockedRegion = xBlockedRegion;
                        bestBlockedRegionUses = xBlockedRegionUses;
                        if (xBlockedLayoutUses == 0 && xBlockedRegionUses == 0)
                            break;
                    }
                }
            }

            LevelData ld = BuildLevelData(config, bestCandidate.segments, bestCandidate.blocked);
            ld.cellShape = CellShape.ThreeGen;
            levels[localIndex] = ld;

            recentSignatures.Enqueue(bestCandidate.signature);
            while (recentSignatures.Count > 15) recentSignatures.Dequeue();

            if (!usedSignatures.ContainsKey(bestCandidate.signature))
                usedSignatures[bestCandidate.signature] = 0;
            usedSignatures[bestCandidate.signature]++;

            string chosenValueSet = BuildValueSetFingerprint(bestCandidate.segments);
            if (!usedValueSetsByTier.ContainsKey(config.tierName))
                usedValueSetsByTier[config.tierName] = new Dictionary<string, int>();
            if (!usedValueSetsByTier[config.tierName].ContainsKey(chosenValueSet))
                usedValueSetsByTier[config.tierName][chosenValueSet] = 0;
            usedValueSetsByTier[config.tierName][chosenValueSet]++;

            string chosenRegion = BuildRegionFingerprint(bestCandidate.segments, config);
            recentRegionSets.Enqueue(chosenRegion);
            while (recentRegionSets.Count > 10) recentRegionSets.Dequeue();

            allFingerprints.Add(bestCandidate.contentFingerprint);
            if (!string.IsNullOrEmpty(bestBlockedLayout))
            {
                if (!usedBlockedLayouts.ContainsKey(bestBlockedLayout))
                    usedBlockedLayouts[bestBlockedLayout] = 0;
                usedBlockedLayouts[bestBlockedLayout]++;
            }
            if (!string.IsNullOrEmpty(bestBlockedRegion))
            {
                if (!usedBlockedRegions.ContainsKey(bestBlockedRegion))
                    usedBlockedRegions[bestBlockedRegion] = 0;
                usedBlockedRegions[bestBlockedRegion]++;
            }
        }

        return levels;
    }

    private static LevelCandidate TryGenerateThreeGenCandidate(
        int levelIndex,
        CampaignConfig config,
        int attempts,
        Queue<string> recentSignatures,
        Dictionary<string, int> usedSignatures,
        HashSet<string> allFingerprints,
        Dictionary<string, Dictionary<string, int>> usedValueSetsByTier,
        Queue<string> recentRegionSets,
        int pathAttemptLimit,
        Dictionary<string, int> usedBlockedLayouts,
        Dictionary<string, int> usedBlockedRegions)
    {
        LevelCandidate bestCandidate = null;
        int baseSeed = (levelIndex + 3000) * 7331 + 41;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var candidateRng = new System.Random(baseSeed + (attempt + 1) * 1237);
            int desiredBlockedCount = config.minBlocked == config.maxBlocked
                ? config.minBlocked
                : candidateRng.Next(config.minBlocked, config.maxBlocked + 1);
            HashSet<Vector2Int> blocked = GenerateAnchoredTriangleBlockedCellsExact(config, desiredBlockedCount, levelIndex, attempt + 1, candidateRng);
            if (blocked.Count != desiredBlockedCount)
                blocked = GenerateBlockedCellsExact(config, desiredBlockedCount, candidateRng, triMode: true);
            if (config.maxBlocked > 0 && blocked.Count == 0)
                continue;

            List<Vector2Int> path = HamiltonianPath(config.width, config.height, blocked, candidateRng, triMode: true, maxAttempts: pathAttemptLimit);
            if (path == null) continue;

            List<List<Vector2Int>> segments = SplitPath(path, config, candidateRng);
            if (segments == null || segments.Count == 0) continue;

            var blockedList = new List<Vector2Int>(blocked);
            string signature = BuildSignature(config, segments);
            string contentFingerprint = BuildContentFingerprint(config, segments, blockedList);
            float score = ScoreCandidate(path, segments, config, signature, contentFingerprint,
                recentSignatures, usedSignatures, allFingerprints, usedValueSetsByTier, recentRegionSets);
            score += ComputeTriangleBlockedScatterScore(blockedList, config);
            score += ComputeTriangleBlockedAnchorScore(blockedList, config, levelIndex);
            string blockedLayout = BuildBlockedLayoutFingerprint(blockedList);
            if (!string.IsNullOrEmpty(blockedLayout) && usedBlockedLayouts.TryGetValue(blockedLayout, out int blockedLayoutUses))
                score -= blockedLayoutUses * 18f;
            string blockedRegion = BuildBlockedRegionFingerprint(config, blockedList);
            if (!string.IsNullOrEmpty(blockedRegion) && usedBlockedRegions.TryGetValue(blockedRegion, out int blockedRegionUses))
                score -= blockedRegionUses * 12f;

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

        return bestCandidate;
    }

    private static LevelCandidate BuildTriangleFallbackCandidate(int levelIndex, CampaignConfig config, System.Random rng)
    {
        int variantIndex = (levelIndex * 5 + config.maxBlocked) & 7;
        List<Vector2Int> fullPath = HamiltonianPath(
            config.width,
            config.height,
            new HashSet<Vector2Int>(),
            rng,
            triMode: true,
            maxAttempts: 240);
        if (fullPath == null)
        {
            fullPath = TransformTrianglePathVariant(
                TriangleSnakePath(config.width, config.height),
                config.width,
                config.height,
                variantIndex);
        }
        var blocked = new List<Vector2Int>();
        List<Vector2Int> playablePath = fullPath;

        if (config.maxBlocked > 0 && fullPath.Count > config.minSegment + config.minBlocked)
        {
            int desiredBlocked = Mathf.Clamp(
                config.minBlocked + (levelIndex % Mathf.Max(1, config.maxBlocked - config.minBlocked + 1)),
                config.minBlocked,
                config.maxBlocked);

            desiredBlocked = Mathf.Min(desiredBlocked, Mathf.Max(0, fullPath.Count - config.minSegment));
            LevelCandidate bestScatteredCandidate = null;
            for (int blockedTarget = desiredBlocked; blockedTarget >= 1; blockedTarget--)
            {
                int scatteredAttempts = Mathf.Max(config.candidateCount * 4, blockedTarget >= 4 ? 120 : 96);
                for (int attempt = 0; attempt < scatteredAttempts; attempt++)
                {
                    var attemptRng = new System.Random((levelIndex + 7000) * 9151 + blockedTarget * 131 + (attempt + 1) * 53);
                    HashSet<Vector2Int> scatteredBlocked = GenerateAnchoredTriangleBlockedCellsExact(config, blockedTarget, levelIndex, attempt + 1, attemptRng);
                    if (scatteredBlocked.Count != blockedTarget)
                        scatteredBlocked = GenerateBlockedCellsExact(config, blockedTarget, attemptRng, triMode: true);
                    if (scatteredBlocked.Count != blockedTarget)
                        continue;

                    List<Vector2Int> scatteredPath = HamiltonianPath(
                        config.width,
                        config.height,
                        scatteredBlocked,
                        attemptRng,
                        triMode: true,
                        maxAttempts: blockedTarget >= 4 ? 420 : 320);
                    if (scatteredPath == null)
                        continue;

                    List<List<Vector2Int>> scatteredSegments = SplitPath(scatteredPath, config, attemptRng);
                    if (scatteredSegments == null || scatteredSegments.Count == 0)
                        continue;

                    var scatteredBlockedList = new List<Vector2Int>(scatteredBlocked);
                    float scatteredScore = ComputeTriangleBlockedScatterScore(scatteredBlockedList, config)
                        + ComputeTriangleBlockedAnchorScore(scatteredBlockedList, config, levelIndex)
                        + blockedTarget * 2.5f;
                    if (bestScatteredCandidate == null || scatteredScore > bestScatteredCandidate.score)
                    {
                        bestScatteredCandidate = new LevelCandidate
                        {
                            path = scatteredPath,
                            segments = scatteredSegments,
                            signature = BuildSignature(config, scatteredSegments),
                            contentFingerprint = BuildContentFingerprint(config, scatteredSegments, scatteredBlockedList),
                            score = scatteredScore,
                            blocked = scatteredBlockedList
                        };
                    }
                }
            }

            if (bestScatteredCandidate != null)
                return bestScatteredCandidate;

            LevelCandidate distributedFallbackCandidate = BuildTriangleBundledCandidate(levelIndex, config);
            if (distributedFallbackCandidate != null)
                return distributedFallbackCandidate;

            if (!TryBuildInteriorTriangleFallback(fullPath, levelIndex, config, rng, desiredBlocked, out playablePath, out blocked))
            {
                blocked = new List<Vector2Int>();
                int frontBlocked = desiredBlocked / 2;
                int backBlocked = desiredBlocked - frontBlocked;

                if ((levelIndex & 1) == 0)
                {
                    int temp = frontBlocked;
                    frontBlocked = backBlocked;
                    backBlocked = temp;
                }

                blocked.AddRange(fullPath.GetRange(0, frontBlocked));
                if (backBlocked > 0)
                    blocked.AddRange(fullPath.GetRange(fullPath.Count - backBlocked, backBlocked));

                playablePath = fullPath.GetRange(frontBlocked, fullPath.Count - frontBlocked - backBlocked);
            }
        }

        List<List<Vector2Int>> fallbackSegs = SplitPath(playablePath, config, rng)
            ?? UniformSplit(playablePath, config.minSegment);

        return new LevelCandidate
        {
            path = playablePath,
            segments = fallbackSegs,
            signature = BuildSignature(config, fallbackSegs),
            contentFingerprint = BuildContentFingerprint(config, fallbackSegs, blocked),
            score = 0f,
            blocked = blocked
        };
    }

    private static CampaignConfig GetThreeGenConfig(int idx)
    {
        CampaignConfig c = new CampaignConfig();

        if (idx < 20)          // Tier 1: 7×6  Intro   (901-920)
        {
            c.width = 7; c.height = 6;
            c.minSegment = 2; c.maxSegment = 6; c.candidateCount = 26;
            c.tierName = "3gen Intro";
            c.rectanglePenalty = 3.5f; c.densePenalty = 2.5f;
            c.straightPenalty = 2.0f; c.turnWeight = 1.2f;
            c.squarePenalty = 1.5f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 45)     // Tier 2: 9×6  Easy    (921-945)
        {
            c.width = 9; c.height = 6;
            c.minSegment = 2; c.maxSegment = 7; c.candidateCount = 28;
            c.tierName = "3gen Easy";
            c.rectanglePenalty = 3.2f; c.densePenalty = 2.3f;
            c.straightPenalty = 1.9f; c.turnWeight = 1.15f;
            c.squarePenalty = 1.3f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 75)     // Tier 3: 9×8  Easy+   (946-975)
        {
            c.width = 9; c.height = 8;
            c.minSegment = 3; c.maxSegment = 7; c.candidateCount = 30;
            c.tierName = "3gen Easy";
            c.rectanglePenalty = 3.0f; c.densePenalty = 2.1f;
            c.straightPenalty = 1.8f; c.turnWeight = 1.1f;
            c.squarePenalty = 1.1f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 100)    // Tier 4: 11×8  Normal  (976-1000)
        {
            c.width = 11; c.height = 8;
            c.minSegment = 3; c.maxSegment = 8; c.candidateCount = 32;
            c.tierName = "3gen Normal";
            c.rectanglePenalty = 2.6f; c.densePenalty = 1.9f;
            c.straightPenalty = 1.6f; c.turnWeight = 1.05f;
            c.squarePenalty = 0.9f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 135)    // Tier 5: 11×8  Normal+ (1001-1035)
        {
            c.width = 11; c.height = 8;
            c.minSegment = 3; c.maxSegment = 9; c.candidateCount = 34;
            c.tierName = "3gen Normal";
            c.rectanglePenalty = 2.2f; c.densePenalty = 1.7f;
            c.straightPenalty = 1.4f; c.turnWeight = 1.0f;
            c.squarePenalty = 0.75f; c.lateRectangleBonus = 0f;
            c.minBlocked = 1; c.maxBlocked = 2;
        }
        else if (idx < 170)    // Tier 6: 13×8  Hard    (1036-1070)
        {
            c.width = 13; c.height = 8;
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 36;
            c.tierName = "3gen Hard";
            c.rectanglePenalty = 1.9f; c.densePenalty = 1.5f;
            c.straightPenalty = 1.2f; c.turnWeight = 0.95f;
            c.squarePenalty = 0.65f; c.lateRectangleBonus = 0.05f;
            c.minBlocked = 2; c.maxBlocked = 3;
        }
        else if (idx < 210)    // Tier 7: 13×10 Hard+   (1071-1110)
        {
            c.width = 13; c.height = 10;
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 38;
            c.tierName = "3gen Hard";
            c.rectanglePenalty = 1.6f; c.densePenalty = 1.3f;
            c.straightPenalty = 1.1f; c.turnWeight = 0.9f;
            c.squarePenalty = 0.55f; c.lateRectangleBonus = 0.1f;
            c.minBlocked = 3; c.maxBlocked = 4;
        }
        else if (idx < 250)    // Tier 8: 15×10 Advanced (1111-1150)
        {
            c.width = 15; c.height = 10;
            c.minSegment = 4; c.maxSegment = 11; c.candidateCount = 40;
            c.tierName = "3gen Advanced";
            c.rectanglePenalty = 1.3f; c.densePenalty = 1.0f;
            c.straightPenalty = 0.95f; c.turnWeight = 0.85f;
            c.squarePenalty = 0.45f; c.lateRectangleBonus = 0.15f;
            c.minBlocked = 3; c.maxBlocked = 4;
        }
        else if (idx < 275)    // Tier 9: 15×10 Expert  (1151-1175)
        {
            c.width = 15; c.height = 10;
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 42;
            c.tierName = "3gen Expert";
            c.rectanglePenalty = 1.0f; c.densePenalty = 0.8f;
            c.straightPenalty = 0.85f; c.turnWeight = 0.78f;
            c.squarePenalty = 0.35f; c.lateRectangleBonus = 0.2f;
            c.minBlocked = 3; c.maxBlocked = 4;
        }
        else                   // Tier 10: 15×12 Master (1176-1200)
        {
            c.width = 15; c.height = 12;
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 44;
            c.tierName = "3gen Master";
            c.rectanglePenalty = 0.8f; c.densePenalty = 0.6f;
            c.straightPenalty = 0.75f; c.turnWeight = 0.7f;
            c.squarePenalty = 0.25f; c.lateRectangleBonus = 0.3f;
            c.minBlocked = 3; c.maxBlocked = 4;
        }

        return c;
    }
}
