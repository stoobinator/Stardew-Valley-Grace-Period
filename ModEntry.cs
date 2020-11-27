using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using TerrainFeatures = StardewValley.TerrainFeatures;

namespace SeasonSpillover
{
    /// <summary>
    /// Mod to allow crops planted in a previous season to survive into the next
    /// </summary>
    public class ModEntry : Mod
    {
        #region Configuration

        /// <summary>
        /// Configuration for the mod
        /// </summary>
        private ModConfig Config;

        /// <summary>
        /// The seasons to allow a grace period
        /// </summary>
        private List<string> AllowedSeasons = new List<string>();

        /// <summary>
        /// The number of days to allow an already-planted crop to continue growing
        /// </summary>
        private int GracePeriod;

        #endregion

        #region Fields

        /// <summary>
        /// A list of crops that should be destroyed if harvested
        /// </summary>
        private List<Crop> CropsToWatch;

        #endregion

        #region Entry point

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            AllowedSeasons = Config.GetAllowedSeasons();
            GracePeriod = Config.GracePeriod;

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.Saving += OnSaving;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Tracks crops that might need to be cleaned up at day's end
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The event data</param>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            GameLocation farm = Game1.getLocationFromName("Farm");

            if (AllowedSeasons.Contains(Game1.currentSeason))
            {
                CropsToWatch = new List<Crop>();

                foreach (var tile in farm.terrainFeatures)
                {
                    foreach (TerrainFeatures.TerrainFeature feature in tile.Values)
                    {
                        if (feature is TerrainFeatures.HoeDirt dirt
                            && IsSuspiciousCrop(dirt.crop))
                        {
                            CropsToWatch.Add(dirt.crop);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if plant is an out of season, fully-grown crop that can be
        /// harvested multiple times, e.g. a bean plant in summer
        /// </summary>
        /// <param name="dirt">A tile of dirt with a crop in it</param>
        private bool IsSuspiciousCrop(Crop crop)
        {
            if (crop != null
                && crop.regrowAfterHarvest > -1
                && !crop.seasonsToGrowIn.Contains(Game1.currentSeason)
                && !crop.fullyGrown)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Kill all crops not protected by this mod
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The event data</param>
        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            GameLocation farm = Game1.getLocationFromName("Farm");
            string season = SDate.Now().AddDays(1).Season;

            if (AllowedSeasons.Contains(season))
            {
                farm.IsGreenhouse = true;

                // Manually kill all out of season crops not protected by the grace period
                foreach (var tile in farm.terrainFeatures)
                {
                    foreach (TerrainFeatures.TerrainFeature feature in tile.Values)
                    {
                        if (feature is TerrainFeatures.HoeDirt dirt &&
                            ShouldKillCrop(dirt.crop, season))
                        {
                            dirt.crop.Kill();
                        }
                    }
                }

                // Kill any out of season multi-harvest crops that were harvested 
                foreach (Crop crop in CropsToWatch)
                {
                    if (crop.fullyGrown)
                    {
                        crop.Kill();
                    }
                }
            }
        }

        /// <summary>
        /// Check if a crop should be killed
        /// </summary>
        /// <param name="crop"></param>
        /// <param name="season"></param>
        /// <returns>True if the crop should be killed, false otherwise</returns>
        private bool ShouldKillCrop(Crop crop, string season)
        {
            // Don't kill a non-existent crop
            if (crop == null)
            {
                return false;
            }
            // If it is currently the crop's season, don't kill it
            else if (crop.seasonsToGrowIn.Contains(season))
            {
                return false;
            }
            // If grace period is 3 or more seasons, it will never expire
            else if (GracePeriod > 2)
            {
                return false;
            }
            // If the crop is out of season and there's no grace period, kill it
            else if (GracePeriod == 0)
            {
                return true;
            }
            // Check if crop is within the grace period 
            else
            {
                for (int i = 1; i <= GracePeriod; i++)
                {
                    if (crop.seasonsToGrowIn.Contains(SubtractSeason(season, i)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Determines the season a certain number before another season
        /// </summary>
        /// <param name="season">The season</param>
        /// <param name="subtrahend">The number to subtract</param>
        /// <returns>The calculated season</returns>
        private string SubtractSeason(string season, int subtrahend)
        {
            string[] seasons = { "spring", "summer", "fall", "winter" };
            return seasons[(Array.IndexOf(seasons, season) - subtrahend + 4) % 4];
        }

        /// <summary>
        /// Turn off greenhouse effect
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The event data</param>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            Game1.getLocationFromName("Farm").IsGreenhouse = false;
        }

        #endregion
    }

    /// <summary>
    /// Configuration class for this mod
    /// </summary>
    internal class ModConfig
    {
        /// <summary>
        /// Whether to protect crops in Spring
        /// </summary>
        public bool Spring { get; set; } = true;

        /// <summary>
        /// Whether to protect crops in Summer
        /// </summary>
        public bool Summer { get; set; } = true;

        /// <summary>
        /// Whether to protect crops in Fall
        /// </summary>
        public bool Fall { get; set; } = true;

        /// <summary>
        /// Whether to protect crops in Winter
        /// </summary>
        public bool Winter { get; set; } = false;

        /// <summary>
        /// The number of days to allow an already-planted crop to continue growing
        /// </summary>
        public int GracePeriod;

        /// <summary>
        /// Gets the list of seasons to protect previous seasons' crops
        /// </summary>
        /// <returns>List of allowed seasons</returns>
        public List<string> GetAllowedSeasons()
        {
            var allowedSeasons = new List<string>();
            if (Spring)
            {
                allowedSeasons.Add("spring");
            }
            if (Summer)
            {
                allowedSeasons.Add("summer");
            }
            if (Fall)
            {
                allowedSeasons.Add("fall");
            }
            if (Winter)
            {
                allowedSeasons.Add("winter");
            }
            return allowedSeasons;
        }
    }
}