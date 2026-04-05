using DarkestDepths.Helpers;
using DarkestDepths.Labyrinth;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.LocationContexts;
using System.Threading;
using xTile.Format;

namespace DarkestDepths
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        LocationContextData? labyrinthContext;

        string buildId(string id)
        {
            return ModManifest.UniqueID + "_" + id;
        }

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            DataHelper.MyHelper = helper;
            DataHelper.MyMonitor = Monitor;
            DataHelper.UniqueID = ModManifest.UniqueID;

            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Player.Warped += OnWarped;
            Helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
            Helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

            LabyrinthManager.RegisterLabyrinthEvents();
            
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            Monitor.Log("Received "+ e.Type + " Message for Mod: " + e.FromModID);
            if (e.FromModID != this.ModManifest.UniqueID)
            {
                Monitor.Log("Message from another mod. Ignore.", LogLevel.Debug);
                return;

            }

            switch (e.Type)
            {
                case MessageType.InitializeMPGame:
                    Dictionary<string, int> seedData = e.ReadAs<Dictionary<string, int>>();
                    Monitor.Log("Received Seed Data: " + seedData["GameSeed"].ToString() + " " + seedData["DaySeed"], LogLevel.Debug);
                    LabyrinthManager.buildOnMultiplayerPeerLoad(seedData["GameSeed"], seedData["DaySeed"]);
                    break;
                case MessageType.RequestInitialLabryinthLevel:
                    if (Game1.IsMasterGame)
                    {
                        Monitor.Log("Received Initial Labyrinth Level Request", LogLevel.Debug);
                        //build the initial level and broadcast the level to other peers.
                        LabyrinthManager.InitializeBaseLevel();
                    }
                    break;
                case MessageType.RequestLabyrinthLevel:
                    if (Game1.IsMasterGame)
                    {
                        Monitor.Log("Received Labyrinth Level Request", LogLevel.Debug);
                        //build a level then broadcast the level out.
                        //a new labryinth level has been built by the host, load it.
                        LabyrinthLevelRequest request = e.ReadAs<LabyrinthLevelRequest>();
                        LabyrinthLocation location = new(request, Monitor);
                        Game1.locations.Add(location);
                        LabyrinthManager.current_labyrinth_levels.Add(location.Name, location);
                        Helper.Multiplayer.SendMessage(request, MessageType.LabyrinthLevelCreated, modIDs: new[] { ModManifest.UniqueID });
                            
                        //location.buildMap();
                    }
                    break;
                case MessageType.LabyrinthLevelCreated:
                    if (!Game1.IsMasterGame)
                    {
                        Monitor.Log("Host Created Labyrinth Level", LogLevel.Debug);
                        //a new labryinth level has been built by the host, load it.
                        LabyrinthLevelRequest request = e.ReadAs<LabyrinthLevelRequest>();
                        LabyrinthLocation location = new(request, Monitor);
                        Game1.locations.Add(location);
                        LabyrinthManager.current_labyrinth_levels.Add((request.Level > 0 ? location.Name : "base"), location);
                    }
                    break;
            }
                
            
        }

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (!e.Peer.IsHost && Game1.IsMasterGame)
            {
                Dictionary<string, int> seedData = new()
                {
                    { "GameSeed", LabyrinthManager.GameSeed},
                    { "DaySeed", LabyrinthManager.DailySeed }
                };
                Monitor.Log("Sending Seed Data to "+e.Peer.PlayerID+": " + seedData["GameSeed"].ToString() + " " + seedData["DaySeed"], LogLevel.Info);
                Helper.Multiplayer.SendMessage(seedData, MessageType.InitializeMPGame, modIDs: new[] { this.ModManifest.UniqueID }, playerIDs: new[] {e.Peer.PlayerID});
            }
            
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            if (Game1.locationContextData.ContainsKey(LabyrinthManager.CONTEXT_NAME))
            {
                Game1.locationContextData.Remove(LabyrinthManager.CONTEXT_NAME);
            }
        }

        /*********
        ** Private methods
        *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            // print button presses to the console window

            if (e.Button == SButton.O)
            {
                LocationContextData locationContext;
                if (!Game1.locationContextData.TryGetValue(LabyrinthManager.CONTEXT_NAME, out locationContext))
                {
                    Monitor.Log("Location Context Not Found");
                    loadLocationContext(LabyrinthManager.buildContext(ModManifest.UniqueID));
                } 

                Monitor.Log("Location Context Game SEED: " + locationContext.CustomFields[LabyrinthManager.SEED_FIELD_NAME], LogLevel.Info);
                Monitor.Log("         Manager Game Seed: " + LabyrinthManager.GameSeed.ToString(), LogLevel.Info);
                Monitor.Log("Location Context DAILY SEED: " + locationContext.CustomFields[LabyrinthManager.DAILY_SEED_NAME], LogLevel.Info);
                Monitor.Log("         Manager Daily Seed: " + LabyrinthManager.DailySeed.ToString(), LogLevel.Info);

                Monitor.Log("Labyrinth Levels: " + LabyrinthManager.current_labyrinth_levels.Count().ToString(), LogLevel.Info);
                if (LabyrinthManager.current_labyrinth_levels.ContainsKey("base"))
                {
                    Monitor.Log("Base Labyrinth Level Map Data: ");
                    GameLocation baseMap = LabyrinthManager.current_labyrinth_levels["base"];
                    Monitor.Log("Base Labyrinth Level Name: " + baseMap.Name);
                    if (baseMap.Map != null)
                    {
                        Monitor.Log("Base Labyrinth Level Map Size: (" +baseMap.map.DisplayWidth+ ","+ baseMap.map.DisplayHeight + ")");
                    } else
                    {
                        Monitor.Log("Null Map");
                    }
                } else
                {
                    Monitor.Log("No base labyrinth level found.");
                }
                

                Monitor.Log("All Game Locations: ");

                foreach(var location in Game1.locations)
                {
                    Monitor.Log(location.Name, LogLevel.Debug);
                }

            }

            if (e.Button == SButton.P)
            {
                Monitor.Log($"{Game1.player.Name} pressed {e.Button}.", LogLevel.Debug);
                Monitor.Log($"Warp to level 120");
                Game1.warpFarmer("UndergroundMine120", 13, 6, 2);
            }
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {   //we're warping from the base camp back to the mines. As a result we need to reset the postion to the new mine entrance that
            //was added to level 120.
            if (e.OldLocation.Name == LabyrinthManager.BASE_CAMP_NAME && e.NewLocation.Name == "UndergroundMine120")
            {
                int X = 13;
                int Y = 6;
                e.Player.Position = new Vector2(X * 64, Y * 64 - (e.Player.Sprite.getHeight() - 32) + 16);
                e.Player.faceDirection(2);
            }
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            writeSaveData();
        }


        private void OnSaveLoaded(object? sender, EventArgs e)
        {
            Monitor.Log("Save Loaded", LogLevel.Trace);
            if (!Context.IsWorldReady)
                return;

            Monitor.Log("Save Loaded. World is Ready.", LogLevel.Trace);

            loadSaveData();

            if (!Game1.locationContextData.ContainsKey(LabyrinthManager.CONTEXT_NAME))
            {
                loadLocationContext(LabyrinthManager.buildContext(ModManifest.UniqueID));
            }

            var baseCamp = Game1.getLocationFromName(LabyrinthManager.BASE_CAMP_NAME);
            if (baseCamp != null)
            {
                baseCamp.locationContextId = LabyrinthManager.CONTEXT_NAME;
            }

            var playerTent = Game1.getLocationFromName(LabyrinthManager.PLAYER_TENT_NAME);
            if (playerTent != null)
            {
                playerTent.locationContextId = LabyrinthManager.CONTEXT_NAME;
            }

            var nikoTent = Game1.getLocationFromName(LabyrinthManager.NIKO_TENT_NAME);
            if (nikoTent != null)
            {
                nikoTent.locationContextId = LabyrinthManager.CONTEXT_NAME;
            }

            var jakanTent = Game1.getLocationFromName(LabyrinthManager.JAKAN_TENT_NAME);
            if (jakanTent != null)
            {
                jakanTent.locationContextId = LabyrinthManager.CONTEXT_NAME;
            }
        }

        private void loadLocationContext(LocationContextData locationContextData)
        {
            Monitor.Log("Loading location context for " + LabyrinthManager.CONTEXT_NAME);
            Game1.locationContextData.Add(LabyrinthManager.CONTEXT_NAME, LabyrinthManager.buildContext(ModManifest.UniqueID));
        }

        private void writeSaveData()
        {
            LocationContextData locationContext = new LocationContextData();
            if (Game1.locationContextData.TryGetValue(LabyrinthManager.CONTEXT_NAME, out locationContext))
            {
                Helper.Data.WriteSaveData(DataHelper.LOCATION_CONTEXT, locationContext);
            }
            else
            {
                Monitor.Log("Error while saving location context " + DataHelper.LOCATION_CONTEXT + ": Location Context not found.", LogLevel.Error);
            }
        }

        private void loadSaveData()
        {
            if (Context.IsMainPlayer)
            {
                var locationContext = Helper.Data.ReadSaveData<LocationContextData>(DataHelper.LOCATION_CONTEXT);

                if (locationContext == null)
                {
                    Monitor.Log("Save Loaded. Building initial mod data.  This should only run once.", LogLevel.Trace);
                    locationContext = LabyrinthManager.buildContext(ModManifest.UniqueID);
                }
                else
                {
                    Monitor.Log("Save Loaded. Rebuild the Labyrinth Manager.");
                    string serializedGameSeed = locationContext.CustomFields[LabyrinthManager.SEED_FIELD_NAME];
                    string serializedDailySeed = locationContext.CustomFields[LabyrinthManager.DAILY_SEED_NAME];

                    int gameSeed;
                    int dailySeed;

                    if (int.TryParse(serializedGameSeed, out gameSeed) && int.TryParse(serializedDailySeed, out dailySeed))
                    {
                        LabyrinthManager.rebuildAfterSave(gameSeed, dailySeed);
                    }
                }

                loadLocationContext(locationContext);
            }
        }

        /// <summary>
        /// Builds the location context for the game.  This should run once and is a perminant record added to the save.
        /// In addition to storing basic location context info for the labyrinth, it also serves as a place to store overall
        /// unique mod/game info such as the base seed for the save.
        /// </summary>
        /// <returns></returns>

    }
}
