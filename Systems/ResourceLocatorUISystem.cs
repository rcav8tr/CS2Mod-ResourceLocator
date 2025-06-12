using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game.Areas;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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

    /// <summary>
    /// System to send building data to UI.
    /// </summary>
    public partial class ResourceLocatorUISystem : InfoviewUISystemBase
    {
        // Variables to track when updates should occur.
        private long _previousBindingUpdateTicks;
        private bool _previouslyDisplayed;

        // Other systems.
        private NameSystem              _nameSystem;
        private ToolSystem              _toolSystem;
        private ResourceSystem          _resourceSystem;
        private PrefabSystem            _prefabSystem;
        private CountCompanyDataSystem  _countCompanyDataSystem;
        private IndustrialDemandSystem  _industrialDemandSystem;
        private CommercialDemandSystem  _commercialDemandSystem;
        private BuildingColorSystem     _buildingColorSystem;

        // C# to UI binding names.
        public const string BindingNameSelectedDistrict = "SelectedDistrict";
        public const string BindingNameDistrictInfos    = "DistrictInfos";
        public const string BindingNameDisplayOption    = "DisplayOption";
        public const string BindingNameResourceInfos    = "ResourceInfos";

        // C# to UI bindings.
        private ValueBinding<Entity>    _bindingSelectedDistrict;
        private RawValueBinding         _bindingDistrictInfos;
        private ValueBinding<int>       _bindingDisplayOption;
        private RawValueBinding         _bindingResourceInfos;

        // UI to C# event names.
        public const string EventNameSelectedDistrictChanged    = "SelectedDistrictChanged";
        public const string EventNameDisplayOptionClicked       = "DisplayOptionClicked";

        // Districts.
        private EntityQuery _districtQuery;
        private DistrictInfos _districtInfos = new DistrictInfos();
        public static Entity EntireCity { get; } = Entity.Null;
        public Entity selectedDistrict { get; set; } = EntireCity;

        // Display option.
        public DisplayOption displayOption { get; set; } = DisplayOption.Requires;

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
                _nameSystem             = base.World.GetOrCreateSystemManaged<NameSystem>();
                _toolSystem             = base.World.GetOrCreateSystemManaged<ToolSystem>();
                _resourceSystem         = base.World.GetOrCreateSystemManaged<ResourceSystem>();
                _prefabSystem           = base.World.GetOrCreateSystemManaged<PrefabSystem>();
                _countCompanyDataSystem = base.World.GetOrCreateSystemManaged<CountCompanyDataSystem>();
                _industrialDemandSystem = base.World.GetOrCreateSystemManaged<IndustrialDemandSystem>();
                _commercialDemandSystem = base.World.GetOrCreateSystemManaged<CommercialDemandSystem>();
                _buildingColorSystem    = base.World.GetOrCreateSystemManaged<BuildingColorSystem>();

                // Add bindings for C# to UI.
                AddBinding(_bindingSelectedDistrict = new ValueBinding<Entity>(ModAssemblyInfo.Name, BindingNameSelectedDistrict, EntireCity                 ));
                AddBinding(_bindingDistrictInfos    = new RawValueBinding     (ModAssemblyInfo.Name, BindingNameDistrictInfos,    WriteDistrictInfos         ));
                AddBinding(_bindingDisplayOption    = new ValueBinding<int>   (ModAssemblyInfo.Name, BindingNameDisplayOption,    (int)DisplayOption.Requires));
                AddBinding(_bindingResourceInfos    = new RawValueBinding     (ModAssemblyInfo.Name, BindingNameResourceInfos,    WriteResourceInfos         ));

                // Add bindings for UI to C#.
                AddBinding(new TriggerBinding<Entity>(ModAssemblyInfo.Name, EventNameSelectedDistrictChanged,   SelectedDistrictChanged));
                AddBinding(new TriggerBinding<int   >(ModAssemblyInfo.Name, EventNameDisplayOptionClicked,      DisplayOptionClicked   ));

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
            // Get production and consumption data.
            // Logic adapted from Game.UI.InGame.ProductionUISystem.UpdateCache().
		    NativeArray<int> productionData        = _countCompanyDataSystem.GetProduction (out JobHandle deps1);
		    NativeArray<int> industrialConsumption = _industrialDemandSystem.GetConsumption(out JobHandle deps3);
		    NativeArray<int> commercialConsumption = _commercialDemandSystem.GetConsumption(out JobHandle deps4);
		    JobHandle.CompleteAll(ref deps1, ref deps3, ref deps4);
		    CountCompanyDataSystem.CommercialCompanyDatas commercialCompanyDatas = _countCompanyDataSystem.GetCommercialCompanyDatas(out JobHandle deps2);
		    deps2.Complete();
			ResourcePrefabs resourcePrefabs = _resourceSystem.GetPrefabs();
		    for (int i = 0; i < productionData.Length; i++)
		    {
			    if (!base.EntityManager.GetComponentData<ResourceData>(resourcePrefabs[EconomyUtils.GetResource(i)]).m_IsProduceable)
			    {
				    productionData[i] = commercialCompanyDatas.m_ProduceCapacity[i];

                    // Following resources are not produceable.
                    // Of these, this mod cares only about Lodging, Meals, Entertainment, and Recreation.
                    //      Money
                    //      Meals
                    //      Lodging
                    //      UnsortedMail
                    //      LocalMail
                    //      OutgoingMail
                    //      Entertainment
                    //      Recreation
                    //      Garbage
			    }
		    }

            // Get latest storage amounts.
            _buildingColorSystem.GetStorageAmounts(out int[] storageRequires, out int[] storageProduces, out int[] storageSells, out int[] storageStores);

            // Define variables to hold maximum of each value.
            int maxStorageRequires = 0;
            int maxStorageProduces = 0;
            int maxStorageSells    = 0;
            int maxStorageStores   = 0;

            int maxProduction      = 0;
            int maxSurplus         = 0;
            int maxDeficit         = 0;

            // Get resource info for each building type.
            ResourceInfos resourceInfos = new ResourceInfos();
            foreach (RLBuildingType buildingType in (RLBuildingType[])Enum.GetValues(typeof(RLBuildingType)))
            {
                // Skip special cases.
                if (RLBuildingTypeUtils.IsSpecialCase(buildingType))
                {
                    continue;
                }

                // Get resource index for this building type.
    			Entity resourceEntity = resourcePrefabs[RLBuildingTypeUtils.GetResource(buildingType)];
		        int resourceIndex = EconomyUtils.GetResourceIndex(EconomyUtils.GetResource(_prefabSystem.GetPrefab<ResourcePrefab>(resourceEntity).m_Resource));

                // Compute production, surplus, and deficit.
                // Logic adapted from Game.UI.InGame.ProductionUISystem.GetData().
                int production = productionData[resourceIndex];
                int consumption = commercialConsumption[resourceIndex] + industrialConsumption[resourceIndex];
                int minConsumptionProduction = math.min(consumption, production);
                int surplus = production  - minConsumptionProduction;
                int deficit = consumption - minConsumptionProduction;

                // Compute max values.
                maxStorageRequires = Math.Max(maxStorageRequires, storageRequires[resourceIndex]);
                maxStorageProduces = Math.Max(maxStorageProduces, storageProduces[resourceIndex]);
                maxStorageSells    = Math.Max(maxStorageSells,    storageSells   [resourceIndex]);
                maxStorageStores   = Math.Max(maxStorageStores,   storageStores  [resourceIndex]);
                
                maxProduction      = Math.Max(maxProduction,      production                    );
                maxSurplus         = Math.Max(maxSurplus,         surplus                       );
                maxDeficit         = Math.Max(maxDeficit,         deficit                       );

                // Add a new resource info.
                resourceInfos.Add(new ResourceInfo(
                    buildingType,
                    storageRequires[resourceIndex],
                    storageProduces[resourceIndex],
                    storageSells   [resourceIndex],
                    storageStores  [resourceIndex],
                    production, 
                    surplus, 
                    deficit));
            }

            // Include the entry for max values.
            // Passing the max values in its own resource info
            // allows the UI logic to quickly obtain the max values from this entry
            // instead of recomputing the max values for every infomode.
            resourceInfos.Add(new ResourceInfo(
                RLBuildingType.MaxValues,
                maxStorageRequires,
                maxStorageProduces,
                maxStorageSells,
                maxStorageStores,
                maxProduction, 
                maxSurplus, 
                maxDeficit));

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
            DistrictInfos districtInfos = new DistrictInfos();
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
            displayOption = (DisplayOption)newDisplayOption;

            // Immediately send the display option back to the UI.
            _bindingDisplayOption.Update((int)displayOption);
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
