using Colossal.Collections;
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

namespace ResourceLocator
{
    /// <summary>
    /// System to set building colors.
    /// Adapted from Game.Rendering.ObjectColorSystem.
    /// This system replaces the game's ObjectColorSystem logic when this mod's infoview is selected.
    /// </summary>
    public partial class BuildingColorSystem : Game.GameSystemBase
    {
        /// <summary>
        /// Storage amount for a resource.
        /// </summary>
        private struct StorageAmount
        { 
            public Game.Economy.Resource resource;
            public int amount;
        }

        /// <summary>
        /// Information for an active infomode.
        /// </summary>
        private struct ActiveInfomode : IComparable<ActiveInfomode>
        {
            // Resource and its corresponding infomode index.
            public Game.Economy.Resource resource;
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
        /// </summary>
        [BurstCompile]
        private partial struct UpdateColorsJobDefault : IJobChunk
        {
            // Color component type to update.
            public ComponentTypeHandle<Game.Objects.Color> ComponentTypeHandleColor;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Set color to default for all objects.
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ComponentTypeHandleColor);
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = default;
                }
            }
        }

        /// <summary>
        /// Job to set the color of each main building.
        /// </summary>
        [BurstCompile]
        private partial struct UpdateColorsJobMainBuilding : IJobChunk
        {
            // Color component type to update (not ReadOnly).
            public ComponentTypeHandle<Game.Objects.Color> ComponentTypeHandleColor;

            // Buffer lookups.
            [ReadOnly] public BufferLookup<Game.Buildings.          Renter                      > BufferLookupRenter;
            [ReadOnly] public BufferLookup<Game.Economy.            Resources                   > BufferLookupResources;

            // Component lookups.
            [ReadOnly] public ComponentLookup<Game.Prefabs.         BuildingData                > ComponentLookupBuildingData;
            [ReadOnly] public ComponentLookup<Game.Prefabs.         BuildingPropertyData        > ComponentLookupBuildingPropertyData;
            [ReadOnly] public ComponentLookup<Game.Companies.       CompanyData                 > ComponentLookupCompanyData;
            [ReadOnly] public ComponentLookup<Game.Companies.       ExtractorCompany            > ComponentLookupExtractorCompany;
            [ReadOnly] public ComponentLookup<Game.Prefabs.         IndustrialProcessData       > ComponentLookupIndustrialProcessData;
            [ReadOnly] public ComponentLookup<Game.Prefabs.         PrefabRef                   > ComponentLookupPrefabRef;
            [ReadOnly] public ComponentLookup<Game.Companies.       ProcessingCompany           > ComponentLookupProcessingCompany;
            [ReadOnly] public ComponentLookup<Game.Companies.       ServiceAvailable            > ComponentLookupServiceAvailable;
            [ReadOnly] public ComponentLookup<Game.Companies.       StorageCompany              > ComponentLookupStorageCompany;
            [ReadOnly] public ComponentLookup<Game.Prefabs.         StorageCompanyData          > ComponentLookupStorageCompanyData;

            // Component type handles for buildings.
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   CargoTransportStation       > ComponentTypeHandleCargoTransportStation;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   CommercialProperty          > ComponentTypeHandleCommercialProperty;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   ElectricityProducer         > ComponentTypeHandleElectricityProducer;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   EmergencyShelter            > ComponentTypeHandleEmergencyShelter;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   GarbageFacility             > ComponentTypeHandleGarbageFacility;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   Hospital                    > ComponentTypeHandleHospital;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   IndustrialProperty          > ComponentTypeHandleIndustrialProperty;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.   ResourceProducer            > ComponentTypeHandleResourceProducer;

            // Component type handles for miscellaneous.
            [ReadOnly] public ComponentTypeHandle<Game.Areas.       CurrentDistrict             > ComponentTypeHandleCurrentDistrict;
            [ReadOnly] public ComponentTypeHandle<Game.Common.      Destroyed                   > ComponentTypeHandleDestroyed;
            [ReadOnly] public ComponentTypeHandle<Game.Objects.     OutsideConnection           > ComponentTypeHandleOutsideConnection;
            [ReadOnly] public ComponentTypeHandle<Game.Prefabs.     PrefabRef                   > ComponentTypeHandlePrefabRef;
            [ReadOnly] public ComponentTypeHandle<Game.Objects.     UnderConstruction           > ComponentTypeHandleUnderConstruction;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            // Active infomodes.
            [ReadOnly] public NativeArray<ActiveInfomode> ActiveInfomodes;

            // Array of lists to return storage amounts to the BuildingColorSystem.
            // The outer array is one for each possible thread.
            // The inner list is one for each storage amount computed in that thread.
            // Even though the outer array is read only, entries can still be added to the inner lists.
            [ReadOnly] public NativeArray<NativeList<StorageAmount>> StorageAmountRequires;
            [ReadOnly] public NativeArray<NativeList<StorageAmount>> StorageAmountProduces;
            [ReadOnly] public NativeArray<NativeList<StorageAmount>> StorageAmountSells;
            [ReadOnly] public NativeArray<NativeList<StorageAmount>> StorageAmountStores;

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

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Get colors to set.
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ComponentTypeHandleColor);

                // Get whether or not the property type is valid for this mod.
                // This is used below to quickly skip the lengthy building color logic for property types this mod does not care about.
                bool propertyTypeIsValid =
                    chunk.Has(ref ComponentTypeHandleCargoTransportStation  ) ||
                    chunk.Has(ref ComponentTypeHandleCommercialProperty     ) || 
                    chunk.Has(ref ComponentTypeHandleElectricityProducer    ) ||
                    chunk.Has(ref ComponentTypeHandleEmergencyShelter       ) ||
                    chunk.Has(ref ComponentTypeHandleGarbageFacility        ) ||
                    chunk.Has(ref ComponentTypeHandleHospital               ) ||
                    chunk.Has(ref ComponentTypeHandleIndustrialProperty     );      // For both industrial and office.
                
                // Get arrays from the chunk.
                NativeArray<Entity                        > entities           = chunk.GetNativeArray(EntityTypeHandle);
                NativeArray<Game.Prefabs.PrefabRef        > prefabRefs         = chunk.GetNativeArray(ref ComponentTypeHandlePrefabRef);
                NativeArray<Game.Areas.CurrentDistrict    > districts          = chunk.GetNativeArray(ref ComponentTypeHandleCurrentDistrict);
                NativeArray<Game.Common.Destroyed         > destroyeds         = chunk.GetNativeArray(ref ComponentTypeHandleDestroyed);
                NativeArray<Game.Objects.UnderConstruction> underConstructions = chunk.GetNativeArray(ref ComponentTypeHandleUnderConstruction);

                // Do each entity (i.e. building).
                for (int i = 0; i < entities.Length; i++)
                {
                    // Property type must be valid for this mod.
                    if (propertyTypeIsValid)
                    {
                        // Building must be in selected district.
                        if (SelectedDistrictIsEntireCity || districts[i].m_District == SelectedDistrict)
                        {
                            // Get building's company, if any.
                            if (Game.UI.InGame.CompanyUIUtils.HasCompany(
                                entities[i],
                                prefabRefs[i].m_Prefab,
                                ref BufferLookupRenter,
                                ref ComponentLookupBuildingPropertyData,
                                ref ComponentLookupCompanyData,
                                out Entity companyEntity))
                            {
                                // Building has a company.
                                // Set building color for a company building.
                                SetBuildingColorCompany(companyEntity, colors, i);
                            }
                            else
                            {
                                // Building has no company.
                                // Set building color for a special case building.
                                SetBuildingColorSpecialCase(chunk, entities[i], colors, i);
                            }
                        }
                    }

                    // Check if should set SubColor flag on the color.
                    // Logic adapted from Game.Rendering.ObjectColorSystem.CheckColors().
                    if ((ComponentLookupBuildingData[prefabRefs[i].m_Prefab].m_Flags & Game.Prefabs.BuildingFlags.ColorizeLot) != 0 ||
                        (CollectionUtils.TryGet(destroyeds, i, out Game.Common.Destroyed destroyed) && destroyed.m_Cleared >= 0f) ||
                        (CollectionUtils.TryGet(underConstructions, i, out Game.Objects.UnderConstruction underConstruction) && underConstruction.m_NewPrefab == Entity.Null))
                    {
                        // Set SubColor flag on the color.
                        // Not sure what the SubColor flag does.
                        Game.Objects.Color color = colors[i];
                        color.m_SubColor = true;
                        colors[i] = color;
                    }
                }
            }

            /// <summary>
            /// Set building color for a company building.
            /// </summary>
            private void SetBuildingColorCompany(Entity companyEntity, NativeArray<Game.Objects.Color> colors, int colorsIndex)
            {
                // Logic adapated from Game.UI.InGame.CompanySection.OnProcess().

                // Company must have a resources buffer and industrial process data.
                if (BufferLookupResources.TryGetBuffer(companyEntity, out DynamicBuffer<Game.Economy.Resources> bufferResources) &&
                    ComponentLookupPrefabRef.TryGetComponent(companyEntity, out Game.Prefabs.PrefabRef companyPrefabRef) &&
                    ComponentLookupIndustrialProcessData.TryGetComponent(companyPrefabRef.m_Prefab, out Game.Prefabs.IndustrialProcessData companyIndustrialProcessData))
                {
                    // Resources for requires, produces, sells, and stores.
                    Game.Economy.Resource resourceRequires1 = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceRequires2 = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceProduces  = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceSells     = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceStores    = Game.Economy.Resource.NoResource;

                    // Get input and output resources.
                    Game.Economy.Resource resourceInput1 = companyIndustrialProcessData.m_Input1.m_Resource;
                    Game.Economy.Resource resourceInput2 = companyIndustrialProcessData.m_Input2.m_Resource;
                    Game.Economy.Resource resourceOutput = companyIndustrialProcessData.m_Output.m_Resource;
                            
                    // A company with service available might require resources but always sells.
                    bool serviceCompany = ComponentLookupServiceAvailable.HasComponent(companyEntity);
                    if (serviceCompany)
                    {
                        // Check if building requires input 1 or 2 resources.
                        if (resourceInput1 != Game.Economy.Resource.NoResource && resourceInput1 != resourceOutput)
                        {
                            resourceRequires1 = resourceInput1;
                        }
                        if (resourceInput2 != Game.Economy.Resource.NoResource && resourceInput2 != resourceOutput && resourceInput2 != resourceInput1)
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
                        // But not every processing company is an extrator company.
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
                        if (ComponentLookupStorageCompanyData.TryGetComponent(companyPrefabRef.m_Prefab, out Game.Prefabs.StorageCompanyData storageCompanyData))
                        {
                            // Building stores the stored resource.
                            resourceStores = storageCompanyData.m_StoredResources;
                        }
                    }

                    // Set building color according to the display option and the resources found above.
                    switch (DisplayOption)
                    {
                        case DisplayOption.Requires: SetBuildingColorForActiveInfomode(colors, colorsIndex, resourceRequires1, resourceRequires2); break;
                        case DisplayOption.Produces: SetBuildingColorForActiveInfomode(colors, colorsIndex, resourceProduces                    ); break;
                        case DisplayOption.Sells:    SetBuildingColorForActiveInfomode(colors, colorsIndex, resourceSells                       ); break;
                        case DisplayOption.Stores:   SetBuildingColorForActiveInfomode(colors, colorsIndex, resourceStores                      ); break;
                    }

                    // Save storage amounts for each resource in the buffer.
                    foreach (Game.Economy.Resources resources in bufferResources)
                    {
                        if (resources.m_Resource == resourceRequires1) { SaveAmount(ref StorageAmountRequires, resourceRequires1, resources.m_Amount); }
                        if (resources.m_Resource == resourceRequires2) { SaveAmount(ref StorageAmountRequires, resourceRequires2, resources.m_Amount); }
                        if (resources.m_Resource == resourceProduces ) { SaveAmount(ref StorageAmountProduces, resourceProduces,  resources.m_Amount); }
                        if (resources.m_Resource == resourceSells    ) { SaveAmount(ref StorageAmountSells,    resourceSells,     resources.m_Amount); }
                        if (resources.m_Resource == resourceStores   ) { SaveAmount(ref StorageAmountStores,   resourceStores,    resources.m_Amount); }
                    }
                }
            }

            /// <summary>
            /// Set building color for special case buildings.
            /// </summary>
            private void SetBuildingColorSpecialCase(ArchetypeChunk chunk, Entity entity, NativeArray<Game.Objects.Color> colors, int colorsIndex)
            {
                // Building must not be an outside connection.
                if (chunk.Has(ref ComponentTypeHandleOutsideConnection))
                {
                    return;
                }

                // Building must have a resources buffer that can be checked.
                if (!BufferLookupResources.TryGetBuffer(entity, out DynamicBuffer<Game.Economy.Resources> bufferResources))
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
                        // Do each resource in the buffer,
                        for (int i = 0; i < bufferResources.Length; i++)
                        {
                            // Save amount for Produces.
                            // This will save resource amounts for resources this mod does not care about (e.g. garbage).
                            // These unneeded saved resource amounts will simply be ignored in later logic.
                            // It is faster to save and ignore these few unneeded amounts than
                            // to determine which few unneeded resources should not be saved in the first place.
                            SaveAmount(ref StorageAmountProduces, bufferResources[i].m_Resource, bufferResources[i].m_Amount);
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
                                Game.Economy.Resource activeInfomodeResource = activeInfomode.resource;
                                for (int i = 0; i < bufferResources.Length; i++)
                                {
                                    // Check if resource from buffer is resource for this active infomode.
                                    if (bufferResources[i].m_Resource == activeInfomodeResource)
                                    {
                                        // Found resource.
                                        // Set building color according to this active infomode.
                                        SetBuildingColor(colors, colorsIndex, activeInfomode.infomodeIndex);

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
                    if (IncludeCoalPowerPlant) { SetBuildingColorForStores(colors, colorsIndex, bufferResources, Game.Economy.Resource.Coal          ); }
                    if (IncludeGasPowerPlant ) { SetBuildingColorForStores(colors, colorsIndex, bufferResources, Game.Economy.Resource.Petrochemicals); }
                    return;
                }

                // Check for a hospital that stores pharmaceuticals.
                if (chunk.Has(ref ComponentTypeHandleHospital))
                {
                    // Check if medical facility is included.
                    if (IncludeMedicalFacility) { SetBuildingColorForStores(colors, colorsIndex, bufferResources, Game.Economy.Resource.Pharmaceuticals); }
                    return;
                }

                // Check for an emergency shelter that stores food.
                if (chunk.Has(ref ComponentTypeHandleEmergencyShelter))
                {
                    // Check if emergency shelter is included.
                    if (IncludeEmeregencyShelter) { SetBuildingColorForStores(colors, colorsIndex, bufferResources, Game.Economy.Resource.Food); }
                    return;
                }

                // Check for a cargo transport station that can store multiple resources.
                if (chunk.Has(ref ComponentTypeHandleCargoTransportStation))
                {
                    // Check if cargo station is included.
                    if (IncludeCargoStation)
                    {
                        // Do each resource in the buffer,
                        for (int i = 0; i < bufferResources.Length; i++)
                        {
                            // Save amount for Stores.
                            // This will save resource amounts for resources this mod does not care about (e.g. mail).
                            // These unneeded saved resource amounts will simply be ignored in later logic.
                            // It is faster to save and ignore these few unneeded amounts than
                            // to determine which few unneeded resources should not be saved in the first place.
                            SaveAmount(ref StorageAmountStores, bufferResources[i].m_Resource, bufferResources[i].m_Amount);
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
                                Game.Economy.Resource activeInfomodeResource = activeInfomode.resource;
                                for (int i = 0; i < bufferResources.Length; i++)
                                {
                                    // Check if resource from buffer is resource for this active infomode.
                                    if (bufferResources[i].m_Resource == activeInfomodeResource && bufferResources[i].m_Amount > 0)
                                    {
                                        // Found resource.
                                        // Set building color according to this active infomode.
                                        SetBuildingColor(colors, colorsIndex, activeInfomode.infomodeIndex);

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
                NativeArray<Game.Objects.Color> colors,
                int colorsIndex,
                Game.Economy.Resource resourceToCheckForActive1,
                Game.Economy.Resource resourceToCheckForActive2 = Game.Economy.Resource.NoResource)
            {
                // Do each active infomode.
                foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                {
                    // Check if either resource to check is active.
                    if (resourceToCheckForActive1 == activeInfomode.resource || resourceToCheckForActive2 == activeInfomode.resource)
                    {
                        // Resource is active.
                        // Set building color according to this active infomode.
                        SetBuildingColor(colors, colorsIndex, activeInfomode.infomodeIndex);

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
                NativeArray<Game.Objects.Color> colors,
                int colorsIndex,
                DynamicBuffer<Game.Economy.Resources> bufferResources,
                Game.Economy.Resource resourceToCheck
            )
            {
                // Do each resource in the buffer.
                for (int i = 0; i < bufferResources.Length; i++)
                {
                    // Check if buffer resource is resource to check.
                    if (bufferResources[i].m_Resource == resourceToCheck)
                    {
                        // Found the resource.
                        // Set building color only for Stores display option.
                        if (DisplayOption == DisplayOption.Stores)
                        {
                            SetBuildingColorForActiveInfomode(colors, colorsIndex, resourceToCheck);
                        }

                        // Save amount for Stores.
                        SaveAmount(ref StorageAmountStores, resourceToCheck, bufferResources[i].m_Amount);

                        // Stop checking.
                        break;
                    }
                }
            }

            /// <summary>
            /// Set building color according to infomode index.
            /// </summary>
            private void SetBuildingColor(NativeArray<Game.Objects.Color> colors, int colorsIndex, byte infomodeIndex)
            {
                // Much of the logic in this job is for this right here.
                colors[colorsIndex] = new Game.Objects.Color(infomodeIndex, (byte)255);
            }

            /// <summary>
            /// Save a storage amount.
            /// </summary>
            private void SaveAmount(ref NativeArray<NativeList<StorageAmount>> storageAmounts, Game.Economy.Resource resource, int amount)
            {
                // Resource must be valid.
                // Do not save zeroes.
                if (resource != Game.Economy.Resource.NoResource && amount != 0)
                {
                    // Add an entry of storage amount for this thread.
                    // By having a separate entry for each thread, parallel threads will never access the same inner list at the same time.
                    storageAmounts[JobsUtility.ThreadIndex].Add(new StorageAmount() { resource = resource, amount = amount });
                }
            }
        }


        /// <summary>
        /// Job to set the color of each middle building to the color of its owner.
        /// Middle buildings include sub buildings (i.e. building upgrades placed around the perimeter of the main building).
        /// Logic is adapted from Game.Rendering.ObjectColorSystem except to handle only buildings and variables are renamed to improve readability.
        /// </summary>
        [BurstCompile]
        private struct UpdateColorsJobMiddleBuilding : IJobChunk
        {
            // Color component lookup to update.
            [NativeDisableParallelForRestriction] public ComponentLookup<Game.Objects.Color> ComponentLookupColor;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Game.Common.Owner> ComponentTypeHandleOwner;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do each entity.
                NativeArray<Game.Common.Owner> owners   = chunk.GetNativeArray(ref ComponentTypeHandleOwner);
                NativeArray<Entity           > entities = chunk.GetNativeArray(EntityTypeHandle);
                for (int i = 0; i < entities.Length; i++)
                {
                    // Get the color of the owner entity.
                    if (ComponentLookupColor.TryGetComponent(owners[i].m_Owner, out Game.Objects.Color ownerColor))
                    {
                        // Set color of this entity to color of owner entity.
                        Entity entity = entities[i];
                        Game.Objects.Color color = ComponentLookupColor[entity];
                        color.m_Index = ownerColor.m_Index;
                        color.m_Value = ownerColor.m_Value;
                        ComponentLookupColor[entity] = color;
                    }
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
            [NativeDisableParallelForRestriction] public ComponentLookup<Game.Objects.Color> ComponentLookupColor;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Game.Objects.Attachment> ComponentTypeHandleAttachment;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do each attachment entity.
                NativeArray<Game.Objects.Attachment> attachments = chunk.GetNativeArray(ref ComponentTypeHandleAttachment);
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                for (int i = 0; i < entities.Length; i++)
                {
                    // Get the color of the attached entity.
                    if (ComponentLookupColor.TryGetComponent(attachments[i].m_Attached, out Game.Objects.Color attachedColor))
                    {
                        // Set color of this attachment entity to the color of the attached entity.
                        Entity entity = entities[i];
                        Game.Objects.Color color = ComponentLookupColor[entity];
                        color.m_Index = attachedColor.m_Index;
                        color.m_Value = attachedColor.m_Value;
                        ComponentLookupColor[entity] = color;
                    }
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
            [NativeDisableParallelForRestriction] public ComponentLookup<Game.Objects.Color> ComponentLookupColor;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Game.Tools.Temp> ComponentTypeHandleTemp;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Set color of object to color of its original.
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                NativeArray<Game.Tools.Temp> temps = chunk.GetNativeArray(ref ComponentTypeHandleTemp);
                for (int i = 0; i < temps.Length; i++)
                {
                    if (ComponentLookupColor.TryGetComponent(temps[i].m_Original, out Game.Objects.Color originalColor))
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
            [NativeDisableParallelForRestriction] public ComponentLookup<Game.Objects.Color> ComponentLookupColor;

            // Component lookups.
            [ReadOnly] public ComponentLookup<Game.Buildings.   Building    > ComponentLookupBuilding;
            [ReadOnly] public ComponentLookup<Game.Objects.     Elevation   > ComponentLookupElevation;
            [ReadOnly] public ComponentLookup<Game.Common.      Owner       > ComponentLookupOwner;
            [ReadOnly] public ComponentLookup<Game.Vehicles.    Vehicle     > ComponentLookupVehicle;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<Game.Objects. Elevation   > ComponentTypeHandleElevation;
            [ReadOnly] public ComponentTypeHandle<Game.Common.  Owner       > ComponentTypeHandleOwner;
            [ReadOnly] public ComponentTypeHandle<Game.Objects. Tree        > ComponentTypeHandleTree;

            // Entity type handle.
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Game.Common.Owner> owners = chunk.GetNativeArray(ref ComponentTypeHandleOwner);
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                if (chunk.Has(ref ComponentTypeHandleTree))
                {
                    NativeArray<Game.Objects.Elevation> elevations = chunk.GetNativeArray(ref ComponentTypeHandleElevation);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity entity = entities[i];
                        Game.Common.Owner owner = owners[i];
                        Game.Objects.Elevation elevation;
                        bool flag = CollectionUtils.TryGet(elevations, i, out elevation) && (elevation.m_Flags & Game.Objects.ElevationFlags.OnGround) == 0;
                        bool flag2 = flag && !ComponentLookupColor.HasComponent(owner.m_Owner);
                        Game.Common.Owner newOwner;
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
                                    flag &= ComponentLookupElevation.TryGetComponent(owner.m_Owner, out elevation) && (elevation.m_Flags & Game.Objects.ElevationFlags.OnGround) == 0;
                                }
                            }
                            owner = newOwner;
                        }
                        if (ComponentLookupColor.TryGetComponent(owner.m_Owner, out Game.Objects.Color color) && (flag || color.m_SubColor))
                        {
                            ComponentLookupColor[entity] = color;
                        }
                    }
                    return;
                }

                for (int j = 0; j < entities.Length; j++)
                {
                    Game.Common.Owner owner = owners[j];
                    Game.Common.Owner newOwner;
                    while (ComponentLookupOwner.TryGetComponent(owner.m_Owner, out newOwner) && !ComponentLookupBuilding.HasComponent(owner.m_Owner) && !ComponentLookupVehicle.HasComponent(owner.m_Owner))
                    {
                        owner = newOwner;
                    }
                    if (ComponentLookupColor.TryGetComponent(owner.m_Owner, out Game.Objects.Color color))
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
        private Game.Tools.ToolSystem _toolSystem;
        private ResourceLocatorUISystem _resourceLocatorUISystem;

        // Entity queries.
        private EntityQuery _queryDefault;
        private EntityQuery _queryMainBuilding;
        private EntityQuery _queryMiddleBuilding;
        private EntityQuery _queryAttachmentBuilding;
        private EntityQuery _queryTempObject;
        private EntityQuery _querySubObject;
        private EntityQuery _queryActiveBuildingData;
        
        // Harmony ID.
        private const string HarmonyID = "rcav8tr." + ModAssemblyInfo.Name;

        // Max number of thread entries in the previous frame.
        private const int defaultMaxThreadEntries = 8;
        private int _previousMaxThreadEntriesStorageRequires = defaultMaxThreadEntries;
        private int _previousMaxThreadEntriesStorageProduces = defaultMaxThreadEntries;
        private int _previousMaxThreadEntriesStorageSells    = defaultMaxThreadEntries;
        private int _previousMaxThreadEntriesStorageStores   = defaultMaxThreadEntries;

        // Arrays to hold total storage amounts by resource.
        private int[] _totalStorageRequires = new int[Game.Economy.EconomyUtils.ResourceCount];
        private int[] _totalStorageProduces = new int[Game.Economy.EconomyUtils.ResourceCount];
        private int[] _totalStorageSells    = new int[Game.Economy.EconomyUtils.ResourceCount];
        private int[] _totalStorageStores   = new int[Game.Economy.EconomyUtils.ResourceCount];

        // Lock for accessing total storage amounts.
        private readonly object _totalStorageLock = new object();

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
            _toolSystem              = base.World.GetOrCreateSystemManaged<Game.Tools.ToolSystem>();
            _resourceLocatorUISystem = base.World.GetOrCreateSystemManaged<ResourceLocatorUISystem>();

            // Query to get default objects (i.e. every object that has a color).
		    _queryDefault = GetEntityQuery
            (
                new EntityQueryDesc
		        {
			        All = new ComponentType[]
			        {
				        ComponentType.ReadOnly <Game.Objects.   Object>(),
				        ComponentType.ReadWrite<Game.Objects.   Color>(),
			        },
			        None = new ComponentType[]
			        {
				        ComponentType.ReadOnly<Game.Tools.      Hidden>(),
				        ComponentType.ReadOnly<Game.Common.     Deleted>(),
			        }
		        }
            );

            // Query to get main buildings.
            // Adapted from Game.Rendering.ObjectColorSystem.
		    _queryMainBuilding = GetEntityQuery
            (
                new EntityQueryDesc
		        {
			        All = new ComponentType[]
			        {
				        ComponentType.ReadOnly <Game.Objects.   Object>(),
				        ComponentType.ReadWrite<Game.Objects.   Color>(),
			        },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Buildings.  Building>(),
                    },
			        None = new ComponentType[]
			        {
                        // Do not exclude hidden buildings because they must be included in the storage data.
				        //ComponentType.ReadOnly<Hidden>(),

                        ComponentType.ReadOnly<Game.Buildings.  Abandoned>(),   // Exclude abandoned buildings. 
                        ComponentType.ReadOnly<Game.Buildings.  Condemned>(),   // Exclude condemned buildings.
				        ComponentType.ReadOnly<Game.Common.     Deleted>(),     // Exclude deleted   buildings.
                        ComponentType.ReadOnly<Game.Common.     Destroyed>(),   // Exclude destroyed buildings.
				        ComponentType.ReadOnly<Game.Common.     Owner>(),       // Exclude subbuildings (see middle buildings query below).
                        ComponentType.ReadOnly<Game.Objects.    Attachment>(),  // Exclude attachments (see attachments query below).
				        ComponentType.ReadOnly<Game.Tools.      Temp>(),        // Exclude temp (see temp objects query below).
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
                        ComponentType.ReadOnly<Game.Buildings.  Building>(),
                        ComponentType.ReadOnly<Game.Common.     Owner>(),
                        ComponentType.ReadWrite<Game.Objects.   Color>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Objects.    Attachment>(),  // Exclude attachments (see attachment buildings query below).
                        ComponentType.ReadOnly<Game.Tools.      Hidden>(),
                        ComponentType.ReadOnly<Game.Common.     Deleted>(),
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
                        ComponentType.ReadOnly <Game.Buildings. Building>(),
                        ComponentType.ReadOnly <Game.Objects.   Attachment>(),
                        ComponentType.ReadWrite<Game.Objects.   Color>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Common.     Owner>(),       // Exclude middle buildings (see middle buildings query above).
                        ComponentType.ReadOnly<Game.Tools.      Hidden>(),
                        ComponentType.ReadOnly<Game.Common.     Deleted>(),
                    }
                }
            );

            // Query to get Temp objects.
            // Temp objects are when cursor is hovered over an object.
            // The original object gets hidden and a temp object is placed over the original.
            // Copied exactly from Game.Rendering.ObjectColorSystem.
            _queryTempObject = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly <Game.Objects.   Object>(),
                        ComponentType.ReadWrite<Game.Objects.   Color>(),
                        ComponentType.ReadOnly <Game.Tools.     Temp>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Tools.      Hidden>(),
                        ComponentType.ReadOnly<Game.Common.     Deleted>(),
                    }
                }
            );

            // Query that will get building extensions (i.e. the building upgrades attached to the main building).
            // This query will likely also get other objects.
            // Copied exactly from Game.Rendering.ObjectColorSystem.
            _querySubObject = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly <Game.Objects.   Object>(),
                        ComponentType.ReadOnly <Game.Common.    Owner>(),
                        ComponentType.ReadWrite<Game.Objects.   Color>(),
                    },
                    None = new ComponentType[]
                    {
                        // Exclude all same things as base game logic.
                        ComponentType.ReadOnly<Game.Tools.      Hidden>(),
                        ComponentType.ReadOnly<Game.Common.     Deleted>(),
                        ComponentType.ReadOnly<Game.Vehicles.   Vehicle>(),
                        ComponentType.ReadOnly<Game.Creatures.  Creature>(),
                        ComponentType.ReadOnly<Game.Buildings.  Building>(),
                        ComponentType.ReadOnly<Game.Objects.    UtilityObject>(),
                    }
                }
            );

            // Query to get active building datas.
            // All infomodes for this mod are BuildingInfomodePrefab which generates InfoviewBuildingData.
            // So there is no need to check for other datas.
            _queryActiveBuildingData = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Prefabs.    InfoviewBuildingData>(),
                        ComponentType.ReadOnly<Game.Prefabs.    InfomodeActive>(),
                    }
                }
            );

            // Use Harmony to patch ObjectColorSystem.OnUpdate with BuildingColorSystem.OnUpdatePrefix.
            // When this mod's infoview is displayed, it is not necessary to execute ObjectColorSystem.OnUpdate.
            // By using a Harmony prefix, this system can prevent the execution of ObjectColorSystem.OnUpdate.
            // Note that ObjectColorSystem.OnUpdate can be patched, but the jobs in ObjectColorSystem cannot be patched because they are burst compiled.
            // Create this patch last to ensure all other initializations are complete before OnUpdatePrefix is called.
            MethodInfo originalMethod = typeof(Game.Rendering.ObjectColorSystem).GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (originalMethod == null)
            {
                Mod.log.Error($"Unable to find original method {nameof(Game.Rendering.ObjectColorSystem)}.OnUpdate.");
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
            // Create jobs to set building colors.


            // Create a job to update default colors.
            UpdateColorsJobDefault updateColorsJobDefault = new UpdateColorsJobDefault()
            {
                ComponentTypeHandleColor = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),
            };


            // Create native array of active infomodes.
            ComponentTypeHandle<Game.Prefabs.InfoviewBuildingData> componentTypeHandleInfoviewBuildingData = SystemAPI.GetComponentTypeHandle<Game.Prefabs.InfoviewBuildingData>(true);
            ComponentTypeHandle<Game.Prefabs.InfomodeActive      > componentTypeHandleInfomodeActive       = SystemAPI.GetComponentTypeHandle<Game.Prefabs.InfomodeActive      >(true);
            List<ActiveInfomode> tempActiveInfomodes = new List<ActiveInfomode>();
            NativeArray<ArchetypeChunk> tempActiveBuildingDataChunks = _queryActiveBuildingData.ToArchetypeChunkArray(Allocator.TempJob);
            foreach (ArchetypeChunk activeBuildingDataChunk in tempActiveBuildingDataChunks)
            {
                // Do each active building data.
                NativeArray<Game.Prefabs.InfoviewBuildingData> infoviewBuildingDatas = activeBuildingDataChunk.GetNativeArray(ref componentTypeHandleInfoviewBuildingData);
                NativeArray<Game.Prefabs.InfomodeActive      > infomodeActives       = activeBuildingDataChunk.GetNativeArray(ref componentTypeHandleInfomodeActive);
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
            NativeArray<ActiveInfomode> activeInfomodes = new NativeArray<ActiveInfomode>(tempActiveInfomodes.ToArray(), Allocator.TempJob);
            tempActiveBuildingDataChunks.Dispose();

            // Create arrays for storage amounts, one entry for each possible parallel job thread.
            int threadCount = JobsUtility.ThreadIndexCount;
            NativeArray<NativeList<StorageAmount>> storageAmountRequires = new(threadCount, Allocator.TempJob);
            NativeArray<NativeList<StorageAmount>> storageAmountProduces = new(threadCount, Allocator.TempJob);
            NativeArray<NativeList<StorageAmount>> storageAmountSells    = new(threadCount, Allocator.TempJob);
            NativeArray<NativeList<StorageAmount>> storageAmountStores   = new(threadCount, Allocator.TempJob);
            for (int i = 0; i < threadCount; i++)
            {
                // Each thread array entry is a list to hold storage amounts.
                // When the list capacity needs to be expanded because a new entry is added, a new larger block of memory is allocated,
                // old list is copied there, then old list memory is released.  Doing all this every frame would reduce performance.
                // Initial list capacity is set to the max number of thread entries from the previous frame.
                // This is to try to minimize the number of times the list capacity needs to be expanded when adding new entries
                // while at the same time not using much more memory than is needed for the list.
                // It appears from empirical testing that:
                //      The actual initial list capacity is the power of 2 that is at least as large as the initial capacity.
                //      If the list capacity needs to be increased to add another entry, the list capacity is doubled.
                //      Therefore, list capacity is always a power of 2.
                storageAmountRequires[i] = new NativeList<StorageAmount>(_previousMaxThreadEntriesStorageRequires, Allocator.TempJob);
                storageAmountProduces[i] = new NativeList<StorageAmount>(_previousMaxThreadEntriesStorageProduces, Allocator.TempJob);
                storageAmountSells   [i] = new NativeList<StorageAmount>(_previousMaxThreadEntriesStorageSells,    Allocator.TempJob);
                storageAmountStores  [i] = new NativeList<StorageAmount>(_previousMaxThreadEntriesStorageStores,   Allocator.TempJob);
            }

            // Create a job to update main building colors.
            UpdateColorsJobMainBuilding updateColorsJobMainBuilding = new UpdateColorsJobMainBuilding()
            {
                ComponentTypeHandleColor                        = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),

                BufferLookupRenter                              = SystemAPI.GetBufferLookup<Game.Buildings.         Renter                      >(true),
                BufferLookupResources                           = SystemAPI.GetBufferLookup<Game.Economy.           Resources                   >(true),
                
                ComponentLookupBuildingData                     = SystemAPI.GetComponentLookup<Game.Prefabs.        BuildingData                >(true),
                ComponentLookupBuildingPropertyData             = SystemAPI.GetComponentLookup<Game.Prefabs.        BuildingPropertyData        >(true),
                ComponentLookupCompanyData                      = SystemAPI.GetComponentLookup<Game.Companies.      CompanyData                 >(true),
                ComponentLookupExtractorCompany                 = SystemAPI.GetComponentLookup<Game.Companies.      ExtractorCompany            >(true),
                ComponentLookupIndustrialProcessData            = SystemAPI.GetComponentLookup<Game.Prefabs.        IndustrialProcessData       >(true),
                ComponentLookupPrefabRef                        = SystemAPI.GetComponentLookup<Game.Prefabs.        PrefabRef                   >(true),
                ComponentLookupProcessingCompany                = SystemAPI.GetComponentLookup<Game.Companies.      ProcessingCompany           >(true),
                ComponentLookupServiceAvailable                 = SystemAPI.GetComponentLookup<Game.Companies.      ServiceAvailable            >(true),
                ComponentLookupStorageCompany                   = SystemAPI.GetComponentLookup<Game.Companies.      StorageCompany              >(true),
                ComponentLookupStorageCompanyData               = SystemAPI.GetComponentLookup<Game.Prefabs.        StorageCompanyData          >(true),
                
                ComponentTypeHandleCargoTransportStation        = SystemAPI.GetComponentTypeHandle<Game.Buildings.  CargoTransportStation       >(true),
                ComponentTypeHandleCommercialProperty           = SystemAPI.GetComponentTypeHandle<Game.Buildings.  CommercialProperty          >(true),
                ComponentTypeHandleElectricityProducer          = SystemAPI.GetComponentTypeHandle<Game.Buildings.  ElectricityProducer         >(true),
                ComponentTypeHandleEmergencyShelter             = SystemAPI.GetComponentTypeHandle<Game.Buildings.  EmergencyShelter            >(true),
                ComponentTypeHandleGarbageFacility              = SystemAPI.GetComponentTypeHandle<Game.Buildings.  GarbageFacility             >(true),
                ComponentTypeHandleHospital                     = SystemAPI.GetComponentTypeHandle<Game.Buildings.  Hospital                    >(true),
                ComponentTypeHandleIndustrialProperty           = SystemAPI.GetComponentTypeHandle<Game.Buildings.  IndustrialProperty          >(true),
                ComponentTypeHandleResourceProducer             = SystemAPI.GetComponentTypeHandle<Game.Buildings.  ResourceProducer            >(true),

                ComponentTypeHandleCurrentDistrict              = SystemAPI.GetComponentTypeHandle<Game.Areas.      CurrentDistrict             >(true),
                ComponentTypeHandleDestroyed                    = SystemAPI.GetComponentTypeHandle<Game.Common.     Destroyed                   >(true),
                ComponentTypeHandleOutsideConnection            = SystemAPI.GetComponentTypeHandle<Game.Objects.    OutsideConnection           >(true),
                ComponentTypeHandlePrefabRef                    = SystemAPI.GetComponentTypeHandle<Game.Prefabs.    PrefabRef                   >(true),
                ComponentTypeHandleUnderConstruction            = SystemAPI.GetComponentTypeHandle<Game.Objects.    UnderConstruction           >(true),
                
                EntityTypeHandle                                = SystemAPI.GetEntityTypeHandle(),
                
                ActiveInfomodes                                 = activeInfomodes,
                
                StorageAmountRequires                           = storageAmountRequires,
                StorageAmountProduces                           = storageAmountProduces,
                StorageAmountSells                              = storageAmountSells,
                StorageAmountStores                             = storageAmountStores,

                IncludeRecyclingCenter                          = Mod.ModSettings.IncludeRecyclingCenter,
                IncludeCoalPowerPlant                           = Mod.ModSettings.IncludeCoalPowerPlant,
                IncludeGasPowerPlant                            = Mod.ModSettings.IncludeGasPowerPlant,
                IncludeMedicalFacility                          = Mod.ModSettings.IncludeMedicalFacility,
                IncludeEmeregencyShelter                        = Mod.ModSettings.IncludeEmeregencyShelter,
                IncludeCargoStation                             = Mod.ModSettings.IncludeCargoStation,

                SelectedDistrict                                = _resourceLocatorUISystem.selectedDistrict,
                SelectedDistrictIsEntireCity                    = _resourceLocatorUISystem.selectedDistrict == ResourceLocatorUISystem.EntireCity,

                DisplayOption                                   = _resourceLocatorUISystem.displayOption,
            };


            // Create a job to update middle building colors.
            UpdateColorsJobMiddleBuilding updateColorsJobMiddleBuilding = new UpdateColorsJobMiddleBuilding()
            {
                ComponentLookupColor        = SystemAPI.GetComponentLookup<Game.Objects.Color>(false),
                ComponentTypeHandleOwner    = SystemAPI.GetComponentTypeHandle<Game.Common.Owner>(true),
                EntityTypeHandle            = SystemAPI.GetEntityTypeHandle(),
            };


            // Create a job to update attachment building colors.
            UpdateColorsJobAttachmentBuilding updateColorsJobAttachmentBuilding = new UpdateColorsJobAttachmentBuilding()
            {
                ComponentLookupColor            = SystemAPI.GetComponentLookup<Game.Objects.Color>(false),
                ComponentTypeHandleAttachment   = SystemAPI.GetComponentTypeHandle<Game.Objects.Attachment>(true),
                EntityTypeHandle                = SystemAPI.GetEntityTypeHandle(),
            };


            // Create a job to update temp object colors.
            UpdateColorsJobTempObject updateColorsJobTempObject = new UpdateColorsJobTempObject()
            {
                ComponentLookupColor    = SystemAPI.GetComponentLookup<Game.Objects.Color>(false),
                ComponentTypeHandleTemp = SystemAPI.GetComponentTypeHandle<Game.Tools.Temp>(true),
                EntityTypeHandle        = SystemAPI.GetEntityTypeHandle(),
            };

            
            // Create a job to update sub object colors.
            UpdateColorsJobSubObject updateColorsJobSubObject = new UpdateColorsJobSubObject()
            {
                ComponentLookupColor            = SystemAPI.GetComponentLookup<Game.Objects.Color>(false),
                ComponentLookupBuilding         = SystemAPI.GetComponentLookup<Game.Buildings.      Building    >(true),
                ComponentLookupElevation        = SystemAPI.GetComponentLookup<Game.Objects.        Elevation   >(true),
                ComponentLookupOwner            = SystemAPI.GetComponentLookup<Game.Common.         Owner       >(true),
                ComponentLookupVehicle          = SystemAPI.GetComponentLookup<Game.Vehicles.       Vehicle     >(true),
                ComponentTypeHandleElevation    = SystemAPI.GetComponentTypeHandle<Game.Objects.    Elevation   >(true),
                ComponentTypeHandleOwner        = SystemAPI.GetComponentTypeHandle<Game.Common.     Owner       >(true),
                ComponentTypeHandleTree         = SystemAPI.GetComponentTypeHandle<Game.Objects.    Tree        >(true),
                EntityTypeHandle                = SystemAPI.GetEntityTypeHandle(),
            };


            // Schedule the jobs with dependencies so the jobs run in order.
            // Schedule each job to execute in parallel (i.e. job uses multiple threads, if available).
            // Parallel threads execute much faster than a single thread.
            JobHandle jobHandleDefault            = JobChunkExtensions.ScheduleParallel(updateColorsJobDefault,            _queryDefault,            base.Dependency);
            JobHandle jobHandleMainBuilding       = JobChunkExtensions.ScheduleParallel(updateColorsJobMainBuilding,       _queryMainBuilding,       jobHandleDefault);
            JobHandle jobHandleMiddleBuilding     = JobChunkExtensions.ScheduleParallel(updateColorsJobMiddleBuilding,     _queryMiddleBuilding,     jobHandleMainBuilding);
            JobHandle jobHandleAttachmentBuilding = JobChunkExtensions.ScheduleParallel(updateColorsJobAttachmentBuilding, _queryAttachmentBuilding, jobHandleMiddleBuilding);
            JobHandle jobHandleTempObject         = JobChunkExtensions.ScheduleParallel(updateColorsJobTempObject,         _queryTempObject,         jobHandleAttachmentBuilding);
            JobHandle jobHandleSubObject          = JobChunkExtensions.ScheduleParallel(updateColorsJobSubObject,          _querySubObject,          jobHandleTempObject);

            // Prevent these jobs from running again until last job is complete.
            base.Dependency = jobHandleSubObject;

            // Wait for the main building job to complete before accessing storage data.
            jobHandleMainBuilding.Complete();
            
            // Dispose of native collections no longer needed once the main building job is complete.
            activeInfomodes.Dispose();

            // Lock the thread while writing totals.
            lock (_totalStorageLock)
            {
                // Accumulate totals.
                AccumulateTotals(ref storageAmountRequires, ref _totalStorageRequires, ref _previousMaxThreadEntriesStorageRequires);
                AccumulateTotals(ref storageAmountProduces, ref _totalStorageProduces, ref _previousMaxThreadEntriesStorageProduces);
                AccumulateTotals(ref storageAmountSells,    ref _totalStorageSells,    ref _previousMaxThreadEntriesStorageSells   );
                AccumulateTotals(ref storageAmountStores,   ref _totalStorageStores,   ref _previousMaxThreadEntriesStorageStores  );
            }

            // Wait for the attachment building job to complete.
            // This seems to help prevent screen flicker.
            jobHandleAttachmentBuilding.Complete();

            // Note that the jobs after the attachment building job could still be executing at this point, which is okay.

            // This system handled building colors for this mod's infoview.
            // Do not execute the original game logic.
            return false;
        }

        /// <summary>
        /// Accumulate totals from storage amounts.
        /// </summary>
        private void AccumulateTotals(ref NativeArray<NativeList<StorageAmount>> storageAmounts, ref int[] total, ref int previousMaxThreadEntries)
        {
            // Initialize return values.
            for (int i = 0; i < total.Length; i++)
            {
                total[i] = 0;
            }
            previousMaxThreadEntries = defaultMaxThreadEntries;

            // Do each thread entry in the storage amounts array.
            for (int i = 0; i < storageAmounts.Length; i++)
            {
                // Do each storage amount entry in the storage amount list.
                NativeList<StorageAmount> storageAmountList = storageAmounts[i];
                for (int j = 0; j < storageAmountList.Length; j++)
                {
                    // Add storage amount from this entry to total.
                    // Index into the total array is the resource index.
                    StorageAmount storageAmountEntry = storageAmountList[j];
                    int resourceIndex = Game.Economy.EconomyUtils.GetResourceIndex(storageAmountEntry.resource);
                    total[resourceIndex] += storageAmountEntry.amount;
                }

                // Compute maximum thread entries.
                if (storageAmountList.Length > previousMaxThreadEntries)
                {
                    previousMaxThreadEntries = storageAmountList.Length;
                }
                
                // This storage amount list is no longer needed.
                storageAmountList.Dispose();
            }

            // The storage amount array is no longer needed.
            storageAmounts.Dispose();
        }

        /// <summary>
        /// Get storage amounts.
        /// </summary>
        public void GetStorageAmounts(out int[] storageRequires, out int[] storageProduces, out int[] storageSells, out int[] storageStores)
        {
            // Initialize return arrays.
            storageRequires = new int[_totalStorageRequires.Length];
            storageProduces = new int[_totalStorageProduces.Length];
            storageSells    = new int[_totalStorageSells   .Length];
            storageStores   = new int[_totalStorageStores  .Length];

            // Lock the thread while reading totals.
            lock(_totalStorageStores)
            {
                // Copy storage amounts to return arrays.
                Array.Copy(_totalStorageRequires, storageRequires, _totalStorageRequires.Length);
                Array.Copy(_totalStorageProduces, storageProduces, _totalStorageProduces.Length);
                Array.Copy(_totalStorageSells,    storageSells,    _totalStorageSells   .Length);
                Array.Copy(_totalStorageStores,   storageStores,   _totalStorageStores  .Length);
            }
        }
    }
}
