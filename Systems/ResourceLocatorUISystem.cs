using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game.Areas;
using Game.Economy;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ResourceLocator
{
    // Define display options.
    public enum DisplayOption
    {
        Requires,
        Produces,
        Sells,
        Stores,
    }

    // Define color options.
    public enum ColorOption
    {
        Multiple,
        One
    }

    /// <summary>
    /// System to send building data to UI.
    /// </summary>
    public partial class ResourceLocatorUISystem : InfoviewUISystemBase
    {
        // Variables to track when updates should occur.
        private long _previousBindingUpdateTicks;
        private bool _previouslyDisplayed;

        // Other systems.
        private BuildingColorSystem _buildingColorSystem;
        private NameSystem          _nameSystem;
        private ResourceSystem      _resourceSystem;
        private ToolSystem          _toolSystem;

        // C# to UI binding names.
        public const string BindingNameSelectedDistrict = "SelectedDistrict";
        public const string BindingNameDistrictInfos    = "DistrictInfos";
        public const string BindingNameDisplayOption    = "DisplayOption";
        public const string BindingNameColorOption      = "ColorOption";
        public const string BindingNameOneColor         = "OneColor";
        public const string BindingNameResourceInfos    = "ResourceInfos";

        // C# to UI bindings.
        private ValueBinding<Entity>    _bindingSelectedDistrict;
        private RawValueBinding         _bindingDistrictInfos;
        private ValueBinding<int>       _bindingDisplayOption;
        private ValueBinding<int>       _bindingColorOption;
        private ValueBinding<Color>     _bindingOneColor;
        private RawValueBinding         _bindingResourceInfos;

        // UI to C# event names.
        public const string EventNameSelectedDistrictChanged    = "SelectedDistrictChanged";
        public const string EventNameDisplayOptionClicked       = "DisplayOptionClicked";
        public const string EventNameColorOptionClicked         = "ColorOptionClicked";
        public const string EventNameOneColorChanged            = "OneColorChanged";

        // Districts.
        private EntityQuery _districtQuery;
        private DistrictInfos _districtInfos = new();
        public static Entity EntireCity { get; } = Entity.Null;
        public Entity selectedDistrict { get; set; } = EntireCity;

        /// <summary>
        /// Do one-time initialization of the system.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.log.Info($"{nameof(ResourceLocatorUISystem)}.{nameof(OnCreate)}");
            
            try
            {
                // Get other systems.
                _buildingColorSystem    = base.World.GetOrCreateSystemManaged<BuildingColorSystem>();
                _nameSystem             = base.World.GetOrCreateSystemManaged<NameSystem>();
                _resourceSystem         = base.World.GetOrCreateSystemManaged<ResourceSystem>();
                _toolSystem             = base.World.GetOrCreateSystemManaged<ToolSystem>();

                // Add bindings for C# to UI.
                AddBinding(_bindingSelectedDistrict = new ValueBinding<Entity>(ModAssemblyInfo.Name, BindingNameSelectedDistrict, EntireCity));
                AddBinding(_bindingDistrictInfos    = new RawValueBinding     (ModAssemblyInfo.Name, BindingNameDistrictInfos,    WriteDistrictInfos));
                AddBinding(_bindingDisplayOption    = new ValueBinding<int>   (ModAssemblyInfo.Name, BindingNameDisplayOption,    (int)Mod.ModSettings.DisplayOption));
                AddBinding(_bindingColorOption      = new ValueBinding<int>   (ModAssemblyInfo.Name, BindingNameColorOption,      (int)Mod.ModSettings.ColorOption));
                AddBinding(_bindingOneColor         = new ValueBinding<Color> (ModAssemblyInfo.Name, BindingNameOneColor,         Mod.ModSettings.OneColor));
                AddBinding(_bindingResourceInfos    = new RawValueBinding     (ModAssemblyInfo.Name, BindingNameResourceInfos,    WriteResourceInfos));

                // Add bindings for UI to C#.
                AddBinding(new TriggerBinding<Entity>(ModAssemblyInfo.Name, EventNameSelectedDistrictChanged,   SelectedDistrictChanged));
                AddBinding(new TriggerBinding<int   >(ModAssemblyInfo.Name, EventNameDisplayOptionClicked,      DisplayOptionClicked   ));
                AddBinding(new TriggerBinding<int   >(ModAssemblyInfo.Name, EventNameColorOptionClicked,        ColorOptionClicked     ));
                AddBinding(new TriggerBinding<Color >(ModAssemblyInfo.Name, EventNameOneColorChanged,           OneColorChanged        ));

                // Define entity query to get districts.
                _districtQuery = GetEntityQuery(ComponentType.ReadOnly<District>());
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Write district infos to the UI.
        /// </summary>
        private void WriteDistrictInfos(IJsonWriter writer)
        {
            _districtInfos.Write(writer);
        }

        /// <summary>
        /// Write resource infos to the UI.
        /// </summary>
        private void WriteResourceInfos(IJsonWriter writer)
        {
            // Get latest storage amounts and company counts.
            _buildingColorSystem.GetStorageAmountsCompanyCounts(
                out int[] storageAmountsRequires,
                out int[] storageAmountsProduces,
                out int[] storageAmountsSells,
                out int[] storageAmountsStores,
                out int[] storageAmountsInTransit,
                
                out int[] companyCountsRequires,
                out int[] companyCountsProduces,
                out int[] companyCountsSells,
                out int[] companyCountsStores);

            // It is desired to use the same production and surplus data as the Production tab of the Economy view.
            // The Production tab data comes from the ProductionUISystem.
            // But ProductionUISystem updates only 32 times per game day, which is one update every 45 game minutes.
            // Get production and surplus amounts now.
            // Ignore company productions.
            // Always write the values, even if not valid.
            bool productionSurplusValid = ProductionSurplus.GetAmounts(out int[] productionAmounts, out int[] surplusAmounts, out _);

            // Define variables to hold maximum of each value.
            int maxStorageRequires  = 0;
            int maxStorageProduces  = 0;
            int maxStorageSells     = 0;
            int maxStorageStores    = 0;
            int maxStorageInTransit = 0;

            int maxProduction       = 0;
            int maxSurplus          = 0;

            // Get resource stuff.
            ResourcePrefabs resourcePrefabs = _resourceSystem.GetPrefabs();
            ComponentLookup<ResourceData> componentLookupResourceData = SystemAPI.GetComponentLookup<ResourceData>(true);

            // Get resource info for each building type.
            ResourceInfos resourceInfos = new();
            foreach (RLBuildingType buildingType in (RLBuildingType[])Enum.GetValues(typeof(RLBuildingType)))
            {
                // Skip special cases.
                if (RLBuildingTypeUtils.IsSpecialCase(buildingType))
                {
                    continue;
                }

                // Get resource index for this building type.
                Resource resource = RLBuildingTypeUtils.GetResource(buildingType);
                int resourceIndex = EconomyUtils.GetResourceIndex(resource);

                // Compute max values.
                maxStorageRequires  = Math.Max(maxStorageRequires,  storageAmountsRequires  [resourceIndex]);
                maxStorageProduces  = Math.Max(maxStorageProduces,  storageAmountsProduces  [resourceIndex]);
                maxStorageSells     = Math.Max(maxStorageSells,     storageAmountsSells     [resourceIndex]);
                maxStorageStores    = Math.Max(maxStorageStores,    storageAmountsStores    [resourceIndex]);
                maxStorageInTransit = Math.Max(maxStorageInTransit, storageAmountsInTransit [resourceIndex]);

                maxProduction       = Math.Max(maxProduction,       productionAmounts       [resourceIndex]);
                maxSurplus          = Math.Max(maxSurplus, Math.Abs(surplusAmounts          [resourceIndex]));

                // Get whether or not building type has weight.
                bool hasWeight = EconomyUtils.IsResourceHasWeight(resource, resourcePrefabs, ref componentLookupResourceData);

                // Add a new resource info.
                resourceInfos.Add(new ResourceInfo
                { 
                    BuildingType           = buildingType,
                    StorageAmountRequires  = storageAmountsRequires  [resourceIndex],
                    StorageAmountProduces  = storageAmountsProduces  [resourceIndex],
                    StorageAmountSells     = storageAmountsSells     [resourceIndex],
                    StorageAmountStores    = storageAmountsStores    [resourceIndex],
                    StorageAmountInTransit = storageAmountsInTransit [resourceIndex],
                    RateValid              = productionSurplusValid,
                    RateProduction         = productionAmounts       [resourceIndex],
                    RateSurplus            = surplusAmounts          [resourceIndex],
                    CompanyCountRequires   = companyCountsRequires   [resourceIndex],
                    CompanyCountProduces   = companyCountsProduces   [resourceIndex],
                    CompanyCountSells      = companyCountsSells      [resourceIndex],
                    CompanyCountStores     = companyCountsStores     [resourceIndex],
                    HasWeight              = hasWeight
                });
            }

            // Include the entry for max values.
            // Passing the max values in its own resource info
            // allows the UI logic to quickly obtain the max values from this entry
            // instead of recomputing the max values for every infomode.
            resourceInfos.Add(new ResourceInfo
            {
                BuildingType           = RLBuildingType.MaxValues,
                StorageAmountRequires  = maxStorageRequires,
                StorageAmountProduces  = maxStorageProduces,
                StorageAmountSells     = maxStorageSells,
                StorageAmountStores    = maxStorageStores,
                StorageAmountInTransit = maxStorageInTransit,
                RateValid              = false,
                RateProduction         = maxProduction, 
                RateSurplus            = maxSurplus, 
                CompanyCountRequires   = 0,
                CompanyCountProduces   = 0,
                CompanyCountSells      = 0,
                CompanyCountStores     = 0,
                HasWeight              = false
            });

            // Write resource infos to the UI.
            resourceInfos.Write(writer);
        }

        /// <summary>
        /// Check for any change in districts.
        /// </summary>
        private void CheckForDistrictChange()
        {
            // Get district infos and find selected district.
            bool foundSelectedDistrict = (selectedDistrict == EntireCity);
            DistrictInfos districtInfos = new();
            NativeArray<Entity> districtEntities = _districtQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity districtEntity in districtEntities)
            {
                // Skip the special district that the game creates while a new district is being drawn.
                string districtName = _nameSystem.GetRenderedLabelName(districtEntity);
                if (districtName != "Assets.DISTRICT_NAME")
                {
                    // Add a district info for this district.
                    districtInfos.Add(new DistrictInfo(districtEntity, districtName));

                    // Check if this is the selected district.
                    if (districtEntity == selectedDistrict)
                    {
                        foundSelectedDistrict = true;
                    }
                }
            }

            // Check if selected district was not found.
            if (!foundSelectedDistrict)
            {
                // Selected district was not found, most likely because the selected district was deleted.
                // Change selected district to entire city.
                SelectedDistrictChanged(EntireCity);
            }

            // Sort district infos by name.
            districtInfos.Sort();

            // First district info is always for entire city.
            districtInfos.Insert(0, new DistrictInfo(EntireCity, Translation.Get(UITranslationKey.EntireCity)));

            // Check if district infos have changed.
            bool districtsChanged = false;
            if (districtInfos.Count != _districtInfos.Count)
            {
                districtsChanged = true;
            }
            else
            {
                // Compare each district info.
                for (int i = 0; i < districtInfos.Count; i++)
                {
                    if (districtInfos[i].entity != _districtInfos[i].entity || districtInfos[i].name != _districtInfos[i].name)
                    {
                        districtsChanged = true;
                        break;
                    }
                }
            }
            
            // Check if a district info change was found.
            if (districtsChanged)
            {
                // Write district infos to the UI.
                _districtInfos = districtInfos;
                _bindingDistrictInfos.Update();
            }
        }

        /// <summary>
        /// Event callback for selected district changed.
        /// </summary>
        private void SelectedDistrictChanged(Entity newDistrict)
        {
            // Save selected district.
            selectedDistrict = newDistrict;

            // Immediately send the selected district back to the UI.
            _bindingSelectedDistrict.Update(selectedDistrict);
        }

        /// <summary>
        /// Event callback for display option clicked.
        /// </summary>
        private void DisplayOptionClicked(int newDisplayOption)
        {
            // Save the new display option.
            Mod.ModSettings.DisplayOption = (DisplayOption)newDisplayOption;

            // Immediately send the display option back to the UI.
            _bindingDisplayOption.Update(newDisplayOption);
        }

        /// <summary>
        /// Event callback for color option clicked.
        /// </summary>
        private void ColorOptionClicked(int newColorOption)
        {
            // Save the new color option.
            Mod.ModSettings.ColorOption = (ColorOption)newColorOption;

            // Immediately send the color option back to the UI.
            _bindingColorOption.Update(newColorOption);

            // Use the newly selected color option.
            RLInfoviewUtils.SetInfomodeColors();
            RLInfoviewUtils.RefreshInfoview();
        }

        /// <summary>
        /// Event callback for one color changed.
        /// </summary>
        private void OneColorChanged(Color newOneColor)
        {
            // Save the new one color.
            Mod.ModSettings.OneColorR = newOneColor.r;
            Mod.ModSettings.OneColorG = newOneColor.g;
            Mod.ModSettings.OneColorB = newOneColor.b;

            // Immediately send the one color back to the UI.
            _bindingOneColor.Update(newOneColor);

            // Use the newly selected one color.
            RLInfoviewUtils.SetInfomodeColors();
            RLInfoviewUtils.RefreshInfoview();
        }

        /// <summary>
        /// Called when a game is done being loaded.
        /// </summary>
        protected override void OnGameLoadingComplete(Purpose purpose, Game.GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Initialize only for game mode.
            if (mode == Game.GameMode.Game)
            {
                // Selected district is entire city.
                SelectedDistrictChanged(EntireCity);

                // Initialize districts.
                _districtInfos.Clear();
                CheckForDistrictChange();

                // Initialize updating variables.
                _previousBindingUpdateTicks = 0L;
                _previouslyDisplayed = false;
            }
        }

        /// <summary>
        /// Called when the game determines that an update is needed.
        /// </summary>
        [Preserve]
        protected override void PerformUpdate()
        {
            // Nothing to do here, but implementation is required.
            // Updates are performed in OnUpdate.
        }

        /// <summary>
        /// Called every frame, even when at the main menu.
        /// </summary>
        protected override void OnUpdate()
        {
            base.OnUpdate();

            // An infoview must be active.
            if (_toolSystem.activeInfoview == null)
            {
                _previouslyDisplayed = false;
                return;
            }

            // Active infoview must be for this mod.
            // The name of this mod's only infoview is the mod assembly name.
            string activeInfoviewName = _toolSystem.activeInfoview.name;
            if (activeInfoviewName == ModAssemblyInfo.Name)
            {
                // Active infoview is for this mod.

                // Check for a change in districts.
                // Note that if the districts change while there is no active infoview or the active infoview is not for this mod
                // (e.g. the last selected district is deleted), then it will take a frame for the district infos to be updated.
                // So for one frame, the district dropdown might be blank.  This is acceptable.
                CheckForDistrictChange();

                // If this infoview was not previously displayed, update resource infos immediately.
                if (!_previouslyDisplayed)
                {
                    _previousBindingUpdateTicks = 0L;
                }

                // Update resource infos every 1 second.
                long currentTicks = DateTime.Now.Ticks;
                if (currentTicks - _previousBindingUpdateTicks >= TimeSpan.TicksPerSecond)
                {
                    // Save binding update ticks.
                    _previousBindingUpdateTicks = currentTicks;

                    // Update resource infos.
                    _bindingResourceInfos.Update();
                }

                // This infoview is previously displayed.
                _previouslyDisplayed = true;
            }
            else
            {
                // Active infoview is not for this mod.
                _previouslyDisplayed = false;
            }
        }
    }
}
