using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class Tile
{
    public readonly int X;
    public readonly int Y;
    public readonly int ScrapAmount;
    public readonly int Owner;
    public readonly int Units;

    public readonly bool Recycler;
    public readonly bool CanBuild;
    public readonly bool CanSpawn;
    public readonly bool InRangeOfRecycler;

    public Tile(int x, int y, int scrapAmount, int owner, int units, bool recycler, bool canBuild, bool canSpawn, bool inRangeOfRecycler)
    {
        X = x;
        Y = y;
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

    static void Main(string[] args)
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        int width = int.Parse(inputs[0]);
        int height = int.Parse(inputs[1]);

        // game loop
        while (true)
        {
            List<Tile> tiles = new List<Tile>();
            List<Tile> myTiles = new List<Tile>();
            List<Tile> oppTiles = new List<Tile>();
            List<Tile> neutralTiles = new List<Tile>();
            List<Tile> myUnits = new List<Tile>();
            List<Tile> oppUnits = new List<Tile>();
            List<Tile> myRecyclers = new List<Tile>();
            List<Tile> oppRecyclers = new List<Tile>();

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

                    Tile tile = new Tile(x, y, scrapAmount, owner, units, recycler, canBuild, canSpawn, inRangeOfRecycler);
                    tiles.Add(tile);

                    if (tile.Owner == ME)
                    {
                        myTiles.Add(tile);

                        if (tile.Units > 0)
                        {
                            myUnits.Add(tile);
                        }
                        else if (tile.Recycler)
                        {
                            myRecyclers.Add(tile);
                        }
                    }
                    else if (tile.Owner == OPP)
                    {
                        oppTiles.Add(tile);

                        if (tile.Units > 0)
                        {
                            oppUnits.Add(tile);
                        }
                        else if (tile.Recycler)
                        {
                            oppRecyclers.Add(tile);
                        }
                    }
                    else
                    {
                        neutralTiles.Add(tile);
                    }
                }
            }

            List<string> actions = new List<string>();

            foreach(Tile tile in myTiles)
            {
                if (tile.CanSpawn)
                {
                    int amount = 1; // TODO: pick amount of robots to spawn here
                    if (amount > 0)
                    {
                        actions.Add($"SPAWN {amount} {tile.X} {tile.Y}");
                    }
                }

                if (tile.CanBuild)
                {
                    bool shouldBuild = false; // TODO: pick whether to build recycler here
                    if (shouldBuild)
                    {
                        actions.Add($"BUILD {tile.X} {tile.Y}");
                    }
                }
            }

            foreach (Tile tile in myUnits)
            {
                Tile target = oppUnits.FirstOrDefault(); // TODO: pick a destination
                if (target != null)
                {
                    int amount = 1; // TODO: pick amount of units to move
                    actions.Add($"MOVE {amount} {tile.X} {tile.Y} {target.X} {target.Y}");
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
}