﻿using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace ResourceLocator
{
    /// <summary>
    /// The settings for this mod.
    /// </summary>
    [FileLocation(nameof(ResourceLocator))]
    [SettingsUIGroupOrder(GroupInclude, GroupAbout)]
    [SettingsUIShowGroupName(GroupInclude, GroupAbout)]
    public class ModSettings : ModSetting
    {
        // Group constants.
        public const string GroupInclude = "Include";
        public const string GroupAbout   = "About";
        
        // Constructor.
        public ModSettings(IMod mod) : base(mod)
        {
            Mod.log.Info($"{nameof(ModSettings)}.{nameof(ModSettings)}");

            SetDefaults();
        }
        
        /// <summary>
        /// Set a default value for every setting that has a value that can change.
        /// </summary>
        public override void SetDefaults()
        {
            // It is important to set a default for every value.
            IncludeRecyclingCenter   = false;
            IncludeCoalPowerPlant    = false;
            IncludeGasPowerPlant     = false;
            IncludeMedicalFacility   = false;
            IncludeEmeregencyShelter = false;
            IncludeCargoStation      = false;
        }

        // General description for special case buildings.
        [SettingsUISection(GroupInclude)]
        [SettingsUIMultilineText]
        public string IncludeGeneralDescription => Translation.Get(UITranslationKey.SettingIncludeGeneralDescription);

        // Include special case buildings.
        [SettingsUISection(GroupInclude)] public bool IncludeRecyclingCenter   { get; set; }
        [SettingsUISection(GroupInclude)] public bool IncludeCoalPowerPlant    { get; set; }
        [SettingsUISection(GroupInclude)] public bool IncludeGasPowerPlant     { get; set; }
        [SettingsUISection(GroupInclude)] public bool IncludeMedicalFacility   { get; set; }
        [SettingsUISection(GroupInclude)] public bool IncludeEmeregencyShelter { get; set; }
        [SettingsUISection(GroupInclude)] public bool IncludeCargoStation      { get; set; }

        // Display mod version in settings.
        [SettingsUISection(GroupAbout)]
        public string ModVersion { get { return ModAssemblyInfo.Version; } }
    }
}
