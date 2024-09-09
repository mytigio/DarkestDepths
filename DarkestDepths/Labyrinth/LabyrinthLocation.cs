using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.GameData.LocationContexts;
using StardewValley.GameData.Locations;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using xTile;
using xTile.Layers;
using xTile.ObjectModel;
using xTile.Tiles;

namespace DarkestDepths.Labyrinth
{
    [XmlInclude(typeof(LabyrinthLocation))]
    public class LabyrinthLocation : GameLocation
    {
        Random seeded_random;
        private IMonitor _monitor;

        public int size = 0;
        public int startTile;

        public Vector2 fogPos;

        public LabyrinthTile[,] TileMap;

        static readonly Dictionary<Vector2, int> cellDirections = new()
        {
            {new Vector2(0, -LabyrinthManager.TILE_SPACING), Wall.North},
            {new Vector2(LabyrinthManager.TILE_SPACING, 0), Wall.East},
            {new Vector2(0, LabyrinthManager.TILE_SPACING), Wall.South},
            {new Vector2(-LabyrinthManager.TILE_SPACING, 0), Wall.West}
        };

        static readonly Dictionary<int, Vector2> inverseCellDirections = new()
        {
            { Wall.North, new Vector2(0, -LabyrinthManager.TILE_SPACING)},
            { Wall.East, new Vector2(LabyrinthManager.TILE_SPACING, 0)},
            { Wall.South, new Vector2(0, LabyrinthManager.TILE_SPACING)},
            { Wall.West, new Vector2(-LabyrinthManager.TILE_SPACING, 0)},
            { Wall.NorthWest, new Vector2(-LabyrinthManager.TILE_SPACING, -LabyrinthManager.TILE_SPACING)},
            { Wall.SouthWest, new Vector2(-LabyrinthManager.TILE_SPACING, LabyrinthManager.TILE_SPACING)},
            { Wall.NorthEast, new Vector2(LabyrinthManager.TILE_SPACING, -LabyrinthManager.TILE_SPACING)},
            { Wall.SouthEast, new Vector2(LabyrinthManager.TILE_SPACING, LabyrinthManager.TILE_SPACING)},
        };

        static readonly Dictionary<String, int> treasureFloorTiles = new()
        {
            {"upperLeft", 1},
            {"upperCenter", 2 },
            {"upperRight", 3 },
            {"middleLeft", 17 },
            {"middleCenter", 18 },
            {"middleRight", 19 },
            {"bottomLeft", 33 },
            {"bottomCenter", 34 },
            {"bottomRight", 35 }
        };

        public const int upper_tile_index_1 = 74;
        public const int upper_tile_index_2 = 90;
        public const int upper_tile_index_3 = 106;
        public const int upper_tile_index_4 = 122;
        public const int left_tile_index = 132;
        public const int right_tile_index = 175;
        public const int bottom_tile_index = 215;
        public const int floor_tile_index = 138;
        public const int darkness_tile_index = 10;

        public const int southwest_convex_corner = 216;
        public const int southwest_convex_corner_bottom = 232;

        public const int southeast_convex_corner = 220;
        public const int southeast_convex_corner_bottom = 236;

        public const int northwest_convex_corner_1 = 71;
        public const int northwest_convex_corner_2 = 87;
        public const int northwest_convex_corner_3 = 103;
        public const int northwest_convex_corner_4 = 119;

        public const int northeast_convex_corner_1 = 72;
        public const int northeast_convex_corner_2 = 88;
        public const int northeast_convex_corner_3 = 104;
        public const int northeast_convex_corner_4 = 120;

        public const int northWesternCorner_index_1 = 68;
        public const int northWesternCorner_index_2 = 84;
        public const int northWesternCorner_index_3 = 100;
        public const int northWesternCorner_index_4 = 116;

        public const int northEasternCorner_index_1 = 111;
        public const int northEasternCorner_index_2 = 127;
        public const int northEasternCorner_index_3 = 143;
        public const int northEasternCorner_index_4 = 159;

        public const int southWesternCorner = 196;
        public const int southEasternCorner = 221;


        public Dictionary<Vector2, LightSource> lightSources = new();

        //how deep you are in the labyrinth.
        //this will control monster spawn levels, and whether you are transitioning from the caves to the worked stone areas.
        public readonly NetInt level = new(0);
        public readonly NetInt levelSeed = new(0);

        private Microsoft.Xna.Framework.Rectangle fogSource = new Microsoft.Xna.Framework.Rectangle(640, 0, 64, 64);

        private readonly NetColor netFogColor = new NetColor();

        public Color fogColor
        {
            get
            {
                return this.netFogColor.Value;
            }
            set
            {
                this.netFogColor.Value = value;
            }
        }

        /// <summary>
        /// The level of this room.  Must be greater then or equal to 0.
        /// </summary>
        public int Level
        {
            get
            {
                return level.Value;
            }
            set
            {
                if (value >= 0)
                {
                    level.Value = value;
                }
                else
                {
                    level.Value = 0;
                }
            }
        }

        //the width of the map.  This might get multiplied by a spacer when we're done.
        public readonly NetInt width = new(0);

        public int Width
        {
            get { return width.Value; }
            set { width.Value = value; }
        }
        //the height of the map.  This might get multiplied by a spacer when we're done.
        public readonly NetInt height = new(0);

        public int Height
        {
            get { return height.Value; }
            set { height.Value = value; }
        }

        //Info about the level above us.  Used so we can transition back up a level.
        //since lower levels are deterministic based on the exist position of the parent, this allows us to have a
        //consistent maze that changes daily.
        public NetString parentLevel = new();

        public String ParentLevel
        {
            get { return parentLevel.Value; }
            set { parentLevel.Value = value; }
        }

        public NetPoint parentExitPoint = new();


        //where we entered this level.  Used to determine which spot can take us back up a level.
        public NetPoint entrancePosition = new();

        //exits on this level.  Each exit can take us to a different next level.
        public NetObjectList<LabyrinthExit> exits = new();

        private LocationContextData? contextData;

        /// <summary>
        /// 
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("SMAPI.CommonErrors", "AvoidNetField")]
        public LabyrinthLocation() : base()
        {
            locationContextId = LabyrinthManager.CONTEXT_NAME;
            Game1.locationContextData.TryGetValue(locationContextId, out contextData);

            base.IsOutdoors = false;
            base.IsFarm = false;
            base.IsGreenhouse = false;
            Color ambientcolor = new Color(70f, 70f, 70f);
            base.ignoreOutdoorLighting.Value = true;
            base.indoorLightingColor = ambientcolor;
            base.indoorLightingNightColor = ambientcolor;
            base.LightLevel = 0.60f;
            base._updateAmbientLighting();

            LocationData data = new LocationData();
            data.CustomFields = new();
            data.CustomFields.Add("LabyrinthLocation", "true");

            name.Value = "";
            critters = new List<Critter>();
            this.fogColor = new Color(58, 0, 102);
            //this.fogColor = new Color(141, 73, 70);
        }
        /// <summary>
        /// Builds a new labyrinth location with just the name.  Should not typically be used.
        /// </summary>
        /// <param name="name">Name of the location</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("SMAPI.CommonErrors", "AvoidNetField")]
        public LabyrinthLocation(string name, IMonitor monitor) : this()
        {
            base.name.Value = name;
            Level = 0;
            _monitor = monitor;
            int baseSeed = LabyrinthManager.DailySeed;

            base.name.Value = "Labyrinth_" + (baseSeed.ToString() ?? "no_seed") + "_" + Level.ToString() + "_0_0";
            monitor.Log("Level set to: " + Level, LogLevel.Trace);
            monitor.Log("Name set to " + Name, LogLevel.Trace);
            string psudoRandomSeedString = (Game1.Date.TotalDays % 1680).ToString().PadLeft(4, '0') + Level.ToString().PadLeft(2, '0') + "00" + "00";
            buildMap(psudoRandomSeedString);
        }

        public static String buildLabyrinthName(LabyrinthLocation parentLocation, LabyrinthExit parentExit, int baseSeed)
        {
            return "Labyrinth_" + (baseSeed.ToString() ?? "no_seed") + "_" + (parentLocation.Level + 1).ToString() + "_" + parentExit.Position.X.ToString() + "_" + parentExit.Position.Y.ToString();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("SMAPI.CommonErrors", "AvoidNetField")]
        public LabyrinthLocation(LabyrinthLocation parentLocation, LabyrinthExit parentExit, IMonitor monitor) : this()
        {
            _monitor = monitor;
            Level = parentLocation.Level + 1;
            monitor.Log("Level set to: " + Level, LogLevel.Trace);
            IsOutdoors = false;
            IsFarm = false;
            IsGreenhouse = false;
            parentLevel.Value = parentLocation.Name;
            parentExitPoint.Value = parentExit.Position;

            //base.modData.Add("parent_location", parentLocation.Name);
            //base.modData.Add("parent_exit_x", parentExit.Position.X.ToString());
            //base.modData.Add("parent_exit_y", parentExit.Position.Y.ToString());

            int baseSeed = LabyrinthManager.DailySeed;

            name.Value = buildLabyrinthName(parentLocation, parentExit, baseSeed);


            //create a number representation from the level and position of the exit on the parents.
            //we want the individual seed used to build the level itself to be predictable given a set game and daily seed so that different players visiting the labyrinth on
            //the same day will visit the same places, and a player making more then 1 runt hrough the labyrinth will see the same levels on the same day.
            //each day the labyrinth will regenerate and shift.

            //monsters will not use this same seed, so that they are less predictible on subsequent visits (not sure if they will respawn on each visit or after a certain time).
            string psudoRandomSeedString = (Game1.Date.TotalDays % 1680).ToString().PadLeft(4, '0') + Level.ToString().PadLeft(2, '0') + parentExit.Position.X.ToString().PadLeft(2, '0') + parentExit.Position.Y.ToString().PadLeft(2, '0');
            buildMap(psudoRandomSeedString);
        }

        private void InitializeMap()
        {
            Map map = new Map(Name);
            string caveImageSource = Path.Combine("Maps", "Mines", "mine_dark_dangerous");
            string volcanoImageSource = Path.Combine("Maps", "Mines", "volcano_dungeon");
            Game1.temporaryContent.Load<Texture2D>(caveImageSource);
            Game1.temporaryContent.Load<Texture2D>(volcanoImageSource);

            map.DisposeTileSheets(Game1.mapDisplayDevice);

            //add the dangerous dark mine tilesheet, this is the one we'll use to build our labyrinths for now. Add others later?
            map.AddTileSheet(new TileSheet(LabyrinthManager.DARK_MINES_TILESHEET, map, caveImageSource, new xTile.Dimensions.Size(16, 18), new xTile.Dimensions.Size(16, 16)));
            map.AddTileSheet(new TileSheet(LabyrinthManager.VOLCANO_TILESHEET, map, volcanoImageSource, new xTile.Dimensions.Size(16, 36), new xTile.Dimensions.Size(16, 16)));
            map.LoadTileSheets(Game1.mapDisplayDevice);



            //add each of the 5 map layers.
            int adjustedWidth = Width + 1;
            int adjustedHeight = Height + 1;

            map.AddLayer(new Layer("Back", map, new xTile.Dimensions.Size(adjustedWidth, adjustedHeight), new xTile.Dimensions.Size(64, 64)));
            map.AddLayer(new Layer("Buildings", map, new xTile.Dimensions.Size(adjustedWidth, adjustedHeight), new xTile.Dimensions.Size(64, 64)));
            map.AddLayer(new Layer("Front", map, new xTile.Dimensions.Size(adjustedWidth, adjustedHeight), new xTile.Dimensions.Size(64, 64)));
            map.AddLayer(new Layer("Paths", map, new xTile.Dimensions.Size(adjustedWidth, adjustedHeight), new xTile.Dimensions.Size(64, 64)));
            map.AddLayer(new Layer("AlwaysFront", map, new xTile.Dimensions.Size(adjustedWidth, adjustedHeight), new xTile.Dimensions.Size(64, 64)));

            this.map = map;

            SortLayers();
        }

        private void buildMap(string psudoRandomSeedString)
        {
            int psudoRandomSeed;
            if (int.TryParse(psudoRandomSeedString, out psudoRandomSeed))
            {
                //we now have a psudo random level seed, lets store it!
                levelSeed.Value = psudoRandomSeed;
                _monitor.Log("levelSeed set to " + levelSeed.Value, LogLevel.Trace);
                Random levelRng = new Random(psudoRandomSeed);
                seeded_random = levelRng;
                //our maze levels are square dimensions so we just generate 1 and use it for both x and y
                size = seeded_random.Next(LabyrinthManager.MINIMUM_MAP_SIZE, LabyrinthManager.MAXIMUM_MAP_SIZE);

                var mapSize = size * LabyrinthManager.TILE_SPACING;

                //now ensure the size leaves room for walls on the east and south sides.
                int modded = mapSize % LabyrinthManager.TILE_SPACING;
                int doubleSpaceAtStart = LabyrinthManager.TILE_SPACING / 2 * 2;
                int difference = doubleSpaceAtStart - modded;
                int finalSize = mapSize + difference;

                int spacing_adjustment = 0;
                if (LabyrinthManager.TILE_SPACING % 2 == 0)
                    spacing_adjustment = 1;

                startTile = LabyrinthManager.TILE_SPACING / 2 + spacing_adjustment;

                Width = finalSize;
                Height = finalSize;
                _monitor.Log("map size set to [" + Width.ToString() + ", " + Height.ToString() + "]", LogLevel.Trace);
                buildMapStructure();

            }
        }

        private void buildMapStructure()
        {
            //maps already initialized, no reason to rebuild it.
            if (map != null && map.Id == Name) { return; }

            InitializeMap();

            List<Vector2> unvisitedPoints = new List<Vector2>();

            TileMap = new LabyrinthTile[Width, Height];

            TileSheet dark_sheet = map.GetTileSheet(LabyrinthManager.DARK_MINES_TILESHEET);
            TileSheet volcano_sheet = map.GetTileSheet(LabyrinthManager.VOLCANO_TILESHEET);
            var tileMap = createFloorPlan(unvisitedPoints, dark_sheet);
            var tileGroupings = placeMapTiles(tileMap, dark_sheet);
            spawnFeatures(tileMap);
            placeEntrance(tileMap, tileGroupings, volcano_sheet, dark_sheet);
            placeExits(tileMap, tileGroupings, volcano_sheet, dark_sheet);
            placeTreasureSpots(tileMap, tileGroupings, volcano_sheet, dark_sheet);
            _monitor.Log("Endcap spots remaining with nothing in them: " + tileGroupings["emptyEndcaps"].Count());
        }

        private void placeEntrance(LabyrinthTile[,] tileMap, Dictionary<String, List<LabyrinthTile>> tileGroupings, TileSheet volcano_sheet, TileSheet cave_sheet)
        {
            //get layers
            Layer backLayer = map.GetLayer("Back");
            Layer buildingLayer = map.GetLayer("Buildings");
            Layer frontLayer = map.GetLayer("Front");

            var topEndcaps = tileGroupings["topEndcaps"];

            if (topEndcaps.Count() > 0)
            {

                int random_endcap = seeded_random.Next(0, topEndcaps.Count());
                var entrance = topEndcaps[random_endcap];
                topEndcaps.Remove(entrance);

                int x = (int)entrance.Point.X;
                int y = (int)entrance.Point.Y;

                _monitor.Log(String.Format("Add an entrance to [{0},{1}]", x.ToString(), y.ToString()));

                addNorthEastCornerConvex(x + 1, y, cave_sheet, backLayer, buildingLayer, frontLayer);
                addNorthWestCornerConvex(x - 1, y, cave_sheet, backLayer, buildingLayer, frontLayer);
                addEntrance(x, y, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                this.entrancePosition.Value = entrance.Point.ToPoint();

                PropertyValue value = new PropertyValue("Labyrinth_Entrance");
                var warp_tile = backLayer.Tiles[x, y - 2];
                warp_tile.Properties.Add("TouchAction", value);
            }
            else
            {
                _monitor.Log("By random chance, we don't have a place to put an entrance.  Now what?");
            }
        }

        private void placeExits(LabyrinthTile[,] tileMap, Dictionary<String, List<LabyrinthTile>> tileGroupings, TileSheet volcano_sheet, TileSheet cave_sheet)
        {
            //get layers
            Layer backLayer = map.GetLayer("Back");
            Layer buildingLayer = map.GetLayer("Buildings");
            Layer frontLayer = map.GetLayer("Front");

            var bottomEndCaps = tileGroupings["bottomEndcaps"];

            if (bottomEndCaps.Count() > 0)
            {
                //as we move down through the labyrinth the likelyhood of 2 exits increases.
                int numberOfExits = Math.Max(seeded_random.Next(1, LabyrinthManager.MAX_EXITS + Level), LabyrinthManager.MAX_EXITS);
                List<LabyrinthTile> exitsToAdd = new();

                for (int i = 0; i < numberOfExits && bottomEndCaps.Count() > 0; i++)
                {
                    int random_endcap = seeded_random.Next(0, bottomEndCaps.Count());
                    var exit = bottomEndCaps[random_endcap];
                    exitsToAdd.Add(exit);
                    bottomEndCaps.Remove(exit);
                }

                foreach (LabyrinthTile exit in exitsToAdd)
                {
                    int x = (int)exit.Point.X;
                    int y = (int)exit.Point.Y;

                    _monitor.Log(String.Format("Add an exit to [{0},{1}]", x.ToString(), y.ToString()));

                    addSouthEastCornerConvex(x + 1, y, cave_sheet, backLayer, buildingLayer, frontLayer);
                    addSouthWestCornerConvex(x - 1, y, cave_sheet, backLayer, buildingLayer, frontLayer);
                    addExit(x, y, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                    LabyrinthExit lexit = new LabyrinthExit(exit.Point.ToPoint(), this.Name);
                    this.exits.Add(lexit);

                    PropertyValue value = new PropertyValue("Labyrinth_Exit");
                    var warp_tile = backLayer.Tiles[x, y + 2];
                    warp_tile.Properties.Add("TouchAction", value);
                }
            }
        }

        private void placeTreasureSpots(LabyrinthTile[,] tileMap, Dictionary<String, List<LabyrinthTile>> tileGroupings, TileSheet volcano_sheet, TileSheet cave_sheet)
        {
            //get layers
            Layer backLayer = map.GetLayer("Back");
            Layer buildingLayer = map.GetLayer("Buildings");
            Layer frontLayer = map.GetLayer("Front");

            var allEndcaps = tileGroupings["bottomEndcaps"];
            allEndcaps.AddRange(tileGroupings["topEndcaps"]);
            allEndcaps.AddRange(tileGroupings["leftEndcaps"]);
            allEndcaps.AddRange(tileGroupings["rightEndcaps"]);

            if (allEndcaps.Count() > 0)
            {
                //as we move down through the labyrinth the likelyhood of 2 exits increases.
                int minimumSpots = (int)Math.Min((allEndcaps.Count() * (0.4 + (0.1 * Level))), allEndcaps.Count());
                int maximumSpots = (int)Math.Min(Math.Ceiling((allEndcaps.Count() * (0.8 + 0.1 * Level))), allEndcaps.Count());
                int treasureSpots = seeded_random.Next(minimumSpots, maximumSpots + 1);
                List<LabyrinthTile> treasureSpotsToAdd = new();

                for (int i = 0; i < treasureSpots && allEndcaps.Count() > 0; i++)
                {
                    int random_endcap = seeded_random.Next(0, allEndcaps.Count());
                    var spot = allEndcaps[random_endcap];
                    treasureSpotsToAdd.Add(spot);
                    allEndcaps.Remove(spot);
                }

                foreach (LabyrinthTile spot in treasureSpotsToAdd)
                {
                    placeTreasureSpot(spot, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                }

                tileGroupings["emptyEndcaps"] = allEndcaps;
            }
        }

        private void placeTreasureSpot(LabyrinthTile spot, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int x = (int)spot.Point.X;
            int y = (int)spot.Point.Y;
            _monitor.Log(String.Format("Add a treasure spot to [{0},{1}]", x.ToString(), y.ToString()));
            switch (spot.Walls)
            {
                case Wall.LeftEnd:
                    placeLeftEndTreasureSpot(x, y, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                    break;
                case Wall.RightEnd:
                    placeRightEndTreasureSpot(x, y, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                    break;
                case Wall.NorthEnd:
                    placeUpperEndTreasureSpot(x, y, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                    break;
                case Wall.SouthEnd:
                    placeLowerEndTreasureSpot(x, y, volcano_sheet, cave_sheet, backLayer, buildingLayer, frontLayer);
                    break;

            }
        }

        private void putDwarvenBarrelAtLocation(int x, int y)
        {
            if (seeded_random.NextBool(Math.Min(0.35 + (0.15 * Level), 1.0)))
            {
                var barrelPosition = new Vector2(x, y);
                base.objects.Add(barrelPosition, BreakableContainer.GetBarrelForVolcanoDungeon(barrelPosition));
            }
        }

        private void placeLeftEndTreasureSpot(int x, int y, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            int upOne = y - 1;
            int leftOne = x - 1;
            int leftTwo = x - 2;

            //place the right side based on the left spot.
            backLayer.Tiles[x, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleRight"]);
            backLayer.Tiles[x, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperRight"]);
            backLayer.Tiles[x, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomRight"]);
            putDwarvenBarrelAtLocation(x, y);
            putDwarvenBarrelAtLocation(x, upOne);
            putDwarvenBarrelAtLocation(x, downOne);

            //now rest of the structure is placed to the left of this.
            backLayer.Tiles[leftOne, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleCenter"]);
            backLayer.Tiles[leftOne, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperCenter"]);
            backLayer.Tiles[leftOne, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomCenter"]);
            putDwarvenBarrelAtLocation(leftOne, y);
            putDwarvenBarrelAtLocation(leftOne, upOne);
            putDwarvenBarrelAtLocation(leftOne, downOne);

            backLayer.Tiles[leftTwo, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleLeft"]);
            backLayer.Tiles[leftTwo, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperLeft"]);
            backLayer.Tiles[leftTwo, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomLeft"]);
        }

        private void placeRightEndTreasureSpot(int x, int y, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            int upOne = y - 1;
            int rightOne = x + 1;
            int rightTwo = x + 2;

            //place the right side based on the left spot.
            backLayer.Tiles[x, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleLeft"]);
            backLayer.Tiles[x, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperLeft"]);
            backLayer.Tiles[x, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomLeft"]);
            putDwarvenBarrelAtLocation(x, y);
            putDwarvenBarrelAtLocation(x, upOne);
            putDwarvenBarrelAtLocation(x, downOne);

            //now rest of the structure is placed to the left of this.
            backLayer.Tiles[rightOne, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleCenter"]);
            backLayer.Tiles[rightOne, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperCenter"]);
            backLayer.Tiles[rightOne, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomCenter"]);
            putDwarvenBarrelAtLocation(rightOne, y);
            putDwarvenBarrelAtLocation(rightOne, upOne);
            putDwarvenBarrelAtLocation(rightOne, downOne);

            backLayer.Tiles[rightTwo, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleRight"]);
            backLayer.Tiles[rightTwo, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperRight"]);
            backLayer.Tiles[rightTwo, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomRight"]);
        }

        private void placeUpperEndTreasureSpot(int x, int y, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int upOne = y - 1;
            int upTwo = y - 2;
            int rightOne = x + 1;
            int leftOne = x - 1;

            //place the right side based on the left spot.
            backLayer.Tiles[x, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomCenter"]);
            backLayer.Tiles[leftOne, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomLeft"]);
            backLayer.Tiles[rightOne, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomRight"]);
            putDwarvenBarrelAtLocation(x, y);
            putDwarvenBarrelAtLocation(leftOne, y);
            putDwarvenBarrelAtLocation(rightOne, y);

            //now rest of the structure is placed to the top of this.
            backLayer.Tiles[x, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleCenter"]);
            backLayer.Tiles[leftOne, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleLeft"]);
            backLayer.Tiles[rightOne, upOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleRight"]);
            putDwarvenBarrelAtLocation(x, upOne);
            putDwarvenBarrelAtLocation(leftOne, upOne);
            putDwarvenBarrelAtLocation(rightOne, upOne);

            backLayer.Tiles[x, upTwo] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperCenter"]);
            backLayer.Tiles[leftOne, upTwo] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperLeft"]);
            backLayer.Tiles[rightOne, upTwo] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperRight"]);
        }

        private void placeLowerEndTreasureSpot(int x, int y, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            int downTwo = y + 2;
            int rightOne = x + 1;
            int leftOne = x - 1;

            //place the right side based on the left spot.
            backLayer.Tiles[x, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperCenter"]);
            backLayer.Tiles[leftOne, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperLeft"]);
            backLayer.Tiles[rightOne, y] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["upperRight"]);
            putDwarvenBarrelAtLocation(x, y);
            putDwarvenBarrelAtLocation(leftOne, y);
            putDwarvenBarrelAtLocation(rightOne, y);

            //now rest of the structure is placed to the top of this.
            backLayer.Tiles[x, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleCenter"]);
            backLayer.Tiles[leftOne, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleLeft"]);
            backLayer.Tiles[rightOne, downOne] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["middleRight"]);
            putDwarvenBarrelAtLocation(x, downOne);
            putDwarvenBarrelAtLocation(leftOne, downOne);
            putDwarvenBarrelAtLocation(rightOne, downOne);

            backLayer.Tiles[x, downTwo] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomCenter"]);
            backLayer.Tiles[leftOne, downTwo] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomLeft"]);
            backLayer.Tiles[rightOne, downTwo] = new StaticTile(backLayer, cave_sheet, BlendMode.Alpha, treasureFloorTiles["bottomRight"]);
        }

        private LabyrinthTile[,] createFloorPlan(List<Vector2> unvisitedPoints, TileSheet sheet)
        {

            //set out the grid of node points that we'll connect to make a map.
            for (int x = startTile; x <= Width; x += LabyrinthManager.TILE_SPACING)
            {
                for (int y = startTile; y <= Height; y += LabyrinthManager.TILE_SPACING)
                {
                    //add this point to the list of unvisited points and add it with all 4 walls to the overall map.
                    //Later we'll build the real map from this representation once we know where all the walls go and such.
                    Vector2 point = new Vector2(x, y);
                    unvisitedPoints.Add(point);
                    TileMap[x, y] = new LabyrinthTile(point, Wall.All);
                }
            }

            Stack<Vector2> stack = new();

            var current = new Vector2(startTile, startTile);
            unvisitedPoints.Remove(current);

            while (unvisitedPoints.Count > 0)
            {
                var available_neighbors = getAvailableNeighbors(current, unvisitedPoints);
                if (available_neighbors.Length > 0)
                {
                    var random_neighbor = seeded_random.Next(0, available_neighbors.Length);
                    var next = available_neighbors[random_neighbor];
                    stack.Push(current);
                    var direction = next - current;
                    var currentTile = TileMap[(int)current.X, (int)current.Y];
                    var nextTile = TileMap[(int)next.X, (int)next.Y];
                    var current_new_walls = currentTile.Walls - cellDirections[direction];
                    var next_new_walls = nextTile.Walls - cellDirections[-direction];
                    currentTile.Walls = current_new_walls;
                    nextTile.Walls = next_new_walls;

                    //now connect the next point and current point.
                    var offsetTile = direction / LabyrinthManager.TILE_SPACING;
                    for (int i = 1; i < LabyrinthManager.TILE_SPACING; i++)
                    {
                        var connector = current + offsetTile * i;
                        if (direction.X != 0)
                        {
                            TileMap[(int)connector.X, (int)connector.Y] = new LabyrinthTile(connector, Wall.Horizontal);
                        }
                        else
                        {
                            TileMap[(int)connector.X, (int)connector.Y] = new LabyrinthTile(connector, Wall.Verticle);
                        }
                    }

                    current = next;
                    unvisitedPoints.Remove(current);
                }
                else if (stack.Count > 0)
                {
                    current = stack.Pop();
                }
            }

            //add wall cutouts to make the map less maze-like.
            var PercentToRemove = LabyrinthManager.PERCENT_WALLS_TO_REMOVE / 100.0;
            var wallsToRemove = (int)(size * size * PercentToRemove);

            for (int i = 1; i <= wallsToRemove; i++)
            {
                var randomX = seeded_random.Next(1, size - 1);
                var x = randomX * LabyrinthManager.TILE_SPACING + startTile;
                var randomY = seeded_random.Next(1, size - 1);
                var y = randomY * LabyrinthManager.TILE_SPACING + startTile;

                var tileToEdit = TileMap[x, y];
                var walls = tileToEdit.Walls;

                //pick a random neighbor
                var directionToDelete = seeded_random.Next(0, cellDirections.Count - 1);
                var cellDirection = cellDirections.Keys.ElementAt(directionToDelete);
                var neighborCellPosition = tileToEdit.Point + cellDirection;
                var wallNumber = cellDirections;
                var neighborCell = TileMap[(int)neighborCellPosition.X, (int)neighborCellPosition.Y];

                //the tile and neighbor share a wall.
                if ((walls & cellDirections[cellDirection]) > 0)
                {
                    var newWalls = walls - cellDirections[cellDirection];
                    var newNeighborWalls = neighborCell.Walls - cellDirections[-cellDirection];

                    tileToEdit.Walls = newWalls;
                    neighborCell.Walls = newNeighborWalls;

                    //now connect the next point and current point.
                    var offsetTile = cellDirection / LabyrinthManager.TILE_SPACING;
                    for (int j = 1; j < LabyrinthManager.TILE_SPACING; j++)
                    {
                        var connector = tileToEdit.Point + offsetTile * j;
                        if (cellDirection.X != 0)
                        {
                            TileMap[(int)connector.X, (int)connector.Y] = new LabyrinthTile(connector, Wall.Horizontal);
                        }
                        else
                        {
                            TileMap[(int)connector.X, (int)connector.Y] = new LabyrinthTile(connector, Wall.Verticle);
                        }
                    }
                }
            }

            _monitor.Log("Floorplan is constructed. Place the tiles on the map according to the plan.");
            return TileMap;
        }

        private Dictionary<String, List<LabyrinthTile>> placeMapTiles(LabyrinthTile[,] TileMap, TileSheet sheet)
        {
            //get layers
            Layer backLayer = map.GetLayer("Back");
            Layer buildingLayer = map.GetLayer("Buildings");
            Layer frontLayer = map.GetLayer("Front");

            //the whole map is now built, so lets go put tiles down in those spots and see what we have!
            Dictionary<String, List<LabyrinthTile>> tileGroupings = new();
            List<LabyrinthTile> corners = new List<LabyrinthTile>();
            tileGroupings.Add("corners", corners);
            List<LabyrinthTile> endcaps = new List<LabyrinthTile>();
            tileGroupings.Add("endcaps", endcaps);
            List<LabyrinthTile> leftEndcaps = new List<LabyrinthTile>();
            tileGroupings.Add("leftEndcaps", leftEndcaps);
            List<LabyrinthTile> rightEndcaps = new List<LabyrinthTile>();
            tileGroupings.Add("rightEndcaps", rightEndcaps);
            List<LabyrinthTile> topEndcaps = new List<LabyrinthTile>();
            tileGroupings.Add("topEndcaps", topEndcaps);
            List<LabyrinthTile> bottomEndcaps = new List<LabyrinthTile>();
            tileGroupings.Add("bottomEndcaps", bottomEndcaps);
            List<LabyrinthTile> tSplits = new List<LabyrinthTile>();
            tileGroupings.Add("tSplits", tSplits);
            List<LabyrinthTile> fourWays = new List<LabyrinthTile>();
            tileGroupings.Add("fourWays", fourWays);
            List<LabyrinthTile> connectors = new List<LabyrinthTile>();
            tileGroupings.Add("connectors", connectors);

            _monitor.Log(string.Format("Found {0} tiles to process.", TileMap.Length));

            for (int i = 0; i < TileMap.GetLength(0); i++)
            {
                for (int j = 0; j < TileMap.GetLength(1); j++)
                {
                    frontLayer.Tiles[i, j] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, darkness_tile_index);
                }
            }

            for (int i = 0; i < TileMap.GetLength(0); i++)
            {
                for (int j = 0; j < TileMap.GetLength(1); j++)
                {
                    var tile = TileMap[i, j];
                    if (tile != null)
                    {
                        if (Wall.isCorner(tile.Walls))
                            corners.Add(tile);

                        if (Wall.isEndCap(tile.Walls))
                        {
                            endcaps.Add(tile);
                            if (Wall.SouthEnd == tile.Walls)
                                bottomEndcaps.Add(tile);
                            if (Wall.NorthEnd == tile.Walls)
                                topEndcaps.Add(tile);
                            if (Wall.LeftEnd == tile.Walls)
                                leftEndcaps.Add(tile);
                            if (Wall.RightEnd == tile.Walls)
                                rightEndcaps.Add(tile);
                        }


                        if (Wall.isTJunction(tile.Walls))
                            tSplits.Add(tile);

                        if (Wall.isStraight(tile.Walls))
                            connectors.Add(tile);

                        if (Wall.None == tile.Walls)
                            fourWays.Add(tile);

                        addStandardWalls(tile, sheet, backLayer, buildingLayer, frontLayer);
                    }
                }

            }

            _monitor.Log(string.Format("Found {0} endcaps to fix.", endcaps.Count.ToString()));
            foreach (var endcap in endcaps)
            {
                addEndCap(endcap, sheet, backLayer, buildingLayer, frontLayer);
            }

            _monitor.Log(string.Format("Found {0} corners to fix.", corners.Count.ToString()));
            foreach (var corner in corners)
            {
                addCorner(corner, sheet, backLayer, buildingLayer, frontLayer);
            }

            _monitor.Log(string.Format("Found {0} tSplits to fix.", tSplits.Count.ToString()));
            foreach (var tSplit in tSplits)
            {
                addTSplit(tSplit, sheet, backLayer, buildingLayer, frontLayer);
            }

            _monitor.Log(string.Format("Found {0} fourWays to fix.", fourWays.Count.ToString()));
            foreach (var fourWay in fourWays)
            {
                addFourWayJunction(fourWay, sheet, backLayer, buildingLayer, frontLayer);
            }

            _monitor.Log("Labyrinth Level done building");
            return tileGroupings;
        }

        private void addStandardWalls(LabyrinthTile tile, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int x = (int)tile.Point.X;
            int y = (int)tile.Point.Y;

            bool hasNorth = (tile.Walls & Wall.North) == Wall.North;
            bool hasEast = (tile.Walls & Wall.East) == Wall.East;
            bool hasSouth = (tile.Walls & Wall.South) == Wall.South;
            bool hasWest = (tile.Walls & Wall.West) == Wall.West;

            //find the direction walls are in, and place floors out spacing/2 spaces to either side of the center point toward each wall.
            backLayer.Tiles[x, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, y] = null;
            //_monitor.Log(String.Format("Wall value for [{0}, {1}]: {2}", x, y, tile.Walls));
            //add tiles to the north.  Add flooring and upper walls.
            if (hasNorth)
                addNorthernWall(x, y, sheet, backLayer, buildingLayer, frontLayer);

            //add tiles to the east. Add flooring and right walls.
            if (hasEast)
                addEasternWall(x, y, sheet, backLayer, buildingLayer, frontLayer);

            //add tiles to the south. Add flooring and lower walls.
            if (hasSouth)
                addSouthernWall(x, y, sheet, backLayer, buildingLayer, frontLayer);

            //add tiles to the west. Add flooring and lower walls.
            if (hasWest)
                addWesternWall(x, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addEndCap(LabyrinthTile tile, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int x = (int)tile.Point.X;
            int y = (int)tile.Point.Y;

            if (tile.Walls == Wall.LeftEnd)
                addLeftEnd(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.RightEnd)
                addRightEnd(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.NorthEnd)
                addTopEnd(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.SouthEnd)
                addBottomEnd(x, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addCorner(LabyrinthTile tile, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {

            int x = (int)tile.Point.X;
            int y = (int)tile.Point.Y;

            if (tile.Walls == Wall.NorthEast)
                addUpperRightCorner(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.SouthWest)
                addLowerLeftCorner(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.SouthEast)
                addLowerRightCorner(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.NorthWest)
                addUpperLeftCorner(x, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addTSplit(LabyrinthTile tile, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int x = (int)tile.Point.X;
            int y = (int)tile.Point.Y;

            if (tile.Walls == Wall.LeftT)
                addLeftTJunction(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.RightT)
                addRightTJunction(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.TopT)
                addTopTJunction(x, y, sheet, backLayer, buildingLayer, frontLayer);
            if (tile.Walls == Wall.BottomT)
                addBottomTJunction(x, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addFourWayJunction(LabyrinthTile tile, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int x = (int)tile.Point.X;
            int y = (int)tile.Point.Y;

            if (tile.Walls == Wall.None)
                addFourWayJunction(x, y, sheet, backLayer, buildingLayer, frontLayer);
        }

        //straight wall sections
        private void addNorthernWall(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneUp = y - 1;
            int twoUp = y - 2;
            int threeUp = y - 3;
            int fourUp = y - 4;
            int fiveUp = y - 5;
            buildingLayer.Tiles[x, fiveUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, upper_tile_index_1);
            frontLayer.Tiles[x, fiveUp] = null;
            buildingLayer.Tiles[x, fourUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, upper_tile_index_2);
            frontLayer.Tiles[x, fourUp] = null;
            buildingLayer.Tiles[x, threeUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, upper_tile_index_3);
            frontLayer.Tiles[x, threeUp] = null;
            buildingLayer.Tiles[x, twoUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, upper_tile_index_4);
            frontLayer.Tiles[x, twoUp] = null;
            backLayer.Tiles[x, twoUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, twoUp] = null;
            backLayer.Tiles[x, oneUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, oneUp] = null;
        }
        private void addEasternWall(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneRight = x + 1;
            int twoRight = x + 2;

            backLayer.Tiles[twoRight, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[twoRight, y] = null;
            buildingLayer.Tiles[twoRight, y] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, right_tile_index);
            backLayer.Tiles[oneRight, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[oneRight, y] = null;
        }
        private void addSouthernWall(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            backLayer.Tiles[x, y + 2] = new StaticTile(backLayer, sheet, BlendMode.Alpha, darkness_tile_index);
            frontLayer.Tiles[x, y + 2] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, darkness_tile_index);
            buildingLayer.Tiles[x, y + 2] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, darkness_tile_index);
            frontLayer.Tiles[x, y + 1] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, bottom_tile_index);
            backLayer.Tiles[x, y + 1] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);

        }
        private void addWesternWall(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            backLayer.Tiles[x - 2, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            buildingLayer.Tiles[x - 2, y] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, left_tile_index);
            backLayer.Tiles[x - 1, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x - 2, y] = null;
            frontLayer.Tiles[x - 1, y] = null;
        }

        //convex corners
        private void addNorthWestCornerConcave(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneUp = y - 1;
            int twoUp = y - 2;
            int threeUp = y - 3;
            int fourUp = y - 4;
            int fiveUp = y - 5;
            buildingLayer.Tiles[x, fiveUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northWesternCorner_index_1);
            frontLayer.Tiles[x, fiveUp] = null;
            buildingLayer.Tiles[x, fourUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northWesternCorner_index_2);
            frontLayer.Tiles[x, fourUp] = null;
            buildingLayer.Tiles[x, threeUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northWesternCorner_index_3);
            frontLayer.Tiles[x, threeUp] = null;
            buildingLayer.Tiles[x, twoUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northWesternCorner_index_4);
            frontLayer.Tiles[x, twoUp] = null;
            backLayer.Tiles[x, twoUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            buildingLayer.Tiles[x, oneUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, left_tile_index);
            backLayer.Tiles[x, oneUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, oneUp] = null;

        }
        private void addNorthWestCornerConvex(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneUp = y - 1;
            int twoUp = y - 2;
            int threeUp = y - 3;
            int fourUp = y - 4;
            int fiveUp = y - 5;
            buildingLayer.Tiles[x, fiveUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northwest_convex_corner_1);
            frontLayer.Tiles[x, fiveUp] = null;
            buildingLayer.Tiles[x, fourUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northwest_convex_corner_2);
            frontLayer.Tiles[x, fourUp] = null;
            buildingLayer.Tiles[x, threeUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northwest_convex_corner_3);
            frontLayer.Tiles[x, threeUp] = null;
            buildingLayer.Tiles[x, twoUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northwest_convex_corner_4);
            backLayer.Tiles[x, twoUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, twoUp] = null;
            buildingLayer.Tiles[x, oneUp] = null;
            backLayer.Tiles[x, oneUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, oneUp] = null;
        }
        private void addNorthEastCornerConcave(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneUp = y - 1;
            int twoUp = y - 2;
            int threeUp = y - 3;
            int fourUp = y - 4;
            int fiveUp = y - 5;
            buildingLayer.Tiles[x, fiveUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northEasternCorner_index_1);
            frontLayer.Tiles[x, fiveUp] = null;
            buildingLayer.Tiles[x, fourUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northEasternCorner_index_2);
            frontLayer.Tiles[x, fourUp] = null;
            buildingLayer.Tiles[x, threeUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northEasternCorner_index_3);
            frontLayer.Tiles[x, threeUp] = null;
            buildingLayer.Tiles[x, twoUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northEasternCorner_index_4);
            backLayer.Tiles[x, twoUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, twoUp] = null;
            buildingLayer.Tiles[x, oneUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, right_tile_index);
            backLayer.Tiles[x, oneUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, oneUp] = null;
        }
        private void addNorthEastCornerConvex(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneUp = y - 1;
            int twoUp = y - 2;
            int threeUp = y - 3;
            int fourUp = y - 4;
            int fiveUp = y - 5;
            buildingLayer.Tiles[x, fiveUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northeast_convex_corner_1);
            frontLayer.Tiles[x, fiveUp] = null;
            buildingLayer.Tiles[x, fourUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northeast_convex_corner_2);
            frontLayer.Tiles[x, fourUp] = null;
            buildingLayer.Tiles[x, threeUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northeast_convex_corner_3);
            frontLayer.Tiles[x, threeUp] = null;
            buildingLayer.Tiles[x, twoUp] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, northeast_convex_corner_4);
            backLayer.Tiles[x, twoUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, twoUp] = null;
            buildingLayer.Tiles[x, oneUp] = null;
            backLayer.Tiles[x, oneUp] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            frontLayer.Tiles[x, oneUp] = null;
        }

        //concave corners
        private void addSouthWestCornerConcave(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            buildingLayer.Tiles[x, y + 1] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, southWesternCorner);
            frontLayer.Tiles[x, y + 1] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, southWesternCorner);
            backLayer.Tiles[x, y + 1] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
        }
        private void addSouthEastCornerConcave(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            buildingLayer.Tiles[x, y + 1] = new StaticTile(buildingLayer, sheet, BlendMode.Alpha, southEasternCorner);
            frontLayer.Tiles[x, y + 1] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, southEasternCorner);
            backLayer.Tiles[x, y + 1] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
        }
        private void addSouthWestCornerConvex(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            backLayer.Tiles[x, downOne] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            buildingLayer.Tiles[x, downOne] = null;
            frontLayer.Tiles[x, downOne] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, southwest_convex_corner);

            int downTwo = downOne + 1;
            frontLayer.Tiles[x, downTwo] = null;
            backLayer.Tiles[x, downTwo] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            buildingLayer.Tiles[x, downTwo] = new StaticTile(backLayer, sheet, BlendMode.Alpha, southwest_convex_corner_bottom);
        }
        private void addSouthEastCornerConvex(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            backLayer.Tiles[x, downOne] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            buildingLayer.Tiles[x, downOne] = null;
            frontLayer.Tiles[x, downOne] = new StaticTile(frontLayer, sheet, BlendMode.Alpha, southeast_convex_corner);

            int downTwo = downOne + 1;
            frontLayer.Tiles[x, downTwo] = null;
            backLayer.Tiles[x, downTwo] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
            buildingLayer.Tiles[x, downTwo] = new StaticTile(backLayer, sheet, BlendMode.Alpha, southeast_convex_corner_bottom);
        }

        //clear extra crap added to the floor
        private void clearWestFloor(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            for (int i = 1; i <= 2; i++)
            {
                int offset = x - i;
                backLayer.Tiles[offset, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
                buildingLayer.Tiles[offset, y] = null;
                frontLayer.Tiles[offset, y] = null;
            }
        }
        private void clearEastFloor(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            for (int i = 1; i <= 2; i++)
            {
                int offset = x + i;
                backLayer.Tiles[offset, y] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
                buildingLayer.Tiles[offset, y] = null;
                frontLayer.Tiles[offset, y] = null;
            }
        }
        private void clearNorthFloor(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            for (int i = 1; i <= 5; i++)
            {
                int offset = y - i;
                backLayer.Tiles[x, offset] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
                buildingLayer.Tiles[x, offset] = null;
                frontLayer.Tiles[x, offset] = null;
            }
        }
        private void clearSouthFloor(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            for (int i = 1; i <= 2; i++)
            {
                int offset = y + i;
                backLayer.Tiles[x, offset] = new StaticTile(backLayer, sheet, BlendMode.Alpha, floor_tile_index);
                buildingLayer.Tiles[x, offset] = null;
                frontLayer.Tiles[x, offset] = null;
            }
        }

        /// <summary>
        /// Takes an input tile (the center position tile) and builds an endcap with west, north and east walls.
        /// </summary>
        /// <param name="x">X coordinate of the center tile.</param>
        /// <param name="y">Y coordinate of the center tile.</param>
        /// <param name="sheet">Tilesheet to use.</param>
        /// <param name="backLayer">The background layer of the map</param>
        /// <param name="buildingLayer">The building layer of the map</param>
        /// <param name="frontLayer">The front layer of the map.</param>
        private void addTopEnd(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            //go up one from the starting point.
            int upOne = y - 1;
            addWesternWall(x, upOne, sheet, backLayer, buildingLayer, frontLayer);
            addEasternWall(x, upOne, sheet, backLayer, buildingLayer, frontLayer);

            //go to the left and right and build northern walls.
            addNorthernWall(x - 1, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthernWall(x + 1, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthWestCornerConcave(x - 2, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthEastCornerConcave(x + 2, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addBottomEnd(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            //go down one from the starting point.
            int downOne = y + 1;
            addWesternWall(x, downOne, sheet, backLayer, buildingLayer, frontLayer);
            addEasternWall(x, downOne, sheet, backLayer, buildingLayer, frontLayer);

            //go to the left and right and build southern walls.
            addSouthernWall(x - 1, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthernWall(x + 1, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthWestCornerConcave(x - 2, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthEastCornerConcave(x + 2, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addRightEnd(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            //go right one from the starting point.
            int rightOne = x + 1;
            addNorthernWall(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthernWall(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);

            //now go right again and add the corners.
            int rightTwo = rightOne + 1;
            addSouthEastCornerConcave(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthEastCornerConcave(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addLeftEnd(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            //go left one from the starting point.
            int leftOne = x - 1;
            addNorthernWall(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthernWall(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);

            //now go left again and add the corners.
            int leftTwo = leftOne - 1;
            addSouthWestCornerConcave(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthWestCornerConcave(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }

        //add corners to the map
        private void addUpperRightCorner(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            //go right one from the starting point.
            int rightOne = x + 1;
            addNorthernWall(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            int rightTwo = rightOne + 1;
            addNorthEastCornerConcave(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);

            //clean up the tile to the left of the center, just in case.  Other tiles could have caused them to be blocked in a corner.
            int leftOne = x - 1;
            clearSouthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);

            int leftTwo = leftOne - 1;
            addSouthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addUpperLeftCorner(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            //go right one from the starting point.
            int rightOne = x + 1;
            int rightTwo = rightOne + 1;
            int leftOne = x - 1;
            int leftTwo = leftOne - 1;

            addNorthernWall(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthWestCornerConcave(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);

            //clean up the tile to the right of the center, just in case.  Other tiles could have caused them to be blocked in a corner.            
            clearSouthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addLowerLeftCorner(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int rightOne = x + 1;
            clearNorthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            int rightTwo = rightOne + 1;
            addNorthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);

            int leftOne = x - 1;
            addSouthernWall(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);

            int leftTwo = leftOne - 1;
            addSouthWestCornerConcave(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addLowerRightCorner(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int leftOne = x - 1;
            clearNorthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);
            int leftTwo = leftOne - 1;
            addNorthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);

            int rightOne = x + 1;
            addSouthernWall(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);

            int rightTwo = rightOne + 1;
            addSouthEastCornerConcave(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }

        //t junctions
        private void addTopTJunction(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int rightOne = x + 1;
            int rightTwo = rightOne + 1;
            int leftOne = x - 1;
            int leftTwo = leftOne - 1;

            clearSouthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearSouthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);

            addSouthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addBottomTJunction(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int rightOne = x + 1;
            int rightTwo = rightOne + 1;
            int leftOne = x - 1;
            int leftTwo = leftOne - 1;

            clearNorthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearNorthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);

            addNorthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addLeftTJunction(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int rightOne = x + 1;
            int rightTwo = rightOne + 1;

            clearNorthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearSouthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);

            addNorthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }
        private void addRightTJunction(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int leftOne = x - 1;
            int leftTwo = leftOne - 1;

            clearNorthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearSouthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);

            addNorthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }

        private void addFourWayJunction(int x, int y, TileSheet sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int leftOne = x - 1;
            int leftTwo = leftOne - 1;
            int rightOne = x + 1;
            int rightTwo = rightOne + 1;

            clearNorthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearNorthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearSouthFloor(leftOne, y, sheet, backLayer, buildingLayer, frontLayer);
            clearSouthFloor(rightOne, y, sheet, backLayer, buildingLayer, frontLayer);


            addNorthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addNorthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthWestCornerConvex(leftTwo, y, sheet, backLayer, buildingLayer, frontLayer);
            addSouthEastCornerConvex(rightTwo, y, sheet, backLayer, buildingLayer, frontLayer);
        }

        private void addEntrance(int x, int y, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int oneUp = y - 1;
            int twoUp = y - 2;
            int threeUp = y - 3;
            int fourUp = y - 4;
            int fiveUp = y - 5;

            int upperstair_4 = 268;
            int upperstair_3 = 284;
            int upperstair_2 = 300;
            int upperstair_1 = 316;

            buildingLayer.Tiles[x, fiveUp] = new StaticTile(buildingLayer, cave_sheet, BlendMode.Alpha, darkness_tile_index);
            frontLayer.Tiles[x, fiveUp] = null;
            backLayer.Tiles[x, fourUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_4);
            buildingLayer.Tiles[x, fourUp] = new StaticTile(buildingLayer, volcano_sheet, BlendMode.Alpha, upperstair_4);
            frontLayer.Tiles[x, fourUp] = null;
            backLayer.Tiles[x, threeUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_3);
            buildingLayer.Tiles[x, threeUp] = null;
            frontLayer.Tiles[x, threeUp] = null;

            backLayer.Tiles[x, twoUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_2);
            backLayer.Tiles[x + 1, twoUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_2);
            backLayer.Tiles[x - 1, twoUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_2);
            buildingLayer.Tiles[x, twoUp] = null;
            frontLayer.Tiles[x, twoUp] = null;

            backLayer.Tiles[x, oneUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_1);
            buildingLayer.Tiles[x, oneUp] = null;
            frontLayer.Tiles[x, oneUp] = null;
            backLayer.Tiles[x + 1, oneUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_1);
            backLayer.Tiles[x - 1, oneUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_1);
            backLayer.Tiles[x + 2, oneUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_1);
            backLayer.Tiles[x - 2, oneUp] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, upperstair_1);
        }

        private void addExit(int x, int y, TileSheet volcano_sheet, TileSheet cave_sheet, Layer backLayer, Layer buildingLayer, Layer frontLayer)
        {
            int downOne = y + 1;
            int downTwo = y + 2;
            int downThree = y + 3;

            int lowerstair_2 = 286;
            int lowerstair_1 = 302;

            backLayer.Tiles[x, downThree] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, darkness_tile_index);
            buildingLayer.Tiles[x, downThree] = new StaticTile(buildingLayer, cave_sheet, BlendMode.Alpha, darkness_tile_index);
            frontLayer.Tiles[x, downThree] = null;

            backLayer.Tiles[x, downTwo] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_1);
            backLayer.Tiles[x + 1, downTwo] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_1);
            backLayer.Tiles[x - 1, downTwo] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_1);
            buildingLayer.Tiles[x, downTwo] = null;
            frontLayer.Tiles[x, downTwo] = null;

            backLayer.Tiles[x, downOne] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_2);
            buildingLayer.Tiles[x, downOne] = null;
            frontLayer.Tiles[x, downOne] = null;
            backLayer.Tiles[x + 1, downOne] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_2);
            backLayer.Tiles[x - 1, downOne] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_2);
            backLayer.Tiles[x + 2, downOne] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_2);
            backLayer.Tiles[x - 2, downOne] = new StaticTile(backLayer, volcano_sheet, BlendMode.Alpha, lowerstair_2);
        }

        private Vector2[] getAvailableNeighbors(Vector2 current, List<Vector2> unvisited)
        {
            List<Vector2> neighbors = new List<Vector2>();

            foreach (var cell in cellDirections.Keys)
            {
                if (unvisited.Contains(current + cell))
                {
                    neighbors.Add(current + cell);
                }
            }
            return neighbors.ToArray();
        }


        protected override void resetLocalState()
        {
            base.resetLocalState();

            foreach (var lightSource in lightSources.Values)
            {
                Game1.currentLightSources.Add(lightSource);
            }
        }

        public override void UpdateWhenCurrentLocation(GameTime time)
        {
            base.UpdateWhenCurrentLocation(time);
            bool num = Game1.currentLocation == this;
            /*
            if (num)
            {
                this.fogPos = Game1.updateFloatingObjectPositionForMovement(current: new Vector2(Game1.viewport.X, Game1.viewport.Y), w: this.fogPos, previous: Game1.previousViewportPosition, speed: -1f);
                this.fogPos.X = (this.fogPos.X + 0.5f) % 256f;
                this.fogPos.Y = (this.fogPos.Y + 0.5f) % 256f;
            }
            */
        }



        public override void drawAboveAlwaysFrontLayer(SpriteBatch b)
        {
            base.drawAboveAlwaysFrontLayer(b);
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            /*
            Vector2 v = default(Vector2);
            for (float x = -256 + (int)(this.fogPos.X % 256f); x < (float)Game1.graphics.GraphicsDevice.Viewport.Width; x += 256f)
            {
                for (float y = -256 + (int)(this.fogPos.Y % 256f); y < (float)Game1.graphics.GraphicsDevice.Viewport.Height; y += 256f)
                {
                    v.X = (int)x;
                    v.Y = (int)y;
                    b.Draw(Game1.mouseCursors, v, this.fogSource, this.fogColor, 0f, Vector2.Zero, 4.001f, SpriteEffects.None, 1f);
                }
            }
            */
            if (Game1.game1.takingMapScreenshot)
            {
                return;
            }
            Color col = SpriteText.color_Purple;
            string txt = this.Level.ToString() ?? "";
            Microsoft.Xna.Framework.Rectangle tsarea = Game1.game1.GraphicsDevice.Viewport.GetTitleSafeArea();
            int height = SpriteText.getHeightOfString(txt);
            SpriteText.drawString(b, txt, tsarea.Left + 16, tsarea.Top + 16, 999999, -1, height, 1f, 1f, junimoText: false, 2, "", col);
            int text_width = SpriteText.getWidthOfString(txt);
        }

        private void spawnFeatures(LabyrinthTile[,] tileMap)
        {
            foreach (var tile in tileMap)
            {
                if (tile != null && !Wall.isEndCap(tile.Walls))
                {
                    int random = seeded_random.Next(0, 99);

                    if (random < 3)
                    {
                        spawnTree(tile, "7");
                    }
                    else if (random < 9)
                    {
                        spawnTree(tile, "mytigio.DarkestDepthsAssets_Tree_Voidshroom");
                    }
                    else if (random < 13)
                    {
                        spawnTree(tile, "mytigio.DarkestDepthsAssets_Tree_Glowshroom");
                    }
                    else if (random < 16)
                    {
                        spawnTree(tile, "mytigio.DarkestDepthsAssets_Tree_Brightshroom");
                    }
                    else if (random < 20 + Level)
                    {
                        spawnMonster(tile);
                    }
                    else
                    {
                        spawnResourceOrStone(tile, random);
                    }
                }

            }
        }


        private void spawnTree(LabyrinthTile tile, string type)
        {

            Vector2 position = pickDirection(tile);

            if (!base.IsTileOccupiedBy(position))
            {
                int treeSize = seeded_random.Next(1, 6);
                if (treeSize >= 2)
                {
                    Tree newTree = new Tree(type, treeSize);
                    terrainFeatures.Add(position, newTree);

                    var treeData = newTree.GetData();
                    if (treeSize >= 4 && (treeData?.CustomFields?.ContainsKey("mytigio.DarkestDepthsAssets_Glow_Color") ?? false))
                    {
                        string colorString = treeData.CustomFields["mytigio.DarkestDepthsAssets_Glow_Color"];
                        Color color;
                        try
                        {
                            string[] colors = colorString.Split(' ');
                            int r = this.indoorLightingColor.R - byte.Parse(colors[0]);
                            int g = this.indoorLightingColor.G - byte.Parse(colors[1]);
                            int b = this.indoorLightingColor.B - byte.Parse(colors[2]);
                            int a = this.indoorLightingColor.A - byte.Parse(colors[3]);

                            color = new Color(r, g, b, 255);
                        }
                        catch
                        {
                            color = Color.PaleGoldenrod;
                        }

                        float x = (position.X * 64 + 32);
                        float y = (position.Y * 64 + 32);

                        this.lightSources.Add(position, new LightSource(LightSource.sconceLight, new Vector2(x, y), 0.2f * (treeSize * treeSize), color));
                    }
                }
            }
        }

        private void spawnTreasture(LabyrinthTile tile)
        {

        }

        private void spawnMonster(LabyrinthTile tile)
        {
            Vector2 position = pickDirection(tile);
            if (!base.IsTileOccupiedBy(position, CollisionMask.All, CollisionMask.All))
            {
                Monster monster = pickMonster();
                monster.setTilePosition(position.ToPoint());
                characters.Add(monster);
            }
        }

        private void spawnResourceOrStone(LabyrinthTile tile, int random)
        {
            Vector2 position = pickDirection(tile);
            if (!base.IsTileOccupiedBy(position))
            {
                String choice = "";
                bool isSpawned = false;
                
                if (random >= 98)
                {
                    //glowcap
                    isSpawned = true;
                    choice = "mytigio.DarkestDepthsAssets_Voidcap";
                }
                else if (random >= 97)
                {
                    isSpawned = true;
                    choice = "mytigio.DarkestDepthsAssets_Glowcap";
                }
                else if (random >= 96)
                {
                    isSpawned = true;
                    choice = "mytigio.DarkestDepthsAssets_Brightcap";
                }
                else if (random >= 95)
                {
                    //gem node
                    choice = "44";
                }
                else if (random >= 94)
                {
                    //iridium ore node.
                    choice = "765";
                }
                else if (random >= 92)
                {
                    //gold ore node.
                    choice = "764";
                }
                else if (random >= 81)
                {
                    //iron ore node.
                    choice = "290";
                }
                else if (random >= 87)
                {
                    //coper ore node.
                    choice = "751";
                }
                else if (random >= 77)
                {
                    choice = "670";
                }
                else if (random >= 72)
                {
                    choice = "404";
                    isSpawned = true;
                }
                else if (random >= 65)
                {
                    List<String> options = new List<String>() {
                        "343",
                        "450",
                        "760",
                        "762",
                    };
                    choice = options[random % options.Count];
                }

                if (!String.IsNullOrEmpty(choice))
                {
                    StardewValley.Object o = new StardewValley.Object(choice, 1);
                    o.IsSpawnedObject = isSpawned;
                    this.objects.Add(position, o);
                }
            }
        }

        private Monster pickMonster()
        {
            int random = seeded_random.Next(0, 99);

            int min_difficulty_slider = Level;
            int max_difficult_slider = Level * 2;
            int base_difficulty_slider = seeded_random.Next(min_difficulty_slider, max_difficult_slider);

            Monster monster;
            if (random < 10)
            {
                monster = new RockCrab(Vector2.Zero, "Lava Crab");
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
            }
            else if (random < 15)
            {
                monster = new RockCrab(Vector2.Zero, "Iridium Crab");
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
            }
            else if (random < 25)
            {
                monster = new RockGolem(Vector2.Zero);
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
                
            }
            else if (random < 30)
            {
                monster = new ShadowBrute(Vector2.Zero);
                monster.BuffForAdditionalDifficulty(base_difficulty_slider + 1);
            }
            else if (random < 40)
            {
                monster = new ShadowShaman(Vector2.Zero);
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
            }
            else if (random < 60)
            {
                monster = new ShadowBrute(Vector2.Zero);
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
            }
            else if (random < 75)
            {
                monster = new Shooter(Vector2.Zero);
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
            }
            else if (random < 90)
            {
                monster = createEnhancedGolem(base_difficulty_slider, "Shadow Golem");
            }
            else if (random < 93)
            {
                monster = createFalseMushroomCap(base_difficulty_slider, "False Voidcap", "(O)mytigio.DarkestDepthsAssets_Voidcap");
            }
            else if (random < 95)
            {
                monster = createFalseMushroomCap(base_difficulty_slider, "False Glowcap", "(O)mytigio.DarkestDepthsAssets_Glowcap");
            }
            else if (random < 97)
            {
                monster = createFalseMushroomCap(base_difficulty_slider, "False Brightcap", "(O)mytigio.DarkestDepthsAssets_Brightcap");
            }
            else
            {
                monster = new Skeleton(Vector2.Zero, true);
                monster.BuffForAdditionalDifficulty(base_difficulty_slider);
            }

            return monster;
        }

        private Monster createFalseMushroomCap(int difficulty_slider, string name, string drop)
        {
            Monster monster = new RockCrab(Vector2.Zero, "False Magma Cap");
            monster.Name = name;
            monster.Sprite = new AnimatedSprite("Characters\\Monsters\\" + monster.Name, 0, 16, 24);
            monster.DamageToFarmer += 5;
            monster.ExperienceGained += 5;
            monster.objectsToDrop.Clear();
            monster.objectsToDrop.Add(drop);

            monster.BuffForAdditionalDifficulty(difficulty_slider);

            return monster;
        }

        private Monster createEnhancedGolem(int difficulty_slider, String golemName = "Iridium Golem")
        {
            Monster monster = new RockGolem(Vector2.Zero);
            monster.Name = golemName;
            monster.Sprite = new AnimatedSprite("Characters\\Monsters\\" + monster.Name, 0, 16, 24);
            monster.IsWalkingTowardPlayer = false;
            monster.Slipperiness = 3;
            monster.HideShadow = true;
            monster.jitteriness.Value = 0.0;
            monster.Speed *= 2;
            monster.Health += 400;
            monster.MaxHealth += 400;
            monster.DamageToFarmer += 10;
            monster.ExperienceGained += 10;

            String itemToAdd = "337";

            if (Game1.random.NextDouble() < 0.03)
            {
                monster.objectsToDrop.Add(itemToAdd);
            }

            if (Game1.random.NextDouble() < 0.03)
            {
                monster.objectsToDrop.Add(itemToAdd);
            }

            monster.BuffForAdditionalDifficulty(difficulty_slider);

            monster.Sprite.currentFrame = 16;
            monster.Sprite.loop = false;
            monster.Sprite.UpdateSourceRect();

            return monster;
        }

        private Vector2 pickDirection(LabyrinthTile tile)
        {
            List<int> wallDirections = new();

            wallDirections.Add(0);
            if ((tile.Walls & Wall.North) == Wall.North)
                wallDirections.Add(Wall.North);
            if ((tile.Walls & Wall.East) == Wall.East)
                wallDirections.Add(Wall.East);
            if ((tile.Walls & Wall.South) == Wall.South)
                wallDirections.Add(Wall.South);
            if ((tile.Walls & Wall.West) == Wall.West)
                wallDirections.Add(Wall.West);
            if ((tile.Walls & Wall.NorthWest) == Wall.NorthWest)
                wallDirections.Add(Wall.NorthWest);
            if ((tile.Walls & Wall.NorthEast) == Wall.NorthEast)
                wallDirections.Add(Wall.NorthEast);
            if ((tile.Walls & Wall.SouthWest) == Wall.SouthWest)
                wallDirections.Add(Wall.SouthWest);
            if ((tile.Walls & Wall.SouthEast) == Wall.SouthEast)
                wallDirections.Add(Wall.SouthEast);


            int direction = wallDirections[seeded_random.Next(0, wallDirections.Count)];

            if (direction > 0)
            {
                return tile.Point + inverseCellDirections[direction] / LabyrinthManager.TILE_SPACING;
            }
            else
            {
                return tile.Point;
            }
        }
    }
}
