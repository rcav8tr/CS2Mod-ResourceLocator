﻿using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Modding;
using Game.SceneFlow;
using System;
using System.IO;

namespace ResourceLocator
{
    public class Mod : IMod
    {
        // Create a new log just for this mod.
        // This mod will have its own log file in the game's Logs folder.
        public static readonly ILog log = LogManager.GetLogger(ModAssemblyInfo.Name)
            .SetShowsErrorsInUI(true)                       // Show message in UI for severity level Error and above.
            .SetShowsStackTraceAboveLevels(Level.Error);    // Include stack trace for severity level Error and above.

        // The global settings for this mod.
        public static ModSettings ModSettings { get; set; }

        // URI for UI images.
        // When the URI is used to access an image, the game forces the URI portion to lower case.
        // So make the URI lower case here to be compatible.
        public static string ImagesURI { get { return ModAssemblyInfo.Name.ToLower(); } }

        /// <summary>
        /// One-time mod loading.
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} Version {ModAssemblyInfo.Version}");
            
            try
            {
                // Register and load mod settings.
                ModSettings = new ModSettings(this);
                ModSettings.RegisterInOptionsUI();
                AssetDatabase.global.LoadSettings(ModAssemblyInfo.Name, ModSettings, new ModSettings(this));

                // Initialize translations.
                Translation.Initialize();

                // Add mod UI images directory to UI resource handler.
                if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out ExecutableAsset modExecutableAsset))
                {
                    log.Error("Unable to get mod executable asset.");
                    return;
                }
                string assemblyPath = Path.GetDirectoryName(modExecutableAsset.path);
                string imagesPath = Path.Combine(assemblyPath, "Images");
                UIManager.defaultUISystem.AddHostLocation(ImagesURI, imagesPath);

                // Initialize the infoview.
                RLInfoviewUtils.Initialize();

                // Create and activate this mod's systems.
                updateSystem.UpdateAt<ResourceLocatorUISystem>(SystemUpdatePhase.UIUpdate);
                updateSystem.UpdateAt<BuildingColorSystem>(SystemUpdatePhase.Rendering);

#if DEBUG
                // Get localized text from the game where the value is or contains specific text.
                //Colossal.Localization.LocalizationManager localizationManager = GameManager.instance.localizationManager;
                //foreach (System.Collections.Generic.KeyValuePair<string, string> keyValue in localizationManager.activeDictionary.entries)
                //{
                //    // Exclude assets.
                //    if (!keyValue.Key.StartsWith("Assets."))
                //    {
                //        //if (keyValue.Value.ToLower().StartsWith("color"))
                //        //if (keyValue.Value.StartsWith("Cargo"))
                //        if (keyValue.Value == ("Distinct"))
                //        {
                //            log.Info(keyValue.Key + "\t" + keyValue.Value);
                //        }
                //    }
                //}

                // For a specific localization key, get the localized text for each base game locale ID.
                //string[] localeIDs = new string[] { "en-US", "de-DE", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "ru-RU", "zh-HANS", "zh-HANT" };
                //foreach (string localeID in localeIDs)
                //{
                //    localizationManager.SetActiveLocale(localeID);
                //    foreach (System.Collections.Generic.KeyValuePair<string, string> keyValue in localizationManager.activeDictionary.entries)
                //    {
                //        if (keyValue.Key == "EconomyPanel.PRODUCTION_PAGE_PRODUCTIONLINK[Import]")
                //        {
                //            log.Info(keyValue.Key + "\t" + localeID + "\t" + keyValue.Value);
                //            break;
                //        }
                //    }
                //}
                //localizationManager.SetActiveLocale("en-US");

                // Create UI files.
                // Uncomment this only when the UI files need to be created or recreated.
                // Then run the mod once in the game to create the files.
                // Then comment this again.  The UI files are now available to use.
                //CreateUIFiles.Create();
#endif
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }

            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} complete.");
        }

        /// <summary>
        /// One-time mod disposing.
        /// </summary>
        public void OnDispose()
        {
            log.Info($"{nameof(Mod)}.{nameof(OnDispose)}");

            // Unregister mod settings.
            ModSettings?.UnregisterInOptionsUI();
            ModSettings = null;
        }
    }
}
