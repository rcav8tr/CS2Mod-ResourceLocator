using Game.Economy;
using System.Collections.Generic;

namespace ResourceLocator
{
    // This mod's building types.
    // The "RL" (i.e. Resource Locator) prefix differentiates this enum from Game.Prefabs.BuildingType.
    // Start at an arbitrary large number to avoid overlap with the game's BuildingType and
    // hopefully avoid conflicts with any other mod's building types.
    // This mod has logic that assumes these are named the same as the resource enum names and the resource image file names.
    public enum RLBuildingType
    {
        None = 246800,

        District,
        DisplayOption,
        SelectDeselect,

        HeadingMaterials,
        Wood,
        Grain,
        Livestock,
        Vegetables,
        Cotton,
        Oil,         // "Crude Oil"
        Ore,         // "Metal Ore"
        Coal,
        Stone,       // "Rock"

        HeadingMaterialGoods,
        Metals,
        Steel,
        Minerals,
        Concrete,
        Machinery,
        Petrochemicals,
        Chemicals,
        Plastics,
        Pharmaceuticals,
        Electronics,
        Vehicles,
        Beverages,
        ConvenienceFood,
        Food,
        Textiles,
        Timber,
        Paper,
        Furniture,
        
        HeadingImmaterialGoods,
        Software,
        Telecom,
        Financial,
        Media,
        Lodging,
        Meals,
        Entertainment,
        Recreation,

        // UI logic assumes this is the last building type.
        MaxValues,
    }

    /// <summary>
    /// Building type utilities.
    /// </summary>
    public static class RLBuildingTypeUtils
    {
        // Conversion from building type to resource.
        private readonly static Dictionary<RLBuildingType, Resource> _convertBuildingTypeToResource = new Dictionary<RLBuildingType, Resource>()
        {
            { RLBuildingType.Wood,            Resource.Wood            },
            { RLBuildingType.Grain,           Resource.Grain           },
            { RLBuildingType.Livestock,       Resource.Livestock       },
            { RLBuildingType.Vegetables,      Resource.Vegetables      },
            { RLBuildingType.Cotton,          Resource.Cotton          },
            { RLBuildingType.Oil,             Resource.Oil             },
            { RLBuildingType.Ore,             Resource.Ore             },
            { RLBuildingType.Coal,            Resource.Coal            },
            { RLBuildingType.Stone,           Resource.Stone           },
            
            { RLBuildingType.Metals,          Resource.Metals          },
            { RLBuildingType.Steel,           Resource.Steel           },
            { RLBuildingType.Minerals,        Resource.Minerals        },
            { RLBuildingType.Concrete,        Resource.Concrete        },
            { RLBuildingType.Machinery,       Resource.Machinery       },
            { RLBuildingType.Petrochemicals,  Resource.Petrochemicals  },
            { RLBuildingType.Chemicals,       Resource.Chemicals       },
            { RLBuildingType.Plastics,        Resource.Plastics        },
            { RLBuildingType.Pharmaceuticals, Resource.Pharmaceuticals },
            { RLBuildingType.Electronics,     Resource.Electronics     },
            { RLBuildingType.Vehicles,        Resource.Vehicles        },
            { RLBuildingType.Beverages,       Resource.Beverages       },
            { RLBuildingType.ConvenienceFood, Resource.ConvenienceFood },
            { RLBuildingType.Food,            Resource.Food            },
            { RLBuildingType.Textiles,        Resource.Textiles        },
            { RLBuildingType.Timber,          Resource.Timber          },
            { RLBuildingType.Paper,           Resource.Paper           },
            { RLBuildingType.Furniture,       Resource.Furniture       },
            
            { RLBuildingType.Software,        Resource.Software        },
            { RLBuildingType.Telecom,         Resource.Telecom         },
            { RLBuildingType.Financial,       Resource.Financial       },
            { RLBuildingType.Media,           Resource.Media           },
            { RLBuildingType.Lodging,         Resource.Lodging         },
            { RLBuildingType.Meals,           Resource.Meals           },
            { RLBuildingType.Entertainment,   Resource.Entertainment   },
            { RLBuildingType.Recreation,      Resource.Recreation      },
        };

        /// <summary>
        /// Return whether or not building type is a special case.
        /// </summary>
        public static bool IsSpecialCase(RLBuildingType buildingType)
        {
            return 
                buildingType == RLBuildingType.None                     ||
                buildingType == RLBuildingType.District                 ||
                buildingType == RLBuildingType.DisplayOption            ||
                buildingType == RLBuildingType.SelectDeselect           ||
                buildingType == RLBuildingType.HeadingMaterials         ||
                buildingType == RLBuildingType.HeadingMaterialGoods     ||
                buildingType == RLBuildingType.HeadingImmaterialGoods   ||
                buildingType == RLBuildingType.MaxValues;
        }

        /// <summary>
        /// Convert building type enum to building type name.
        /// </summary>
        public static string GetBuildingTypeName(RLBuildingType buildingType)
        {
            // Simply prefix the building type with the mod name.
            return ModAssemblyInfo.Name + buildingType.ToString();
        }

        /// <summary>
        /// Get the resource corresponding to the building type.
        /// </summary>
        public static Resource GetResource(RLBuildingType buildingType)
        {
            // Return the resource for the building type.
            return _convertBuildingTypeToResource[buildingType];
        }
    }
}
