using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.LocationContexts;
using Netcode;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using static DarkestDepths.Helpers.DataHelper;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using DarkestDepths.Helpers;
using System.Reflection.Emit;
using System.Threading;
using StardewValley.GameData.WildTrees;
using StardewValley.TerrainFeatures;

namespace DarkestDepths.Labyrinth
{
    internal class LabyrinthManager
    {
        public const string CONTEXT_NAME = "mytigio.DarkestDepths_Context_Labyrinth";
        public const string BASE_CAMP_NAME = "mytigio.DarkestDepthsAssets.Location.BaseCamp";
        public const string PLAYER_TENT_NAME = "mytigio.DarkestDepthsAssets_Location.PlayerTent";
        public const string NIKO_TENT_NAME = "mytigio.DarkestDepthsAssets_Location.NikoTent";
        public const string JAKAN_TENT_NAME = "mytigio.DarkestDepthsAssets_Location.JakanTent";


        public const string SEED_FIELD_NAME = "Base_Seed";
        public const string DAILY_SEED_NAME = "Daily_Seed";

        public const string DARK_MINES_TILESHEET = "";
        public const string VOLCANO_TILESHEET = "volcano_cavern_tilesheet";

        public static LabyrinthManager? me;
        public static NetInt gameSeed = new NetInt(0);
        public static NetInt dailySeed = new NetInt(0);

        public static NetBool isDayResetting = new NetBool(false);

        public static bool IsDayResetting
        {
            get
            {
                return isDayResetting.Value;
            }
            set
            {
                isDayResetting.Value = value;
            }
        }

        public static bool DrawLighting { get; set; } = false;

        private Random seeded_rng;

        public const int MINIMUM_MAP_SIZE = 10; //15
        public const int MAXIMUM_MAP_SIZE = 20; //35        
        public const int TILE_SPACING = 13;
        public const int PERCENT_WALLS_TO_REMOVE = 15;
        public const int MAX_EXITS = 2;

        private LabyrinthManager()
        {
            int baseSeed = Game1.random.Next(int.MaxValue / 2);
            gameSeed.Value = baseSeed;
            dailySeed.Value = 0;

            var todaySeed = generateDailySeed();
            dailySeed.Value = todaySeed;
        }

        private LabyrinthManager(int gameSeed, int dailySeed)
        {
            LabyrinthManager.gameSeed.Value = gameSeed;
            LabyrinthManager.dailySeed.Value = dailySeed;
        }

        public static int GameSeed
        {
            get
            {
                return gameSeed.Value;
            }
        }

        public static int DailySeed
        {
            get
            {
                return dailySeed.Value;
            }
        }

        public static LabyrinthManager getManager()
        {
            if (me == null)
            {
                me = new LabyrinthManager();
            }

            return me;
        }

        public static LabyrinthManager rebuildAfterSave(int gameSeed, int dailySeed)
        {
            if (me == null)
            {
                me = new LabyrinthManager(gameSeed, dailySeed);
            }

            return me;
        }

        static public LocationContextData buildContext(string id_base)
        {
            if (me == null)
            {
                me = new LabyrinthManager();
            }

            LocationContextData labyrinthContext = new LocationContextData();

            labyrinthContext.AllowRainTotem = false;

            //passout data for the labyrinth context.
            labyrinthContext.MaxPassOutCost = 0;
            ReviveLocation baseCamp = new ReviveLocation();
            baseCamp.Id = LabyrinthManager.BASE_CAMP_NAME;
            baseCamp.Location = LabyrinthManager.BASE_CAMP_NAME;
            baseCamp.Position = new Point(35, 40);

            ReviveLocation playerTent = new ReviveLocation();
            baseCamp.Id = LabyrinthManager.PLAYER_TENT_NAME;
            baseCamp.Location = LabyrinthManager.PLAYER_TENT_NAME;
            baseCamp.Position = new Point(4, 4);

            labyrinthContext.PassOutLocations = new List<ReviveLocation>() { baseCamp };
            labyrinthContext.ReviveLocations = new List<ReviveLocation>() { baseCamp };

            //music and sound info for this region.
            labyrinthContext.DefaultMusic = "LavaMine";
            labyrinthContext.DayAmbience = "Lava_Ambient";
            labyrinthContext.NightAmbience = "darkCaveLoop";

            labyrinthContext.CustomFields = new Dictionary<string, string>()
            {
                {SEED_FIELD_NAME, gameSeed.Value.ToString()},
                {DAILY_SEED_NAME, dailySeed.Value.ToString() }
            };

            return labyrinthContext;
        }

        public static int regenerateDailySeed()
        {
            if (me == null)
            {
                me = new LabyrinthManager();
            }
            //mod this thing on max value.
            return generateDailySeed();
        }

        private static int generateDailySeed()
        {
            long input = (gameSeed.Value) + (dailySeed.Value);
            int finalInput = (int)(input % int.MaxValue);
            return new Random(finalInput).Next();
        }

        static Dictionary<String, LabyrinthLocation> current_labyrinth_levels = new();

        public static void RegisterLabyrinthEvents()
        {
            MyHelper.Events.Player.Warped += LabyrinthManager.OnWarped;
            MyHelper.Events.GameLoop.DayEnding += LabyrinthManager.OnDayEnding;
            MyHelper.Events.GameLoop.DayStarted += LabyrinthManager.OnDayStarted;
            MyHelper.Events.World.TerrainFeatureListChanged += LabyrinthManager.OnTerrainFeatureListchanged;
            GameLocation.RegisterTouchAction("Enter_Labyrinth", LabyrinthManager.EnterLabyrinth);
            GameLocation.RegisterTouchAction("Labyrinth_Entrance", LabyrinthManager.WarpUpLevel);
            GameLocation.RegisterTouchAction("Labyrinth_Exit", LabyrinthManager.WarpDownLevel);
        }

        public static void EnterLabyrinth(GameLocation location, string[] args, Farmer farmer, Vector2 tile)
        {
            LabyrinthLocation initial_level;
            if (current_labyrinth_levels.ContainsKey("base"))
            {
                initial_level = current_labyrinth_levels["base"];
            }
            else
            {
                MyMonitor.Log("Base Labyrinth level not generated.", LogLevel.Error);
                return;
            }

            int x = initial_level.entrancePosition.X;
            int y = initial_level.entrancePosition.Y;

            Game1.warpFarmer(initial_level.Name, x, y, 2);
        }

        public static void WarpUpLevel(GameLocation location, string[] args, Farmer farmer, Vector2 tile)
        {
            if (location is LabyrinthLocation)
            {
                var labyrinthLevel = (LabyrinthLocation)location;

                if (labyrinthLevel.Level == 0)
                {
                    //current labyrinth level is 0.  Warp to the base camp.
                    Game1.warpFarmer(LabyrinthManager.BASE_CAMP_NAME, 26, 56, 0);
                    MyMonitor.Log("Still have " + Game1.locations.Count() + " locations in the game.");
                }
                else
                {
                    int x = labyrinthLevel.parentExitPoint.X;
                    int y = labyrinthLevel.parentExitPoint.Y;
                    Game1.warpFarmer(labyrinthLevel.ParentLevel, x, y, 1);
                }

                MyMonitor.Log("Farmer invoked warp on " + labyrinthLevel.Name);
                MyMonitor.Log("Farmer: " + farmer.Name);
                MyMonitor.Log("Tile touched:" + tile.ToPoint().ToString());
            }
        }

        public static void WarpDownLevel(GameLocation location, string[] args, Farmer farmer, Vector2 tile)
        {
            if (location is LabyrinthLocation)
            {
                Point activationTile = tile.ToPoint();
                var labyrinthLevel = (LabyrinthLocation)location;
                bool exitFound = false;
                int i = 0;
                LabyrinthExit matchingExit = null;
                while (!exitFound && i < labyrinthLevel.exits.Count)
                {
                    var exit = labyrinthLevel.exits[i];
                    Point exitPoint = exit.Position;

                    if (Math.Abs(activationTile.X - exitPoint.X) <= 3 && Math.Abs(activationTile.X - exitPoint.X) <= 3)
                    {
                        matchingExit = exit;
                        exitFound = true;
                    }
                    i++;
                }

                if (exitFound && matchingExit != null)
                {
                    String newLevelName = LabyrinthLocation.buildLabyrinthName(labyrinthLevel, matchingExit, DailySeed);
                    if (current_labyrinth_levels.ContainsKey(newLevelName))
                    {
                        var nextLevel = current_labyrinth_levels[newLevelName];
                        Game1.warpFarmer(newLevelName, nextLevel.entrancePosition.X, nextLevel.entrancePosition.Y, 2);
                        return;
                    }
                    else
                    {
                        MyMonitor.Log("Error. Level " + newLevelName + " not found.", LogLevel.Error);
                    }
                }
                else
                {
                    MyMonitor.Log("Error. No matching exit for tile [" + activationTile.X.ToString() + "," + activationTile.Y.ToString() + "] found", LogLevel.Error);
                }

            }
        }

        public static void InitializeBaseLevel()
        {
            LabyrinthLocation initial_level;
            if (current_labyrinth_levels.ContainsKey("base"))
            {
                MyMonitor.Log("Warped to base camp. First maze level already exists.");
                initial_level = current_labyrinth_levels["base"];
            }
            else
            {
                MyMonitor.Log("Warped to base camp. Build first maze level");
                initial_level = new LabyrinthLocation("base", MyMonitor);
                Game1.locations.Add(initial_level);
                current_labyrinth_levels["base"] = initial_level;
            }
        }

        public static void OnWarped(object sender, WarpedEventArgs e)
        {
            if (Game1.IsMasterGame && !LabyrinthManager.IsDayResetting)
            {
                if (e.NewLocation.Name == LabyrinthManager.BASE_CAMP_NAME)
                {
                    LabyrinthManager.InitializeBaseLevel();
                }
                if (e.NewLocation is LabyrinthLocation)
                {
                    MyMonitor.Log(e.Player.Name + " warped to new a labyrinth level.");
                    MyMonitor.Log("Build next layer of maze levels.");
                    LabyrinthLocation newLevel = e.NewLocation as LabyrinthLocation;
                    List<LabyrinthLocation> nextLevels = new List<LabyrinthLocation>();

                    foreach (LabyrinthExit exit in newLevel.exits)
                    {
                        String newLevelName = LabyrinthLocation.buildLabyrinthName(newLevel, exit, DailySeed);
                        if (!LabyrinthManager.current_labyrinth_levels.ContainsKey(newLevelName))
                        {
                            LabyrinthLocation location = new LabyrinthLocation(newLevel, exit, MyMonitor);
                            Game1.locations.Add(location);
                            LabyrinthManager.current_labyrinth_levels.Add(location.Name, location);
                        }
                    }
                }
            }
        }

        public static void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            LabyrinthManager.IsDayResetting = true;
            MyMonitor.Log("Need to clear out " + LabyrinthManager.current_labyrinth_levels.Count + " Labyrinth levels.");
            foreach (var level in LabyrinthManager.current_labyrinth_levels.Values)
            {
                MyMonitor.Log("Delete " + level.Name + " from Game Locations");
                Game1.locations.Remove(level);
            }

            MyMonitor.Log("Clear all labyrinth levels from the manager.");
            LabyrinthManager.current_labyrinth_levels.Clear();
        }

        public static void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            LabyrinthManager.IsDayResetting = false;
            LabyrinthManager.regenerateDailySeed();
            foreach (var farmer in Game1.getAllFarmers())
            {
                if (farmer.currentLocation.Name == BASE_CAMP_NAME)
                {
                    LabyrinthManager.InitializeBaseLevel();
                }
            }
        }

        public static void OnTerrainFeatureListchanged(object sender, TerrainFeatureListChangedEventArgs e)
        {
            if (e.Location is LabyrinthLocation)
            {
                var labyrinthLevel = (LabyrinthLocation)e.Location;
                foreach(var removed in e.Removed)
                {
                    var feature = removed.Value;
                    if (feature is Tree)
                    {
                        Tree tree = (Tree)feature;
                        var treeData = tree.GetData();
                        if (treeData?.CustomFields?.ContainsKey("mytigio.DarkestDepthsAssets_Glow_Color") ?? false)
                        {
                            if (labyrinthLevel?.lightSources?.ContainsKey(removed.Key) ?? false)
                            {
                                MyMonitor.Log($"Remove the glow color at tile: [{removed.Key.X.ToString()},{removed.Key.Y.ToString()}]");
                                var lightSourceToRemove = labyrinthLevel.lightSources[removed.Key];
                                Game1.currentLightSources.Remove(lightSourceToRemove);
                                labyrinthLevel.lightSources.Remove(removed.Key);
                            }
                            
                        }
                    }
                }
            }
        }

        public static void forceCleanupLevels()
        {
            List<GameLocation> locationsToDelete = new List<GameLocation>();
            foreach (var location in Game1.locations)
            {
                if (location is LabyrinthLocation)
                {
                    locationsToDelete.Add(location);
                }
            }

            MyMonitor.Log("Found " + locationsToDelete.Count + " locations to delete.");

            foreach (var location in locationsToDelete)
            {
                Game1.locations.Remove(location);
            }

            LabyrinthManager.current_labyrinth_levels.Clear();
        }
    }
}
