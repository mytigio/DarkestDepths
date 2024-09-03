using DarkestDepths.Helpers;
using DarkestDepths.Labyrinth;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.LocationContexts;

namespace DarkestDepths
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        LocationContextData? labyrinthContext;
        LabyrinthLocation? testLocation;

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
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            LabyrinthManager.RegisterLabyrinthEvents();
            
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
                Monitor.Log($"{Game1.player.Name} pressed {e.Button}. Force clear the labryinth levels.", LogLevel.Debug);
                Monitor.Log($"IsOutdoors: {Game1.currentLocation.IsOutdoors} IsFarm: {Game1.currentLocation.IsFarm}");
            }

            if (e.Button == SButton.P)
            {
                Monitor.Log($"{Game1.player.Name} pressed {e.Button}.", LogLevel.Debug);
                Monitor.Log($"Warp to dwarf basecamp");
                Game1.warpFarmer(LabyrinthManager.BASE_CAMP_NAME, 26, 34, 2);
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

        /// <summary>
        /// Builds the location context for the game.  This should run once and is a perminant record added to the save.
        /// In addition to storing basic location context info for the labyrinth, it also serves as a place to store overall
        /// unique mod/game info such as the base seed for the save.
        /// </summary>
        /// <returns></returns>

    }
}
