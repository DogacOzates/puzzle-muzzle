using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public enum CellShape { Square, Pentagon, Hexagon, ThreeGen }

[Serializable]
public class BlockedCellData
{
    public int x;
    public int y;

    public BlockedCellData(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

[Serializable]
public class NumberCellData
{
    public int x;
    public int y;
    public int value;

    public NumberCellData(int x, int y, int value)
    {
        this.x = x;
        this.y = y;
        this.value = value;
    }
}

// Stores a solution path as flat coordinate pairs: x0,y0, x1,y1, ...
// Last pair is always the target cell.
[Serializable]
public class SolutionPath
{
    public int[] coords;

    public SolutionPath(params int[] coords)
    {
        this.coords = coords;
    }

    public int Length => coords.Length / 2;
    public int GetX(int i) => coords[i * 2];
    public int GetY(int i) => coords[i * 2 + 1];
}

[Serializable]
public class LevelData
{
    public string levelName;
    public int gridWidth;
    public int gridHeight;
    public NumberCellData[] numberCells;
    public SolutionPath[] solutions;
    public BlockedCellData[] blockedCells;
    public CellShape cellShape = CellShape.Square;

    public LevelData(string name, int width, int height, NumberCellData[] cells, SolutionPath[] solutions, BlockedCellData[] blockedCells = null)
    {
        levelName = name;
        gridWidth = width;
        gridHeight = height;
        numberCells = cells;
        this.solutions = solutions;
        this.blockedCells = blockedCells ?? new BlockedCellData[0];
    }
}

public static class LevelDatabase
{
    public const int TotalLevels = 900;
    private const int CampaignLoadBatchSize = 1;
    private const int GeneratedLevelCacheVersion = 2;
    private const string BundledLevelsResourcePath = "generated_levels";

    private struct LevelBatchLoad
    {
        public int absoluteStart;
        public LevelData[] levels;
    }

    [Serializable]
    private class CachedLevelPayload
    {
        public LevelData level;
    }

    [Serializable]
    private class BundledLevelsPayload
    {
        public LevelData[] levels;
    }

    private static LevelData[] _levels;
    private static readonly object _sync = new object();
    private static readonly Dictionary<int, Task> _prefetchTasks = new Dictionary<int, Task>();
    private static string CacheDirectoryPath => Path.Combine(Application.persistentDataPath, $"generated-level-cache-v{GeneratedLevelCacheVersion}");
    private static bool _usingBundledLevels;

    // Call this whenever generation logic changes to flush the in-memory cache.
    public static void InvalidateCache()
    {
        lock (_sync)
        {
            _levels = null;
            _prefetchTasks.Clear();
            _usingBundledLevels = false;
        }
    }

    public static LevelData GetLevel(int index)
    {
        if (index < 0 || index >= TotalLevels)
            return null;

        EnsureInitialized();
        Task pendingTask = null;
        LevelData readyLevel = null;

        lock (_sync)
        {
            if (_levels[index] != null)
                readyLevel = _levels[index];

            if (readyLevel == null)
                _prefetchTasks.TryGetValue(index, out pendingTask);
        }

        if (readyLevel != null)
        {
            if (!_usingBundledLevels)
                PersistLevelIfNeeded(index, readyLevel);
            return readyLevel;
        }

        if (pendingTask != null)
        {
            pendingTask.Wait();
            lock (_sync)
                readyLevel = _levels[index];

            if (!_usingBundledLevels)
                PersistLevelIfNeeded(index, readyLevel);
            return readyLevel;
        }

        if (TryLoadCachedLevel(index, out LevelData cachedLevel))
        {
            lock (_sync)
            {
                _levels[index] = cachedLevel;
                return _levels[index];
            }
        }

        LevelBatchLoad batch = GenerateCampaignBatch(index);
        PersistBatch(batch);
        return StoreBatchAndGetLevel(batch, index);
    }

    public static void PrefetchLevel(int index)
    {
        if (index < 0 || index >= TotalLevels)
            return;

        EnsureInitialized();

        lock (_sync)
        {
            if (_usingBundledLevels || _levels[index] != null || _prefetchTasks.ContainsKey(index))
                return;

            _prefetchTasks[index] = Task.Run(() =>
            {
                try
                {
                    if (TryLoadCachedLevel(index, out LevelData cachedLevel))
                    {
                        StoreBatch(new LevelBatchLoad
                        {
                            absoluteStart = index,
                            levels = new[] { cachedLevel }
                        });
                        return;
                    }

                    LevelBatchLoad batch = GenerateCampaignBatch(index);
                    StoreBatch(batch);
                }
                finally
                {
                    lock (_sync)
                        _prefetchTasks.Remove(index);
                }
            });
        }
    }

    public static LevelData[] Levels
    {
        get
        {
            EnsureInitialized();
            EnsureAllLoaded();
            return _levels;
        }
    }

    private static void EnsureInitialized()
    {
        lock (_sync)
        {
            if (_levels != null) return;

            _levels = new LevelData[TotalLevels];
            _levels[0] = PreTutorialLevel();
            _levels[1] = TutorialLevel();
            _levels[2] = SecondLevel();
            _prefetchTasks.Clear();
            _usingBundledLevels = false;

            if (TryLoadBundledLevels(out LevelData[] bundledLevels))
            {
                _levels = bundledLevels;
                _usingBundledLevels = true;
            }
        }
    }

    private static LevelBatchLoad GenerateCampaignBatch(int index)
    {
        if (index < 3)
        {
            return new LevelBatchLoad
            {
                absoluteStart = index,
                levels = new[] { _levels[index] }
            };
        }

        if (index < 300)
        {
            int relativeIndex = index - 3;
            int batchStart = (relativeIndex / CampaignLoadBatchSize) * CampaignLoadBatchSize;
            int batchCount = Math.Min(CampaignLoadBatchSize, 297 - batchStart);
            int absoluteStart = 3 + batchStart;
            return new LevelBatchLoad
            {
                absoluteStart = absoluteStart,
                levels = LevelGenerator.GenerateCampaign(batchStart, batchCount)
            };
        }

        if (index < 600)
        {
            int relativeIndex = index - 300;
            int batchStart = (relativeIndex / CampaignLoadBatchSize) * CampaignLoadBatchSize;
            int batchCount = Math.Min(CampaignLoadBatchSize, 300 - batchStart);
            int absoluteStart = 300 + batchStart;
            return new LevelBatchLoad
            {
                absoluteStart = absoluteStart,
                levels = LevelGenerator.GenerateHexagonCampaign(batchStart, batchCount)
            };
        }

        int triRelativeIndex = index - 600;
        int triBatchStart = (triRelativeIndex / CampaignLoadBatchSize) * CampaignLoadBatchSize;
        int triBatchCount = Math.Min(CampaignLoadBatchSize, 300 - triBatchStart);
        int triAbsoluteStart = 600 + triBatchStart;
        return new LevelBatchLoad
        {
            absoluteStart = triAbsoluteStart,
            levels = LevelGenerator.GenerateThreeGenCampaign(triBatchStart, triBatchCount)
        };
    }

    private static void StoreBatch(LevelBatchLoad batch)
    {
        lock (_sync)
        {
            if (_levels == null) return;

            for (int i = 0; i < batch.levels.Length; i++)
            {
                int absoluteIndex = batch.absoluteStart + i;
                if (_levels[absoluteIndex] == null)
                    _levels[absoluteIndex] = batch.levels[i];
            }
        }
    }

    private static LevelData StoreBatchAndGetLevel(LevelBatchLoad batch, int requestedIndex)
    {
        lock (_sync)
        {
            if (_levels == null)
                EnsureInitialized();

            for (int i = 0; i < batch.levels.Length; i++)
            {
                int absoluteIndex = batch.absoluteStart + i;
                if (_levels[absoluteIndex] == null)
                    _levels[absoluteIndex] = batch.levels[i];
            }

            return _levels[requestedIndex];
        }
    }

    private static void EnsureAllLoaded()
    {
        lock (_sync)
        {
            if (_usingBundledLevels)
                return;

            if (_levels[3] == null || _levels[299] == null)
            {
                LevelData[] generatedLevels = LevelGenerator.GenerateCampaign(297);
                for (int i = 3; i < 300; i++)
                    _levels[i] = generatedLevels[i - 3];
            }

            if (_levels[300] == null || _levels[599] == null)
            {
                LevelData[] hexagonLevels = LevelGenerator.GenerateHexagonCampaign(300);
                for (int i = 0; i < 300; i++)
                    _levels[300 + i] = hexagonLevels[i];
            }

            if (_levels[600] == null || _levels[899] == null)
            {
                LevelData[] threeGenLevels = LevelGenerator.GenerateThreeGenCampaign(300);
                for (int i = 0; i < 300; i++)
                    _levels[600 + i] = threeGenLevels[i];
            }
        }
    }

    public static string BuildBundledLevelsJson(bool prettyPrint = false)
    {
        var payload = new BundledLevelsPayload
        {
            levels = BuildAllLevelsProcedurally()
        };
        return JsonUtility.ToJson(payload, prettyPrint);
    }

    private static LevelData[] BuildAllLevelsProcedurally()
    {
        bool previousBundledBuildMode = LevelGenerator.UseBundledBuildOptimizations;
        LevelGenerator.UseBundledBuildOptimizations = true;
        try
        {
            var levels = new LevelData[TotalLevels];
            levels[0] = PreTutorialLevel();
            levels[1] = TutorialLevel();
            levels[2] = SecondLevel();

            const int bundleChunkSize = 25;

            Parallel.For(0, Mathf.CeilToInt(297f / bundleChunkSize), chunkIndex =>
            {
                int start = chunkIndex * bundleChunkSize;
                int count = Math.Min(bundleChunkSize, 297 - start);
                LevelData[] chunk = LevelGenerator.GenerateCampaign(start, count);
                for (int i = 0; i < count; i++)
                    levels[3 + start + i] = chunk[i];
            });

            Parallel.For(0, Mathf.CeilToInt(300f / bundleChunkSize), chunkIndex =>
            {
                int start = chunkIndex * bundleChunkSize;
                int count = Math.Min(bundleChunkSize, 300 - start);
                LevelData[] chunk = LevelGenerator.GenerateHexagonCampaign(start, count);
                for (int i = 0; i < count; i++)
                    levels[300 + start + i] = chunk[i];
            });

            Parallel.For(0, Mathf.CeilToInt(300f / bundleChunkSize), chunkIndex =>
            {
                int start = chunkIndex * bundleChunkSize;
                int count = Math.Min(bundleChunkSize, 300 - start);
                LevelData[] chunk = LevelGenerator.GenerateThreeGenCampaign(start, count);
                for (int i = 0; i < count; i++)
                    levels[600 + start + i] = chunk[i];
            });

            return levels;
        }
        finally
        {
            LevelGenerator.UseBundledBuildOptimizations = previousBundledBuildMode;
        }
    }

    private static bool TryLoadBundledLevels(out LevelData[] levels)
    {
        levels = null;

        TextAsset bundledAsset = Resources.Load<TextAsset>(BundledLevelsResourcePath);
        if (bundledAsset == null || string.IsNullOrEmpty(bundledAsset.text))
            return false;

        BundledLevelsPayload payload = JsonUtility.FromJson<BundledLevelsPayload>(bundledAsset.text);
        if (payload == null || payload.levels == null || payload.levels.Length != TotalLevels)
            return false;

        if (payload.levels[0] == null) payload.levels[0] = PreTutorialLevel();
        if (payload.levels[1] == null) payload.levels[1] = TutorialLevel();
        if (payload.levels[2] == null) payload.levels[2] = SecondLevel();

        levels = payload.levels;
        return true;
    }

    private static bool TryLoadCachedLevel(int index, out LevelData level)
    {
        level = null;
        if (index < 3)
            return false;

        string filePath = GetCacheFilePath(index);
        if (!File.Exists(filePath))
            return false;

        string json = File.ReadAllText(filePath);
        if (string.IsNullOrEmpty(json))
            return false;

        CachedLevelPayload payload = JsonUtility.FromJson<CachedLevelPayload>(json);
        if (payload == null || payload.level == null)
            return false;

        level = payload.level;
        return true;
    }

    private static void PersistBatch(LevelBatchLoad batch)
    {
        if (batch.levels == null || batch.levels.Length == 0)
            return;

        Directory.CreateDirectory(CacheDirectoryPath);
        for (int i = 0; i < batch.levels.Length; i++)
        {
            int absoluteIndex = batch.absoluteStart + i;
            PersistLevelIfNeeded(absoluteIndex, batch.levels[i]);
        }
    }

    private static string GetCacheFilePath(int index)
        => Path.Combine(CacheDirectoryPath, $"level-{index:D4}.json");

    private static void PersistLevelIfNeeded(int index, LevelData level)
    {
        if (index < 3 || level == null)
            return;

        string filePath = GetCacheFilePath(index);
        if (File.Exists(filePath))
            return;

        Directory.CreateDirectory(CacheDirectoryPath);
        CachedLevelPayload payload = new CachedLevelPayload { level = level };
        File.WriteAllText(filePath, JsonUtility.ToJson(payload));
    }

    static LevelData PreTutorialLevel()
    {
        // 4 cells in a horizontal row; rightmost cell is the number "4"
        return new LevelData("Intro", 4, 1, new NumberCellData[]
        {
            new NumberCellData(3, 0, 4),
        },
        new SolutionPath[]
        {
            new SolutionPath(0,0, 1,0, 2,0, 3,0),
        });
    }

    static LevelData TutorialLevel()
    {
        return new LevelData("Tutorial", 3, 3, new NumberCellData[]
        {
            new NumberCellData(1, 0, 2),
            new NumberCellData(0, 1, 4),
            new NumberCellData(2, 2, 3),
        },
        new SolutionPath[]
        {
            new SolutionPath(0,0, 1,0),
            new SolutionPath(2,0, 2,1, 1,1, 0,1),
            new SolutionPath(0,2, 1,2, 2,2),
        });
    }

    static LevelData SecondLevel()
    {
        return new LevelData("Easy", 4, 4, new NumberCellData[]
        {
            new NumberCellData(3, 0, 3),
            new NumberCellData(1, 1, 4),
            new NumberCellData(2, 2, 5),
            new NumberCellData(0, 3, 4),
        },
        new SolutionPath[]
        {
            new SolutionPath(1,0, 2,0, 3,0),
            new SolutionPath(3,2, 3,1, 2,1, 1,1),
            new SolutionPath(3,3, 2,3, 1,3, 1,2, 2,2),
            new SolutionPath(0,0, 0,1, 0,2, 0,3),
        });
    }
}
