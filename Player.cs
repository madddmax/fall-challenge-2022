using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FallChallenge2022;

public readonly record struct Map(int Width, int Height)
{
    public IEnumerable<Point> Directions(Point point)
    {
        // Right
        if (point.X + 1 < Width)
        {
            yield return new Point(point.X + 1, point.Y);
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

        // Down
        if (point.Y + 1 < Height)
        {
            yield return new Point(point.X, point.Y + 1);
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
    public double MyForceScore;

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
}

class Player
{
    static readonly int ME = 1;
    static readonly int OPP = 0;
    static readonly int NOONE = -1;

    static readonly Dictionary<Point, Tile> Tiles = new();
    static readonly Dictionary<Point, Tile> myTiles = new();
    static readonly Dictionary<Point, Tile> oppTiles = new();
    static readonly Dictionary<Point, Tile> neutralTiles = new();
    static readonly Dictionary<Point, Tile> myUnits = new();
    static readonly Dictionary<Point, Tile> oppUnits = new();
    static readonly Dictionary<Point, Tile> myRecyclers = new();
    static readonly Dictionary<Point, Tile> oppRecyclers = new();

    static readonly List<string> actions = new List<string>();

    static void Main(string[] args)
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]);
        int height = int.Parse(inputs[1]);
        var map = new Map(width, height);

        // game loop
        while (true)
        {
            Tiles.Clear();
            myTiles.Clear();
            oppTiles.Clear();
            neutralTiles.Clear();
            myUnits.Clear();
            oppUnits.Clear();
            myRecyclers.Clear();
            oppRecyclers.Clear();

            inputs = Console.ReadLine().Split(' ');
            int myMatter = int.Parse(inputs[0]);
            int oppMatter = int.Parse(inputs[1]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
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
                        myTiles.Add(point, tile);

                        if (tile.Units > 0)
                        {
                            myUnits.Add(point, tile);
                        }
                        else if (tile.Recycler)
                        {
                            myRecyclers.Add(point, tile);
                        }
                    }
                    else if (tile.Owner == OPP)
                    {
                        oppTiles.Add(point, tile);

                        if (tile.Units > 0)
                        {
                            oppUnits.Add(point, tile);
                        }
                        else if (tile.Recycler)
                        {
                            oppRecyclers.Add(point, tile);
                        }
                    }
                    else
                    {
                        neutralTiles.Add(point, tile);
                    }
                }
            }

            actions.Clear();

            foreach(var tile in myTiles.Values)
            {
                if (tile.CanSpawn)
                {
                    int amount = 1;
                    if (amount > 0)
                    {
                        actions.Add($"SPAWN {amount} {tile.Point}");
                    }
                }

                if (tile.CanBuild)
                {
                    bool shouldBuild = false;
                    if (shouldBuild)
                    {
                        actions.Add($"BUILD {tile.Point}");
                    }
                }
            }

            foreach (var myUnit in myUnits.Values)
            {
                var targets = GetNearest(myUnit.Point, oppTiles.Keys);
                var target = targets
                    .Select(p => Tiles[p])
                    .OrderByDescending(t => t.Units)
                    .FirstOrDefault();

                if (target == null)
                {
                    continue;
                }

                var directions = map.Directions(myUnit.Point)
                    .Select(p => Tiles[p])
                    .Where(t => !t.Recycler && t.ScrapAmount != 0)
                    .Select(t => t.Point);

                var moves = GetNearest(myUnit.Point, directions)
                    .Select(p => Tiles[p]);

                Tile bestMove = null;
                int bestScore = 0;
                foreach (var move in moves)
                {
                    int score = 0;
                    if (move.Owner == OPP)
                    {
                        score = move.Units + 1;
                    }
                    else if (move.Owner == NOONE)
                    {
                        score = 2;
                    }
                    else
                    {
                        score = 1;
                    }

                    if (score > bestScore)
                    {
                        bestMove = move;
                        bestScore = score;
                    }
                }

                if (bestMove != null)
                {
                    actions.Add($"MOVE {myUnit.Units} {myUnit.Point} {bestMove.Point}");
                }
            }

            if (actions.Any())
            {
                Console.WriteLine(string.Join(';', actions));
            }
            else
            {
                Console.WriteLine("WAIT");
            }
        }
    }

    static List<Point> GetNearest(Point point, IEnumerable<Point> targets)
    {
        List<Point> nearest = new List<Point>();
        double minDistance = int.MaxValue;

        foreach (var target in targets)
        {
            var distance = point.SqrEuclideanTo(target);
            if (Math.Abs(distance - minDistance) < 0.0001)
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
}