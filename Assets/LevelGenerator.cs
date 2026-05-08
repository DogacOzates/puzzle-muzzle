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
        // Tier-local value-set balancing: prevents same number combos dominating a tier
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        // Recent region fingerprints: prevents endpoints always clustering in same bands
        var recentRegionSets = new Queue<string>();

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

    private static List<Vector2Int> HamiltonianPath(int width, int height, HashSet<Vector2Int> blocked, System.Random rng, bool hexMode = false, bool colHexMode = false)
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
                List<Vector2Int> neighbors = colHexMode
                    ? HexColNeighbors(current.x, current.y, width, height, visited)
                    : (hexMode
                        ? HexNeighbors(current.x, current.y, width, height, visited)
                        : Neighbors(current.x, current.y, width, height, visited));
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
                    int onwardMoves = colHexMode
                        ? HexColNeighbors(next.x, next.y, width, height, visited).Count
                        : (hexMode
                            ? HexNeighbors(next.x, next.y, width, height, visited).Count
                            : Neighbors(next.x, next.y, width, height, visited).Count);
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
                    score += (colHexMode
                        ? CountColHexFutureDeadEnds(next, width, height, visited)
                        : (hexMode
                            ? CountHexFutureDeadEnds(next, width, height, visited)
                            : CountFutureDeadEnds(next, width, height, visited))) * 1.3f;
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

    private static HashSet<Vector2Int> GenerateBlockedCells(CampaignConfig config, System.Random rng, bool hexMode = false, bool colHexMode = false)
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
            if (!IsGridConnected(config.width, config.height, blocked, hexMode, colHexMode))
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

    private static bool IsGridConnected(int width, int height, HashSet<Vector2Int> blocked, bool hexMode = false, bool colHexMode = false)
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

    public static LevelData[] GeneratePentagonCampaign(int count)
    {
        var levels = new LevelData[count];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        var recentRegionSets = new Queue<string>();

        for (int i = 0; i < count; i++)
        {
            var rng = new System.Random((i + 500) * 7919 + 11);
            CampaignConfig config = GetPentagonConfig(i);
            LevelCandidate bestCandidate = null;

            for (int ci = 0; ci < config.candidateCount; ci++)
            {
                var candidateRng = new System.Random(rng.Next());
                HashSet<Vector2Int> blocked = GenerateBlockedCells(config, candidateRng, hexMode: true);
                List<Vector2Int> path = HamiltonianPath(config.width, config.height, blocked, candidateRng, hexMode: true);
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
            int extraAttempts = config.maxBlocked > 0 ? 100 : 60;
            if (allFingerprints.Contains(bestCandidate.contentFingerprint))
            {
                for (int extra = 0; extra < extraAttempts; extra++)
                {
                    var xRng = new System.Random((i + 500) * 7919 + 11 + (extra + 1) * 1013);
                    HashSet<Vector2Int> xBlocked = GenerateBlockedCells(config, xRng, hexMode: true);
                    List<Vector2Int> xPath = HamiltonianPath(config.width, config.height, xBlocked, xRng, hexMode: true);
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
            levels[i] = ld;

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
            c.minBlocked = 3; c.maxBlocked = 4;
        }
        else if (idx < 275)    // Tier 8: 6×11, Expert (546-575)
        {
            SetRectangularBoard(ref c, 6, 11);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 30;
            c.tierName = "Hex Expert";
            c.rectanglePenalty = 1.0f; c.densePenalty = 0.75f;
            c.straightPenalty = 0.85f; c.turnWeight = 0.77f;
            c.squarePenalty = 0.3f; c.lateRectangleBonus = 0.2f;
            c.minBlocked = 4; c.maxBlocked = 5;
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
    {
        var levels = new LevelData[count];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        var recentRegionSets = new Queue<string>();

        for (int i = 0; i < count; i++)
        {
            var rng = new System.Random((i + 1000) * 7919 + 17);
            CampaignConfig config = GetHexagonConfig(i);
            LevelCandidate bestCandidate = null;

            for (int ci = 0; ci < config.candidateCount; ci++)
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

            int extraAttempts = config.maxBlocked > 0 ? 100 : 60;
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
            levels[i] = ld;

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

    // --- 7gen campaign (flat-top column-offset hex with circle cells) ---

    public static LevelData[] GenerateSevenGenCampaign(int count)
    {
        var levels = new LevelData[count];
        var recentSignatures = new Queue<string>();
        var usedSignatures = new Dictionary<string, int>();
        var allFingerprints = new HashSet<string>();
        var usedValueSetsByTier = new Dictionary<string, Dictionary<string, int>>();
        var recentRegionSets = new Queue<string>();

        for (int i = 0; i < count; i++)
        {
            var rng = new System.Random((i + 2000) * 9001 + 31);
            CampaignConfig config = GetSevenGenConfig(i);
            LevelCandidate bestCandidate = null;

            for (int ci = 0; ci < config.candidateCount; ci++)
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

            int extraAttempts = config.maxBlocked > 0 ? 100 : 60;
            if (allFingerprints.Contains(bestCandidate.contentFingerprint))
            {
                for (int extra = 0; extra < extraAttempts; extra++)
                {
                    var xRng = new System.Random((i + 2000) * 9001 + 31 + (extra + 1) * 1013);
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
            ld.cellShape = CellShape.SevenGen;
            levels[i] = ld;

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

    private static CampaignConfig GetSevenGenConfig(int idx)
    {
        CampaignConfig c = new CampaignConfig();

        if (idx < 20)          // Tier 1:  4×4  Intro   (901-920)
        {
            c.width = 4; c.height = 4;
            c.minSegment = 2; c.maxSegment = 6; c.candidateCount = 20;
            c.tierName = "7gen Intro";
            c.rectanglePenalty = 3.5f; c.densePenalty = 2.5f;
            c.straightPenalty = 2.0f; c.turnWeight = 1.2f;
            c.squarePenalty = 1.5f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 45)     // Tier 2:  4×5  Easy    (921-945)
        {
            SetRectangularBoard(ref c, 4, 5);
            c.minSegment = 2; c.maxSegment = 7; c.candidateCount = 22;
            c.tierName = "7gen Easy";
            c.rectanglePenalty = 3.2f; c.densePenalty = 2.3f;
            c.straightPenalty = 1.9f; c.turnWeight = 1.15f;
            c.squarePenalty = 1.3f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 75)     // Tier 3:  5×5  Easy+   (946-975)
        {
            c.width = 5; c.height = 5;
            c.minSegment = 3; c.maxSegment = 7; c.candidateCount = 24;
            c.tierName = "7gen Easy";
            c.rectanglePenalty = 3.0f; c.densePenalty = 2.1f;
            c.straightPenalty = 1.8f; c.turnWeight = 1.1f;
            c.squarePenalty = 1.1f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 0;
        }
        else if (idx < 105)    // Tier 4:  5×6  Normal  (976-1005)
        {
            SetRectangularBoard(ref c, 5, 6);
            c.minSegment = 3; c.maxSegment = 8; c.candidateCount = 24;
            c.tierName = "7gen Normal";
            c.rectanglePenalty = 2.6f; c.densePenalty = 1.9f;
            c.straightPenalty = 1.6f; c.turnWeight = 1.05f;
            c.squarePenalty = 0.9f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 1;
        }
        else if (idx < 140)    // Tier 5:  5×7  Normal+ (1006-1040)
        {
            SetRectangularBoard(ref c, 5, 7);
            c.minSegment = 3; c.maxSegment = 9; c.candidateCount = 26;
            c.tierName = "7gen Normal";
            c.rectanglePenalty = 2.2f; c.densePenalty = 1.7f;
            c.straightPenalty = 1.4f; c.turnWeight = 1.0f;
            c.squarePenalty = 0.75f; c.lateRectangleBonus = 0f;
            c.minBlocked = 0; c.maxBlocked = 2;
        }
        else if (idx < 175)    // Tier 6:  6×7  Hard    (1041-1075)
        {
            SetRectangularBoard(ref c, 6, 7);
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 26;
            c.tierName = "7gen Hard";
            c.rectanglePenalty = 1.9f; c.densePenalty = 1.5f;
            c.straightPenalty = 1.2f; c.turnWeight = 0.95f;
            c.squarePenalty = 0.65f; c.lateRectangleBonus = 0.05f;
            c.minBlocked = 1; c.maxBlocked = 3;
        }
        else if (idx < 215)    // Tier 7:  6×8  Hard+   (1076-1115)
        {
            SetRectangularBoard(ref c, 6, 8);
            c.minSegment = 4; c.maxSegment = 10; c.candidateCount = 28;
            c.tierName = "7gen Hard";
            c.rectanglePenalty = 1.6f; c.densePenalty = 1.3f;
            c.straightPenalty = 1.1f; c.turnWeight = 0.9f;
            c.squarePenalty = 0.55f; c.lateRectangleBonus = 0.1f;
            c.minBlocked = 2; c.maxBlocked = 4;
        }
        else if (idx < 255)    // Tier 8:  7×8  Advanced(1116-1155)
        {
            SetRectangularBoard(ref c, 7, 8);
            c.minSegment = 4; c.maxSegment = 11; c.candidateCount = 28;
            c.tierName = "7gen Advanced";
            c.rectanglePenalty = 1.3f; c.densePenalty = 1.0f;
            c.straightPenalty = 0.95f; c.turnWeight = 0.85f;
            c.squarePenalty = 0.45f; c.lateRectangleBonus = 0.15f;
            c.minBlocked = 3; c.maxBlocked = 5;
        }
        else if (idx < 275)    // Tier 9:  7×9  Expert  (1156-1175)
        {
            SetRectangularBoard(ref c, 7, 9);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 30;
            c.tierName = "7gen Expert";
            c.rectanglePenalty = 1.0f; c.densePenalty = 0.8f;
            c.straightPenalty = 0.85f; c.turnWeight = 0.78f;
            c.squarePenalty = 0.35f; c.lateRectangleBonus = 0.2f;
            c.minBlocked = 4; c.maxBlocked = 6;
        }
        else                   // Tier 10: 7×10 Master  (1176-1200)
        {
            SetRectangularBoard(ref c, 7, 10);
            c.minSegment = 5; c.maxSegment = 12; c.candidateCount = 32;
            c.tierName = "7gen Master";
            c.rectanglePenalty = 0.8f; c.densePenalty = 0.6f;
            c.straightPenalty = 0.75f; c.turnWeight = 0.7f;
            c.squarePenalty = 0.25f; c.lateRectangleBonus = 0.3f;
            c.minBlocked = 5; c.maxBlocked = 7;
        }

        return c;
    }
}
