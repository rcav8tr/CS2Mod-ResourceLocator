// This entire file is only for creating UI files when in DEBUG.
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ResourceLocator
{
    /// <summary>
    /// Create the files for UI:
    ///     One file for constants (i.e. building types and display options).
    ///     One file for the data bindings between C# and UI.
    ///     One file for UI translation keys for C#.
    ///     One file for UI translation keys for UI.
    /// By creating the files from the C# enums/constants/etc, C# and UI are ensured to be the same as each other.
    /// </summary>
    public static class CreateUIFiles
    {
        // Shortcut for UI constants dictionary.
        // Dictionary key is the constant name.
        // Dictionary value is the constant value.
        private class UIConstants : Dictionary<string, string> { }

        // Shortcut for translation keys list.
        // Entry is used for constant name and constant value suffix.
        private class TranslationKeys : List<string> { }


        /// <summary>
        /// Create the UI files.
        /// </summary>
        public static void Create()
        {
            CreateFileUIConstants();
            CreateFileUIBindings();
            CreateFileUITranslationKeys(true);
            CreateFileUITranslationKeys(false);
        }

        /// <summary>
        /// Create the file for UI constants.
        /// </summary>
        private static void CreateFileUIConstants()
        {
            // Start with the do not modify instructions.
            StringBuilder sb = new StringBuilder();
            sb.Append(DoNotModify());

            // Include building types.
            sb.AppendLine();
            sb.AppendLine("// Define building types.");
            sb.AppendLine("export enum " + nameof(RLBuildingType));
            sb.AppendLine("{");
            foreach (RLBuildingType buildingType in Enum.GetValues(typeof(RLBuildingType)))
            {
                sb.AppendLine($"    {buildingType.ToString().PadRight(32)} = {(int)buildingType},");
            }
            sb.AppendLine("}");

            // Include display options.
            sb.AppendLine();
            sb.AppendLine("// Define display options.");
            sb.AppendLine("export enum " + nameof(DisplayOption));
            sb.AppendLine("{");
            foreach (DisplayOption displayOption in Enum.GetValues(typeof(DisplayOption)))
            {
                sb.AppendLine($"    {displayOption.ToString().PadRight(32)} = {(int)displayOption},");
            }
            sb.AppendLine("}");

            // Include color options.
            sb.AppendLine();
            sb.AppendLine("// Define color options.");
            sb.AppendLine("export enum " + nameof(ColorOption));
            sb.AppendLine("{");
            foreach (ColorOption colorOption in Enum.GetValues(typeof(ColorOption)))
            {
                sb.AppendLine($"    {colorOption.ToString().PadRight(32)} = {(int)colorOption},");
            }
            sb.AppendLine("}");

            // Write the file to the UI/src folder.
            string uiConstantsPath = Path.Combine(GetSourceCodePath(), "UI", "src", "uiConstants.tsx");
            File.WriteAllText(uiConstantsPath, sb.ToString());
        }

        /// <summary>
        /// Create the file for UI bindings.
        /// </summary>
        private static void CreateFileUIBindings()
        {
            // Start with the do not modify instructions.
            StringBuilder sb = new StringBuilder();
            sb.Append(DoNotModify());

            // Include binding names.
            sb.AppendLine("// Define binding names for C# to UI.");
            sb.AppendLine("export class uiBindingNames");
            sb.AppendLine("{");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.BindingNameSelectedDistrict      .PadRight(36)} : string = \"{ResourceLocatorUISystem.BindingNameSelectedDistrict}\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.BindingNameDistrictInfos         .PadRight(36)} : string = \"{ResourceLocatorUISystem.BindingNameDistrictInfos   }\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.BindingNameDisplayOption         .PadRight(36)} : string = \"{ResourceLocatorUISystem.BindingNameDisplayOption   }\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.BindingNameColorOption           .PadRight(36)} : string = \"{ResourceLocatorUISystem.BindingNameColorOption     }\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.BindingNameOneColor              .PadRight(36)} : string = \"{ResourceLocatorUISystem.BindingNameOneColor        }\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.BindingNameResourceInfos         .PadRight(36)} : string = \"{ResourceLocatorUISystem.BindingNameResourceInfos   }\";");
            sb.AppendLine("}");

            // Include event names.
            sb.AppendLine();
            sb.AppendLine("// Define event names for UI to C#.");
            sb.AppendLine("export class uiEventNames");
            sb.AppendLine("{");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.EventNameSelectedDistrictChanged .PadRight(36)} : string = \"{ResourceLocatorUISystem.EventNameSelectedDistrictChanged}\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.EventNameDisplayOptionClicked    .PadRight(36)} : string = \"{ResourceLocatorUISystem.EventNameDisplayOptionClicked   }\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.EventNameColorOptionClicked      .PadRight(36)} : string = \"{ResourceLocatorUISystem.EventNameColorOptionClicked     }\";");
            sb.AppendLine($"    public static {ResourceLocatorUISystem.EventNameOneColorChanged         .PadRight(36)} : string = \"{ResourceLocatorUISystem.EventNameOneColorChanged        }\";");
            sb.AppendLine("}");

            // Write the file to the UI/src folder.
            string uiBindingsPath = Path.Combine(GetSourceCodePath(), "UI", "src", "uiBindings.tsx");
            File.WriteAllText(uiBindingsPath, sb.ToString());
        }

        /// <summary>
        /// Create the file for UI transtion keys.
        /// One file for C# (i.e. CS) and one file for UI.
        /// </summary>
        private static void CreateFileUITranslationKeys(bool csFile)
        {
            // Start with the do not modify instructions.
            StringBuilder sb = new StringBuilder();
            sb.Append(DoNotModify());

            // For C# file, include namespace.
            if (csFile)
            {
                sb.AppendLine($"namespace {ModAssemblyInfo.Name}");
                sb.AppendLine("{");
            }

            // Start class.
            const string className = "UITranslationKey";
            if (csFile)
            {
                sb.AppendLine("    // Define UI translation keys.");
                sb.AppendLine("    public class " + className);
                sb.AppendLine("    {");
            }
            else
            {
                sb.AppendLine("// Define UI translation keys.");
                sb.AppendLine("export class " + className);
                sb.AppendLine("{");
            }

            // Include mod title and description.
            TranslationKeys titleDescription = new TranslationKeys()
            {
                "Title",
                "Description",
            };
            sb.Append(GetTranslationsContent(csFile, "Mod title and description.", titleDescription));

            // Include infoview title.
            UIConstants infoviewTitles = new UIConstants();
            infoviewTitles.Add("InfoviewTitle", $"Infoviews.INFOVIEW[{ModAssemblyInfo.Name}]");
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Infoview title.", infoviewTitles));

            // Include infomode titles and tooltips.
            UIConstants infomodeTitles   = new UIConstants();
            UIConstants infomodeTooltips = new UIConstants();
            foreach (RLBuildingType buildingType in Enum.GetValues(typeof(RLBuildingType)))
            {
                // Skip special cases.
                if (!RLBuildingTypeUtils.IsSpecialCase(buildingType))
                {
                    string buildingTypeName = RLBuildingTypeUtils.GetBuildingTypeName(buildingType);
                    infomodeTitles  .Add("InfomodeTitle"   + buildingType.ToString(), $"Infoviews.INFOMODE[{        buildingTypeName}]");
                    infomodeTooltips.Add("InfomodeTooltip" + buildingType.ToString(), $"Infoviews.INFOMODE_TOOLTIP[{buildingTypeName}]");
                }
            }
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Infomode titles.", infomodeTitles));
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Infomode tooltips.", infomodeTooltips));

            // Include district selector text.
            TranslationKeys districtSelectorText = new TranslationKeys() { "EntireCity", "DistrictSelectorTooltip", };
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "District selector text.", districtSelectorText));

            // Include display option text.
            TranslationKeys displayOptionText = new TranslationKeys();
            foreach (DisplayOption displayOption in Enum.GetValues(typeof(DisplayOption)))
            {
                displayOptionText.Add(nameof(DisplayOption) + displayOption.ToString());
            }
            displayOptionText.Add(nameof(DisplayOption) + "Tooltip");
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Display option text.", displayOptionText));

            // Include color option text.
            TranslationKeys colorOptionText = new TranslationKeys();
            colorOptionText.Add(nameof(ColorOption) + "Color");
            foreach (ColorOption colorOption in Enum.GetValues(typeof(ColorOption)))
            {
                colorOptionText.Add(nameof(ColorOption) + colorOption.ToString());
            }
            colorOptionText.Add(nameof(ColorOption) + "Tooltip");
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Color option text.", colorOptionText));

            // Include select/deselect text.
            TranslationKeys selectDeselectText = new TranslationKeys() { "SelectAll", "DeselectAll", "SelectDeselectTooltip", };
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Select/deselect text.", selectDeselectText));

            // Include resource category text.
            TranslationKeys resourceCategoryText = new TranslationKeys() { "Materials", "MaterialGoods", "ImmaterialGoods", };
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Resource category text.", resourceCategoryText));

            // Include unit of measure text.
            TranslationKeys unitOfMeasureText = new TranslationKeys() { "UnitOfMeasurePrefixKilo", };
            sb.AppendLine();
            sb.Append(GetTranslationsContent(csFile, "Unit of measure text.", unitOfMeasureText));

            // For C# file only, include settings translations.
            if (csFile)
            {
                // Construct settings.
                UIConstants _translationKeySettings = new UIConstants()
                {
                    { "SettingTitle",                           Mod.ModSettings.GetSettingsLocaleID()                                                  },


                    { "SettingGroupInclude",                    Mod.ModSettings.GetOptionGroupLocaleID(ModSettings.GroupInclude)                       },

                    { "SettingIncludeGeneralDescription",       Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeGeneralDescription )) },
                    { "SettingIncludeRecyclingCenterLabel",     Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeRecyclingCenter    )) },
                    { "SettingIncludeRecyclingCenterDesc",      Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.IncludeRecyclingCenter    )) },
                    { "SettingIncludeCoalPowerPlantLabel",      Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeCoalPowerPlant     )) },
                    { "SettingIncludeCoalPowerPlantDesc",       Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.IncludeCoalPowerPlant     )) },
                    { "SettingIncludeGasPowerPlantLabel",       Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeGasPowerPlant      )) },
                    { "SettingIncludeGasPowerPlantDesc",        Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.IncludeGasPowerPlant      )) },
                    { "SettingIncludeMedicalFacilityLabel",     Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeMedicalFacility    )) },
                    { "SettingIncludeMedicalFacilityDesc",      Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.IncludeMedicalFacility    )) },
                    { "SettingIncludeEmeregencyShelterLabel",   Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeEmeregencyShelter  )) },
                    { "SettingIncludeEmeregencyShelterDesc",    Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.IncludeEmeregencyShelter  )) },
                    { "SettingIncludeCargoStationLabel",        Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.IncludeCargoStation       )) },
                    { "SettingIncludeCargoStationDesc",         Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.IncludeCargoStation       )) },
                                                                                                                                                              
                    { "SettingGroupAbout",                      Mod.ModSettings.GetOptionGroupLocaleID(ModSettings.GroupAbout)                         },

                    { "SettingModVersionLabel",                 Mod.ModSettings.GetOptionLabelLocaleID(nameof(ModSettings.ModVersion                )) },
                    { "SettingModVersionDesc",                  Mod.ModSettings.GetOptionDescLocaleID (nameof(ModSettings.ModVersion                )) },
                };

                // Append settings to the file.
                sb.AppendLine();
                sb.Append(GetTranslationsContent(csFile, "Settings.", _translationKeySettings));
            }

            // End class.
            sb.AppendLine(csFile ? "    }": "}");

            // For C# file, end namespace.
            if (csFile)
            {
                sb.AppendLine("}");
            }

            // Write the file.
            string uiBindingsPath;
            if (csFile)
            {
                // Write the file to the Localization folder.
                uiBindingsPath = Path.Combine(GetSourceCodePath(), "Localization", "UITranslationKey.cs");
            }
            else
            {
                // Write the file to the UI/src folder.
                uiBindingsPath = Path.Combine(GetSourceCodePath(), "UI", "src", "uiTranslationKey.tsx");
            }
            File.WriteAllText(uiBindingsPath, sb.ToString());
        }

        /// <summary>
        /// Get instructions for do not modify.
        /// </summary>
        /// <returns></returns>
        private static string DoNotModify()
        {
            StringBuilder sb = new StringBuilder();
            // Include do not modify instructions.
            sb.AppendLine($"// DO NOT MODIFY THIS FILE.");
            sb.AppendLine($"// This entire file was automatically generated by class {nameof(CreateUIFiles)}.");
            sb.AppendLine($"// Make any needed changes in class {nameof(CreateUIFiles)}.");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Get the constants content.
        /// </summary>
        private static string GetTranslationsContent(bool csFile, string comment, UIConstants constants)
        {
            string indentation = (csFile ? "        " : "    ");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{indentation}// {comment}");
            foreach (var key in constants.Keys)
            {
                if (csFile)
                {
                    sb.AppendLine($"{indentation}public const string {key.PadRight(50)} = \"{constants[key]}\";");
                }
                else
                {
                    sb.AppendLine($"{indentation}public static {key.PadRight(50)}: string = \"{constants[key]}\";");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get the translations content.
        /// </summary>
        private static string GetTranslationsContent(bool csFile, string comment, TranslationKeys translationKeys)
        {
            string indentation = (csFile ? "        " : "    ");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{indentation}// {comment}");
            foreach (var translationKey in translationKeys)
            {
                if (csFile)
                {
                    sb.AppendLine($"{indentation}public const string {translationKey.PadRight(50)} = \"{ModAssemblyInfo.Name}.{translationKey}\";");
                }
                else
                {
                    sb.AppendLine($"{indentation}public static {translationKey.PadRight(50)}: string = \"{ModAssemblyInfo.Name}.{translationKey}\";");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get the full path of this C# source code file.
        /// </summary>
        private static string GetSourceCodePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
        {
            return Path.GetDirectoryName(sourceFile);
        }
    }
}

#endif
