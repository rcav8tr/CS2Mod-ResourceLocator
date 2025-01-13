import { CSSProperties                  } from "react";

import { bindValue, useValue, trigger   } from "cs2/api";
import { infoviewTypes                  } from "cs2/bindings";
import { useLocalization                } from "cs2/l10n";

import   styles                           from "infomodeItem.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames                 } from "uiBindings";
import { DisplayOption, RLBuildingType  } from "uiConstants";
import { UITranslationKey               } from "uiTranslationKey";

// Define production info passed from C#.
type ProductionInfo =
{
    buildingType: RLBuildingType;
    production:   number;
    surplus:      number;
    deficit:      number;
}

// Interface for unit settings.
interface UnitSettings
{
    timeFormat:         number;
    temperatureUnit:    number;
    unitSystem:         number;
}

// Props for InfomodeItem.
interface InfomodeItemProps
{
    infomode: infoviewTypes.Infomode;
    buildingType: RLBuildingType;
}

// Define bindings.
const bindingDisplayOption   = bindValue<number          >(mod.id, uiBindingNames.DisplayOption, DisplayOption.Requires);
const bindingProductionInfos = bindValue<ProductionInfo[]>(mod.id, uiBindingNames.ProductionInfos);
const bindingUnitSettings    = bindValue<UnitSettings    >("options", "unitSettings");

// Custom infmode item.
// Adapted from the base game's infomode logic.
export const InfomodeItem = ({ infomode, buildingType }: InfomodeItemProps) =>
{
    // Translations.
    const { translate } = useLocalization();
    const infomodeTitle      = translate("Infoviews.INFOMODE[" + infomode.id + "]");
    var   infomodeTooltip    = translate("Infoviews.INFOMODE_TOOLTIP[" + infomode.id + "]");
    const buildingColor      = translate("Infoviews.INFOMODE_TYPE[BuildingColor]");
    const thousandsSeparator = translate("Common.THOUSANDS_SEPARATOR", ",") + "";
    const productionText     = translate("EconomyPanel.PRODUCTION_PAGE_PRODUCTION");
    const surplusText        = translate("EconomyPanel.PRODUCTION_PAGE_SURPLUS")
    const deficitText        = translate("EconomyPanel.PRODUCTION_PAGE_DEFICIT")

    // Get display option.
    const displayOption = useValue(bindingDisplayOption);

    // Get production info.
    const productionInfos: ProductionInfo[] = useValue(bindingProductionInfos);
    var production:   number = 0;
    var surplus:      number = 0;
    var deficit:      number = 0;
    var maxAllValues: number = 0;
    for (var i: number = 0; i < productionInfos.length; i++)
    {
        // Check for building type of this infomode.
        const productionInfo: ProductionInfo = productionInfos[i];
        if (productionInfo.buildingType === buildingType)
        {
            production = productionInfo.production;
            surplus    = productionInfo.surplus;
            deficit    = productionInfo.deficit;
        }

        // Get max of all values.
        maxAllValues = Math.max(maxAllValues, productionInfo.production, productionInfo.surplus, productionInfo.deficit);
    }

    // Compute percents for production info values.
    // Percent is computed compared to the max of all values.
    // This matches the game's Production tab on the City Economy panel.
    const percentProduction: number = maxAllValues === 0 ? 0 : Math.min(100 * production / maxAllValues, 100);
    const percentSurplus:    number = maxAllValues === 0 ? 0 : Math.min(100 * surplus    / maxAllValues, 100);
    const percentDeficit:    number = maxAllValues === 0 ? 0 : Math.min(100 * deficit    / maxAllValues, 100);

    // Compute styles to display production info progress bars.
    // Colors are same as on the game's Production tab on the City Economy panel.
    const styleProductionColor:   Partial<CSSProperties> = { backgroundColor: "var(--progressColor)", }
    const styleSurplusColor:      Partial<CSSProperties> = { backgroundColor: "var(--positiveColor)", }
    const styleDeficitColor:      Partial<CSSProperties> = { backgroundColor: "var(--negativeColor)", }
    const styleProductionPercent: Partial<CSSProperties> = { marginLeft: percentProduction + "%", }
    const styleSurplusPercent:    Partial<CSSProperties> = { marginLeft: percentSurplus    + "%", }
    const styleDeficitPercent:    Partial<CSSProperties> = { marginLeft: percentDeficit    + "%", }

    // Get maximum of production, surplus, and deficit.
    // This maximum is used so that production and surplus/deficit have the same scaling factor and units of measure.
    const maxValue: number = Math.max(production, surplus, deficit);

    // Get scaling factor and unit of measure prefix and text.
    var scalingFactor: number = 1;
    var uomPrefix: string | null = null;
    var uomText: string | null = null;
    const valueUnitSettings = useValue(bindingUnitSettings);
    const unitSystemMetric: number = 0;
    if (valueUnitSettings.unitSystem === unitSystemMetric)
    {
        // Check scale of max value.
        if (maxValue < 100000)
        {
            // No scaling needed.
            uomText = translate(ModuleResolver.instance.Loc.Common.VALUE_KG_PER_MONTH.displayName);
        }
        else if (maxValue < 100000000)
        {
            // Convert kg to tons.
            scalingFactor = 1000;
            uomText = translate(ModuleResolver.instance.Loc.Common.VALUE_TON_PER_MONTH.displayName);
        }
        else
        {
            // Convert kg to kilo tons.
            scalingFactor = 1000000;
            uomPrefix = translate(UITranslationKey.UnitOfMeasurePrefixKilo);
            uomText = translate(ModuleResolver.instance.Loc.Common.VALUE_TON_PER_MONTH.displayName);
        }
    }
    else
    {
        // Convert kg to pounds.
        const maxValuePounds = Math.round(maxValue * 2.204622622);

        // Check scale of max value.
        if (maxValuePounds < 100000)
        {
            // No scaling needed.
            uomText = translate(ModuleResolver.instance.Loc.Common.VALUE_POUND_PER_MONTH.displayName);
        }
        else if (maxValuePounds < 200000000)
        {
            // Convert pounds to short tons.
            scalingFactor = 2000;
            uomText = translate(ModuleResolver.instance.Loc.Common.VALUE_SHORT_TON_PER_MONTH.displayName);
        }
        else
        {
            // Convert pounds to kilo short tons.
            scalingFactor = 2000000;
            uomPrefix = translate(UITranslationKey.UnitOfMeasurePrefixKilo);
            uomText = translate(ModuleResolver.instance.Loc.Common.VALUE_SHORT_TON_PER_MONTH.displayName);
        }
    }

    // Apply scaling factor.
    const scaledProduction: number = Math.round(production / scalingFactor);
    const scaledSurplus:    number = Math.round(surplus    / scalingFactor);
    const scaledDeficit:    number = Math.round(deficit    / scalingFactor);

    // Remove variable placeholders from unit of measure text.
    uomText = "" + uomText?.replace("{SIGN}{VALUE}", "");

    // Function to format a value and append unit of measure.
    function FormatValue(value: number): string
    {
        // Logic adapted from the game's index.js for localized numbers.
        const regexReplacement = /\B(?=(\d{3})+(?!\d))/g;
        return value.toFixed(0).replace(regexReplacement, thousandsSeparator) +
            " " + (uomPrefix && uomPrefix.length > 0 ? uomPrefix + " " : "") + uomText?.trim()
    }

    // Append production and surplus/deficit to the tooltip.
    // Deficit has a positive value.
    // If both surplus and deficit are zero, the zero value will be shown as surplus.
    infomodeTooltip +=
        "\n\n" +                productionText + ":  " + FormatValue(scaledProduction) +
        "\n\n" + (deficit > 0 ? deficitText    + ":  " + FormatValue(scaledDeficit   ) :
                                surplusText    + ":  " + FormatValue(scaledSurplus   ));

    // Get icon based on building type.
    // This logic assumes all building type enum names are the same as the resource file names.
    const buildingTypeEnumName: string = RLBuildingType[buildingType];
    const icon: string = "Media/Game/Resources/" + buildingTypeEnumName + ".svg";

    // Style to set symbol background color from the color in the infomode.
    const symbolStyle: Partial<CSSProperties> =
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
    return (
        <ModuleResolver.instance.Tooltip
            direction="right"
            tooltip={<ModuleResolver.instance.FormattedParagraphs children={infomodeTooltip} />}
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
                                                        styles.resourceLocatorInfomodeColorSymbol)} style={symbolStyle}></div>
                            {
                                displayOption === DisplayOption.Produces &&
                                (
                                    <div className={styles.resourceLocatorInfomodeProducesBars}>
                                        <div className={styles.resourceLocatorInfomodeProducesBar}>
                                            <div className={styles.resourceLocatorInfomodeProducesBarPercent} style={styleProductionColor}>
                                                <div className={styles.resourceLocatorInfomodeProducesBarCover} style={styleProductionPercent}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeProducesBar}>
                                            <div className={styles.resourceLocatorInfomodeProducesBarPercent} style={styleSurplusColor}>
                                                <div className={styles.resourceLocatorInfomodeProducesBarCover} style={styleSurplusPercent}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeProducesBar}>
                                            <div className={styles.resourceLocatorInfomodeProducesBarPercent} style={styleDeficitColor}>
                                                <div className={styles.resourceLocatorInfomodeProducesBarCover} style={styleDeficitPercent}></div>
                                            </div>
                                        </div>
                                        <div className={styles.resourceLocatorInfomodeProducesResourceLabel}>
                                            {infomodeTitle}
                                        </div>
                                    </div>
                                )
                            }
                            {
                                displayOption !== DisplayOption.Produces &&
                                (
                                    <div className={styles.resourceLocatorInfomodeGeneralResourceLabel}>
                                        {infomodeTitle}
                                    </div>
                                )
                            }
                            <div className={joinClasses(ModuleResolver.instance.InfomodeItemClasses.type,
                                                        styles.resourceLocatorInfomodeBuildingColor)}>
                                {buildingColor}
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
