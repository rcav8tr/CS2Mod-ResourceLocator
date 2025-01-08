import { bindValue, useValue    } from "cs2/api";
import { useLocalization        } from "cs2/l10n";

import { DisplayOptionCheckBox  } from "displayOptionCheckBox";
import   styles                   from "displayOptionCheckBoxes.module.scss";
import   mod                      from "../mod.json";
import { ModuleResolver         } from "moduleResolver";
import { uiBindingNames         } from "uiBindings";
import { DisplayOption          } from "uiConstants";
import { UITranslationKey       } from "uiTranslationKey";

// Define display option binding.
const bindingDisplayOption = bindValue<number>(mod.id, uiBindingNames.DisplayOption, DisplayOption.Requires);

// Custom infomode item for display option check boxes.
export const DisplayOptionCheckBoxes = () =>
{
    // Translations.
    const { translate } = useLocalization();
    const labelRequires         = translate(UITranslationKey.DisplayOptionRequires);
    const labelProduces         = translate(UITranslationKey.DisplayOptionProduces);
    const labelSells            = translate(UITranslationKey.DisplayOptionSells   );
    const labelStores           = translate(UITranslationKey.DisplayOptionStores  );
    const tooltipDisplayOption  = translate(UITranslationKey.DisplayOptionTooltip );

    // Get display option from data binding.
    const valueDisplayOption: DisplayOption = useValue(bindingDisplayOption);

    // A row with four check boxes.
    // Only one will be checked based on the display option.
    return (
        <ModuleResolver.instance.Tooltip
            direction="right"
            tooltip={<ModuleResolver.instance.FormattedParagraphs children={tooltipDisplayOption} />}
            theme={ModuleResolver.instance.TooltipClasses}
            children=
            {
                <div className={styles.resourceLocatorDisplayOptionCheckBoxes}>
                    <DisplayOptionCheckBox displayOption={DisplayOption.Requires} isChecked={valueDisplayOption === DisplayOption.Requires} label={labelRequires} />
                    <DisplayOptionCheckBox displayOption={DisplayOption.Produces} isChecked={valueDisplayOption === DisplayOption.Produces} label={labelProduces} />
                    <DisplayOptionCheckBox displayOption={DisplayOption.Sells   } isChecked={valueDisplayOption === DisplayOption.Sells   } label={labelSells   } />
                    <DisplayOptionCheckBox displayOption={DisplayOption.Stores  } isChecked={valueDisplayOption === DisplayOption.Stores  } label={labelStores  } />
                </div>
            }
        />
    );
}
