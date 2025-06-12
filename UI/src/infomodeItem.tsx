import { CSSProperties                  } from "react";

import { bindValue, useValue, trigger   } from "cs2/api";
import { infoviewTypes                  } from "cs2/bindings";
import { UnitSettings, useLocalization  } from "cs2/l10n";
import { FormattedParagraphsProps       } from "cs2/ui";

import   styles                           from "infomodeItem.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames                 } from "uiBindings";
import { DisplayOption, RLBuildingType  } from "uiConstants";
import { UITranslationKey               } from "uiTranslationKey";

// Define unit systems.
// For unknown reasons, using the UnitSystem enum directly from l10n causes a run-time error.
enum UnitSystem
{
    Metric  = 0,
    Freedom = 1
}

// Define resource info passed from C#.
type ResourceInfo =
{
    buildingType:       RLBuildingType;

    storageRequires:    number;
    storageProduces:    number;
    storageSells:       number;
    storageStores:      number;
    
    rateProduction:     number;
    rateSurplus:        number;
    rateDeficit:        number;
}

// Define bindings.
const bindingDisplayOption = bindValue<number        >(mod.id, uiBindingNames.DisplayOption, DisplayOption.Requires);
const bindingResourceInfos = bindValue<ResourceInfo[]>(mod.id, uiBindingNames.ResourceInfos);
const bindingUnitSettings  = bindValue<UnitSettings  >("options", "unitSettings");

// Props for InfomodeItem.
interface InfomodeItemProps
{
    infomode:       infoviewTypes.Infomode;
    buildingType:   RLBuildingType;
}

// Custom infmode item.
// Adapted from the base game's infomode logic.
export const InfomodeItem = ({ infomode, buildingType }: InfomodeItemProps) =>
{
    // Translations.
    const { translate } = useLocalization();
    const translationInfomodeTitle      = translate("Infoviews.INFOMODE[" + infomode.id + "]");
    const translationInfomodeTooltip    = translate("Infoviews.INFOMODE_TOOLTIP[" + infomode.id + "]");
    const translationBuildingColor      = translate("Infoviews.INFOMODE_TYPE[BuildingColor]");
    const translationThousandsSeparator = translate("Common.THOUSANDS_SEPARATOR", ",") + "";
    const translationStorage            = translate("SelectedInfoPanel.WAREHOUSE_STORAGE");
    const translationStorageRequires    = translationStorage + " - " + translate(UITranslationKey.DisplayOptionRequires) + ": ";
    const translationStorageProduces    = translationStorage + " - " + translate(UITranslationKey.DisplayOptionProduces) + ": ";
    const translationStorageSells       = translationStorage + " - " + translate(UITranslationKey.DisplayOptionSells   ) + ": ";
    const translationStorageStores      = translationStorage + " - " + translate(UITranslationKey.DisplayOptionStores  ) + ": ";
    const translationRateProduction     = translate("EconomyPanel.PRODUCTION_PAGE_PRODUCTION") + ": ";
    const translationRateSurplus        = translate("EconomyPanel.PRODUCTION_PAGE_SURPLUS"   ) + ": "
    const translationRateDeficit        = translate("EconomyPanel.PRODUCTION_PAGE_DEFICIT"   ) + ": "

    // Format base infomode tooltip into paragraphs.
    // This also applies markdown formatting embedded in the text.
    // Resource info gets appended later.
    const formattedParagraphsProps: FormattedParagraphsProps = { children: translationInfomodeTooltip };
    const formattedInfomodeTooltip: JSX.Element = ModuleResolver.instance.FormattedParagraphs(formattedParagraphsProps);

    // Get display option.
    const displayOption = useValue(bindingDisplayOption);

    // Define variables for storage and rate values for this resource.
    let valueStorageRequires:   number = 0;
    let valueStorageProduces:   number = 0;
    let valueStorageSells:      number = 0;
    let valueStorageStores:     number = 0;

    let valueRateProduction:    number = 0;
    let valueRateSurplus:       number = 0;
    let valueRateDeficit:       number = 0;

    // Do each resource info.
    const resourceInfos: ResourceInfo[] = useValue(bindingResourceInfos);
    for (let i: number = 0; i < resourceInfos.length; i++)
    {
        // Check if building type of the resource info is for the building type of this infomode.
        const resourceInfo: ResourceInfo = resourceInfos[i];
        if (resourceInfo.buildingType === buildingType)
        {
            // Get storage and rate values for this resource.
            // Note that deficit comes thru as a positive value.
            valueStorageRequires    = resourceInfo.storageRequires;
            valueStorageProduces    = resourceInfo.storageProduces;
            valueStorageSells       = resourceInfo.storageSells;
            valueStorageStores      = resourceInfo.storageStores;

            valueRateProduction     = resourceInfo.rateProduction;
            valueRateSurplus        = resourceInfo.rateSurplus;
            valueRateDeficit        = resourceInfo.rateDeficit;

            // Found the building type.  Stop checking.
            break;
        }
    }

    // Get max storage and rate values from the last resource info, which should be MaxValues.
    let valueStorageMaxAll: number = 0;
    let valueRateMaxAll:    number = 0;
    const maxResourceInfo: ResourceInfo = resourceInfos[resourceInfos.length - 1];
    if (maxResourceInfo.buildingType === RLBuildingType.MaxValues)
    {
        // Get max storage value for the current Display Option.
        switch (displayOption)
        {
            case DisplayOption.Requires: valueStorageMaxAll = maxResourceInfo.storageRequires; break;
            case DisplayOption.Produces: valueStorageMaxAll = maxResourceInfo.storageProduces; break;
            case DisplayOption.Sells:    valueStorageMaxAll = maxResourceInfo.storageSells;    break;
            case DisplayOption.Stores:   valueStorageMaxAll = maxResourceInfo.storageStores;   break;
        }

        // Get max rate value between production, surplus, and deficit.
        valueRateMaxAll = Math.max(valueRateMaxAll, maxResourceInfo.rateProduction, maxResourceInfo.rateSurplus, maxResourceInfo.rateDeficit);
    }

    // Function to compute a value as a percent of a max.
    function ComputePercent(value: number, max: number): number
    {
        // Prevent divide by zero.
        if (max === 0)
        {
            return 0;
        }

        // Compute percent and limit to 0 to 100 percent.
        // Percent is computed compared to the max value.
        // This is similar to the game's Production tab on the City Economy panel.
        return Math.max(Math.min(100 * value / max, 100), 0);
    }

    // Compute a percent for the storage value compared to the max of all storage values for the current display option.
    let percentStorage: number = 0;
    switch (displayOption)
    {
        case DisplayOption.Requires: percentStorage = ComputePercent(valueStorageRequires, valueStorageMaxAll); break;
        case DisplayOption.Produces: percentStorage = ComputePercent(valueStorageProduces, valueStorageMaxAll); break;
        case DisplayOption.Sells:    percentStorage = ComputePercent(valueStorageSells,    valueStorageMaxAll); break;
        case DisplayOption.Stores:   percentStorage = ComputePercent(valueStorageStores,   valueStorageMaxAll); break;
    }

    // Compute percents for the rate values compared to the max of all rate values.
    const percentRateProduction: number = ComputePercent(valueRateProduction, valueRateMaxAll);
    const percentRateSurplus:    number = ComputePercent(valueRateSurplus,    valueRateMaxAll);
    const percentRateDeficit:    number = ComputePercent(valueRateDeficit,    valueRateMaxAll);

    // Set styles that will cover the percent bars starting at the percent value and going to the right.
    const stylePercentStorage:    Partial<CSSProperties> = { marginLeft: percentStorage        + "%", }
    const stylePercentProduction: Partial<CSSProperties> = { marginLeft: percentRateProduction + "%", }
    const stylePercentSurplus:    Partial<CSSProperties> = { marginLeft: percentRateSurplus    + "%", }
    const stylePercentDeficit:    Partial<CSSProperties> = { marginLeft: percentRateDeficit    + "%", }

    // Check if need to convert values.
    const valueUnitSettings = useValue(bindingUnitSettings);
    if (valueUnitSettings.unitSystem === UnitSystem.Freedom)
    {
        // Convert this resource's values from kg to pounds.
        const poundsPerKG: number = 2.204622622;
        valueStorageRequires = Math.round(valueStorageRequires * poundsPerKG);
        valueStorageProduces = Math.round(valueStorageProduces * poundsPerKG);
        valueStorageSells    = Math.round(valueStorageSells    * poundsPerKG);
        valueStorageStores   = Math.round(valueStorageStores   * poundsPerKG);

        valueRateProduction  = Math.round(valueRateProduction  * poundsPerKG);
        valueRateSurplus     = Math.round(valueRateSurplus     * poundsPerKG);
        valueRateDeficit     = Math.round(valueRateDeficit     * poundsPerKG);
    }

    // Get maximum between storage and rate values for this resource.
    const valueMaxThis: number = Math.max(
        valueStorageRequires,
        valueStorageProduces,
        valueStorageSells,
        valueStorageStores,

        valueRateProduction,
        valueRateSurplus,
        valueRateDeficit);

    // Get scaling factor and unit of measure prefix and text for storage and rates values.
    // Scalling factor and UOM prefix are the same between storage and rate.
    // UOM text is different between storage and rate.
    let scalingFactor:  number = 1;
    let uomPrefix:      string | null = null;
    let storageUOMText: string | null = null;
    let rateUOMText:    string | null = null;
    if (valueUnitSettings.unitSystem === UnitSystem.Metric)
    {
        // Check scale of max value.
        if (valueMaxThis < 100000)
        {
            // No scaling needed.
            storageUOMText = translate(ModuleResolver.instance.Loc.Common.VALUE_KILOGRAM.displayName);
            rateUOMText    = translate(ModuleResolver.instance.Loc.Common.VALUE_KG_PER_MONTH.displayName);
        }
        else if (valueMaxThis < 100000000)
        {
            // Convert kg to tons.
            scalingFactor = 1000;
            storageUOMText = translate(ModuleResolver.instance.Loc.Common.VALUE_TON.displayName);
            rateUOMText    = translate(ModuleResolver.instance.Loc.Common.VALUE_TON_PER_MONTH.displayName);
        }
        else
        {
            // Convert kg to kilo tons.
            scalingFactor = 1000000;
            uomPrefix      = translate(UITranslationKey.UnitOfMeasurePrefixKilo);
            storageUOMText = translate(ModuleResolver.instance.Loc.Common.VALUE_TON.displayName);
            rateUOMText    = translate(ModuleResolver.instance.Loc.Common.VALUE_TON_PER_MONTH.displayName);
        }
    }
    else
    {
        // Check scale of max value.
        if (valueMaxThis < 100000)
        {
            // No scaling needed.
            storageUOMText = translate(ModuleResolver.instance.Loc.Common.VALUE_POUND.displayName);
            rateUOMText    = translate(ModuleResolver.instance.Loc.Common.VALUE_POUND_PER_MONTH.displayName);
        }
        else if (valueMaxThis < 200000000)
        {
            // Convert pounds to short tons.
            scalingFactor = 2000;
            storageUOMText = translate(ModuleResolver.instance.Loc.Common.VALUE_SHORT_TON.displayName);
            rateUOMText    = translate(ModuleResolver.instance.Loc.Common.VALUE_SHORT_TON_PER_MONTH.displayName);
        }
        else
        {
            // Convert pounds to kilo short tons.
            scalingFactor = 2000000;
            uomPrefix      = translate(UITranslationKey.UnitOfMeasurePrefixKilo);
            storageUOMText = translate(ModuleResolver.instance.Loc.Common.VALUE_SHORT_TON.displayName);
            rateUOMText    = translate(ModuleResolver.instance.Loc.Common.VALUE_SHORT_TON_PER_MONTH.displayName);
        }
    }

    // Apply scaling factor to storage and rate values.
    const valueScaledStorageRequires: number = Math.round(valueStorageRequires / scalingFactor);
    const valueScaledStorageProduces: number = Math.round(valueStorageProduces / scalingFactor);
    const valueScaledStorageSells:    number = Math.round(valueStorageSells    / scalingFactor);
    const valueScaledStorageStores:   number = Math.round(valueStorageStores   / scalingFactor);

    const valueScaledRateProduction:  number = Math.round(valueRateProduction  / scalingFactor);
    const valueScaledRateSurplus:     number = Math.round(valueRateSurplus     / scalingFactor);
    const valueScaledRateDeficit:     number = Math.round(valueRateDeficit     / scalingFactor);

    // Remove variable placeholders from unit of measure text.
    storageUOMText = "" + storageUOMText?.replace("{SIGN}{VALUE}", "");
    rateUOMText    = "" + rateUOMText   ?.replace("{SIGN}{VALUE}", "");

    // Function to format a value and append unit of measure prefix and text.
    function FormatValue(value: number, uomText: string): string
    {
        // Logic adapted from the game's index.js for localized numbers.
        const regexReplacement = /\B(?=(\d{3})+(?!\d))/g;
        return value.toFixed(0).replace(regexReplacement, translationThousandsSeparator) +
            " " + (uomPrefix && uomPrefix.length > 0 ? uomPrefix + " " : "") + uomText?.trim()
    }

    // Compute formatted storage and rate values.
    const formattedStorageRequires:     string = FormatValue(valueScaledStorageRequires, storageUOMText);
    const formattedStorageProduces:     string = FormatValue(valueScaledStorageProduces, storageUOMText);
    const formattedStorageSells:        string = FormatValue(valueScaledStorageSells,    storageUOMText);
    const formattedStorageStores:       string = FormatValue(valueScaledStorageStores,   storageUOMText);

    const valueScaledRateSurplusDeficit: number = valueScaledRateDeficit > 0 ? valueScaledRateDeficit : valueScaledRateSurplus;
    const formattedRateProduction:      string = FormatValue(valueScaledRateProduction,     rateUOMText);
    const formattedRateSurplusDeficit:  string = FormatValue(valueScaledRateSurplusDeficit, rateUOMText);

    // Compute styles to set text color based on display option.
    const styleColorRequires:       Partial<CSSProperties> = displayOption === DisplayOption.Requires ? { color: "var(--resourceLocatorStorageColor)" } : {};
    const styleColorProduces:       Partial<CSSProperties> = displayOption === DisplayOption.Produces ? { color: "var(--resourceLocatorStorageColor)" } : {};
    const styleColorSells:          Partial<CSSProperties> = displayOption === DisplayOption.Sells    ? { color: "var(--resourceLocatorStorageColor)" } : {};
    const styleColorStores:         Partial<CSSProperties> = displayOption === DisplayOption.Stores   ? { color: "var(--resourceLocatorStorageColor)" } : {};

    const styleColorProduction:     Partial<CSSProperties> = displayOption === DisplayOption.Produces ? { color: "var(--resourceLocatorProductionColor)" } : {};
    const styleColorSurplusDeficit: Partial<CSSProperties> = displayOption === DisplayOption.Produces ? { color: valueRateDeficit > 0 ? "var(--resourceLocatorDeficitColor)" : "var(--resourceLocatorSurplusColor)" } : {};

    // Get icon based on building type.
    // This logic assumes all building type enum names are the same as the resource file names.
    const buildingTypeEnumName: string = RLBuildingType[buildingType];
    const icon: string = "Media/Game/Resources/" + buildingTypeEnumName + ".svg";

    // Style to set symbol background color from the color in the infomode.
    const styleSymbol: Partial<CSSProperties> =
    {
        backgroundColor: infomode.color ? "rgba(" + Math.min(Math.round(infomode.color.r * 255), 255) + "," +
                                                    Math.min(Math.round(infomode.color.g * 255), 255) + "," + 
                                                    Math.min(Math.round(infomode.color.b * 255), 255) + ",1)" :
            "rgba(0, 0, 0, 1)"
    }
    
    // Function to join classes.
    function joinClasses(...classes: any) { return classes.join(" "); }

    // Handle button click.
    function onButtonClick()
    {
        trigger("audio", "playSound", ModuleResolver.instance.UISound.toggleInfoMode, 1);
        trigger("infoviews", "setInfomodeActive", infomode.entity, !infomode.active, infomode.priority);
    }

    // Mostly adapted from the base game building color infomode with the following general changes:
    //      Use element styles to override default base game appearance, mostly for making the infomode more compact.
    //      Add an icon before the symbol.
    //      Add storage and rate bars.
    //      Include resource information in the tooltip.
    return (
        <ModuleResolver.instance.Tooltip
            direction="right"
            tooltip=
            {
                <>
                    {formattedInfomodeTooltip}
                    <div className={styles.resourceLocatorInfomodeData}>
                        <div className={styles.resourceLocatorInfomodeDataRow} style={styleColorRequires}>
                            <div className={styles.resourceLocatorInfomodeDataRowHeading}>{translationStorageRequires}</div>
                            <div className={styles.resourceLocatorInfomodeDataRowValue}>{formattedStorageRequires}</div>
                        </div>
                        <div className={styles.resourceLocatorInfomodeDataRow} style={styleColorProduces}>
                            <div className={styles.resourceLocatorInfomodeDataRowHeading}>{translationStorageProduces}</div>
                            <div className={styles.resourceLocatorInfomodeDataRowValue}>{formattedStorageProduces}</div>
                        </div>
                        <div className={styles.resourceLocatorInfomodeDataRow} style={styleColorSells}>
                            <div className={styles.resourceLocatorInfomodeDataRowHeading}>{translationStorageSells}</div>
                            <div className={styles.resourceLocatorInfomodeDataRowValue}>{formattedStorageSells}</div>
                        </div>
                        <div className={styles.resourceLocatorInfomodeDataRow} style={styleColorStores}>
                            <div className={styles.resourceLocatorInfomodeDataRowHeading}>{translationStorageStores}</div>
                            <div className={styles.resourceLocatorInfomodeDataRowValue}>{formattedStorageStores}</div>
                        </div>
                    </div>
                    <div className={styles.resourceLocatorInfomodeData}>
                        <div className={styles.resourceLocatorInfomodeDataRow} style={styleColorProduction}>
                            <div className={styles.resourceLocatorInfomodeDataRowHeading}>{translationRateProduction}</div>
                            <div className={styles.resourceLocatorInfomodeDataRowValue}>{formattedRateProduction}</div>
                        </div>
                        <div className={styles.resourceLocatorInfomodeDataRow} style={styleColorSurplusDeficit}>
                            <div className={styles.resourceLocatorInfomodeDataRowHeading}>{valueRateDeficit > 0 ? translationRateDeficit : translationRateSurplus}</div>
                            <div className={styles.resourceLocatorInfomodeDataRowValue}>{formattedRateSurplusDeficit}</div>
                        </div>
                    </div>
                    <div className={styles.resourceLocatorInfomodeDataRowBottom} />
                </>
            }
            theme={ModuleResolver.instance.TooltipClasses}
            children=
            {
                <button
                    className={joinClasses(ModuleResolver.instance.TransparentButtonClasses.button,
                                           ModuleResolver.instance.InfomodeItemClasses.infomodeItem,
                                           (infomode.active ? ModuleResolver.instance.InfomodeItemClasses.active : ""),
                                           styles.resourceLocatorInfomodeButton)}
                    onClick={() => onButtonClick()}
                >
                    <div className={ModuleResolver.instance.InfomodeItemClasses.header}>
                        <div className={ModuleResolver.instance.InfomodeItemClasses.title}>
                            <img className={styles.resourceLocatorInfomodeIcon} src={icon} />
                            <div className={joinClasses(ModuleResolver.instance.ColorLegendClasses.symbol,
                                                        ModuleResolver.instance.InfomodeItemClasses.color,
                                                        styles.resourceLocatorInfomodeColorSymbol)} style={styleSymbol}></div>
                            {
                                displayOption === DisplayOption.Produces &&
                                (
                                    <div className={styles.resourceLocatorInfomodeBars}>
                                        <div className={styles.resourceLocatorInfomodeBarProduces}>
                                            <div className={styles.resourceLocatorInfomodeBarPercentStorage}>
                                                <div className={styles.resourceLocatorInfomodeBarCover} style={stylePercentStorage}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeBarProduces}>
                                            <div className={styles.resourceLocatorInfomodeBarPercentProduction}>
                                                <div className={styles.resourceLocatorInfomodeBarCover} style={stylePercentProduction}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeBarProduces}>
                                            <div className={styles.resourceLocatorInfomodeBarPercentSurplus}>
                                                <div className={styles.resourceLocatorInfomodeBarCover} style={stylePercentSurplus}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeBarProduces}>
                                            <div className={styles.resourceLocatorInfomodeBarPercentDeficit}>
                                                <div className={styles.resourceLocatorInfomodeBarCover} style={stylePercentDeficit}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeResourceLabel}>
                                            {translationInfomodeTitle}
                                        </div>
                                    </div>
                                )
                            }
                            {
                                displayOption !== DisplayOption.Produces &&
                                (
                                    <div className={styles.resourceLocatorInfomodeBars}>
                                        <div className={styles.resourceLocatorInfomodeBarStorage}>
                                            <div className={styles.resourceLocatorInfomodeBarPercentStorage}>
                                                <div className={styles.resourceLocatorInfomodeBarCover} style={stylePercentStorage}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeResourceLabel}>
                                            {translationInfomodeTitle}
                                        </div>
                                    </div>
                                )
                            }
                            <div className={joinClasses(ModuleResolver.instance.InfomodeItemClasses.type,
                                                        styles.resourceLocatorInfomodeBuildingColor)}>
                                {translationBuildingColor}
                                <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.toggle,
                                    ModuleResolver.instance.InfomodeItemClasses.checkbox,
                                    (infomode.active ? "checked" : "unchecked"))}>
                                    <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.checkmark,
                                        (infomode.active ? "checked" : ""))}></div>
                                </div>
                            </div>
                        </div>
                    </div>
                </button>
            }
        />
    );
}
