using Colossal.Collections;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Creatures;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Game.Vehicles;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Scripting;
using BuildingFlags         = Game.Prefabs.     BuildingFlags;
using CargoTransport        = Game.Vehicles.    CargoTransport;
using CargoTransportStation = Game.Buildings.   CargoTransportStation;
using DeliveryTruck         = Game.Vehicles.    DeliveryTruck;
using EmergencyShelter      = Game.Buildings.   EmergencyShelter;
using ExtractorCompany      = Game.Companies.   ExtractorCompany;
using GarbageFacility       = Game.Buildings.   GarbageFacility;
using Hospital              = Game.Buildings.   Hospital;
using Object                = Game.Objects.     Object;
using OutsideConnection     = Game.Objects.     OutsideConnection;
using ProcessingCompany     = Game.Companies.   ProcessingCompany;
using ResourceProducer      = Game.Buildings.   ResourceProducer;
using StorageCompany        = Game.Companies.   StorageCompany;
using UtilityObject         = Game.Objects.     UtilityObject;

namespace ResourceLocator
{
    /// <summary>
    /// System to set building colors.
    /// Adapted from Game.Rendering.ObjectColorSystem.
    /// This system replaces the game's ObjectColorSystem logic when this mod's infoview is selected.
    /// </summary>
    public partial class BuildingColorSystem : GameSystemBase
    {
        /// <summary>
        /// Information for an active infomode.
        /// </summary>
        private struct ActiveInfomode : IComparable<ActiveInfomode>
        {
            // Resource and its corresponding infomode index.
            public Resource resource;
            public byte infomodeIndex;

            // Building type is used only for sorting.
            public RLBuildingType buildingType;
            public int CompareTo(ActiveInfomode other)
            {
                return buildingType.CompareTo(other.buildingType);
            }
        }

        /// <summary>
        /// Job to quickly set the color to default on all objects that have a color.
        /// In this way, any object not set by subsequent jobs is assured to be the default color.
        /// A complete replacement for the part of Game.Rendering.ObjectColorSystem.UpdateObjectColorsJob that sets default color.
        /// </summary>
        [BurstCompile]
        private partial struct UpdateColorsJobDefault : IJobChunk
        {
            // Color component type to update.
            public ComponentTypeHandle<Color> ComponentTypeHandleColor;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do all objects.
                NativeArray<Color> colors = chunk.GetNativeArray(ref ComponentTypeHandleColor);
                for (int i = 0; i < colors.Length; i++)
                {
                    // Set color index and value to default.
                    // SubColor remains unchanged.
                    Color color = colors[i];
                    color.m_Index = 0;
                    color.m_Value = 0;
                    colors[i] = color;
                }
            }
        }


        /// <summary>
        /// Job to set the color of each cargo vehicle according to the resource it is carrying.
        /// A complete replacement for the part of Game.Rendering.ObjectColorSystem.UpdateObjectColorsJob that sets vehicle color.
        /// </summary>
        [BurstCompile]
        private struct UpdateColorsJobCargoVehicle : IJobChunk
        {
            // Color component type to update (not ReadOnly).
                                                  public ComponentTypeHandle<Color> ComponentTypeHandleColor;
            [NativeDisableParallelForRestriction] public ComponentLookup    <Color> ComponentLookupColor;

            // Buffer lookups.
            [ReadOnly] public BufferLookup<Resources            > BufferLookupResources;

            // Component lookups.
            [ReadOnly] public ComponentLookup<Controller        > ComponentLookupController;
            [ReadOnly] public ComponentLookup<CurrentDistrict   > ComponentLookupCurrentDistrict;
            [ReadOnly] public ComponentLookup<OutsideConnection > ComponentLookupOutsideConnection;
            [ReadOnly] public ComponentLookup<Owner             > ComponentLookupOwner;
            [ReadOnly] public ComponentLookup<PropertyRenter    > ComponentLookupPropertyRenter;
            [ReadOnly] public ComponentLookup<Target            > ComponentLookupTarget;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<DeliveryTruck > ComponentTypeHandleDeliveryTruck;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            // Active infomodes.
            [ReadOnly] public NativeArray<ActiveInfomode> ActiveInfomodes;

            // Selected district.
            [ReadOnly] public Entity SelectedDistrict;
            [ReadOnly] public bool SelectedDistrictIsEntireCity;

            // Nested arrays to return in transit amounts to the BuildingColorSystem.
            // The outer array is one for each possible thread.
            // The inner array is one for each resource.
            // Even though the outer array is read only, entries in the inner array can still be updated.
            [ReadOnly] public NativeArray<NativeArray<int>> StorageAmountsInTransit;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Get colors to change.
                NativeArray<Color> colors = chunk.GetNativeArray(ref ComponentTypeHandleColor);

                // Get delivery truck vs cargo transport.
                // Note that the following are neither and do not actually carry any resource (i.e. no Resource buffer).
                // Therefore, these are not colorized:
                //      Reach Stacker
                //      Fishing Boat
                //      Oil Tanker
                NativeArray<DeliveryTruck> deliveryTrucks = chunk.GetNativeArray(ref ComponentTypeHandleDeliveryTruck);
                bool isDeliveryTruck = deliveryTrucks.Length > 0;

                // Do each cargo vehicle.
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                for (int i = 0; i < entities.Length; i++)
                {
                    // Get the vehicle.
                    Entity vehicleEntity = entities[i];

                    // For DelveryTruck vehicles:
                    //      The Delivery Van, Coal (Dump) Truck, Oil Truck, and Delivery Motorbike have an owner directly.
                    //      The Semi Truck has a controller, which then has an owner.

                    // For CargoTransport vehicles:
                    //      The Ship and Airplane have an owner directly.
                    //      The Train car has a controller, which then has an owner.

                    // The vehicle to check is either the vehicle itself or the vehicle's controller if one is present.
                    Entity vehicleToCheck = vehicleEntity;
                    if (ComponentLookupController.TryGetComponent(vehicleEntity, out Controller vehicleController))
                    {
                        vehicleToCheck = vehicleController.m_Controller;
                    }

                    // Get vehicle owner.
                    // Vehicle to check should always have an owner.
                    if (ComponentLookupOwner.TryGetComponent(vehicleToCheck, out Owner vehicleOwner))
                    {
                        // Determine if vehicle is for the selected district.
                        // For Entire City, all vehicles are for the selected district.
                        bool vehicleIsForSelectedDistrict = SelectedDistrictIsEntireCity;
                        if (!SelectedDistrictIsEntireCity)
                        {
                            // A vehicle is included only if the vehicle's district is the selected district.
                            // The vehicle's district is determined by the vehicle owner's property.

                            // Get vehicle's owner property renter, if any.
                            Entity propertyToCheck;
                            if (ComponentLookupPropertyRenter.TryGetComponent(vehicleOwner.m_Owner, out PropertyRenter propertyRenter))
                            {
                                // Property to check is the property of the property renter.
                                propertyToCheck = propertyRenter.m_Property;
                            }
                            else
                            {
                                // Property to check is the direct owner of the vehicle to check.
                                propertyToCheck = vehicleOwner.m_Owner;
                            }

                            // Determine if the property to check is in the selected district.
                            vehicleIsForSelectedDistrict = 
                                ComponentLookupCurrentDistrict.TryGetComponent(propertyToCheck, out CurrentDistrict currentDistrict) &&
                                currentDistrict.m_District == SelectedDistrict;
                        }

                        // Determine if vehicle's owner is an outside connection.
                        bool ownerIsOutsideConnection = ComponentLookupOutsideConnection.HasComponent(vehicleOwner.m_Owner);

                        // Determine if vehicle's target is an outside connection.
                        bool targetIsOutsideConnection = 
                            ComponentLookupTarget.TryGetComponent(vehicleToCheck, out Target vehicleTarget) &&
                            ComponentLookupOutsideConnection.HasComponent(vehicleTarget.m_Target);

                        // Vehicle must be for the selected district.
                        // Vehicle must not be traveling between two outside connections.
                        if (vehicleIsForSelectedDistrict && !(ownerIsOutsideConnection && targetIsOutsideConnection))
                        {
                            // Set vehicle color according to delivery truck vs cargo transport.
                            if (isDeliveryTruck)
                            {
                                SetVehicleColorDeliveryTruck(vehicleEntity, i, deliveryTrucks[i], ref colors);
                            }
                            else
                            {
                                SetVehicleColorCargoTransport(vehicleEntity, i, ref colors);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Set vehicle color for a delivery truck.
            /// </summary>
            private void SetVehicleColorDeliveryTruck(Entity vehicleEntity, int vehicleIndex, DeliveryTruck deliveryTruck, ref NativeArray<Color> colors)
            {
                // Delivery truck must be loaded with a valid resource.
                Resource deliveryTruckResource = deliveryTruck.m_Resource;
                if ((deliveryTruck.m_State & DeliveryTruckFlags.Loaded) != 0 &&
                    deliveryTruck.m_Amount > 0 &&
                    deliveryTruckResource != Resource.NoResource &&
                    deliveryTruckResource != Resource.Garbage)
                {
                    // Find active infomode (if any) corresponding to the delivery truck resource.
                    foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                    {
                        if (activeInfomode.resource == deliveryTruckResource)
                        {
                            // Set delivery truck color according to the active infomode.
                            colors[vehicleIndex] = new Color(activeInfomode.infomodeIndex, 255);

                            // If delivery truck has a controller, then set its color too.
                            // Controller is the tractor of a tractor trailer semi truck.
                            // Both tractor and trailer have DeliveryTruck component.
                            // But only the trailer has the resource; the tractor has NoResource.
                            if (ComponentLookupController.TryGetComponent(vehicleEntity, out Controller controller) &&
                                ComponentLookupColor.HasComponent(controller.m_Controller))
                            {
                                ComponentLookupColor[controller.m_Controller] = new Color(activeInfomode.infomodeIndex, 255);
                            }

                            // Found the active infomode, stop checking infomodes.
                            break;
                        }
                    }

                    // Save the in transit amount.
                    SaveInTransitAmount(deliveryTruckResource, deliveryTruck.m_Amount);
                }
            }

            /// <summary>
            /// Set vehicle color for a cargo transport.
            /// Cargo transports include cargo trains, cargo ships, and cargo airplanes.
            /// </summary>
            private void SetVehicleColorCargoTransport(Entity vehicleEntity, int vehicleIndex, ref NativeArray<Color> colors)
            {
                // Cargo transport must have a resources buffer with at least 1 resource in it.
                if (BufferLookupResources.TryGetBuffer(vehicleEntity, out DynamicBuffer<Resources> resourcesBuffer) &&
                    resourcesBuffer.Length > 0)
                {
                    // Do each active infomode.
                    bool found = false;
                    foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                    {
                        // Find a resource in the buffer corresponding to the active infomode resource.
                        Resource activeInfomodeResource = activeInfomode.resource;
                        foreach (Resources bufferResource in resourcesBuffer)
                        {
                            if (bufferResource.m_Resource == activeInfomodeResource && bufferResource.m_Amount > 0)
                            {
                                // Found a matching resource.
                                found = true;

                                // Set cargo transport color according to the active infomode.
                                colors[vehicleIndex] = new Color(activeInfomode.infomodeIndex, 255);

                                // Stop checking buffer resources.
                                break;
                            }
                        }

                        // If found, stop checking infomodes for this cargo transport.
                        if (found)
                        {
                            break;
                        }
                    }

                    // Save the in transit amount for every resource in the buffer.
                    foreach (Resources bufferResource in resourcesBuffer)
                    {
                        SaveInTransitAmount(bufferResource.m_Resource, bufferResource.m_Amount);
                    }
                }
            }

            /// <summary>
            /// Save in transit amount.
            /// </summary>
            private void SaveInTransitAmount(Resource resource, int amount)
            {
                // Resource must be valid.
                // Skip zero amounts.
                if (resource != Resource.NoResource && amount != 0)
                {
                    // Accumulate in transit amount for this thread and resource.
                    // By having a separate entry for each thread, parallel threads will never access the same inner array at the same time.
                    NativeArray<int> storageAmountsForThread = StorageAmountsInTransit[JobsUtility.ThreadIndex];
                    int resourceIndex = EconomyUtils.GetResourceIndex(resource);
                    storageAmountsForThread[resourceIndex] = storageAmountsForThread[resourceIndex] + amount;
                }
            }
        }


        /// <summary>
        /// Job to set the color of each main building.
        /// A complete replacement for the part of Game.Rendering.ObjectColorSystem.UpdateObjectColorsJob that sets building color.
        /// </summary>
        [BurstCompile]
        private partial struct UpdateColorsJobMainBuilding : IJobChunk
        {
            // Color component type to update (not ReadOnly).
            public ComponentTypeHandle<Color> ComponentTypeHandleColor;

            // Buffer lookups.
            [ReadOnly] public BufferLookup<InstalledUpgrade             > BufferLookupInstalledUpgrade;
            [ReadOnly] public BufferLookup<Renter                       > BufferLookupRenter;
            [ReadOnly] public BufferLookup<Resources                    > BufferLookupResources;

            // Component lookups.
            [ReadOnly] public ComponentLookup<BuildingData              > ComponentLookupBuildingData;
            [ReadOnly] public ComponentLookup<BuildingPropertyData      > ComponentLookupBuildingPropertyData;
            [ReadOnly] public ComponentLookup<CompanyData               > ComponentLookupCompanyData;
            [ReadOnly] public ComponentLookup<EmergencyShelterData      > ComponentLookupEmergencyShelterData;
            [ReadOnly] public ComponentLookup<ExtractorCompany          > ComponentLookupExtractorCompany;
            [ReadOnly] public ComponentLookup<HospitalData              > ComponentLookupHospitalData;
            [ReadOnly] public ComponentLookup<IndustrialProcessData     > ComponentLookupIndustrialProcessData;
            [ReadOnly] public ComponentLookup<PrefabRef                 > ComponentLookupPrefabRef;
            [ReadOnly] public ComponentLookup<ProcessingCompany         > ComponentLookupProcessingCompany;
            [ReadOnly] public ComponentLookup<ServiceAvailable          > ComponentLookupServiceAvailable;
            [ReadOnly] public ComponentLookup<StorageCompany            > ComponentLookupStorageCompany;
            [ReadOnly] public ComponentLookup<StorageCompanyData        > ComponentLookupStorageCompanyData;

            // Component type handles for special case buildings.
            [ReadOnly] public ComponentTypeHandle<CargoTransportStation > ComponentTypeHandleCargoTransportStation;
            [ReadOnly] public ComponentTypeHandle<ElectricityProducer   > ComponentTypeHandleElectricityProducer;
            [ReadOnly] public ComponentTypeHandle<EmergencyShelter      > ComponentTypeHandleEmergencyShelter;
            [ReadOnly] public ComponentTypeHandle<GarbageFacility       > ComponentTypeHandleGarbageFacility;
            [ReadOnly] public ComponentTypeHandle<Hospital              > ComponentTypeHandleHospital;
            [ReadOnly] public ComponentTypeHandle<ResourceProducer      > ComponentTypeHandleResourceProducer;

            // Component type handles for miscellaneous.
            [ReadOnly] public ComponentTypeHandle<CurrentDistrict       > ComponentTypeHandleCurrentDistrict;
            [ReadOnly] public ComponentTypeHandle<Destroyed             > ComponentTypeHandleDestroyed;
            [ReadOnly] public ComponentTypeHandle<PrefabRef             > ComponentTypeHandlePrefabRef;
            [ReadOnly] public ComponentTypeHandle<UnderConstruction     > ComponentTypeHandleUnderConstruction;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            // Active infomodes.
            [ReadOnly] public NativeArray<ActiveInfomode> ActiveInfomodes;

            // Mod settings used in the job.
            [ReadOnly] public bool IncludeRecyclingCenter;
            [ReadOnly] public bool IncludeCoalPowerPlant;
            [ReadOnly] public bool IncludeGasPowerPlant;
            [ReadOnly] public bool IncludeMedicalFacility;
            [ReadOnly] public bool IncludeEmeregencyShelter;
            [ReadOnly] public bool IncludeCargoStation;

            // Selected district.
            [ReadOnly] public Entity SelectedDistrict;
            [ReadOnly] public bool SelectedDistrictIsEntireCity;

            // Display option.
            [ReadOnly] public DisplayOption DisplayOption;

            // Nested arrays to return storage amounts and company counts to OnUpdate.
            // The outer array is one for each possible thread.
            // The inner array is one for each resource.
            // Even though the outer array is read only, entries in the inner array can still be updated.
            [ReadOnly] public NativeArray<NativeArray<int>> StorageAmountsRequires;
            [ReadOnly] public NativeArray<NativeArray<int>> StorageAmountsProduces;
            [ReadOnly] public NativeArray<NativeArray<int>> StorageAmountsSells;
            [ReadOnly] public NativeArray<NativeArray<int>> StorageAmountsStores;

            [ReadOnly] public NativeArray<NativeArray<int>> CompanyCountsRequires;
            [ReadOnly] public NativeArray<NativeArray<int>> CompanyCountsProduces;
            [ReadOnly] public NativeArray<NativeArray<int>> CompanyCountsSells;
            [ReadOnly] public NativeArray<NativeArray<int>> CompanyCountsStores;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Get colors to set.
                NativeArray<Color> colors = chunk.GetNativeArray(ref ComponentTypeHandleColor);

                // Get arrays from the chunk.
                NativeArray<Entity           > entities           = chunk.GetNativeArray(EntityTypeHandle);
                NativeArray<PrefabRef        > prefabRefs         = chunk.GetNativeArray(ref ComponentTypeHandlePrefabRef);
                NativeArray<CurrentDistrict  > currentDistricts   = chunk.GetNativeArray(ref ComponentTypeHandleCurrentDistrict);
                NativeArray<Destroyed        > destroyeds         = chunk.GetNativeArray(ref ComponentTypeHandleDestroyed);
                NativeArray<UnderConstruction> underConstructions = chunk.GetNativeArray(ref ComponentTypeHandleUnderConstruction);

                // Do each entity (i.e. building).
                for (int i = 0; i < entities.Length; i++)
                {
                    // Get building entity and prefab.
                    Entity buildingEntity = entities[i];
                    Entity buildingPrefab = prefabRefs[i].m_Prefab;

                    // Building must be in selected district.
                    if (SelectedDistrictIsEntireCity ||
                        (currentDistricts.IsCreated && currentDistricts.Length > 0 && currentDistricts[i].m_District == SelectedDistrict))
                    {
                        // A building can be a special case and have a company.
                        // Do special case logic first so if building also has a company then the company defines the color of the building.
                        SetBuildingColorSpecialCase(in chunk, buildingEntity, buildingPrefab, ref colors, i);

                        // Set building color for a company, if any.
                        if (BuildingHasCompany(buildingEntity, buildingPrefab, out Entity companyEntity))
                        {
                            SetBuildingColorCompany(companyEntity, ref colors, i);
                        }
                    }

                    // Check if should set SubColor flag on the color.
                    // Logic adapted from Game.Rendering.ObjectColorSystem.CheckColors().
                    if ((ComponentLookupBuildingData[buildingPrefab].m_Flags & BuildingFlags.ColorizeLot) != 0 ||
                        (CollectionUtils.TryGet(destroyeds, i, out Destroyed destroyed) && destroyed.m_Cleared >= 0f) ||
                        (CollectionUtils.TryGet(underConstructions, i, out UnderConstruction underConstruction) && underConstruction.m_NewPrefab == Entity.Null))
                    {
                        // Set SubColor flag on the color.
                        // When SubColor flag is set, the lot that the building sits on is colorized.
                        Color color = colors[i];
                        color.m_SubColor = true;
                        colors[i] = color;
                    }
                }
            }

            /// <summary>
            /// Get whether or not there is a company at the building or its installed upgrades.
            /// </summary>
            private bool BuildingHasCompany(Entity buildingEntity, Entity buildingPrefab, out Entity companyEntity)
            {
                // Initialize.
                companyEntity = Entity.Null;

                // Building must have a renter buffer.
                // Building must allow sold, manufactured, or stored resources.
                // Logic adapted from Game.UI.InGame.CompanyUIUtils.HasCompany().
                if (BufferLookupRenter.TryGetBuffer(buildingEntity, out DynamicBuffer<Renter> bufferRenters) &&
                    ComponentLookupBuildingPropertyData.TryGetComponent(buildingPrefab, out BuildingPropertyData buildingPropertyData) &&
                    (buildingPropertyData.m_AllowedSold         != Resource.NoResource ||
                     buildingPropertyData.m_AllowedManufactured != Resource.NoResource ||
                     buildingPropertyData.m_AllowedStored       != Resource.NoResource))
                {
                    // Find and return the renter that has company data, if any.
                    for (int i = 0; i < bufferRenters.Length; i++)
                    {
                        if (ComponentLookupCompanyData.HasComponent(bufferRenters[i].m_Renter))
                        {
                            companyEntity = bufferRenters[i].m_Renter;
                            return true;
                        }
                    }
                }

                // No company at the building.
                // Get installed upgrades, if any.
                if (BufferLookupInstalledUpgrade.TryGetBuffer(buildingEntity, out DynamicBuffer<InstalledUpgrade> bufferInstalledUpgrades))
                {
                    // Do each installed upgrade.
                    for (int i = 0; i < bufferInstalledUpgrades.Length; i++)
                    {
                        // Get entity and prefab of installed upgrade.
                        Entity upgradeEntity = bufferInstalledUpgrades[i].m_Upgrade;
                        if (ComponentLookupPrefabRef.TryGetComponent(upgradeEntity, out PrefabRef upgradePrefabRef))
                        {
                            // Check if the installed upgrade has a company.
                            // This is a RECURSIVE call.
                            if (BuildingHasCompany(upgradeEntity, upgradePrefabRef.m_Prefab, out companyEntity))
                            {
                                return true;
                            }    
                        }
                    }
                }

                // Company not found.
                return false;
            }

            /// <summary>
            /// Set building color for a company building.
            /// </summary>
            private void SetBuildingColorCompany(Entity companyEntity, ref NativeArray<Color> colors, int colorsIndex)
            {
                // Logic adapated from Game.UI.InGame.CompanySection.OnProcess().

                // Company must have a resources buffer and industrial process data.
                if (BufferLookupResources.TryGetBuffer(companyEntity, out DynamicBuffer<Resources> bufferResources) &&
                    ComponentLookupPrefabRef.TryGetComponent(companyEntity, out PrefabRef companyPrefabRef) &&
                    ComponentLookupIndustrialProcessData.TryGetComponent(companyPrefabRef.m_Prefab, out IndustrialProcessData companyIndustrialProcessData))
                {
                    // Resources for requires, produces, sells, and stores.
                    Resource resourceRequires1 = Resource.NoResource;
                    Resource resourceRequires2 = Resource.NoResource;
                    Resource resourceProduces  = Resource.NoResource;
                    Resource resourceSells     = Resource.NoResource;
                    Resource resourceStores    = Resource.NoResource;

                    // Get input and output resources.
                    Resource resourceInput1 = companyIndustrialProcessData.m_Input1.m_Resource;
                    Resource resourceInput2 = companyIndustrialProcessData.m_Input2.m_Resource;
                    Resource resourceOutput = companyIndustrialProcessData.m_Output.m_Resource;
                            
                    // A company with service available (i.e. commercial) might require resources but always sells.
                    if (ComponentLookupServiceAvailable.HasComponent(companyEntity))
                    {
                        // Check if building requires input 1 or 2 resources.
                        if (resourceInput1 != Resource.NoResource && resourceInput1 != resourceOutput)
                        {
                            resourceRequires1 = resourceInput1;
                        }
                        if (resourceInput2 != Resource.NoResource && resourceInput2 != resourceOutput && resourceInput2 != resourceInput1)
                        {
                            resourceRequires2 = resourceInput2;
                        }

                        // Building sells the output resource.
                        resourceSells = resourceOutput;
                    }

                    // A processing company might require resources but always produces a resource.
                    else if (ComponentLookupProcessingCompany.HasComponent(companyEntity))
                    {
                        // Every extractor company is also a processing company.
                        // But not every processing company is an extractor company.
                        // Only a non-extractor company requires resources.
                        if (!ComponentLookupExtractorCompany.HasComponent(companyEntity))
                        {
                            // Building requires input 1 and 2 resources.
                            // One or the other may be NoResource, but one or both should always be a valid resource.
                            resourceRequires1 = resourceInput1;
                            resourceRequires2 = resourceInput2;
                        }

                        // Building produces the output resource.
                        resourceProduces = resourceOutput;
                    }

                    // A storage company stores.
                    else if (ComponentLookupStorageCompany.HasComponent(companyEntity))
                    {
                        // Get the storage company data.
                        if (ComponentLookupStorageCompanyData.TryGetComponent(companyPrefabRef.m_Prefab, out StorageCompanyData storageCompanyData))
                        {
                            // Building stores the stored resource.
                            resourceStores = storageCompanyData.m_StoredResources;
                        }
                    }

                    // Set building color according to the display option and the resources found above.
                    switch (DisplayOption)
                    {
                        case DisplayOption.Requires: SetBuildingColorForActiveInfomode(ref colors, colorsIndex, resourceRequires1, resourceRequires2); break;
                        case DisplayOption.Produces: SetBuildingColorForActiveInfomode(ref colors, colorsIndex, resourceProduces                    ); break;
                        case DisplayOption.Sells:    SetBuildingColorForActiveInfomode(ref colors, colorsIndex, resourceSells                       ); break;
                        case DisplayOption.Stores:   SetBuildingColorForActiveInfomode(ref colors, colorsIndex, resourceStores                      ); break;
                    }

                    // Add storage amounts for each resource in the buffer.
                    foreach (Resources resources in bufferResources)
                    {
                        if (resources.m_Resource == resourceRequires1) { AddStorageAmount(in StorageAmountsRequires, resourceRequires1, resources.m_Amount); }
                        if (resources.m_Resource == resourceRequires2) { AddStorageAmount(in StorageAmountsRequires, resourceRequires2, resources.m_Amount); }
                        if (resources.m_Resource == resourceProduces ) { AddStorageAmount(in StorageAmountsProduces, resourceProduces,  resources.m_Amount); }
                        if (resources.m_Resource == resourceSells    ) { AddStorageAmount(in StorageAmountsSells,    resourceSells,     resources.m_Amount); }
                        if (resources.m_Resource == resourceStores   ) { AddStorageAmount(in StorageAmountsStores,   resourceStores,    resources.m_Amount); }
                    }

                    // Increment company counts.
                    IncrementCompanyCount(in CompanyCountsRequires, resourceRequires1);
                    IncrementCompanyCount(in CompanyCountsRequires, resourceRequires2);
                    IncrementCompanyCount(in CompanyCountsProduces, resourceProduces);
                    IncrementCompanyCount(in CompanyCountsSells,    resourceSells);
                    IncrementCompanyCount(in CompanyCountsStores,   resourceStores);
                }
            }

            /// <summary>
            /// Set building color for special case buildings.
            /// </summary>
            private void SetBuildingColorSpecialCase(in ArchetypeChunk chunk, Entity entity, Entity prefab, ref NativeArray<Color> colors, int colorsIndex)
            {
                // Building must have a resources buffer that can be checked.
                if (!BufferLookupResources.TryGetBuffer(entity, out DynamicBuffer<Resources> bufferResources))
                {
                    return;
                }

                // Check for a recycling center that can produce multiple resources.
                // Empirical evidence suggests the produced resources are:  Metals, Plastics, Textiles, and Paper.
                if (chunk.Has(ref ComponentTypeHandleGarbageFacility) && chunk.Has(ref ComponentTypeHandleResourceProducer))
                {
                    // Check if recycling center is included.
                    if (IncludeRecyclingCenter)
                    {
                        // Do each resource in the buffer.
                        foreach (Resources bufferResource in bufferResources)
                        {
                            // Add storage amount for Produces.
                            // This will add resource amounts for resources this mod does not care about (e.g. garbage).
                            // These unneeded added resource amounts will simply be ignored in later logic.
                            // It is faster to add and ignore these few unneeded amounts than
                            // to determine which few unneeded resources should not be added in the first place.
                            AddStorageAmount(in StorageAmountsProduces, bufferResource.m_Resource, bufferResource.m_Amount);
                        }

                        // Set building color only for Produces display option.
                        if (DisplayOption == DisplayOption.Produces)
                        {
                            // Building color is set according to the top most active infomode
                            // corresponding to a resource that the building currently allows to be stored
                            // even if the building currently has none of that resource stored.

                            // Do each active infomode.
                            foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                            {
                                // Do each resource in the buffer.
                                Resource activeInfomodeResource = activeInfomode.resource;
                                foreach (Resources bufferResource in bufferResources)
                                {
                                    // Check if resource from buffer is resource for this active infomode.
                                    if (bufferResource.m_Resource == activeInfomodeResource)
                                    {
                                        // Found resource.
                                        // Set building color according to this active infomode.
                                        SetBuildingColor(ref colors, colorsIndex, activeInfomode.infomodeIndex);

                                        // Stop checking.
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    // Found recycling center. No need to check buildings further.
                    return;
                }

                // Check for an electricity producer that stores coal or petrochemicals.
                // This mod ignores the Incineration Plant which produces electricity
                // from stored garbage because garbage is not tracked by this mod.
                if (chunk.Has(ref ComponentTypeHandleElectricityProducer))
                {
                    // Check if coal and gas power plant is included.
                    if (IncludeCoalPowerPlant) { SetBuildingColorForStores(ref colors, colorsIndex, bufferResources, Resource.Coal          ); }
                    if (IncludeGasPowerPlant ) { SetBuildingColorForStores(ref colors, colorsIndex, bufferResources, Resource.Petrochemicals); }
                    return;
                }

                // Check for a hospital that stores pharmaceuticals.
                if (chunk.Has(ref ComponentTypeHandleHospital))
                {
                    // Check if medical facility is included.
                    if (IncludeMedicalFacility)
                    {
                        // Ordinary hospitals have Hospital and HospitalData with patient capacity.
                        // A main building with an available hospital upgrade will have Hospital and HospitalData
                        // but with no patient capacity and only the upgrade has patient capacity.
                        // Such a building is a hospital only if the upgrade is installed.
                        // So need to check both main building and installed upgrades for patient capacity.

                        // Check if building prefab has patient capacity.
                        bool hasCapacity = false;
                        if (ComponentLookupHospitalData.TryGetComponent(prefab, out HospitalData hospitalData) &&
                            hospitalData.m_PatientCapacity > 0)
                        {
                            hasCapacity = true;
                        }
                        else
                        {
                            // Do each installed upgrade.
                            if (BufferLookupInstalledUpgrade.TryGetBuffer(entity, out DynamicBuffer<InstalledUpgrade> installedUpgradeBuffer))
                            {
                                for (int i = 0; i < installedUpgradeBuffer.Length; i++)
                                {
                                    // Check if installed upgrade prefab has patient capacity.
                                    if (ComponentLookupPrefabRef.TryGetComponent(installedUpgradeBuffer[i].m_Upgrade, out PrefabRef upgradePrefabRef) &&
                                        ComponentLookupHospitalData.TryGetComponent(upgradePrefabRef.m_Prefab, out hospitalData) &&
                                        hospitalData.m_PatientCapacity > 0)
                                    {
                                        hasCapacity = true;
                                        break;
                                    }
                                }
                            }
                        }

                        // If building or upgrade has capacity, set building color for storing Pharmaceuticals.
                        if (hasCapacity)
                        {
                            SetBuildingColorForStores(ref colors, colorsIndex, bufferResources, Resource.Pharmaceuticals);
                        }
                    }

                    // Found hospital. No need to check buildings further.
                    return;
                }

                // Check for an emergency shelter that stores food.
                if (chunk.Has(ref ComponentTypeHandleEmergencyShelter))
                {
                    // Check if emergency shelter is included.
                    if (IncludeEmeregencyShelter)
                    {
                        // Ordinary emergency shelters have EmergencyShelter and EmergencyShelterData with shelter capacity.
                        // A main building with an available emergency shelter upgrade will have EmergencyShelter and EmergencyShelterData
                        // but with no shelter capacity and only the upgrade has shelter capacity.
                        // Such a building is an emergency shelter only if the upgrade is installed.
                        // So need to check both main building and installed upgrades for shelter capacity.

                        // Check if building prefab has shelter capacity.
                        bool hasCapacity = false;
                        if (ComponentLookupEmergencyShelterData.TryGetComponent(prefab, out EmergencyShelterData emergencyShelterData) &&
                            emergencyShelterData.m_ShelterCapacity > 0)
                        {
                            hasCapacity = true;
                        }
                        else
                        {
                            // Do each installed upgrade.
                            if (BufferLookupInstalledUpgrade.TryGetBuffer(entity, out DynamicBuffer<InstalledUpgrade> installedUpgradeBuffer))
                            {
                                for (int i = 0; i < installedUpgradeBuffer.Length; i++)
                                {
                                    // Check if installed upgrade prefab has shelter capacity.
                                    if (ComponentLookupPrefabRef.TryGetComponent(installedUpgradeBuffer[i].m_Upgrade, out PrefabRef upgradePrefabRef) &&
                                        ComponentLookupEmergencyShelterData.TryGetComponent(upgradePrefabRef.m_Prefab, out emergencyShelterData) &&
                                        emergencyShelterData.m_ShelterCapacity > 0)
                                    {
                                        hasCapacity = true;
                                        break;
                                    }
                                }
                            }
                        }

                        // If building or upgrade has capacity, set building color for storing Food.
                        if (hasCapacity)
                        {
                            SetBuildingColorForStores(ref colors, colorsIndex, bufferResources, Resource.Food);
                        }
                    }

                    // Found emergency shelter. No need to check buildings further.
                    return;
                }

                // Check for a cargo transport station that can store multiple resources.
                if (chunk.Has(ref ComponentTypeHandleCargoTransportStation))
                {
                    // Check if cargo station is included.
                    if (IncludeCargoStation)
                    {
                        // Do each resource in the buffer.
                        foreach (Resources bufferResource in bufferResources)
                        {
                            // Add storage amount for Stores.
                            // This will save resource amounts for resources this mod does not care about (e.g. mail).
                            // These unneeded added resource amounts will simply be ignored in later logic.
                            // It is faster to add and ignore these few unneeded amounts than
                            // to determine which few unneeded resources should not be added in the first place.
                            AddStorageAmount(in StorageAmountsStores, bufferResource.m_Resource, bufferResource.m_Amount);
                        }

                        // Set building color only for Stores display option.
                        if (DisplayOption == DisplayOption.Stores)
                        {
                            // Building color is set according to the top most active infomode
                            // corresponding to a resource that the building is currently storing.

                            // Do each active infomode.
                            foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                            {
                                // Do each resource in the buffer.
                                Resource activeInfomodeResource = activeInfomode.resource;
                                foreach (Resources bufferResource in bufferResources)
                                {
                                    // Check if resource from buffer is resource for this active infomode.
                                    if (bufferResource.m_Resource == activeInfomodeResource && bufferResource.m_Amount > 0)
                                    {
                                        // Found resource.
                                        // Set building color according to this active infomode.
                                        SetBuildingColor(ref colors, colorsIndex, activeInfomode.infomodeIndex);

                                        // Stop checking.
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    // Found cargo transport station. No need to check buildings further.
                    return;
                }
            }

            /// <summary>
            /// Set building color if the infomode is active corresponding to the resources to check.
            /// </summary>
            private void SetBuildingColorForActiveInfomode(
                ref NativeArray<Color> colors,
                int colorsIndex,
                Resource resourceToCheckForActive1,
                Resource resourceToCheckForActive2 = Resource.NoResource)
            {
                // Do each active infomode.
                foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                {
                    // Check if either resource to check is active.
                    if (resourceToCheckForActive1 == activeInfomode.resource || resourceToCheckForActive2 == activeInfomode.resource)
                    {
                        // Resource is active.
                        // Set building color according to this active infomode.
                        SetBuildingColor(ref colors, colorsIndex, activeInfomode.infomodeIndex);

                        // Stop checking.
                        break;
                    }
                }
            }

            /// <summary>
            /// Set building color and save storage amount for Stores display option.
            /// </summary>
            private void SetBuildingColorForStores
            (
                ref NativeArray<Color> colors,
                int colorsIndex,
                DynamicBuffer<Resources> bufferResources,
                Resource resourceToCheck
            )
            {
                // Do each resource in the buffer.
                foreach (Resources bufferResource in bufferResources)
                {
                    // Check if buffer resource is resource to check.
                    if (bufferResource.m_Resource == resourceToCheck)
                    {
                        // Found the resource.
                        // Set building color only for Stores display option.
                        if (DisplayOption == DisplayOption.Stores)
                        {
                            SetBuildingColorForActiveInfomode(ref colors, colorsIndex, resourceToCheck);
                        }

                        // Add storage amount for Stores.
                        AddStorageAmount(in StorageAmountsStores, resourceToCheck, bufferResource.m_Amount);

                        // Stop checking.
                        break;
                    }
                }
            }

            /// <summary>
            /// Set building color according to infomode index.
            /// </summary>
            private void SetBuildingColor(ref NativeArray<Color> colors, int colorsIndex, byte infomodeIndex)
            {
                // Much of the logic in this job is for this right here.
                // Building's SubColor remains unchanged.
                Color color = colors[colorsIndex];
                color.m_Index = infomodeIndex;
                color.m_Value = (byte)255;
                colors[colorsIndex] = color;
            }

            /// <summary>
            /// Add the storage amount for a resource.
            /// </summary>
            private void AddStorageAmount(in NativeArray<NativeArray<int>> storageAmounts, Resource resource, int amount)
            {
                // Resource must be valid.
                // Amount must be non-zero.
                if (resource != Resource.NoResource && amount != 0)
                {
                    // Add storage amount for this thread and resource.
                    // By having a separate entry for each thread, parallel threads will never access the same inner array at the same time.
                    NativeArray<int> storageAmountsForThread = storageAmounts[JobsUtility.ThreadIndex];
                    int resourceIndex = EconomyUtils.GetResourceIndex(resource);
                    storageAmountsForThread[resourceIndex] = storageAmountsForThread[resourceIndex] + amount;
                }
            }

            /// <summary>
            /// Increment the company count for a resource.
            /// </summary>
            private void IncrementCompanyCount(in NativeArray<NativeArray<int>> companyCounts, Resource resource)
            {
                // Resource must be valid.
                if (resource != Resource.NoResource)
                {
                    // Increment company count for this thread and resource.
                    // By having a separate entry for each thread, parallel threads will never access the same inner array at the same time.
                    NativeArray<int> companyCountsForThread = companyCounts[JobsUtility.ThreadIndex];
                    int resourceIndex = EconomyUtils.GetResourceIndex(resource);
                    companyCountsForThread[resourceIndex] = companyCountsForThread[resourceIndex] + 1;
                }
            }
        }


        /// <summary>
        /// Job to set the color of each attachment building to the color of the building to which it is attached.
        /// Attachment buildings are the lots attached to specialized industry hubs.
        /// </summary>
        [BurstCompile]
        private struct UpdateColorsJobAttachmentBuilding : IJobChunk
        {
            // Color component lookup to update.
            [NativeDisableParallelForRestriction] public ComponentLookup<Color> ComponentLookupColor;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Attachment> ComponentTypeHandleAttachment;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do each attachment entity.
                NativeArray<Attachment> attachments = chunk.GetNativeArray(ref ComponentTypeHandleAttachment);
                NativeArray<Entity    > entities    = chunk.GetNativeArray(EntityTypeHandle);
                for (int i = 0; i < entities.Length; i++)
                {
                    // Get the color of the attached entity.
                    if (ComponentLookupColor.TryGetComponent(attachments[i].m_Attached, out Color attachedColor))
                    {
                        // Set color of this attachment entity to the color of the attached entity.
                        Entity entity = entities[i];
                        Color color = ComponentLookupColor[entity];
                        color.m_Index = attachedColor.m_Index;
                        color.m_Value = attachedColor.m_Value;
                        ComponentLookupColor[entity] = color;
                    }
                }
            }
        }


        /// <summary>
        /// Job to set the color of each middle building to the color of its owner.
        /// Middle buildings include sub buildings (i.e. building upgrades placed around the perimeter of the main building).
        /// Logic is adapted from Game.Rendering.ObjectColorSystem.UpdateMiddleObjectColorsJob except:
        ///     Handle only buildings.
        ///     Handle port middle buildings specially.
        ///     Variables are renamed to improve readability.
        /// </summary>
        [BurstCompile]
        private struct UpdateColorsJobMiddleBuilding : IJobChunk
        {
            // Color component lookup to update.
            [NativeDisableParallelForRestriction] public ComponentLookup<Color> ComponentLookupColor;

            // Component lookups.
            [ReadOnly] public ComponentLookup<BuildingData          > ComponentLookupBuildingData;
            [ReadOnly] public ComponentLookup<GateData              > ComponentLookupGateData;
            [ReadOnly] public ComponentLookup<PrefabRef             > ComponentLookupPrefabRef;
            [ReadOnly] public ComponentLookup<StorageCompanyData    > ComponentLookupStorageCompanyData;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Owner             > ComponentTypeHandleOwner;
            [ReadOnly] public ComponentTypeHandle<PrefabRef         > ComponentTypeHandlePrefabRef;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            // Options.
            [ReadOnly] public DisplayOption DisplayOption;
            [ReadOnly] public bool IncludeCargoStation;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do each entity.
                NativeArray<Owner    > owners     = chunk.GetNativeArray(ref ComponentTypeHandleOwner);
                NativeArray<PrefabRef> prefabRefs = chunk.GetNativeArray(ref ComponentTypeHandlePrefabRef);
                NativeArray<Entity   > entities   = chunk.GetNativeArray(EntityTypeHandle);
                for (int i = 0; i < entities.Length; i++)
                {
                    // Get the entity and owner for this building.
                    Entity entity = entities[i];
                    Entity ownerEntity = owners[i].m_Owner;

                    // Check for port middle building.
                    // All port middle buildings have an Owner that is the main port gate building.
                    // A main port gate building's prefab has the GateData component.
                    if (ComponentLookupPrefabRef.TryGetComponent(ownerEntity, out PrefabRef ownerPrefabRef) &&
                        ComponentLookupGateData.HasComponent(ownerPrefabRef.m_Prefab))
                    {
                        // This is a port middle building.
                        // A port middle building color is set only for the Stores display option
                        // and only if the Include Cargo Station option is turned on.
                        // For all other cases the building remains default color.
                        if (DisplayOption == DisplayOption.Stores && IncludeCargoStation)
                        {
                            SetPortBuildingColor(entity, prefabRefs[i].m_Prefab, ownerEntity);
                        }
                    }
                    else
                    {
                        // This is not a port middle building.
                        // Set this building color same as the owner building.
                        SetBuildingColorToOwnerColor(entity, ownerEntity);
                    }
                }
            }

            /// <summary>
            /// Set color for a port middle building.
            /// </summary>
            private void SetPortBuildingColor(Entity entity, Entity prefab, Entity ownerEntity)
            {
                // Port middle buildings are the port buildings placed in the port's area and include:
                //      Auxiliary Port Gate.
                //      Employee Canteen, Port Security, Emergency Response.
                //      Container Crane.
                //      Passenger Terminal.
                //      Intermodal Train Terminal.
                //      Container Yard, Cargo Warehouse, Tank Farm, Bulk Storage Yard (collectively "storage").

                // Check for auxiliary port gate building.
                // An auxiliary port gate building's prefab has GateData component.
                if (ComponentLookupGateData.HasComponent(prefab))
                {
                    // Set auxiliary port gate building color same as the main port gate building.
                    SetBuildingColorToOwnerColor(entity, ownerEntity);
                    return;
                }

                // Check for port storage building.
                // All port storage buildings have a prefab with StorageCompanyData that defines stored resources.
                // Note that the Container Crane's prefab has StorageCompanyData but does not define stored resources.
                if (ComponentLookupStorageCompanyData.TryGetComponent(prefab, out StorageCompanyData storageCompanyData) &&
                    storageCompanyData.m_StoredResources != Resource.NoResource)
                {
                    // Set port storage building color same as the main port gate building.
                    SetBuildingColorToOwnerColor(entity, ownerEntity);

                    // Determine whether or not this building's lot should be colorized.
                    // This is specified in Color.m_SubColor.
                    // But for unknown reasons, the Bulk Storage Yards start with m_SubColor = false
                    // when the Bulk Storage Yards should have m_SubColor = true;
                    // So need to set m_SubColor here according to the building prefab's ColorizeLot flag.
                    // For simplicity, set SubColor for all port storage buildings, not just Bulk Storage Yards.
                    Color color = ComponentLookupColor[entity];
                    color.m_SubColor =
                        ComponentLookupBuildingData.TryGetComponent(prefab, out BuildingData buildingData) &&
                        (buildingData.m_Flags & BuildingFlags.ColorizeLot) != 0;
                    ComponentLookupColor[entity] = color;

                    return;
                }

                // If get here without setting building color, then building simply remains default color.
            }

            /// <summary>
            /// Set building color same as owner building.
            /// </summary>
            private void SetBuildingColorToOwnerColor(Entity entity, Entity ownerEntity)
            {
                // Get color of owner building.
                if (ComponentLookupColor.TryGetComponent(ownerEntity, out Color ownerColor))
                {
                    // Set color of this entity to color of owner entity.
                    // Building's SubColor remains unchanged.
                    Color color = ComponentLookupColor[entity];
                    color.m_Index = ownerColor.m_Index;
                    color.m_Value = ownerColor.m_Value;
                    ComponentLookupColor[entity] = color;
                }
            }
        }


        /// <summary>
        /// Job to set the color of a temp object to the color of its original.
        /// Temp objects are when cursor is hovered over an object.
        /// Logic copied exactly from Game.Rendering.ObjectColorSystem.UpdateTempObjectColorsJob except variables are renamed to improve readability.
        /// </summary>
        [BurstCompile]
        private struct UpdateColorsJobTempObject : IJobChunk
        {
            // Color component lookup to update.
            [NativeDisableParallelForRestriction] public ComponentLookup<Color> ComponentLookupColor;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Temp> ComponentTypeHandleTemp;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Set color of object to color of its original.
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                NativeArray<Temp> temps = chunk.GetNativeArray(ref ComponentTypeHandleTemp);
                for (int i = 0; i < temps.Length; i++)
                {
                    if (ComponentLookupColor.TryGetComponent(temps[i].m_Original, out Color originalColor))
                    {
                        ComponentLookupColor[entities[i]] = originalColor;
                    }
                }
            }
        }


        /// <summary>
        /// Job to set the color of each sub object to the color of its owner.
        /// Sub objects include building extensions (i.e. building upgrades attached to the main building).
        /// Logic copied exactly from Game.Rendering.ObjectColorSystem.UpdateSubObjectColorsJob except
        /// variables are renamed to improve readability and if owner color cannot be found leave default color.
        /// </summary>
        [BurstCompile]
        private struct UpdateColorsJobSubObject : IJobChunk
        {
            // Color component lookup to update.
            [NativeDisableParallelForRestriction] public ComponentLookup<Color> ComponentLookupColor;

            // Component lookups.
            [ReadOnly] public ComponentLookup<Building      > ComponentLookupBuilding;
            [ReadOnly] public ComponentLookup<Elevation     > ComponentLookupElevation;
            [ReadOnly] public ComponentLookup<Owner         > ComponentLookupOwner;
            [ReadOnly] public ComponentLookup<Vehicle       > ComponentLookupVehicle;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Elevation > ComponentTypeHandleElevation;
            [ReadOnly] public ComponentTypeHandle<Owner     > ComponentTypeHandleOwner;
            [ReadOnly] public ComponentTypeHandle<Tree      > ComponentTypeHandleTree;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Owner > owners   = chunk.GetNativeArray(ref ComponentTypeHandleOwner);
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                if (chunk.Has(ref ComponentTypeHandleTree))
                {
                    NativeArray<Elevation> elevations = chunk.GetNativeArray(ref ComponentTypeHandleElevation);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity entity = entities[i];
                        Owner owner = owners[i];
                        Elevation elevation;
                        bool flag = CollectionUtils.TryGet(elevations, i, out elevation) && (elevation.m_Flags & ElevationFlags.OnGround) == 0;
                        bool flag2 = flag && !ComponentLookupColor.HasComponent(owner.m_Owner);
                        Owner newOwner;
                        while (ComponentLookupOwner.TryGetComponent(owner.m_Owner, out newOwner) && !ComponentLookupBuilding.HasComponent(owner.m_Owner) && !ComponentLookupVehicle.HasComponent(owner.m_Owner))
                        {
                            if (flag2)
                            {
                                if (ComponentLookupColor.HasComponent(owner.m_Owner))
                                {
                                    flag2 = false;
                                }
                                else
                                {
                                    flag &= ComponentLookupElevation.TryGetComponent(owner.m_Owner, out elevation) && (elevation.m_Flags & ElevationFlags.OnGround) == 0;
                                }
                            }
                            owner = newOwner;
                        }
                        if (ComponentLookupColor.TryGetComponent(owner.m_Owner, out Color color) && (flag || color.m_SubColor))
                        {
                            ComponentLookupColor[entity] = color;
                        }
                    }
                    return;
                }

                for (int j = 0; j < entities.Length; j++)
                {
                    Owner owner = owners[j];
                    Owner newOwner;
                    while (ComponentLookupOwner.TryGetComponent(owner.m_Owner, out newOwner) && !ComponentLookupBuilding.HasComponent(owner.m_Owner) && !ComponentLookupVehicle.HasComponent(owner.m_Owner))
                    {
                        owner = newOwner;
                    }
                    if (ComponentLookupColor.TryGetComponent(owner.m_Owner, out Color color))
                    {
                        ComponentLookupColor[entities[j]] = color;
                    }
                }
            }
        }



        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        // The game's instance of this system.
        private static BuildingColorSystem  _buildingColorSystem;

        // Other systems.
        private ToolSystem _toolSystem;
        private ResourceLocatorUISystem _resourceLocatorUISystem;

        // Entity queries.
        private EntityQuery _queryActiveBuildingData;
        private EntityQuery _queryDefault;
        private EntityQuery _queryCargoVehicle;
        private EntityQuery _queryMainBuilding;
        private EntityQuery _queryAttachmentBuilding;
        private EntityQuery _queryMiddleBuilding;
        private EntityQuery _queryTempObject;
        private EntityQuery _querySubObject;
        
        // Harmony ID.
        private const string HarmonyID = "rcav8tr." + ModAssemblyInfo.Name;

        // Nested arrays to hold storage amounts and company counts populated by jobs.
        // The outer array is one for each possible thread.
        // The inner array is one for each resource.
        private NativeArray<NativeArray<int>> _storageAmountsRequires;
        private NativeArray<NativeArray<int>> _storageAmountsProduces;
        private NativeArray<NativeArray<int>> _storageAmountsSells;
        private NativeArray<NativeArray<int>> _storageAmountsStores;
        private NativeArray<NativeArray<int>> _storageAmountsInTransit;

        private NativeArray<NativeArray<int>> _companyCountsRequires;
        private NativeArray<NativeArray<int>> _companyCountsProduces;
        private NativeArray<NativeArray<int>> _companyCountsSells;
        private NativeArray<NativeArray<int>> _companyCountsStores;

        // Arrays to hold total storage amounts and company counts by resource.
        private static readonly int ResourceCount = EconomyUtils.ResourceCount;
        private int[] _totalStorageAmountsRequires  = new int[ResourceCount];
        private int[] _totalStorageAmountsProduces  = new int[ResourceCount];
        private int[] _totalStorageAmountsSells     = new int[ResourceCount];
        private int[] _totalStorageAmountsStores    = new int[ResourceCount];
        private int[] _totalStorageAmountsInTransit = new int[ResourceCount];

        private int[] _totalCompanyCountsRequires   = new int[ResourceCount];
        private int[] _totalCompanyCountsProduces   = new int[ResourceCount];
        private int[] _totalCompanyCountsSells      = new int[ResourceCount];
        private int[] _totalCompanyCountsStores     = new int[ResourceCount];

        // Lock for accessing total storage amounts and company counts.
        private readonly object _totalStorageAmountsCompanyCountsLock = new object();

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.log.Info($"{nameof(BuildingColorSystem)}.{nameof(OnCreate)}");

            // Save the game's instance of this system.
            _buildingColorSystem = this;

            // Get other systems.
            _toolSystem              = base.World.GetOrCreateSystemManaged<ToolSystem>();
            _resourceLocatorUISystem = base.World.GetOrCreateSystemManaged<ResourceLocatorUISystem>();

            // Query to get active building datas.
            // Adapted from Game.Rendering.ObjectColorSystem.m_InfomodeQuery.
            // All infomodes for this mod are BuildingInfomodePrefab which generates InfoviewBuildingData.
            // So there is no need to include other datas.
            _queryActiveBuildingData = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<InfomodeActive>(),
                        ComponentType.ReadOnly<InfoviewBuildingData>(),
                    }
                }
            );

            // Query to get default objects (i.e. every object that has a color).
            // Adapted from first part of Game.Rendering.ObjectColorSystem.m_ObjectQuery except Owner is not excluded.
		    _queryDefault = GetEntityQuery
            (
                new EntityQueryDesc
		        {
			        All = new ComponentType[]
			        {
				        ComponentType.ReadOnly <Object>(),
				        ComponentType.ReadWrite<Color>(),
			        },
			        None = new ComponentType[]
			        {
				        ComponentType.ReadOnly<Hidden>(),
				        ComponentType.ReadOnly<Deleted>(),
			        }
		        }
            );

            // Query to get cargo vehicles.
            // Adapted from second part of Game.Rendering.ObjectColorSystem.m_ObjectQuery except only for cargo vehicles.
            _queryCargoVehicle = GetEntityQuery
            (
                new EntityQueryDesc
                {
			        All = new ComponentType[]
			        {
				        ComponentType.ReadOnly <Object>(),
                        ComponentType.ReadOnly <Vehicle>(),
				        ComponentType.ReadWrite<Color>(),
			        },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<DeliveryTruck>(),    // All road cargo vehicles.
                        ComponentType.ReadOnly<CargoTransport>(),   // Cargo trains, ships, and airplanes.
                    },
			        None = new ComponentType[]
			        {
                        // Do not exclude hidden vehicles because they must be included in the in transit data.
				        //ComponentType.ReadOnly<Hidden>(),

				        ComponentType.ReadOnly<Deleted>(),      // Exclude deleted vehicles.
				        ComponentType.ReadOnly<Temp>(),         // Exclude temp (see temp objects query below).
			        }
                }
            );

            // Query to get main buildings.
            // Adapted from second part of Game.Rendering.ObjectColorSystem.m_ObjectQuery
            // except only Building is included (i.e. Vehicle, Creature, and UtilityObject are not included).
            // See cargo vehicle query above.
		    _queryMainBuilding = GetEntityQuery
            (
                new EntityQueryDesc
		        {
			        All = new ComponentType[]
			        {
				        ComponentType.ReadOnly <Object>(),
                        ComponentType.ReadOnly <Building>(),
				        ComponentType.ReadWrite<Color>(),
			        },
			        None = new ComponentType[]
			        {
                        // Do not exclude hidden buildings because they must be included in the storage data.
				        //ComponentType.ReadOnly<Hidden>(),

                        ComponentType.ReadOnly<Abandoned>(),            // Exclude abandoned buildings. 
                        ComponentType.ReadOnly<Condemned>(),            // Exclude condemned buildings.
				        ComponentType.ReadOnly<Deleted>(),              // Exclude deleted   buildings.
                        ComponentType.ReadOnly<Destroyed>(),            // Exclude destroyed buildings.
                        ComponentType.ReadOnly<OutsideConnection>(),    // Exclude outside connections.
				        ComponentType.ReadOnly<Owner>(),                // Exclude subbuildings (see middle buildings query below).
                        ComponentType.ReadOnly<Attachment>(),           // Exclude attachments  (see attachments      query below).
				        ComponentType.ReadOnly<Temp>(),                 // Exclude temp         (see temp objects     query below).
			        }
		        }
            );

            // Query to get attachment buildings.
            // Attachments are the lots attached to specialized industry.
            _queryAttachmentBuilding = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly <Building>(),
                        ComponentType.ReadOnly <Attachment>(),
                        ComponentType.ReadWrite<Color>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Owner>(),        // Exclude middle buildings (see middle buildings query below).
                        ComponentType.ReadOnly<Hidden>(),
                        ComponentType.ReadOnly<Deleted>(),
                    }
                }
            );

            // Query to get middle buildings.
            // Middle buildings include sub buildings (i.e. building upgrades placed around the perimeter of the main building).
            // Copied exactly from Game.Rendering.ObjectColorSystem except Vehicles with Controllers and attachments are excluded.
            _queryMiddleBuilding = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly <Building>(),
                        ComponentType.ReadOnly <Owner>(),
                        ComponentType.ReadWrite<Color>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Attachment>(),   // Exclude attachments (see attachment buildings query above).
                        ComponentType.ReadOnly<Hidden>(),
                        ComponentType.ReadOnly<Deleted>(),
                    }
                }
            );

            // Query to get Temp objects.
            // Temp objects are when cursor is hovered over an object.
            // The original object gets hidden and a temp object is placed over the original.
            // Copied exactly from Game.Rendering.ObjectColorSystem.m_TempObjectQuery.
            _queryTempObject = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly <Object>(),
                        ComponentType.ReadWrite<Color>(),
                        ComponentType.ReadOnly <Temp>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Hidden>(),
                        ComponentType.ReadOnly<Deleted>(),
                    }
                }
            );

            // Query that will get building extensions (i.e. the building upgrades attached to the main building).
            // This query will likely also get other sub objects.
            // Copied exactly from Game.Rendering.ObjectColorSystem.m_SubObjectQuery.
            _querySubObject = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly <Object>(),
                        ComponentType.ReadOnly <Owner>(),
                        ComponentType.ReadWrite<Color>(),
                    },
                    None = new ComponentType[]
                    {
                        // Exclude all same things as base game logic.
                        ComponentType.ReadOnly<Hidden>(),
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Vehicle>(),
                        ComponentType.ReadOnly<Creature>(),
                        ComponentType.ReadOnly<Building>(),
                        ComponentType.ReadOnly<UtilityObject>(),
                    }
                }
            );

            // Create nested arrays to hold storage amounts and company counts.
            _storageAmountsRequires  = ProductionConsumptionUtils.CreateArrays();
            _storageAmountsProduces  = ProductionConsumptionUtils.CreateArrays();
            _storageAmountsSells     = ProductionConsumptionUtils.CreateArrays();
            _storageAmountsStores    = ProductionConsumptionUtils.CreateArrays();
            _storageAmountsInTransit = ProductionConsumptionUtils.CreateArrays();
            
            _companyCountsRequires   = ProductionConsumptionUtils.CreateArrays();
            _companyCountsProduces   = ProductionConsumptionUtils.CreateArrays();
            _companyCountsSells      = ProductionConsumptionUtils.CreateArrays();
            _companyCountsStores     = ProductionConsumptionUtils.CreateArrays();

            // Use Harmony to patch ObjectColorSystem.OnUpdate with BuildingColorSystem.OnUpdatePrefix.
            // When this mod's infoview is displayed, it is not necessary to execute ObjectColorSystem.OnUpdate.
            // By using a Harmony prefix, this system can prevent the execution of ObjectColorSystem.OnUpdate.
            // Note that ObjectColorSystem.OnUpdate can be patched, but the jobs in ObjectColorSystem cannot be patched because they are burst compiled.
            // Create this patch last to ensure all other initializations are complete before OnUpdatePrefix is called.
            MethodInfo originalMethod = typeof(ObjectColorSystem).GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (originalMethod == null)
            {
                Mod.log.Error($"Unable to find original method {nameof(ObjectColorSystem)}.OnUpdate.");
                return;
            }
            MethodInfo prefixMethod = typeof(BuildingColorSystem).GetMethod(nameof(OnUpdatePrefix), BindingFlags.Static | BindingFlags.NonPublic);
            if (prefixMethod == null)
            {
                Mod.log.Error($"Unable to find patch prefix method {nameof(BuildingColorSystem)}.{nameof(OnUpdatePrefix)}.");
                return;
            }
            new Harmony(HarmonyID).Patch(originalMethod, new HarmonyMethod(prefixMethod), null);
        }

        /// <summary>
        /// One time system destruction.
        /// </summary>
        protected override void OnDestroy()
        {
            // Dispose of persistent storage amount and company count arrays.
            ProductionConsumptionUtils.DisposeArrays(in _storageAmountsRequires );
            ProductionConsumptionUtils.DisposeArrays(in _storageAmountsProduces );
            ProductionConsumptionUtils.DisposeArrays(in _storageAmountsSells    );
            ProductionConsumptionUtils.DisposeArrays(in _storageAmountsStores   );
            ProductionConsumptionUtils.DisposeArrays(in _storageAmountsInTransit);
            
            ProductionConsumptionUtils.DisposeArrays(in _companyCountsRequires  );
            ProductionConsumptionUtils.DisposeArrays(in _companyCountsProduces  );
            ProductionConsumptionUtils.DisposeArrays(in _companyCountsSells     );
            ProductionConsumptionUtils.DisposeArrays(in _companyCountsStores    );

            base.OnDestroy();
        }

        /// <summary>
        /// Called every frame, even when at the main menu.
        /// </summary>
        protected override void OnUpdate()
        {
            // Nothing to do here, but implementation is required.
        }

        /// <summary>
        /// Prefix patch method for ObjectColorSystem.OnUpdate().
        /// </summary>
        private static bool OnUpdatePrefix()
        {
            // Call the implementation of OnUpdate for the game's instance of this system.
            return _buildingColorSystem.OnUpdateImpl();
        }

        /// <summary>
        /// Implementation method that potentially replaces the call to ObjectColorSystem.OnUpdate().
        /// </summary>
        private bool OnUpdateImpl()
        {
            // If no active infoview, then execute original game logic.
            if (_toolSystem.activeInfoview == null)
            {
                return true;
            }

            // If active infoview is not for this mod, then execute original game logic.
            // The name of this mod's only infoview is the mod assembly name.
            if (_toolSystem.activeInfoview.name != ModAssemblyInfo.Name)
            {
                return true;
            }

            // Active infoview is for this mod.

            // Create native array of active infomodes.
            ComponentTypeHandle<InfoviewBuildingData> componentTypeHandleInfoviewBuildingData = SystemAPI.GetComponentTypeHandle<InfoviewBuildingData>(true);
            ComponentTypeHandle<InfomodeActive      > componentTypeHandleInfomodeActive       = SystemAPI.GetComponentTypeHandle<InfomodeActive      >(true);
            List<ActiveInfomode> tempActiveInfomodes = new();
            NativeArray<ArchetypeChunk> tempActiveBuildingDataChunks = _queryActiveBuildingData.ToArchetypeChunkArray(Allocator.Temp);
            foreach (ArchetypeChunk activeBuildingDataChunk in tempActiveBuildingDataChunks)
            {
                // Do each active building data.
                NativeArray<InfoviewBuildingData> infoviewBuildingDatas = activeBuildingDataChunk.GetNativeArray(ref componentTypeHandleInfoviewBuildingData);
                NativeArray<InfomodeActive      > infomodeActives       = activeBuildingDataChunk.GetNativeArray(ref componentTypeHandleInfomodeActive);
                for (int j = 0; j < infoviewBuildingDatas.Length; j++)
                {
                    // Skip special cases.
                    RLBuildingType activeBuildingType = (RLBuildingType)infoviewBuildingDatas[j].m_Type;
                    if (!RLBuildingTypeUtils.IsSpecialCase(activeBuildingType))
                    {
                        // This infomode is active.
                        tempActiveInfomodes.Add(new ActiveInfomode()
                        {
                            resource = RLBuildingTypeUtils.GetResource(activeBuildingType), 
                            infomodeIndex = (byte)infomodeActives[j].m_Index,
                            buildingType = activeBuildingType,
                        });
                    }
                }
            }
            tempActiveInfomodes.Sort();
            NativeArray<ActiveInfomode> activeInfomodes = new(tempActiveInfomodes.ToArray(), Allocator.TempJob);

            // Initialize storage amounts and company counts.
            ProductionConsumptionUtils.InitializeArrays(in _storageAmountsRequires );
            ProductionConsumptionUtils.InitializeArrays(in _storageAmountsProduces );
            ProductionConsumptionUtils.InitializeArrays(in _storageAmountsSells    );
            ProductionConsumptionUtils.InitializeArrays(in _storageAmountsStores   );
            ProductionConsumptionUtils.InitializeArrays(in _storageAmountsInTransit);
            
            ProductionConsumptionUtils.InitializeArrays(in _companyCountsRequires  );
            ProductionConsumptionUtils.InitializeArrays(in _companyCountsProduces  );
            ProductionConsumptionUtils.InitializeArrays(in _companyCountsSells     );
            ProductionConsumptionUtils.InitializeArrays(in _companyCountsStores    );


            // Create a job to update default colors.
            UpdateColorsJobDefault updateColorsJobDefault = new()
            {
                ComponentTypeHandleColor = SystemAPI.GetComponentTypeHandle<Color>(false),
            };


            // Create a job to update cargo vehicle colors.
            UpdateColorsJobCargoVehicle updateColorsJobCargoVehicle = new()
            {
                ComponentTypeHandleColor            = SystemAPI.GetComponentTypeHandle  <Color              >(false),
                ComponentLookupColor                = SystemAPI.GetComponentLookup      <Color              >(false),

                BufferLookupResources               = SystemAPI.GetBufferLookup         <Resources          >(true),

                ComponentLookupController           = SystemAPI.GetComponentLookup      <Controller         >(true),
                ComponentLookupCurrentDistrict      = SystemAPI.GetComponentLookup      <CurrentDistrict    >(true),
                ComponentLookupOutsideConnection    = SystemAPI.GetComponentLookup      <OutsideConnection  >(true),
                ComponentLookupOwner                = SystemAPI.GetComponentLookup      <Owner              >(true),
                ComponentLookupPropertyRenter       = SystemAPI.GetComponentLookup      <PropertyRenter     >(true),
                ComponentLookupTarget               = SystemAPI.GetComponentLookup      <Target             >(true),

                ComponentTypeHandleDeliveryTruck    = SystemAPI.GetComponentTypeHandle  <DeliveryTruck      >(true),

                EntityTypeHandle                    = SystemAPI.GetEntityTypeHandle(),

                ActiveInfomodes                     = activeInfomodes,

                SelectedDistrict                    = _resourceLocatorUISystem.selectedDistrict,
                SelectedDistrictIsEntireCity        = _resourceLocatorUISystem.selectedDistrict == ResourceLocatorUISystem.EntireCity,

                StorageAmountsInTransit             = _storageAmountsInTransit,
            };


            // Create a job to update main building colors.
            UpdateColorsJobMainBuilding updateColorsJobMainBuilding = new()
            {
                ComponentTypeHandleColor                    = SystemAPI.GetComponentTypeHandle<Color>(false),

                BufferLookupInstalledUpgrade                = SystemAPI.GetBufferLookup<InstalledUpgrade                >(true),
                BufferLookupRenter                          = SystemAPI.GetBufferLookup<Renter                          >(true),
                BufferLookupResources                       = SystemAPI.GetBufferLookup<Resources                       >(true),
                
                ComponentLookupBuildingData                 = SystemAPI.GetComponentLookup<BuildingData                 >(true),
                ComponentLookupBuildingPropertyData         = SystemAPI.GetComponentLookup<BuildingPropertyData         >(true),
                ComponentLookupCompanyData                  = SystemAPI.GetComponentLookup<CompanyData                  >(true),
                ComponentLookupEmergencyShelterData         = SystemAPI.GetComponentLookup<EmergencyShelterData         >(true),
                ComponentLookupExtractorCompany             = SystemAPI.GetComponentLookup<ExtractorCompany             >(true),
                ComponentLookupHospitalData                 = SystemAPI.GetComponentLookup<HospitalData                 >(true),
                ComponentLookupIndustrialProcessData        = SystemAPI.GetComponentLookup<IndustrialProcessData        >(true),
                ComponentLookupPrefabRef                    = SystemAPI.GetComponentLookup<PrefabRef                    >(true),
                ComponentLookupProcessingCompany            = SystemAPI.GetComponentLookup<ProcessingCompany            >(true),
                ComponentLookupServiceAvailable             = SystemAPI.GetComponentLookup<ServiceAvailable             >(true),
                ComponentLookupStorageCompany               = SystemAPI.GetComponentLookup<StorageCompany               >(true),
                ComponentLookupStorageCompanyData           = SystemAPI.GetComponentLookup<StorageCompanyData           >(true),
                
                ComponentTypeHandleCargoTransportStation    = SystemAPI.GetComponentTypeHandle<CargoTransportStation    >(true),
                ComponentTypeHandleElectricityProducer      = SystemAPI.GetComponentTypeHandle<ElectricityProducer      >(true),
                ComponentTypeHandleEmergencyShelter         = SystemAPI.GetComponentTypeHandle<EmergencyShelter         >(true),
                ComponentTypeHandleGarbageFacility          = SystemAPI.GetComponentTypeHandle<GarbageFacility          >(true),
                ComponentTypeHandleHospital                 = SystemAPI.GetComponentTypeHandle<Hospital                 >(true),
                ComponentTypeHandleResourceProducer         = SystemAPI.GetComponentTypeHandle<ResourceProducer         >(true),

                ComponentTypeHandleCurrentDistrict          = SystemAPI.GetComponentTypeHandle<CurrentDistrict          >(true),
                ComponentTypeHandleDestroyed                = SystemAPI.GetComponentTypeHandle<Destroyed                >(true),
                ComponentTypeHandlePrefabRef                = SystemAPI.GetComponentTypeHandle<PrefabRef                >(true),
                ComponentTypeHandleUnderConstruction        = SystemAPI.GetComponentTypeHandle<UnderConstruction        >(true),
                
                EntityTypeHandle                            = SystemAPI.GetEntityTypeHandle(),
                
                ActiveInfomodes                             = activeInfomodes,

                IncludeRecyclingCenter                      = Mod.ModSettings.IncludeRecyclingCenter,
                IncludeCoalPowerPlant                       = Mod.ModSettings.IncludeCoalPowerPlant,
                IncludeGasPowerPlant                        = Mod.ModSettings.IncludeGasPowerPlant,
                IncludeMedicalFacility                      = Mod.ModSettings.IncludeMedicalFacility,
                IncludeEmeregencyShelter                    = Mod.ModSettings.IncludeEmeregencyShelter,
                IncludeCargoStation                         = Mod.ModSettings.IncludeCargoStation,

                SelectedDistrict                            = _resourceLocatorUISystem.selectedDistrict,
                SelectedDistrictIsEntireCity                = _resourceLocatorUISystem.selectedDistrict == ResourceLocatorUISystem.EntireCity,

                DisplayOption                               = Mod.ModSettings.DisplayOption,
                
                StorageAmountsRequires                      = _storageAmountsRequires,
                StorageAmountsProduces                      = _storageAmountsProduces,
                StorageAmountsSells                         = _storageAmountsSells,
                StorageAmountsStores                        = _storageAmountsStores,
                
                CompanyCountsRequires                       = _companyCountsRequires,
                CompanyCountsProduces                       = _companyCountsProduces,
                CompanyCountsSells                          = _companyCountsSells,
                CompanyCountsStores                         = _companyCountsStores,
            };


            // Create a job to update attachment building colors.
            UpdateColorsJobAttachmentBuilding updateColorsJobAttachmentBuilding = new()
            {
                ComponentLookupColor            = SystemAPI.GetComponentLookup<Color>(false),
                ComponentTypeHandleAttachment   = SystemAPI.GetComponentTypeHandle<Attachment>(true),
                EntityTypeHandle                = SystemAPI.GetEntityTypeHandle(),
            };


            // Create a job to update middle building colors.
            UpdateColorsJobMiddleBuilding updateColorsJobMiddleBuilding = new()
            {
                ComponentLookupColor                = SystemAPI.GetComponentLookup<Color                >(false),
                
                ComponentLookupBuildingData         = SystemAPI.GetComponentLookup<BuildingData         >(true),
                ComponentLookupGateData             = SystemAPI.GetComponentLookup<GateData             >(true),
                ComponentLookupPrefabRef            = SystemAPI.GetComponentLookup<PrefabRef            >(true),
                ComponentLookupStorageCompanyData   = SystemAPI.GetComponentLookup<StorageCompanyData   >(true),
                
                ComponentTypeHandleOwner            = SystemAPI.GetComponentTypeHandle<Owner            >(true),
                ComponentTypeHandlePrefabRef        = SystemAPI.GetComponentTypeHandle<PrefabRef        >(true),

                EntityTypeHandle                    = SystemAPI.GetEntityTypeHandle(),

                DisplayOption                       = Mod.ModSettings.DisplayOption,
                IncludeCargoStation                 = Mod.ModSettings.IncludeCargoStation,
            };


            // Create a job to update temp object colors.
            UpdateColorsJobTempObject updateColorsJobTempObject = new()
            {
                ComponentLookupColor    = SystemAPI.GetComponentLookup<Color>(false),
                ComponentTypeHandleTemp = SystemAPI.GetComponentTypeHandle<Temp>(true),
                EntityTypeHandle        = SystemAPI.GetEntityTypeHandle(),
            };

            
            // Create a job to update sub object colors.
            UpdateColorsJobSubObject updateColorsJobSubObject = new()
            {
                ComponentLookupColor            = SystemAPI.GetComponentLookup<Color            >(false),

                ComponentLookupBuilding         = SystemAPI.GetComponentLookup<Building         >(true),
                ComponentLookupElevation        = SystemAPI.GetComponentLookup<Elevation        >(true),
                ComponentLookupOwner            = SystemAPI.GetComponentLookup<Owner            >(true),
                ComponentLookupVehicle          = SystemAPI.GetComponentLookup<Vehicle          >(true),

                ComponentTypeHandleElevation    = SystemAPI.GetComponentTypeHandle<Elevation    >(true),
                ComponentTypeHandleOwner        = SystemAPI.GetComponentTypeHandle<Owner        >(true),
                ComponentTypeHandleTree         = SystemAPI.GetComponentTypeHandle<Tree         >(true),

                EntityTypeHandle                = SystemAPI.GetEntityTypeHandle(),
            };


            // Schedule the jobs with dependencies so the jobs run in order.
            // The cargo vehicle and main building jobs can run at the same time as each other but only after the default job.
            // Schedule each job to execute in parallel (i.e. job uses multiple threads, if available).
            // Parallel threads execute much faster than a single thread.
            // Do attachment buildings before middle buildings because some middle buildings have an attachment building as owner.
            JobHandle jobHandleDefault            = JobChunkExtensions.ScheduleParallel(updateColorsJobDefault,            _queryDefault,            base.Dependency);
            JobHandle jobHandleCargoVehicle       = JobChunkExtensions.ScheduleParallel(updateColorsJobCargoVehicle,       _queryCargoVehicle,       jobHandleDefault);
            JobHandle jobHandleMainBuilding       = JobChunkExtensions.ScheduleParallel(updateColorsJobMainBuilding,       _queryMainBuilding,       jobHandleDefault);
            JobHandle jobHandleAttachmentBuilding = JobChunkExtensions.ScheduleParallel(updateColorsJobAttachmentBuilding, _queryAttachmentBuilding, jobHandleMainBuilding);
            JobHandle jobHandleMiddleBuilding     = JobChunkExtensions.ScheduleParallel(updateColorsJobMiddleBuilding,     _queryMiddleBuilding,     jobHandleAttachmentBuilding);
            JobHandle jobHandleTempObject         = JobChunkExtensions.ScheduleParallel(updateColorsJobTempObject,         _queryTempObject,         jobHandleMiddleBuilding);
            JobHandle jobHandleSubObject          = JobChunkExtensions.ScheduleParallel(updateColorsJobSubObject,          _querySubObject,          jobHandleTempObject);

            // Prevent these jobs from running again until last job is complete.
            base.Dependency = jobHandleSubObject;

            // Wait for the cargo vehicle and main building jobs to complete before accessing storage data.
            JobHandle.CompleteAll(ref jobHandleCargoVehicle, ref jobHandleMainBuilding);

            // Note that the subsequent jobs could still be executing at this point, which is okay.

            // Lock the thread while writing totals.
            lock (_totalStorageAmountsCompanyCountsLock)
            {
                // Accumulate totals.
                ProductionConsumptionUtils.ConsolidateValues(in _storageAmountsRequires,  out _totalStorageAmountsRequires);
                ProductionConsumptionUtils.ConsolidateValues(in _storageAmountsProduces,  out _totalStorageAmountsProduces);
                ProductionConsumptionUtils.ConsolidateValues(in _storageAmountsSells,     out _totalStorageAmountsSells);
                ProductionConsumptionUtils.ConsolidateValues(in _storageAmountsStores,    out _totalStorageAmountsStores);
                ProductionConsumptionUtils.ConsolidateValues(in _storageAmountsInTransit, out _totalStorageAmountsInTransit);
                
                ProductionConsumptionUtils.ConsolidateValues(in _companyCountsRequires,   out _totalCompanyCountsRequires);
                ProductionConsumptionUtils.ConsolidateValues(in _companyCountsProduces,   out _totalCompanyCountsProduces);
                ProductionConsumptionUtils.ConsolidateValues(in _companyCountsSells,      out _totalCompanyCountsSells);
                ProductionConsumptionUtils.ConsolidateValues(in _companyCountsStores,     out _totalCompanyCountsStores);
            }

            // Complete the rest of the jobs to help prevent screen flicker.
            jobHandleSubObject.Complete();

            // Dispose active infomodes.
            activeInfomodes.Dispose();

            // This system handled object colors for this mod's infoview.
            // Do not execute the original game logic.
            return false;
        }

        /// <summary>
        /// Get storage amounts and company counts.
        /// </summary>
        public void GetStorageAmountsCompanyCounts(
            out int[] storageAmountsRequires,
            out int[] storageAmountsProduces,
            out int[] storageAmountsSells,
            out int[] storageAmountsStores,
            out int[] storageAmountsInTransit,

            out int[] companyCountsRequires,
            out int[] companyCountsProduces,
            out int[] companyCountsSells,
            out int[] companyCountsStores)
        {
            // Initialize return arrays.
            storageAmountsRequires  = new int[_totalStorageAmountsRequires .Length];
            storageAmountsProduces  = new int[_totalStorageAmountsProduces .Length];
            storageAmountsSells     = new int[_totalStorageAmountsSells    .Length];
            storageAmountsStores    = new int[_totalStorageAmountsStores   .Length];
            storageAmountsInTransit = new int[_totalStorageAmountsInTransit.Length];

            companyCountsRequires   = new int[_totalCompanyCountsRequires  .Length];
            companyCountsProduces   = new int[_totalCompanyCountsProduces  .Length];
            companyCountsSells      = new int[_totalCompanyCountsSells     .Length];
            companyCountsStores     = new int[_totalCompanyCountsStores    .Length];

            // Lock the thread while reading totals.
            lock (_totalStorageAmountsCompanyCountsLock)
            {
                // Copy storage amounts and company counts to return arrays.
                Array.Copy(_totalStorageAmountsRequires,  storageAmountsRequires,  _totalStorageAmountsRequires .Length);
                Array.Copy(_totalStorageAmountsProduces,  storageAmountsProduces,  _totalStorageAmountsProduces .Length);
                Array.Copy(_totalStorageAmountsSells,     storageAmountsSells,     _totalStorageAmountsSells    .Length);
                Array.Copy(_totalStorageAmountsStores,    storageAmountsStores,    _totalStorageAmountsStores   .Length);
                Array.Copy(_totalStorageAmountsInTransit, storageAmountsInTransit, _totalStorageAmountsInTransit.Length);

                Array.Copy(_totalCompanyCountsRequires,   companyCountsRequires,   _totalCompanyCountsRequires  .Length);
                Array.Copy(_totalCompanyCountsProduces,   companyCountsProduces,   _totalCompanyCountsProduces  .Length);
                Array.Copy(_totalCompanyCountsSells,      companyCountsSells,      _totalCompanyCountsSells     .Length);
                Array.Copy(_totalCompanyCountsStores,     companyCountsStores,     _totalCompanyCountsStores    .Length);
            }
        }
    }
}
