using System;

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
    public const int TotalLevels = 1200;

    private static LevelData[] _levels;

    // Call this whenever generation logic changes to flush the in-memory cache.
    public static void InvalidateCache() => _levels = null;

    public static LevelData GetLevel(int index)
    {
        if (index < 0 || index >= TotalLevels)
            return null;

        EnsureInitialized();
        if (_levels[index] == null)
            EnsureCampaignLoaded(index);
        return _levels[index];
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
        if (_levels != null) return;

        _levels = new LevelData[TotalLevels];
        _levels[0] = PreTutorialLevel();
        _levels[1] = TutorialLevel();
        _levels[2] = SecondLevel();
    }

    private static void EnsureCampaignLoaded(int index)
    {
        if (index < 3) return;

        if (index < 300)
        {
            if (_levels[3] != null) return;
            LevelData[] generatedLevels = LevelGenerator.GenerateCampaign(297);
            for (int i = 3; i < 300; i++)
                _levels[i] = generatedLevels[i - 3];
        }
        else if (index < 600)
        {
            if (_levels[300] != null) return;
            LevelData[] pentagonLevels = LevelGenerator.GeneratePentagonCampaign(300);
            for (int i = 0; i < 300; i++)
                _levels[300 + i] = pentagonLevels[i];
        }
        else if (index < 900)
        {
            if (_levels[600] != null) return;
            LevelData[] hexagonLevels = LevelGenerator.GenerateHexagonCampaign(300);
            for (int i = 0; i < 300; i++)
                _levels[600 + i] = hexagonLevels[i];
        }
        else
        {
            if (_levels[900] != null) return;
            LevelData[] threeGenLevels = LevelGenerator.GenerateThreeGenCampaign(300);
            for (int i = 0; i < 300; i++)
                _levels[900 + i] = threeGenLevels[i];
            _levels[1090] = ThreeGenBlockedShowcaseLevel();
        }
    }

    private static void EnsureAllLoaded()
    {
        EnsureCampaignLoaded(3);
        EnsureCampaignLoaded(300);
        EnsureCampaignLoaded(600);
        EnsureCampaignLoaded(900);
    }

    static LevelData ThreeGenBlockedShowcaseLevel()
    {
        var level = new LevelData("3gen Blocked", 8, 5, new NumberCellData[]
        {
            new NumberCellData(5, 0, 3),
            new NumberCellData(4, 1, 9),
            new NumberCellData(4, 2, 6),
            new NumberCellData(5, 3, 9),
            new NumberCellData(0, 4, 9),
        },
        new SolutionPath[]
        {
            new SolutionPath(7,0, 6,0, 5,0),
            new SolutionPath(3,0, 2,0, 1,0, 0,0, 0,1, 1,1, 2,1, 3,1, 4,1),
            new SolutionPath(6,1, 7,1, 7,2, 6,2, 5,2, 4,2),
            new SolutionPath(2,2, 1,2, 0,2, 0,3, 1,3, 2,3, 3,3, 4,3, 5,3),
            new SolutionPath(7,3, 7,4, 6,4, 5,4, 4,4, 3,4, 2,4, 1,4, 0,4),
        },
        new BlockedCellData[]
        {
            new BlockedCellData(4, 0),
            new BlockedCellData(5, 1),
            new BlockedCellData(3, 2),
            new BlockedCellData(6, 3),
        });
        level.cellShape = CellShape.ThreeGen;
        return level;
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
