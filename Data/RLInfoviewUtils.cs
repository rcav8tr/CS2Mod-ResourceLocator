using Game.Prefabs;
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

        /// <summary>
        /// Initialize the infoview.
        /// </summary>
        public static void Initialize()
        {
            LogUtil.Info($"{nameof(RLInfoviewUtils)}.{nameof(Initialize)}");
            
            try
            {
                // The game's infoviews must be created before this mod's infoview.
                // That will be the normal case, but perform the check anyway just in case.
                World defaultWorld = World.DefaultGameObjectInjectionWorld;
                InfoviewInitializeSystem infoviewInitializeSystem = defaultWorld.GetOrCreateSystemManaged<InfoviewInitializeSystem>();
                if (infoviewInitializeSystem == null || infoviewInitializeSystem.infoviews.Count() == 0)
                {
                    LogUtil.Error("The game's infoviews must be created before this mod's infoview.");
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
                InfoviewPrefab infoviewPrefab = ScriptableObject.CreateInstance<InfoviewPrefab>();

                // Set infoview prefab properties.
                // The name of this mod's one infoview is the mod assembly name.
                infoviewPrefab.name = ModAssemblyInfo.Name;
                infoviewPrefab.m_Group = infoviewGroupNumber;
                infoviewPrefab.m_Priority = 1;
                infoviewPrefab.m_Editor = false;
                infoviewPrefab.m_IconPath = $"coui://{Mod.ImagesURI}/Infoview{ModAssemblyInfo.Name}.svg";

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
                infoviewPrefab.m_Infomodes = infomodeInfos.ToArray();

                // Add the infoview prefab to the prefab system.
                PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
                prefabSystem.AddPrefab(infoviewPrefab);
            }
            catch (Exception ex)
            {
                LogUtil.Exception(ex);
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

            // Set infomode color based on building type.
            // Except where noted, each color was taken manually from the corresponding resource icon.
            switch (buildingType)
            {
                case RLBuildingType.Wood:               infomodePrefab.m_Color = GetColor(136,  77,  31); break;    // Dark part of the wood.
                case RLBuildingType.Grain:              infomodePrefab.m_Color = GetColor(244, 195, 110); break;    // Light part of the grain.
                case RLBuildingType.Livestock:          infomodePrefab.m_Color = GetColor(194,  54,   2); break;    // Top of the chicken head.
                case RLBuildingType.Vegetables:         infomodePrefab.m_Color = GetColor(252, 158,  51); break;    // Center of carrot.
                case RLBuildingType.Cotton:             infomodePrefab.m_Color = GetColor(227, 235, 242); break;    // Top cotton ball.
                case RLBuildingType.Oil:                infomodePrefab.m_Color = GetColor(132, 120, 109); break;    // Middle of 3 colors.
                case RLBuildingType.Ore:                infomodePrefab.m_Color = GetColor( 82, 240, 218); break;    // Blue from ore.
                case RLBuildingType.Coal:               infomodePrefab.m_Color = GetColor( 76,  79,  85); break;    // Middle of 3 colors.
                case RLBuildingType.Stone:              infomodePrefab.m_Color = GetColor(139, 131, 131); break;    // Darker of 2 colors.
                
                case RLBuildingType.Metals:             infomodePrefab.m_Color = GetColor(138, 141, 143); break;    // Lighter of 2 colors.
                case RLBuildingType.Steel:              infomodePrefab.m_Color = GetColor(182, 213, 236); break;    // Lighter of 2 colors.
                case RLBuildingType.Minerals:           infomodePrefab.m_Color = GetColor( 10, 176, 153); break;    // Darkest of colors.
                case RLBuildingType.Concrete:           infomodePrefab.m_Color = GetColor(117,   0, 143); break;    // Ignore icon, use purple.
                case RLBuildingType.Machinery:          infomodePrefab.m_Color = GetColor(150, 172, 183); break;    // Darker of 2 gears.
                case RLBuildingType.Petrochemicals:     infomodePrefab.m_Color = GetColor(255, 221, 186); break;    // Lightest of 3 colors.
                case RLBuildingType.Chemicals:          infomodePrefab.m_Color = GetColor(147, 226,  44); break;    // Green from flask.
                case RLBuildingType.Plastics:           infomodePrefab.m_Color = GetColor(117, 164, 255); break;    // Main bottle color.
                case RLBuildingType.Pharmaceuticals:    infomodePrefab.m_Color = GetColor(233, 129, 129); break;    // Light color from red pill.
                case RLBuildingType.Electronics:        infomodePrefab.m_Color = GetColor(173, 195, 206); break;    // Main color.
                case RLBuildingType.Vehicles:           infomodePrefab.m_Color = GetColor(241, 105,  35); break;    // Car body.
                case RLBuildingType.Beverages:          infomodePrefab.m_Color = GetColor(243, 141,  52); break;    // Darkest color from bottle.
                case RLBuildingType.ConvenienceFood:    infomodePrefab.m_Color = GetColor(207, 194, 160); break;    // Front food item.
                case RLBuildingType.Food:               infomodePrefab.m_Color = GetColor(250, 220, 162); break;    // Basket.
                case RLBuildingType.Textiles:           infomodePrefab.m_Color = GetColor(240, 236, 236); break;    // Only one color.
                case RLBuildingType.Timber:             infomodePrefab.m_Color = GetColor(198, 133, 100); break;    // Middle of 3 colors.
                case RLBuildingType.Paper:              infomodePrefab.m_Color = GetColor(220,   0, 180); break;    // Ignore icon, use pink.
                case RLBuildingType.Furniture:          infomodePrefab.m_Color = GetColor(175, 110,  46); break;    // Chair right side.
                
                case RLBuildingType.Software:           infomodePrefab.m_Color = GetColor(193, 255, 104); break;    // The only green.
                case RLBuildingType.Telecom:            infomodePrefab.m_Color = GetColor(230, 221, 199); break;    // Back conversation bubble.
                case RLBuildingType.Financial:          infomodePrefab.m_Color = GetColor(114, 171,  95); break;    // Right side of money stack.
                case RLBuildingType.Media:              infomodePrefab.m_Color = GetColor(135, 151, 156); break;    // Lighter of 2 colors.
                case RLBuildingType.Lodging:            infomodePrefab.m_Color = GetColor(106, 154, 174); break;    // Blanket on bed.
                case RLBuildingType.Meals:              infomodePrefab.m_Color = GetColor(180,   0, 220); break;    // Ignore icon, use purple.
                case RLBuildingType.Entertainment:      infomodePrefab.m_Color = GetColor(208, 234, 163); break;    // Left face.
                case RLBuildingType.Recreation:         infomodePrefab.m_Color = GetColor(198, 133, 101); break;    // Left mountain.
                
                default:                                infomodePrefab.m_Color = Color.red;               break;
            }

            // Return a new infomode based on the infomode prefab.
            return new InfomodeInfo() { m_Mode = infomodePrefab, m_Priority = priority };
        }

        /// <summary>
        /// Get a color based on bytes, not floats.
        /// </summary>
        private static Color GetColor(byte r, byte g, byte b)
        {
            return new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
        }
    }
}
