using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FallChallenge2022;

public readonly record struct Map(int Width, int Height)
{
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
}

public readonly record struct Point(int X, int Y)
{
    public int ManhattanTo(int x, int y) => Math.Abs(x - X) + Math.Abs(y - Y);
    public int ManhattanTo(Point other) => ManhattanTo(other.X, other.Y);

    public double SqrEuclideanTo(int x, int y) => Math.Pow(x - X, 2) + Math.Pow(y - Y, 2);
    public double SqrEuclideanTo(Point other) => SqrEuclideanTo(other.X, other.Y);

    public override string ToString() => $"{X} {Y}";
}

public class Tile
{
    public readonly Point Point;
    public readonly int ScrapAmount;
    public readonly int Owner;
    public readonly int Units;

    public readonly bool Recycler;
    public readonly bool CanBuild;
    public readonly bool CanSpawn;
    public readonly bool InRangeOfRecycler;

    public double BuildScore;
    public double SpawnScore;
    public double MoveScore;
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
    static readonly Dictionary<Point, Tile> oppRecyclers = new();

    static readonly List<HashSet<Point>> islands = new();
    static readonly List<string> actions = new();
    static readonly HashSet<Point> spawnedPoints = new();

    static Map Map;
    static int MyMatter;
    static int OppMatter;

    static void Main(string[] args)
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]);
        int height = int.Parse(inputs[1]);
        Map = new Map(width, height);

        // game loop
        while (true)
        {
            Init();

            DetectIslands();

            DefenceBuild();

            AttackBuild();

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
                    spawnedPoints.Contains(myTile.Point))
                {
                    continue;
                }

                GetNearest(myTile.Point, oppUnits.Keys, out var distance);
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

    private static void DefenceBuild()
    {
        if (MyMatter < 10)
        {
            return;
        }

        // attack
        Tile buildTile = null;
        int maxUnits = 2;

        foreach (var tile in myTiles.Values)
        {
            if (!tile.CanBuild || tile.ScrapAmount < 2)
            {
                continue;
            }

            var units = Map.Directions(tile.Point)
                .Select(p => Tiles[p])
                .Where(p => p.Owner == OPP)
                .Sum(t => t.Units);

            if (maxUnits < units)
            {
                buildTile = tile;
                maxUnits = units;
            }
        }

        if (buildTile != null)
        {
            MyMatter -= 10;
            spawnedPoints.Add(buildTile.Point);
            actions.Add("MESSAGE DefenceBuild");
            actions.Add($"BUILD {buildTile.Point}");
        }
    }

    private static void AttackBuild()
    {
        if (MyMatter < 10)
        {
            return;
        }

        Tile buildTile = null;
        int maxScrapAmount = 35;

        foreach (var tile in myTiles.Values)
        {
            if (!tile.CanBuild || tile.ScrapAmount < 7)
            {
                continue;
            }

            var scrapAmount = Map.Directions(tile.Point, true)
                .Select(p => Tiles[p])
                .Where(p => !p.Recycler)
                .Sum(t => t.ScrapAmount);

            if (maxScrapAmount < scrapAmount)
            {
                buildTile = tile;
                maxScrapAmount = scrapAmount;
            }
        }

        if (buildTile != null)
        {
            MyMatter -= 10;
            spawnedPoints.Add(buildTile.Point);
            actions.Add("MESSAGE AttackBuild");
            actions.Add($"BUILD {buildTile.Point}");
        }
    }

    private static void CalcMoves()
    {
        foreach (var myUnit in myUnits.Values)
        {
            HashSet<Point> currentIsland = islands.FirstOrDefault(island =>
                island.Contains(myUnit.Point)
            );

            for (var u = 0; u < myUnit.Units; u++)
            {
                var targetsInIsland = oppWithNeutralTiles
                    .Where(t => currentIsland != null && currentIsland.Contains(t.Key))
                    .ToList();

                var targets = targetsInIsland
                    .Where(t => t.Value.MyForceScore == 0)
                    .Select(t => t.Key);

                var nearestTargets = GetNearest(myUnit.Point, targets, out _).ToList();
                if (!nearestTargets.Any())
                {
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
                actions.Add($"MOVE 1 {myUnit.Point} {target.Point}");
            }
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

    private static void DetectIslands() {
        islands.Clear();
        HashSet<Point> computed = new ();
        HashSet<Point> current = new ();

        foreach ((var p, Tile t) in Tiles) {
            if (t.IsHole) {
                continue;
            }
            if (!computed.Contains(p)) {
                Queue<Point> fifo = new ();
                fifo.Enqueue(p);
                computed.Add(p);

                while (fifo.Count != 0) {
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
                islands.Add(new HashSet<Point>(current));
                current.Clear();
            }
        }
    }

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
    }
}