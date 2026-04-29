using System;

public enum CellShape { Square, Pentagon, Hexagon }

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
    public const int TotalLevels = 610;

    private static LevelData[] _levels;

    public static LevelData[] Levels
    {
        get
        {
            if (_levels == null)
            {
                _levels = new LevelData[TotalLevels];
                _levels[0] = TutorialLevel();
                _levels[1] = SecondLevel();
                LevelData[] generatedLevels = LevelGenerator.GenerateCampaign(298);
                for (int i = 2; i < 300; i++)
                    _levels[i] = generatedLevels[i - 2];
                LevelData[] pentagonLevels = LevelGenerator.GeneratePentagonCampaign(300);
                for (int i = 0; i < 300; i++)
                    _levels[300 + i] = pentagonLevels[i];
                LevelData[] hexagonLevels = LevelGenerator.GenerateHexagonCampaign(10);
                for (int i = 0; i < 10; i++)
                    _levels[600 + i] = hexagonLevels[i];
            }
            return _levels;
        }
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
