using Game.Prefabs;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace ResourceLocator
{
    /// <summary>
    /// Infoview utilities.
    /// </summary>
    public static class RLInfoviewUtils
    {
        // The name of the one infoview is the mod assembly name.

        // The infoview prefab.
        private static InfoviewPrefab _infoviewPrefab;

        // Other systems
        private static ToolSystem _toolSystem;

        /// <summary>
        /// Initialize the infoview.
        /// </summary>
        public static void Initialize()
        {
            Mod.log.Info($"{nameof(RLInfoviewUtils)}.{nameof(Initialize)}");
            
            try
            {
                // Get other systems.
                World defaultWorld = World.DefaultGameObjectInjectionWorld;
                _toolSystem = defaultWorld.GetOrCreateSystemManaged<ToolSystem>();

                // The game's infoviews must be created before this mod's infoview.
                // That will be the normal case, but perform the check anyway just in case.
                InfoviewInitializeSystem infoviewInitializeSystem = defaultWorld.GetOrCreateSystemManaged<InfoviewInitializeSystem>();
                if (infoviewInitializeSystem == null || infoviewInitializeSystem.infoviews.Count() == 0)
                {
                    Mod.log.Error("The game's infoviews must be created before this mod's infoview.");
                    return;
                }

                // Get list of infoview group numbers already used by the base game or
                // by other mods that added their own infoview group(s) before this mod.
                List<int> usedInfoGroupNumbers = new List<int>();
                foreach (InfoviewPrefab existingInfoviewPrefab in infoviewInitializeSystem.infoviews)
                {
                    if (!usedInfoGroupNumbers.Contains(existingInfoviewPrefab.m_Group))
                    {
                        usedInfoGroupNumbers.Add(existingInfoviewPrefab.m_Group);
                    }
                }

                // Get the first infoview group number not already used.
                int infoviewGroupNumber = 1;
                while (usedInfoGroupNumbers.Contains(infoviewGroupNumber))
                {
                    infoviewGroupNumber++;
                }

                // Create the infoview prefab.
                _infoviewPrefab = ScriptableObject.CreateInstance<InfoviewPrefab>();

                // Set infoview prefab properties.
                // The name of this mod's one infoview is the mod assembly name.
                _infoviewPrefab.name = ModAssemblyInfo.Name;
                _infoviewPrefab.m_Group = infoviewGroupNumber;
                _infoviewPrefab.m_Priority = 1;
                _infoviewPrefab.m_Editor = false;
                _infoviewPrefab.m_IconPath = $"coui://{Mod.ImagesURI}/Infoview{ModAssemblyInfo.Name}.svg";

                // Set infoview prefab's infomodes, one infomode for each building type except None.
                // Priority determines infomode sort order.
                List<InfomodeInfo> infomodeInfos = new List<InfomodeInfo>();
                int infomodePriority = 1;
                foreach (RLBuildingType buildingType in Enum.GetValues(typeof(RLBuildingType)))
                {
                    if (buildingType != RLBuildingType.None)
                    {
                        infomodeInfos.Add(CreateInfomodeInfo(buildingType, infomodePriority++));
                    }
                }
                _infoviewPrefab.m_Infomodes = infomodeInfos.ToArray();

                // Initialize infomode colors.
                SetInfomodeColors();

                // Add the infoview prefab to the prefab system.
                PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
                prefabSystem.AddPrefab(_infoviewPrefab);
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Create an InfomodeInfo for a building type.
        /// </summary>
        private static InfomodeInfo CreateInfomodeInfo(RLBuildingType buildingType, int priority)
        {
            // Create a new building infomode prefab.
            // All infomodes in this mod are of type BuildingInfomodePrefab.
            // BuildingInfomodePrefab results in InfoviewBuildingData being generated.
            BuildingInfomodePrefab infomodePrefab = ScriptableObject.CreateInstance<BuildingInfomodePrefab>();

            // Set infomode prefab properties.
            infomodePrefab.m_Type = (Game.Prefabs.BuildingType)buildingType;
            infomodePrefab.name = RLBuildingTypeUtils.GetBuildingTypeName(buildingType);

            // Return a new infomode based on the infomode prefab.
            return new InfomodeInfo() { m_Mode = infomodePrefab, m_Priority = priority };
        }

        /// <summary>
        /// Set color for all infomodes.
        /// </summary>
        public static void SetInfomodeColors()
        {
            // Do each infomode in the infoview prefab.
            foreach (InfomodeInfo infomodeInfo in _infoviewPrefab.m_Infomodes)
            {
                // Get infomode prefab to be updated.
                // All infomodes in this mod are of type BuildingInfomodePrefab.
                BuildingInfomodePrefab infomodePrefab = (BuildingInfomodePrefab)infomodeInfo.m_Mode;

                // Get color based on color option.
                Color color = Color.red;
                if (Mod.ModSettings.ColorOption == ColorOption.Multiple)
                {
                    // Get infomode color based on building type.
                    // Except where noted, each color was taken manually from the corresponding resource icon.
                    switch ((RLBuildingType)infomodePrefab.m_Type)
                    {
                        case RLBuildingType.Wood:               color = GetColor(136,  77,  31); break;     // Dark part of the wood.
                        case RLBuildingType.Grain:              color = GetColor(244, 195, 110); break;     // Light part of the grain.
                        case RLBuildingType.Livestock:          color = GetColor(194,  54,   2); break;     // Top of the chicken head.
                        case RLBuildingType.Fish:               color = GetColor(107, 136, 157); break;     // Eye of fish.
                        case RLBuildingType.Vegetables:         color = GetColor(252, 158,  51); break;     // Center of carrot.
                        case RLBuildingType.Cotton:             color = GetColor(227, 235, 242); break;     // Top cotton ball.
                        case RLBuildingType.Oil:                color = GetColor(132, 120, 109); break;     // Middle of 3 colors.
                        case RLBuildingType.Ore:                color = GetColor( 82, 240, 218); break;     // Blue from ore.
                        case RLBuildingType.Coal:               color = GetColor( 76,  79,  85); break;     // Middle of 3 colors.
                        case RLBuildingType.Stone:              color = GetColor(139, 131, 131); break;     // Darker of 2 colors.
                
                        case RLBuildingType.Metals:             color = GetColor(138, 141, 143); break;     // Lighter of 2 colors.
                        case RLBuildingType.Steel:              color = GetColor(182, 213, 236); break;     // Lighter of 2 colors.
                        case RLBuildingType.Minerals:           color = GetColor( 10, 176, 153); break;     // Darkest of colors.
                        case RLBuildingType.Concrete:           color = GetColor(117,   0, 143); break;     // Ignore icon, use purple.
                        case RLBuildingType.Machinery:          color = GetColor(150, 172, 183); break;     // Darker of 2 gears.
                        case RLBuildingType.Petrochemicals:     color = GetColor(255, 221, 186); break;     // Lightest of 3 colors.
                        case RLBuildingType.Chemicals:          color = GetColor(147, 226,  44); break;     // Green from flask.
                        case RLBuildingType.Plastics:           color = GetColor(117, 164, 255); break;     // Main bottle color.
                        case RLBuildingType.Pharmaceuticals:    color = GetColor(233, 129, 129); break;     // Light color from red pill.
                        case RLBuildingType.Electronics:        color = GetColor( 59,  65,  70); break;     // 50% of center color.
                        case RLBuildingType.Vehicles:           color = GetColor(241, 105,  35); break;     // Car body.
                        case RLBuildingType.Beverages:          color = GetColor(243, 141,  52); break;     // Darkest color from bottle.
                        case RLBuildingType.ConvenienceFood:    color = GetColor(207, 194, 160); break;     // Front food item.
                        case RLBuildingType.Food:               color = GetColor(250, 220, 162); break;     // Basket.
                        case RLBuildingType.Textiles:           color = GetColor(240, 236, 236); break;     // Only one color.
                        case RLBuildingType.Timber:             color = GetColor(198, 133, 100); break;     // Middle of 3 colors.
                        case RLBuildingType.Paper:              color = GetColor(220,   0, 180); break;     // Ignore icon, use pink.
                        case RLBuildingType.Furniture:          color = GetColor(175, 110,  46); break;     // Chair right side.
                
                        case RLBuildingType.Software:           color = GetColor(193, 255, 104); break;     // The only green.
                        case RLBuildingType.Telecom:            color = GetColor(230, 221, 199); break;     // Back conversation bubble.
                        case RLBuildingType.Financial:          color = GetColor(114, 171,  95); break;     // Right side of money stack.
                        case RLBuildingType.Media:              color = GetColor(135, 151, 156); break;     // Lighter of 2 colors.
                        case RLBuildingType.Lodging:            color = GetColor(106, 154, 174); break;     // Blanket on bed.
                        case RLBuildingType.Meals:              color = GetColor(180,   0, 220); break;     // Ignore icon, use purple.
                        case RLBuildingType.Entertainment:      color = GetColor(208, 234, 163); break;     // Left face.
                        case RLBuildingType.Recreation:         color = GetColor(198, 133, 101); break;     // Left mountain.
                    }
                }
                else
                {
                    // Use one color from settings for all infomodes.
                    color = Mod.ModSettings.OneColor;
                }

                // Set the infomode color.
                infomodePrefab.m_Color = color;
            }
        }

        /// <summary>
        /// Get a color based on bytes, not floats.
        /// </summary>
        private static Color GetColor(byte r, byte g, byte b)
        {
            return new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
        }

        /// <summary>
        /// Refresh the infoview.
        /// </summary>
        public static void RefreshInfoview()
        {
            // Refreshing the infoview makes all infomodes active again.
            // Save infomodes that are currently inactive.
            List<InfomodePrefab> inactiveInfomodes = new();
            InfomodeInfo[] infomodeInfos = _toolSystem.activeInfoview.m_Infomodes;
            foreach (InfomodeInfo infomodeInfo in infomodeInfos)
            {
                if (!_toolSystem.IsInfomodeActive(infomodeInfo.m_Mode))
                {
                    inactiveInfomodes.Add(infomodeInfo.m_Mode);
                }
            }

            // Clear and immediately set infoview.
            // This causes infoview to be redisplayed.
            _toolSystem.infoview = null;
            _toolSystem.infoview = _infoviewPrefab;

            // Deactivate infomodes that were inactive.
            foreach (InfomodeInfo infomodeInfo in infomodeInfos)
            {
                if (inactiveInfomodes.Contains(infomodeInfo.m_Mode))
                {
                    _toolSystem.SetInfomodeActive(infomodeInfo.m_Mode, false, infomodeInfo.m_Priority);
                }
            }
        }
    }
}
