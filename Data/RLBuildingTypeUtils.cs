using Game.Economy;

namespace ResourceLocator
{
    /// <summary>
    /// Building type utilities.
    /// </summary>
    public static class RLBuildingTypeUtils
    {
        /// <summary>
        /// Return whether or not building type is a special case.
        /// </summary>
        public static bool IsSpecialCase(RLBuildingType buildingType)
        {
            return 
                buildingType == RLBuildingType.None                     ||
                buildingType == RLBuildingType.District                 ||
                buildingType == RLBuildingType.DisplayOption            ||
                buildingType == RLBuildingType.ColorOption              ||
                buildingType == RLBuildingType.SelectDeselect           ||
                buildingType == RLBuildingType.HeadingRawMaterials      ||
                buildingType == RLBuildingType.HeadingProcessedGoods    ||
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
            // Switch statement is faster than a dictionary lookup.
            switch (buildingType)
            {
                case RLBuildingType.Wood            : return Resource.Wood           ;
                case RLBuildingType.Grain           : return Resource.Grain          ;
                case RLBuildingType.Livestock       : return Resource.Livestock      ;
                case RLBuildingType.Fish            : return Resource.Fish           ;
                case RLBuildingType.Vegetables      : return Resource.Vegetables     ;
                case RLBuildingType.Cotton          : return Resource.Cotton         ;
                case RLBuildingType.Oil             : return Resource.Oil            ;
                case RLBuildingType.Ore             : return Resource.Ore            ;
                case RLBuildingType.Coal            : return Resource.Coal           ;
                case RLBuildingType.Stone           : return Resource.Stone          ;
                
                case RLBuildingType.Metals          : return Resource.Metals         ;
                case RLBuildingType.Steel           : return Resource.Steel          ;
                case RLBuildingType.Minerals        : return Resource.Minerals       ;
                case RLBuildingType.Concrete        : return Resource.Concrete       ;
                case RLBuildingType.Machinery       : return Resource.Machinery      ;
                case RLBuildingType.Petrochemicals  : return Resource.Petrochemicals ;
                case RLBuildingType.Chemicals       : return Resource.Chemicals      ;
                case RLBuildingType.Plastics        : return Resource.Plastics       ;
                case RLBuildingType.Pharmaceuticals : return Resource.Pharmaceuticals;
                case RLBuildingType.Electronics     : return Resource.Electronics    ;
                case RLBuildingType.Vehicles        : return Resource.Vehicles       ;
                case RLBuildingType.Beverages       : return Resource.Beverages      ;
                case RLBuildingType.ConvenienceFood : return Resource.ConvenienceFood;
                case RLBuildingType.Food            : return Resource.Food           ;
                case RLBuildingType.Textiles        : return Resource.Textiles       ;
                case RLBuildingType.Timber          : return Resource.Timber         ;
                case RLBuildingType.Paper           : return Resource.Paper          ;
                case RLBuildingType.Furniture       : return Resource.Furniture      ;
                
                case RLBuildingType.Software        : return Resource.Software       ;
                case RLBuildingType.Telecom         : return Resource.Telecom        ;
                case RLBuildingType.Financial       : return Resource.Financial      ;
                case RLBuildingType.Media           : return Resource.Media          ;
                case RLBuildingType.Lodging         : return Resource.Lodging        ;
                case RLBuildingType.Meals           : return Resource.Meals          ;
                case RLBuildingType.Entertainment   : return Resource.Entertainment  ;
                case RLBuildingType.Recreation      : return Resource.Recreation     ;

                default                             : return Resource.NoResource     ;
            }
        }
    }
}
