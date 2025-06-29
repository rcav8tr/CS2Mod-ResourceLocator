import { useLocalization        } from "cs2/l10n";

import { DisplayOptionCheckBox  } from "displayOptionCheckBox";
import   styles                   from "displayOptions.module.scss";
import { ModuleResolver         } from "moduleResolver";
import { DisplayOption          } from "uiConstants";
import { UITranslationKey       } from "uiTranslationKey";

// Custom infomode item for display options.
export const DisplayOptions = () =>
{
    // Get translated text for tooltip.
    const { translate } = useLocalization();
    const tooltipDisplayOption: string = translate(UITranslationKey.DisplayOptionTooltip) || "Choose a display option.";

    // A row with four check boxes.
    return (
        <ModuleResolver.instance.Tooltip
            direction="right"
            tooltip={<ModuleResolver.instance.FormattedParagraphs children={tooltipDisplayOption} />}
            theme={ModuleResolver.instance.TooltipClasses}
            children=
            {
                <div className={styles.resourceLocatorDisplayOptions}>
                    <DisplayOptionCheckBox displayOption={DisplayOption.Requires} />
                    <DisplayOptionCheckBox displayOption={DisplayOption.Produces} />
                    <DisplayOptionCheckBox displayOption={DisplayOption.Sells   } />
                    <DisplayOptionCheckBox displayOption={DisplayOption.Stores  } />
                </div>
            }
        />
    );
}
