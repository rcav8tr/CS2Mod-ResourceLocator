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
                if (BufferLookupResources.TryGetBuffer(companyEntity, out DynamicBuffer<Game.Economy.Resources> _) &&
                    ComponentLookupPrefabRef.TryGetComponent(companyEntity, out Game.Prefabs.PrefabRef companyPrefabRef) &&
                    ComponentLookupIndustrialProcessData.TryGetComponent(companyPrefabRef.m_Prefab, out Game.Prefabs.IndustrialProcessData companyIndustrialProcessData))
                {
                    // Resources for requires, produces, sells, and stores.
                    Game.Economy.Resource resourceRequires1 = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceRequires2 = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceProduces  = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceSells     = Game.Economy.Resource.NoResource;
                    Game.Economy.Resource resourceStores    = Game.Economy.Resource.NoResource;

                    // A company with service available might require resources but always sells.
                    if (ComponentLookupServiceAvailable.HasComponent(companyEntity))
                    {
                        // Get input and output resources.
                        Game.Economy.Resource input1Resource = companyIndustrialProcessData.m_Input1.m_Resource;
                        Game.Economy.Resource input2Resource = companyIndustrialProcessData.m_Input2.m_Resource;
                        Game.Economy.Resource outputResource = companyIndustrialProcessData.m_Output.m_Resource;
                            
                        // Check if building requires input 1 or 2 resources.
                        if (input1Resource != Game.Economy.Resource.NoResource && input1Resource != outputResource)
                        {
                            resourceRequires1 = input1Resource;
                        }
                        if (input2Resource != Game.Economy.Resource.NoResource && input2Resource != outputResource && input2Resource != input1Resource)
                        {
                            resourceRequires2 = input2Resource;
                        }

                        // Building sells the output resource.
                        resourceSells = outputResource;
                    }

                    // A processing company always requires and produces.
                    else if (ComponentLookupProcessingCompany.HasComponent(companyEntity))
                    {
                        // Building requires input 1 and 2 resources.
                        // One or the other may be NoResource, but one or both should always be a valid resource.
                        resourceRequires1 = companyIndustrialProcessData.m_Input1.m_Resource;
                        resourceRequires2 = companyIndustrialProcessData.m_Input2.m_Resource;

                        // Building produces the output resource.
                        resourceProduces = companyIndustrialProcessData.m_Output.m_Resource;
                    }

                    // An extractor company only produces.
                    else if (ComponentLookupExtractorCompany.HasComponent(companyEntity))
                    {
                        // Building produces the output resource.
                        resourceProduces = companyIndustrialProcessData.m_Output.m_Resource;
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
                if (BufferLookupResources.TryGetBuffer(entity, out DynamicBuffer<Game.Economy.Resources> resources))
                {
                    // These special case buildings only produce resources, so proceed only for the Produces display option.
                    if (DisplayOption == DisplayOption.Produces)
                    {
                        // Check for a recycling center that can produce multiple resources.
                        // Building is colored according to the top most active infomode
                        // corresponding to a resource that the building currently has storage for.
                        // Empirical evidence suggests the produced resources are:  Metals, Plastics, Textiles, and Paper.
                        if (chunk.Has(ref ComponentTypeHandleGarbageFacility) && chunk.Has(ref ComponentTypeHandleResourceProducer))
                        {
                            // Check each active infomode.
                            foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                            {
                                Game.Economy.Resource activeInfomodeResource = activeInfomode.resource;
                                for (int i = 0; i < resources.Length; i++)
                                {
                                    if (resources[i].m_Resource == activeInfomodeResource)
                                    {
                                        // Set building color according to this active infomode.
                                        SetBuildingColor(colors, colorsIndex, activeInfomode.infomodeIndex);
                                        return;
                                    }
                                }
                            }

                            // No active infomode found in buffer.
                            return;
                        }
                    }

                    // These special case buildings only store resources, so proceed only for the Stores display option.
                    else if (DisplayOption == DisplayOption.Stores)
                    {
                        // Check for an electricity producer that stores coal or petrochemicals.
                        // This mod ignores the Incinerator Plant which produces electricity from stored garbage.
                        if (chunk.Has(ref ComponentTypeHandleElectricityProducer))
                        {
                            // Check buffer for resources.
                            for (int i = 0; i < resources.Length; i++)
                            {
                                if (resources[i].m_Resource == Game.Economy.Resource.Coal)
                                {
                                    SetBuildingColorForActiveInfomode(colors, colorsIndex, Game.Economy.Resource.Coal);
                                    return;
                                }
                                if (resources[i].m_Resource == Game.Economy.Resource.Petrochemicals)
                                {
                                    SetBuildingColorForActiveInfomode(colors, colorsIndex, Game.Economy.Resource.Petrochemicals);
                                    return;
                                }
                            }

                            // Resources not found in buffer.
                            return;
                        }

                        // Check for a hospital that stores pharmaceuticals.
                        if (chunk.Has(ref ComponentTypeHandleHospital))
                        {
                            // Check buffer for resource.
                            for (int i = 0; i < resources.Length; i++)
                            {
                                if (resources[i].m_Resource == Game.Economy.Resource.Pharmaceuticals)
                                {
                                    SetBuildingColorForActiveInfomode(colors, colorsIndex, Game.Economy.Resource.Pharmaceuticals);
                                    return;
                                }
                            }

                            // Resource not found in buffer.
                            return;
                        }

                        // Check for an emergency shelter that stores food.
                        if (chunk.Has(ref ComponentTypeHandleEmergencyShelter))
                        {
                            // Check buffer for resource.
                            for (int i = 0; i < resources.Length; i++)
                            {
                                if (resources[i].m_Resource == Game.Economy.Resource.Food)
                                {
                                    SetBuildingColorForActiveInfomode(colors, colorsIndex, Game.Economy.Resource.Food);
                                    return;
                                }
                            }

                            // Resource not found in buffer.
                            return;
                        }

                        // Check for a cargo transport station that can store multiple resources.
                        // Building is colored according to the top most active infomode
                        // corresponding to a resource that the building is currently storing.
                        if (chunk.Has(ref ComponentTypeHandleCargoTransportStation))
                        {
                            // Check each active infomode.
                            foreach (ActiveInfomode activeInfomode in ActiveInfomodes)
                            {
                                Game.Economy.Resource activeInfomodeResource = activeInfomode.resource;
                                for (int i = 0; i < resources.Length; i++)
                                {
                                    if (resources[i].m_Resource == activeInfomodeResource && resources[i].m_Amount > 0)
                                    {
                                        // Set building color according to this active infomode.
                                        SetBuildingColor(colors, colorsIndex, activeInfomode.infomodeIndex);
                                        return;
                                    }
                                }
                            }

                            // No active infomode found in buffer.
                            return;
                        }
                    }
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
                        break;
                    }
                }
            }

            /// <summary>
            /// Set building color according to infomode index.
            /// </summary>
            private void SetBuildingColor(NativeArray<Game.Objects.Color> colors, int colorsIndex, byte infomodeIndex)
            {
                // All of the logic in this job is for this right here.
                colors[colorsIndex] = new Game.Objects.Color(infomodeIndex, (byte)255);
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

        // The component lookup and type handle for color.
        // These are used to set building color.
        private ComponentLookup    <Game.Objects.   Color                       > _componentLookupColor;
        private ComponentTypeHandle<Game.Objects.   Color                       > _componentTypeHandleColor;

        // Buffer lookups.
        private BufferLookup<Game.Buildings.        Renter                      > _bufferLookupRenter;
        private BufferLookup<Game.Economy.          Resources                   > _bufferLookupResources;

        // Component lookups.
        private ComponentLookup<Game.Buildings.     Building                    > _componentLookupBuilding;
        private ComponentLookup<Game.Prefabs.       BuildingData                > _componentLookupBuildingData;
        private ComponentLookup<Game.Prefabs.       BuildingPropertyData        > _componentLookupBuildingPropertyData;
        private ComponentLookup<Game.Companies.     CompanyData                 > _componentLookupCompanyData;
        private ComponentLookup<Game.Objects.       Elevation                   > _componentLookupElevation;
        private ComponentLookup<Game.Companies.     ExtractorCompany            > _componentLookupExtractorCompany;
        private ComponentLookup<Game.Prefabs.       IndustrialProcessData       > _componentLookupIndustrialProcessData;
        private ComponentLookup<Game.Common.        Owner                       > _componentLookupOwner;
        private ComponentLookup<Game.Prefabs.       PrefabRef                   > _componentLookupPrefabRef;
        private ComponentLookup<Game.Companies.     ProcessingCompany           > _componentLookupProcessingCompany;
        private ComponentLookup<Game.Companies.     ServiceAvailable            > _componentLookupServiceAvailable;
        private ComponentLookup<Game.Companies.     StorageCompany              > _componentLookupStorageCompany;
        private ComponentLookup<Game.Prefabs.       StorageCompanyData          > _componentLookupStorageCompanyData;
        private ComponentLookup<Game.Vehicles.      Vehicle                     > _componentLookupVehicle;

        // Component type handles for buildings.
        private ComponentTypeHandle<Game.Buildings. CargoTransportStation       > _componentTypeHandleCargoTransportStation;
        private ComponentTypeHandle<Game.Buildings. CommercialProperty          > _componentTypeHandleCommercialProperty;
        private ComponentTypeHandle<Game.Buildings. ElectricityProducer         > _componentTypeHandleElectricityProducer;
        private ComponentTypeHandle<Game.Buildings. EmergencyShelter            > _componentTypeHandleEmergencyShelter;
        private ComponentTypeHandle<Game.Buildings. GarbageFacility             > _componentTypeHandleGarbageFacility;
        private ComponentTypeHandle<Game.Buildings. Hospital                    > _componentTypeHandleHospital;
        private ComponentTypeHandle<Game.Buildings. IndustrialProperty          > _componentTypeHandleIndustrialProperty;
        private ComponentTypeHandle<Game.Buildings. ResourceProducer            > _componentTypeHandleResourceProducer;

        // Component type handles for miscellaneous.
        private ComponentTypeHandle<Game.Objects.   Attachment                  > _componentTypeHandleAttachment;
        private ComponentTypeHandle<Game.Areas.     CurrentDistrict             > _componentTypeHandleCurrentDistrict;
        private ComponentTypeHandle<Game.Common.    Destroyed                   > _componentTypeHandleDestroyed;
        private ComponentTypeHandle<Game.Objects.   Elevation                   > _componentTypeHandleElevation;
        private ComponentTypeHandle<Game.Prefabs.   InfomodeActive              > _componentTypeHandleInfomodeActive;
        private ComponentTypeHandle<Game.Prefabs.   InfoviewBuildingData        > _componentTypeHandleInfoviewBuildingData;
        private ComponentTypeHandle<Game.Objects.   OutsideConnection           > _componentTypeHandleOutsideConnection;
        private ComponentTypeHandle<Game.Common.    Owner                       > _componentTypeHandleOwner;
        private ComponentTypeHandle<Game.Prefabs.   PrefabRef                   > _componentTypeHandlePrefabRef;
        private ComponentTypeHandle<Game.Tools.     Temp                        > _componentTypeHandleTemp;
        private ComponentTypeHandle<Game.Objects.   Tree                        > _componentTypeHandleTree;
        private ComponentTypeHandle<Game.Objects.   UnderConstruction           > _componentTypeHandleUnderConstruction;

        // Entity type handle.
        private EntityTypeHandle _entityTypeHandle;
        
        // Harmony ID.
        private const string HarmonyID = "rcav8tr." + ModAssemblyInfo.Name;

        /// <summary>
        /// Called before OnCreate.
        /// </summary>
        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            LogUtil.Info($"{nameof(BuildingColorSystem)}.{nameof(OnCreateForCompiler)}");

            // Assign components for color.
            // These are the only ones that are read/write.
            _componentLookupColor                           = CheckedStateRef.GetComponentLookup    <Game.Objects.      Color                       >();
            _componentTypeHandleColor                       = CheckedStateRef.GetComponentTypeHandle<Game.Objects.      Color                       >();

            // Assign buffer lookups.
            _bufferLookupRenter                             = CheckedStateRef.GetBufferLookup<Game.Buildings.           Renter                      >(true);
            _bufferLookupResources                          = CheckedStateRef.GetBufferLookup<Game.Economy.             Resources                   >(true);

            // Assign component lookups.
            _componentLookupBuilding                        = CheckedStateRef.GetComponentLookup<Game.Buildings.        Building                    >(true);
            _componentLookupBuildingData                    = CheckedStateRef.GetComponentLookup<Game.Prefabs.          BuildingData                >(true);
            _componentLookupBuildingPropertyData            = CheckedStateRef.GetComponentLookup<Game.Prefabs.          BuildingPropertyData        >(true);
            _componentLookupCompanyData                     = CheckedStateRef.GetComponentLookup<Game.Companies.        CompanyData                 >(true);
            _componentLookupElevation                       = CheckedStateRef.GetComponentLookup<Game.Objects.          Elevation                   >(true);
            _componentLookupExtractorCompany                = CheckedStateRef.GetComponentLookup<Game.Companies.        ExtractorCompany            >(true);
            _componentLookupIndustrialProcessData           = CheckedStateRef.GetComponentLookup<Game.Prefabs.          IndustrialProcessData       >(true);
            _componentLookupOwner                           = CheckedStateRef.GetComponentLookup<Game.Common.           Owner                       >(true);
            _componentLookupPrefabRef                       = CheckedStateRef.GetComponentLookup<Game.Prefabs.          PrefabRef                   >(true);
            _componentLookupProcessingCompany               = CheckedStateRef.GetComponentLookup<Game.Companies.        ProcessingCompany           >(true);
            _componentLookupServiceAvailable                = CheckedStateRef.GetComponentLookup<Game.Companies.        ServiceAvailable            >(true);
            _componentLookupStorageCompany                  = CheckedStateRef.GetComponentLookup<Game.Companies.        StorageCompany              >(true);
            _componentLookupStorageCompanyData              = CheckedStateRef.GetComponentLookup<Game.Prefabs.          StorageCompanyData          >(true);
            _componentLookupVehicle                         = CheckedStateRef.GetComponentLookup<Game.Vehicles.         Vehicle                     >(true);

            // Assign component type handles for buildings.
            _componentTypeHandleCargoTransportStation       = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    CargoTransportStation       >(true);
            _componentTypeHandleCommercialProperty          = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    CommercialProperty          >(true);
            _componentTypeHandleElectricityProducer         = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    ElectricityProducer         >(true);
            _componentTypeHandleEmergencyShelter            = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    EmergencyShelter            >(true);
            _componentTypeHandleGarbageFacility             = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    GarbageFacility             >(true);
            _componentTypeHandleHospital                    = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    Hospital                    >(true);
            _componentTypeHandleIndustrialProperty          = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    IndustrialProperty          >(true);
            _componentTypeHandleResourceProducer            = CheckedStateRef.GetComponentTypeHandle<Game.Buildings.    ResourceProducer            >(true);

            // Assign component type handles for miscellaneous.
            _componentTypeHandleAttachment                  = CheckedStateRef.GetComponentTypeHandle<Game.Objects.      Attachment                  >(true);
            _componentTypeHandleCurrentDistrict             = CheckedStateRef.GetComponentTypeHandle<Game.Areas.        CurrentDistrict             >(true);
            _componentTypeHandleDestroyed                   = CheckedStateRef.GetComponentTypeHandle<Game.Common.       Destroyed                   >(true);
            _componentTypeHandleElevation                   = CheckedStateRef.GetComponentTypeHandle<Game.Objects.      Elevation                   >(true);
            _componentTypeHandleInfomodeActive              = CheckedStateRef.GetComponentTypeHandle<Game.Prefabs.      InfomodeActive              >(true);
            _componentTypeHandleInfoviewBuildingData        = CheckedStateRef.GetComponentTypeHandle<Game.Prefabs.      InfoviewBuildingData        >(true);
            _componentTypeHandleOutsideConnection           = CheckedStateRef.GetComponentTypeHandle<Game.Objects.      OutsideConnection           >(true);
            _componentTypeHandleOwner                       = CheckedStateRef.GetComponentTypeHandle<Game.Common.       Owner                       >(true);
            _componentTypeHandlePrefabRef                   = CheckedStateRef.GetComponentTypeHandle<Game.Prefabs.      PrefabRef                   >(true);
            _componentTypeHandleTemp                        = CheckedStateRef.GetComponentTypeHandle<Game.Tools.        Temp                        >(true);
            _componentTypeHandleTree                        = CheckedStateRef.GetComponentTypeHandle<Game.Objects.      Tree                        >(true);
            _componentTypeHandleUnderConstruction           = CheckedStateRef.GetComponentTypeHandle<Game.Objects.      UnderConstruction           >(true);

            // Assign entity type handle.
            _entityTypeHandle = CheckedStateRef.GetEntityTypeHandle();
        }

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            LogUtil.Info($"{nameof(BuildingColorSystem)}.{nameof(OnCreate)}");

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
				        ComponentType.ReadOnly<Game.Tools.      Hidden>(),      // Exclude hidden buildings.
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
                LogUtil.Error($"Unable to find original method {nameof(Game.Rendering.ObjectColorSystem)}.OnUpdate.");
                return;
            }
            MethodInfo prefixMethod = typeof(BuildingColorSystem).GetMethod(nameof(OnUpdatePrefix), BindingFlags.Static | BindingFlags.NonPublic);
            if (prefixMethod == null)
            {
                LogUtil.Error($"Unable to find patch prefix method {nameof(BuildingColorSystem)}.{nameof(OnUpdatePrefix)}.");
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
            _componentTypeHandleColor.Update(ref CheckedStateRef);
            UpdateColorsJobDefault updateColorsJobDefault = new UpdateColorsJobDefault()
            {
                ComponentTypeHandleColor = _componentTypeHandleColor,
            };


            // Create native array of active infomodes.
            _componentTypeHandleInfoviewBuildingData.Update(ref CheckedStateRef);
            _componentTypeHandleInfomodeActive      .Update(ref CheckedStateRef);
            List<ActiveInfomode> tempActiveInfomodes = new List<ActiveInfomode>();
            NativeArray<ArchetypeChunk> tempActiveBuildingDataChunks = _queryActiveBuildingData.ToArchetypeChunkArray(Allocator.TempJob);
            foreach (ArchetypeChunk activeBuildingDataChunk in tempActiveBuildingDataChunks)
            {
                // Do each active building data.
                NativeArray<Game.Prefabs.InfoviewBuildingData> infoviewBuildingDatas = activeBuildingDataChunk.GetNativeArray(ref _componentTypeHandleInfoviewBuildingData);
                NativeArray<Game.Prefabs.InfomodeActive      > infomodeActives       = activeBuildingDataChunk.GetNativeArray(ref _componentTypeHandleInfomodeActive);
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

            // Update buffers and components for main building colors job.
            _componentTypeHandleColor                       .Update(ref CheckedStateRef);

            _bufferLookupRenter                             .Update(ref CheckedStateRef);
            _bufferLookupResources                          .Update(ref CheckedStateRef);
            
            _componentLookupBuildingData                    .Update(ref CheckedStateRef);
            _componentLookupBuildingPropertyData            .Update(ref CheckedStateRef);
            _componentLookupCompanyData                     .Update(ref CheckedStateRef);
            _componentLookupExtractorCompany                .Update(ref CheckedStateRef);
            _componentLookupIndustrialProcessData           .Update(ref CheckedStateRef);
            _componentLookupPrefabRef                       .Update(ref CheckedStateRef);
            _componentLookupProcessingCompany               .Update(ref CheckedStateRef);
            _componentLookupServiceAvailable                .Update(ref CheckedStateRef);
            _componentLookupStorageCompany                  .Update(ref CheckedStateRef);
            _componentLookupStorageCompanyData              .Update(ref CheckedStateRef);
            
            _componentTypeHandleCargoTransportStation       .Update(ref CheckedStateRef);
            _componentTypeHandleCommercialProperty          .Update(ref CheckedStateRef);
            _componentTypeHandleElectricityProducer         .Update(ref CheckedStateRef);
            _componentTypeHandleEmergencyShelter            .Update(ref CheckedStateRef);
            _componentTypeHandleGarbageFacility             .Update(ref CheckedStateRef);
            _componentTypeHandleHospital                    .Update(ref CheckedStateRef);
            _componentTypeHandleIndustrialProperty          .Update(ref CheckedStateRef);
            _componentTypeHandleResourceProducer            .Update(ref CheckedStateRef);
            
            _componentTypeHandleCurrentDistrict             .Update(ref CheckedStateRef);
            _componentTypeHandleDestroyed                   .Update(ref CheckedStateRef);
            _componentTypeHandleOutsideConnection           .Update(ref CheckedStateRef);
            _componentTypeHandlePrefabRef                   .Update(ref CheckedStateRef);
            _componentTypeHandleUnderConstruction           .Update(ref CheckedStateRef);

            _entityTypeHandle                               .Update(ref CheckedStateRef);

            // Create a job to update main building colors.
            UpdateColorsJobMainBuilding updateColorsJobMainBuilding = new UpdateColorsJobMainBuilding()
            {
                ComponentTypeHandleColor                        = _componentTypeHandleColor,

                BufferLookupRenter                              = _bufferLookupRenter,
                BufferLookupResources                           = _bufferLookupResources,
                
                ComponentLookupBuildingData                     = _componentLookupBuildingData,
                ComponentLookupBuildingPropertyData             = _componentLookupBuildingPropertyData,
                ComponentLookupCompanyData                      = _componentLookupCompanyData,
                ComponentLookupExtractorCompany                 = _componentLookupExtractorCompany,
                ComponentLookupIndustrialProcessData            = _componentLookupIndustrialProcessData,
                ComponentLookupPrefabRef                        = _componentLookupPrefabRef,
                ComponentLookupProcessingCompany                = _componentLookupProcessingCompany,
                ComponentLookupServiceAvailable                 = _componentLookupServiceAvailable,
                ComponentLookupStorageCompany                   = _componentLookupStorageCompany,
                ComponentLookupStorageCompanyData               = _componentLookupStorageCompanyData,
                
                ComponentTypeHandleCargoTransportStation        = _componentTypeHandleCargoTransportStation,
                ComponentTypeHandleCommercialProperty           = _componentTypeHandleCommercialProperty,
                ComponentTypeHandleElectricityProducer          = _componentTypeHandleElectricityProducer,
                ComponentTypeHandleEmergencyShelter             = _componentTypeHandleEmergencyShelter,
                ComponentTypeHandleGarbageFacility              = _componentTypeHandleGarbageFacility,
                ComponentTypeHandleHospital                     = _componentTypeHandleHospital,
                ComponentTypeHandleIndustrialProperty           = _componentTypeHandleIndustrialProperty,
                ComponentTypeHandleResourceProducer             = _componentTypeHandleResourceProducer,

                ComponentTypeHandleCurrentDistrict              = _componentTypeHandleCurrentDistrict,
                ComponentTypeHandleDestroyed                    = _componentTypeHandleDestroyed,
                ComponentTypeHandleOutsideConnection            = _componentTypeHandleOutsideConnection,
                ComponentTypeHandlePrefabRef                    = _componentTypeHandlePrefabRef,
                ComponentTypeHandleUnderConstruction            = _componentTypeHandleUnderConstruction,
                
                EntityTypeHandle                                = _entityTypeHandle,
                
                ActiveInfomodes                                 = activeInfomodes,

                SelectedDistrict                                = _resourceLocatorUISystem.selectedDistrict,
                SelectedDistrictIsEntireCity                    = _resourceLocatorUISystem.selectedDistrict == ResourceLocatorUISystem.EntireCity,

                DisplayOption                                   = _resourceLocatorUISystem.displayOption,
            };


            // Create a job to update middle building colors.
            _componentLookupColor       .Update(ref CheckedStateRef);
            _componentTypeHandleOwner   .Update(ref CheckedStateRef);
            _entityTypeHandle           .Update(ref CheckedStateRef);
            UpdateColorsJobMiddleBuilding updateColorsJobMiddleBuilding = new UpdateColorsJobMiddleBuilding()
            {
                ComponentLookupColor        = _componentLookupColor,
                ComponentTypeHandleOwner    = _componentTypeHandleOwner,
                EntityTypeHandle            = _entityTypeHandle,
            };


            // Create a job to update attachment building colors.
            _componentLookupColor           .Update(ref CheckedStateRef);
            _componentTypeHandleAttachment  .Update(ref CheckedStateRef);
            _entityTypeHandle               .Update(ref CheckedStateRef);
            UpdateColorsJobAttachmentBuilding updateColorsJobAttachmentBuilding = new UpdateColorsJobAttachmentBuilding()
            {
                ComponentLookupColor            = _componentLookupColor,
                ComponentTypeHandleAttachment   = _componentTypeHandleAttachment,
                EntityTypeHandle                = _entityTypeHandle,
            };


            // Create a job to update temp object colors.
            _componentLookupColor       .Update(ref CheckedStateRef);
            _componentTypeHandleTemp    .Update(ref CheckedStateRef);
            _entityTypeHandle           .Update(ref CheckedStateRef);
            UpdateColorsJobTempObject updateColorsJobTempObject = new UpdateColorsJobTempObject()
            {
                ComponentLookupColor    = _componentLookupColor,
                ComponentTypeHandleTemp = _componentTypeHandleTemp,
                EntityTypeHandle        = _entityTypeHandle,
            };

            
            // Create a job to update sub object colors.
            _componentLookupColor           .Update(ref CheckedStateRef);
            _componentLookupBuilding        .Update(ref CheckedStateRef);
            _componentLookupElevation       .Update(ref CheckedStateRef);
            _componentLookupOwner           .Update(ref CheckedStateRef);
            _componentLookupVehicle         .Update(ref CheckedStateRef);
            _componentTypeHandleElevation   .Update(ref CheckedStateRef);
            _componentTypeHandleOwner       .Update(ref CheckedStateRef);
            _componentTypeHandleTree        .Update(ref CheckedStateRef);
            _entityTypeHandle               .Update(ref CheckedStateRef);
            UpdateColorsJobSubObject updateColorsJobSubObject = new UpdateColorsJobSubObject()
            {
                ComponentLookupColor            = _componentLookupColor,
                ComponentLookupBuilding         = _componentLookupBuilding,
                ComponentLookupElevation        = _componentLookupElevation,
                ComponentLookupOwner            = _componentLookupOwner,
                ComponentLookupVehicle          = _componentLookupVehicle,
                ComponentTypeHandleElevation    = _componentTypeHandleElevation,
                ComponentTypeHandleOwner        = _componentTypeHandleOwner,
                ComponentTypeHandleTree         = _componentTypeHandleTree,
                EntityTypeHandle                = _entityTypeHandle,
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

            // Wait for the attachment building job to complete.
            // This seems to help prevent screen flicker.
            jobHandleAttachmentBuilding.Complete();
            
            // Dispose of native collections no longer needed once the main building job is complete.
            activeInfomodes.Dispose();

            // Note that the jobs after the attachment building job could still be executing at this point, which is okay.

            // This system handled building colors for this mod's infoview.
            // Do not execute the original game logic.
            return false;
        }
    }
}
