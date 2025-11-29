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
        ColorOption,
        SelectDeselect,

        HeadingMaterials,
        Wood,
        Grain,
        Livestock,
        Fish,
        Vegetables,
        Cotton,
        Oil,
        Ore,
        Coal,
        Stone,

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
}
