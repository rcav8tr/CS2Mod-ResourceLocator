import { useLocalization              } from "cs2/l10n";

import { DataOptionCheckBox           } from "dataOptionCheckBox";
import   styles                         from "dataOptions.module.scss";
import { ModuleResolver               } from "moduleResolver";
import { DataOption                   } from "uiConstants";
import { UITranslationKey             } from "uiTranslationKey";

// Custom infomode item for data options.
export const DataOptions = () =>
{
    // Get translated text.
    const { translate } = useLocalization();
    const labelDataOption  : string = (translate(UITranslationKey.DataOptionData) || "Data") + ":";
    const tooltipDataOption: string = translate(UITranslationKey.DataOptionTooltip) || "Choose a data option.";

    // A row with two check boxes.
    return (
        <ModuleResolver.instance.Tooltip
            direction="right"
            tooltip={<ModuleResolver.instance.FormattedParagraphs children={tooltipDataOption} />}
            theme={ModuleResolver.instance.TooltipClasses}
            children=
            {
                <div className={styles.resourceLocatorDataOptions}>
                    <div className={styles.resourceLocationDataLabel}>{labelDataOption}</div>
                    <DataOptionCheckBox dataOption={DataOption.Latest } />
                    <DataOptionCheckBox dataOption={DataOption.Average} />
                </div>
            }
        />
    );
}
