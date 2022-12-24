using System;
using System.Collections.Generic;
using System.Linq;

namespace FallChallenge2022;

public readonly record struct Map(int Width, int Height)
{
    public readonly Point Center = new(Width / 2, Height / 2);
    public bool Big => Width >= 18;

    public IEnumerable<Point> Directions(Point point, bool withPoint = false)
    {
        if (withPoint)
        {
            yield return point;
        }

        // Right
        if (point.X + 1 < Width)
        {
            yield return new Point(point.X + 1, point.Y);
        }

        // Down
        if (point.Y + 1 < Height)
        {
            yield return new Point(point.X, point.Y + 1);
        }

        // Left
        if (point.X - 1 >= 0)
        {
            yield return new Point(point.X - 1, point.Y);
        }

        // Up
        if (point.Y - 1 >= 0)
        {
            yield return new Point(point.X, point.Y - 1);
        }
    }

    public static Point CenterRange(List<Point> points)
    {
        int x = 0;
        int y = 0;

        foreach (var point in points)
        {
            x += point.X;
            y += point.Y;
        }

        x /= points.Count;
        y /= points.Count;

        return new Point(x, y);
    }
}

public readonly record struct Point(int X, int Y)
{
    public int ManhattanTo(Point other) => Math.Abs(other.X - X) + Math.Abs(other.Y - Y);

    public override string ToString() => $"{X} {Y}";
}

public class Tile
{
    public readonly Point Point;
    public readonly int ScrapAmount;
    public readonly int Owner;
    public int Units;

    public bool Recycler;
    public readonly bool CanBuild;
    public readonly bool CanSpawn;
    public readonly bool InRangeOfRecycler;

    public int MyForceScore;

    public Tile(Point point, int scrapAmount, int owner, int units, bool recycler, bool canBuild, bool canSpawn, bool inRangeOfRecycler)
    {
        Point = point;
        ScrapAmount = scrapAmount;
        Owner = owner;
        Units = units;
        Recycler = recycler;
        CanBuild = canBuild;
        CanSpawn = canSpawn;
        InRangeOfRecycler = inRangeOfRecycler;
    }

    public bool IsHole => Recycler || ScrapAmount == 0;

    public bool TurnToHole => InRangeOfRecycler && ScrapAmount == 1;
}

internal static class Player
{
    static readonly int ME = 1;
    static readonly int OPP = 0;
    static readonly int NOONE = -1;

    static readonly Dictionary<Point, Tile> Tiles = new();
    static readonly Dictionary<Point, Tile> myTiles = new();
    static readonly Dictionary<Point, Tile> oppTiles = new();
    static readonly Dictionary<Point, Tile> neutralTiles = new();
    static readonly Dictionary<Point, Tile> oppWithNeutralTiles = new();
    static readonly Dictionary<Point, Tile> myUnits = new();
    static readonly Dictionary<Point, Tile> oppUnits = new();
    static readonly Dictionary<Point, Tile> myRecyclers = new();
    static readonly HashSet<Point> myRecyclersRange = new();
    static readonly Dictionary<Point, Tile> oppRecyclers = new();

    static List<HashSet<Point>> Islands = new();
    static readonly List<string> actions = new();
    static readonly HashSet<Point> spawnedPoints = new();

    static bool End;
    static Map Map;
    static Point MyCenter;

    static int MyMatter;
    static int OppMatter;

    static void Main(string[] args)
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]);
        int height = int.Parse(inputs[1]);
        Map = new Map(width, height);

        while (true)
        {
            Init();

            End = EndGame();

            Build();

            Islands = DetectIslands();

            Spawn();

            CalcMoves();

            Console.WriteLine(actions.Any() ? string.Join(';', actions) : "WAIT");
        }
    }

    private static void Spawn()
    {
        while (MyMatter >= 10)
        {
            Tile spawnTile = null;
            int minDistance = int.MaxValue;
            foreach (var myTile in myTiles.Values)
            {
                if (!myTile.CanSpawn ||
                    myTile.Units >= 2 ||
                    myTile.TurnToHole ||
                    spawnedPoints.Contains(myTile.Point))
                {
                    continue;
                }

                HashSet<Point> currentIsland = Islands.FirstOrDefault(island =>
                    island.Contains(myTile.Point)
                );

                var targetTiles = oppUnits.Values.ToList();
                if (currentIsland != null)
                {
                    targetTiles = currentIsland
                        .Select(p => Tiles[p])
                        .Where(t => !t.TurnToHole && (!End && t.Owner == OPP) || (End && t.Owner == NOONE))
                        .ToList();
                }

                GetNearest(myTile.Point, targetTiles.Select(t => t.Point), out var distance);
                if (distance < minDistance)
                {
                    spawnTile = myTile;
                    minDistance = distance;
                }
            }

            if (spawnTile != null)
            {
                MyMatter -= 10;
                spawnedPoints.Add(spawnTile.Point);
                actions.Add($"SPAWN {1} {spawnTile.Point}");
            }
            else
            {
                break;
            }
        }
    }

    private static bool EndGame()
    {
        bool endGame = true;
        foreach (var myTile in myTiles.Values)
        {
            if (!myTile.CanSpawn || myTile.TurnToHole)
            {
                continue;
            }

            HashSet<Point> currentIsland = Islands.FirstOrDefault(island =>
                island.Contains(myTile.Point)
            );

            if (currentIsland == null)
            {
                endGame = false;
                break;
            }

            bool anyOpp = currentIsland
                .Select(p => Tiles[p])
                .Any(t => !t.TurnToHole && t.Owner == OPP);

            if (anyOpp)
            {
                endGame = false;
                break;
            }
        }

        return endGame;
    }

    private record struct BuildResult(int Scrap, int Holes, int OppUnits, int OppTiles, int MyUnits, int MyTiles);

    private static BuildResult CalcBuild(Point recyclerPoint)
    {
        var result = new BuildResult();

        var recyclerTile = Tiles[recyclerPoint];
        var otherTiles = Map
            .Directions(recyclerPoint, true)
            .Select(p => Tiles[p]);

        foreach (var tile in otherTiles)
        {
            if (tile.Owner == OPP)
            {
                result.OppTiles += 1;
                result.OppUnits += tile.Units;
            }

            if (tile.Owner == ME)
            {
                result.MyTiles += 1;
                result.MyUnits += tile.Units;
            }

            if (myRecyclersRange.Contains(tile.Point))
            {
                continue;
            }

            if (tile.ScrapAmount <= recyclerTile.ScrapAmount)
            {
                result.Scrap += tile.ScrapAmount;
                result.Holes++;
            }
            else
            {
                result.Scrap += recyclerTile.ScrapAmount;
            }
        }

        return result;
    }

    private static void Build()
    {
        if (End)
        {
            actions.Add("MESSAGE Catching up Nixxa");
            return;
        }

        while (MyMatter >= 10)
        {
            Tile buildTile = null;
            int maxScrapAmount = Map.Big ? 19 : 24;
            int maxUnits = 0;

            foreach (var tile in myTiles.Values)
            {
                if (!tile.CanBuild ||
                    spawnedPoints.Contains(tile.Point))
                {
                    continue;
                }

                var buildResult = CalcBuild(tile.Point);

                if (myRecyclers.Count <= oppRecyclers.Count + 1 &&
                    buildResult.Holes <= 3 &&
                    maxScrapAmount < buildResult.Scrap)
                {
                    buildTile = tile;
                    maxScrapAmount = buildResult.Scrap;
                }

                if (buildResult.OppUnits - buildResult.MyUnits > maxUnits)
                {
                    buildTile = tile;
                    maxScrapAmount = int.MaxValue;
                    maxUnits = buildResult.OppUnits - buildResult.MyUnits;
                }
            }

            if (buildTile != null &&
                maxScrapAmount != int.MaxValue)
            {
                Tiles[buildTile.Point].Recycler = true;
                var newIslands = DetectIslands();
                if (newIslands.Count > Islands.Count)
                {
                    Tiles[buildTile.Point].Recycler = false;
                    spawnedPoints.Add(buildTile.Point);
                    continue;
                }
            }

            if (buildTile != null)
            {
                MyMatter -= 10;
                spawnedPoints.Add(buildTile.Point);
                Tiles[buildTile.Point].Recycler = true;
                myRecyclers.Add(buildTile.Point, Tiles[buildTile.Point]);
                actions.Add($"BUILD {buildTile.Point}");
            }
            else
            {
                break;
            }
        }
    }

    private static void CalcMoves()
    {
        List<Point> allTargets = new List<Point>();

        foreach (var myUnit in myUnits.Values)
        {
            HashSet<Point> currentIsland = Islands.FirstOrDefault(island =>
                island.Contains(myUnit.Point)
            );

            var targetsInIsland = oppWithNeutralTiles
                .Where(t => currentIsland != null && currentIsland.Contains(t.Key))
                .Where(t => !t.Value.TurnToHole)
                .ToList();

            for (var u = 0; u < myUnit.Units; u++)
            {
                var targets = targetsInIsland
                    .Where(t => t.Value.MyForceScore == 0)
                    .Select(t => t.Key);

                var nearestTargets = GetNearest(myUnit.Point, targets, out _).ToList();
                if (!nearestTargets.Any())
                {
                    // MyForceScore > 0
                    targets = targetsInIsland.Select(t => t.Key);
                    nearestTargets = GetNearest(myUnit.Point, targets, out _).ToList();
                }

                var target = nearestTargets
                    .Select(p => Tiles[p])
                    .OrderByDescending(t => t.Units)
                    .FirstOrDefault();

                if (target == null)
                {
                    continue;
                }

                target.MyForceScore += 1;
                allTargets.Add(target.Point);
            }
        }

        foreach (var target in allTargets)
        {
            HashSet<Point> currentIsland = Islands.FirstOrDefault(island =>
                island.Contains(target)
            );

            var myUnitsInIsland = myUnits
                .Where(u => u.Value.Units > 0)
                .Where(u => currentIsland == null || currentIsland.Contains(u.Key))
                .Select(u => u.Key);

            var unitPoint = GetNearest(target, myUnitsInIsland, out _).FirstOrDefault();
            myUnits[unitPoint].Units -= 1;
            actions.Add($"MOVE 1 {unitPoint} {target}");
        }
    }

    private static IEnumerable<Point> GetNearest(Point point, IEnumerable<Point> targets, out int minDistance)
    {
        List<Point> nearest = new List<Point>();
        minDistance = int.MaxValue;

        foreach (var target in targets)
        {
            var distance = point.ManhattanTo(target);
            if (distance == minDistance)
            {
                nearest.Add(target);
            }

            if (distance < minDistance)
            {
                nearest.Clear();
                nearest.Add(target);
                minDistance = distance;
            }
        }

        return nearest;
    }

    private static List<HashSet<Point>> DetectIslands()
    {
        var result = new List<HashSet<Point>>();
        HashSet<Point> computed = new ();
        HashSet<Point> current = new ();

        foreach ((var p, Tile t) in Tiles)
        {
            if (t.IsHole)
            {
                continue;
            }

            if (!computed.Contains(p))
            {
                Queue<Point> fifo = new ();
                fifo.Enqueue(p);
                computed.Add(p);

                while (fifo.Count != 0)
                {
                    Point e = fifo.Dequeue();
                    foreach (Point direction in Map.Directions(e)) {
                        Tile tile = Tiles[direction];
                        if (!tile.IsHole && !computed.Contains(direction)) {
                            fifo.Enqueue(direction);
                            computed.Add(direction);
                        }
                    }
                    current.Add(e);
                }
                result.Add(new HashSet<Point>(current));
                current.Clear();
            }
        }

        return result;
    }

    private static bool firstInit = true;

    private static void Init()
    {
        Tiles.Clear();
        myTiles.Clear();
        oppTiles.Clear();
        neutralTiles.Clear();
        oppWithNeutralTiles.Clear();
        myUnits.Clear();
        oppUnits.Clear();
        myRecyclers.Clear();
        myRecyclersRange.Clear();
        oppRecyclers.Clear();

        actions.Clear();
        spawnedPoints.Clear();

        var inputs = Console.ReadLine().Split(' ');
        MyMatter = int.Parse(inputs[0]);
        OppMatter = int.Parse(inputs[1]);

        for (int y = 0; y < Map.Height; y++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                inputs = Console.ReadLine().Split(' ');
                int scrapAmount = int.Parse(inputs[0]);
                int owner = int.Parse(inputs[1]); // 1 = me, 0 = foe, -1 = neutral
                int units = int.Parse(inputs[2]);
                bool recycler = int.Parse(inputs[3]) == 1;
                bool canBuild = int.Parse(inputs[4]) == 1;
                bool canSpawn = int.Parse(inputs[5]) == 1;
                bool inRangeOfRecycler = int.Parse(inputs[6]) == 1;

                Point point = new Point(x, y);
                Tile tile = new Tile(point, scrapAmount, owner, units, recycler, canBuild, canSpawn, inRangeOfRecycler);
                Tiles.Add(point, tile);

                if (tile.Owner == ME)
                {
                    if (tile.Recycler)
                    {
                        myRecyclers.Add(point, tile);
                        IEnumerable<Point> points = Map.Directions(point);
                        myRecyclersRange.UnionWith(points);
                    }
                    else
                    {
                        myTiles.Add(point, tile);

                        if (tile.Units > 0)
                        {
                            myUnits.Add(point, tile);
                        }
                    }
                }
                else if (tile.Owner == OPP)
                {
                    if (tile.Recycler)
                    {
                        oppRecyclers.Add(point, tile);
                    }
                    else
                    {
                        oppTiles.Add(point, tile);
                        oppWithNeutralTiles.Add(point, tile);

                        if (tile.Units > 0)
                        {
                            oppUnits.Add(point, tile);
                        }
                    }
                }
                else if (tile.ScrapAmount != 0)
                {
                    neutralTiles.Add(point, tile);
                    oppWithNeutralTiles.Add(point, tile);
                }
            }
        }

        if (firstInit)
        {
            MyCenter = Map.CenterRange(myTiles.Keys.ToList());
            firstInit = false;
        }

        foreach (var (key, value) in oppWithNeutralTiles)
        {
            if (value.Owner == NOONE &&
                (MyCenter.X < Map.Center.X && key.X - 1 <= MyCenter.X) ||
                (MyCenter.X > Map.Center.X && key.X + 1 >= MyCenter.X))
            {
                value.MyForceScore += 1;
            }
        }
    }
}