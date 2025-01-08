import { CSSProperties                  } from "react";

import { bindValue, useValue, trigger   } from "cs2/api";
import { infoviewTypes                  } from "cs2/bindings";
import { useLocalization                } from "cs2/l10n";

import   styles                           from "infomodeItem.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames                 } from "uiBindings";
import { RLBuildingType                 } from "uiConstants";
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
const bindingProductionInfos = bindValue<ProductionInfo[]>(mod.id, uiBindingNames.ProductionInfos);
const bindingUnitSettings    = bindValue<UnitSettings>("options", "unitSettings");

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

    // Get production info for this building type.
    const productionInfos: ProductionInfo[] = useValue(bindingProductionInfos);
    var production: number = 0;
    var surplus:    number = 0;
    var deficit:    number = 0;
    for (var i: number = 0; i < productionInfos.length; i++)
    {
        const productionInfo: ProductionInfo = productionInfos[i];
        if (productionInfo.buildingType === buildingType)
        {
            production = productionInfo.production;
            surplus    = productionInfo.surplus;
            deficit    = productionInfo.deficit;
            break;
        }
    }

    // Get maximum of production, surplus, and deficit.
    // This maximum is used so that production and surplus/deficit have the same scaling factor and units of measure.
    const maxValue = Math.max(production, surplus, deficit);

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
    if (scalingFactor !== 1)
    {
        production = Math.round(production / scalingFactor);
        surplus    = Math.round(surplus    / scalingFactor);
        deficit    = Math.round(deficit    / scalingFactor);
    }

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
        "\n\n" +                productionText + ":  " + FormatValue(production) +
        "\n\n" + (deficit > 0 ? deficitText    + ":  " + FormatValue(deficit   ) :
                                surplusText    + ":  " + FormatValue(surplus   ));

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
                            <div className={ModuleResolver.instance.InfomodeItemClasses.titleText}>{infomodeTitle}</div>
                        </div>
                        <div className={ModuleResolver.instance.InfomodeItemClasses.type}>
                            {buildingColor}
                            <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.toggle,
                                                        ModuleResolver.instance.InfomodeItemClasses.checkbox,
                                                        (infomode.active ? "checked" : "unchecked"))}>
                                <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.checkmark,
                                                            (infomode.active ? "checked" : ""))}></div>
                            </div>
                        </div>
                    </div>
                </button>
            }
        />
    );
}
