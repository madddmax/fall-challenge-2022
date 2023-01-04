using System;
using System.Collections.Generic;
using System.Linq;

namespace FallChallenge2022;

public readonly record struct Map(int Width, int Height)
{
    public readonly Point Center = new(Width / 2, Height / 2);

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

    public bool Border;
}

public static class Player
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
    static readonly HashSet<Point> buildedPoints = new();

    static bool End;
    public static Map Map;
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

            CalcMoves();

            Build();

            Islands = DetectIslands(new HashSet<Point>());

            Spawn();

            Console.WriteLine(actions.Any() ? string.Join(';', actions) : "WAIT");
        }
    }

    private static void Spawn()
    {
        List<Node> moveNodes = new List<Node>();

        foreach (var myTile in myTiles.Values)
        {
            if (!myTile.CanSpawn ||
                !myTile.Border ||
                myTile.TurnToHole ||
                buildedPoints.Contains(myTile.Point))
            {
                continue;
            }

            HashSet<Point> currentIsland = Islands.FirstOrDefault(island =>
                island.Contains(myTile.Point)
            );

            var targetPoints = oppTiles
                .Where(t => !t.Value.TurnToHole)
                .Select(t => t.Key)
                .ToHashSet();

            if (currentIsland != null)
            {
                targetPoints = currentIsland
                    .Select(p => Tiles[p])
                    .Where(t => !t.TurnToHole && (!End && t.Owner == OPP || End && t.Owner == NOONE))
                    .Select(t => t.Point)
                    .ToHashSet();
            }

            if (!targetPoints.Any())
            {
                continue;
            }

            var node = GetMovePath(myTile.Point, 9);
            moveNodes.Add(node);
        }

        moveNodes = moveNodes
            .Where(n => n.Total > 0)
            .OrderByDescending(n => n.Total)
            .ToList();

        while (MyMatter >= 10 && moveNodes.Any())
        {
            for (int i = 0; i < moveNodes.Count && MyMatter >= 10; i++)
            {
                var node = moveNodes[i];
                while (node.Distance != 0)
                {
                    node = node.Parent;
                }

                MyMatter -= 10;
                actions.Add($"SPAWN {1} {node.Point}");
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

    private record struct BuildResult(int Scrap, HashSet<Point> Holes, int OppUnits, int OppTiles, int MyUnits, int MyTiles);

    private static BuildResult CalcBuild(Point recyclerPoint)
    {
        var result = new BuildResult {Holes = new HashSet<Point>()};

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
                result.Holes.Add(tile.Point);
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
            actions.Add("MESSAGE Bye RolloTomasi Hiller1233 Oakio Nixxa");
            return;
        }

        while (MyMatter >= 10)
        {
            Tile buildTile = null;
            int maxScrapAmount = 10;
            int maxUnits = 0;

            for (int maxHoles = 4; maxHoles >= 2 && buildTile == null; maxHoles--)
            {
                maxScrapAmount = 5 * maxHoles + (maxHoles - 2);

                foreach (var tile in myTiles.Values)
                {
                    if (!tile.CanBuild ||
                        buildedPoints.Contains(tile.Point) ||
                        tile.MyForceScore >= 10)
                    {
                        continue;
                    }

                    var buildResult = CalcBuild(tile.Point);

                    if (myRecyclers.Count <= oppRecyclers.Count &&
                        buildResult.Holes.Count < maxHoles &&
                        maxScrapAmount < buildResult.Scrap)
                    {
                        buildTile = tile;
                        maxScrapAmount = buildResult.Scrap;
                    }

                    if (myRecyclers.Count < oppRecyclers.Count &&
                        buildResult.OppUnits - buildResult.MyUnits > maxUnits)
                    {
                        buildTile = tile;
                        maxScrapAmount = int.MaxValue;
                        maxUnits = buildResult.OppUnits - buildResult.MyUnits;
                    }
                }
            }

            if (buildTile != null &&
                maxScrapAmount != int.MaxValue)
            {
                var buildResult = CalcBuild(buildTile.Point);

                var newIslands = DetectIslands(buildResult.Holes);
                var newIslandsCount = newIslands.Count(i => i.All(p => Tiles[p].Owner != ME));
                var islandsCount = Islands.Count(i => i.All(p => Tiles[p].Owner != ME));
                if (newIslandsCount > islandsCount)
                {
                    buildedPoints.Add(buildTile.Point);
                    continue;
                }

                HashSet<Point> currentIsland = Islands.FirstOrDefault(island =>
                    island.Contains(buildTile.Point)
                );

                bool allNone = Islands.Count > 1 &&
                               currentIsland != null &&
                               currentIsland
                                   .Where(p => !buildResult.Holes.Contains(p))
                                   .All(p => Tiles[p].Owner == NOONE);
                if (allNone)
                {
                    buildedPoints.Add(buildTile.Point);
                    continue;
                }

                bool allMyUnits = false;
                foreach (var curr in newIslands)
                {
                    allMyUnits = curr.All(p => Tiles[p].Owner != OPP) &&
                                 curr.Any(p => Tiles[p].Units > 0);

                    if (allMyUnits)
                    {
                        break;
                    }
                }

                if (allMyUnits)
                {
                    buildedPoints.Add(buildTile.Point);
                    continue;
                }
            }

            if (buildTile != null)
            {
                MyMatter -= 10;
                buildedPoints.Add(buildTile.Point);
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
        foreach (var myUnit in myUnits.Values)
        {
            for (var u = 0; u < myUnit.Units; u++)
            {
                var node = GetMovePath(myUnit.Point, 9);
                if (node.Point == myUnit.Point)
                {
                    continue;
                }

                while (node.Parent.Point != myUnit.Point)
                {
                    Tiles[node.Point].MyForceScore += 1;
                    node = node.Parent;
                }

                Point target = node.Point;
                Tiles[target].MyForceScore += 10;
                actions.Add($"MOVE 1 {myUnit.Point} {target}");
            }
        }
    }

    private static List<HashSet<Point>> DetectIslands(HashSet<Point> holes)
    {
        var result = new List<HashSet<Point>>();
        HashSet<Point> computed = new ();
        HashSet<Point> current = new ();

        foreach ((var p, Tile t) in Tiles)
        {
            if (t.IsHole || holes.Contains(t.Point))
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
                        if (!tile.IsHole && !holes.Contains(tile.Point) && !computed.Contains(direction)) {
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
        buildedPoints.Clear();

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

        foreach (var (point, value) in myTiles)
        {
            var neighbours = Map.Directions(point);
            value.Border = neighbours.Any(p => !Tiles[p].IsHole && !Tiles[p].TurnToHole && Tiles[p].Owner != ME);
        }

        foreach (var (point, value) in oppTiles)
        {
            var neighbours = Map.Directions(point);
            value.Border = neighbours.Any(p => !Tiles[p].IsHole && !Tiles[p].TurnToHole && Tiles[p].Owner != OPP);
        }

        foreach (var (key, value) in neutralTiles)
        {
            var borders = Map.Directions(key).Any(p => Tiles[p].Owner == OPP && Tiles[p].Border);
            value.MyForceScore -= borders ? 10 : 0;
        }
    }

    public record Node
    {
        public double Total => Score - MyForce;
        public double Score;
        public double MyForce;
        public int Distance;
        public Point Point;
        public Node Parent;
    }

    public static Node GetMovePath(Point point, int maxDistance)
    {
        Dictionary<Point, int> visited = new Dictionary<Point, int>();
        Queue<Node> frontier = new Queue<Node>();

        var firstNode = new Node {Point = point};
        var bestNode = firstNode;
        var poorNode = firstNode;

        frontier.Enqueue(firstNode);

        while (frontier.Count > 0)
        {
            var currentNode = frontier.Dequeue();
            if (currentNode.Distance > maxDistance)
            {
                continue;
            }

            visited.TryAdd(currentNode.Point, currentNode.Distance);

            if (currentNode.Total > bestNode.Total)
            {
                bestNode = currentNode;
            }

            if (currentNode.Score >= poorNode.Score)
            {
                if (currentNode.Score > poorNode.Score ||
                    currentNode.MyForce < poorNode.MyForce)
                {
                    poorNode = currentNode;
                }
            }

            var neighbours = Map.Directions(currentNode.Point);
            foreach (var neighbour in neighbours)
            {
                var neighbourTile = Tiles[neighbour];

                int myForce = neighbourTile.MyForceScore;
                int tileScore = 0;
                if (neighbourTile.Owner == OPP)
                {
                    tileScore += 2 + neighbourTile.Units;
                }
                else if (neighbourTile.Owner == ME)
                {
                    myForce += 2 + neighbourTile.Units;
                }
                else // NOONE
                {
                    tileScore += End ? 2 : 1;
                }

                int distance = currentNode.Distance + 1;

                if (!neighbourTile.IsHole &&
                    !neighbourTile.TurnToHole &&
                    (!visited.TryGetValue(neighbour, out int d) || d == distance))
                {
                    var neighbourNode = new Node
                    {
                        Point = neighbour,
                        Parent = currentNode,
                        Distance = distance,
                        Score = (currentNode.Score + tileScore) / distance,
                        MyForce = (currentNode.MyForce + myForce) / distance
                    };
                    frontier.Enqueue(neighbourNode);
                }
            }
        }

        return bestNode == firstNode ? poorNode : bestNode;
    }
}