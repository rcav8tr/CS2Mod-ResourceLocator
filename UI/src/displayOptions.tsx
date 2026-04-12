import { bindValue, useValue, trigger } from "cs2/api";
import { useLocalization              } from "cs2/l10n";

import { DisplayOptionCheckBox        } from "displayOptionCheckBox";
import   styles                         from "displayOptions.module.scss";
import   mod                            from "../mod.json";
import { ModuleResolver               } from "moduleResolver";
import { uiBindingNames, uiEventNames } from "uiBindings";
import { DisplayOption                } from "uiConstants";
import { UITranslationKey             } from "uiTranslationKey";

// Define bindings.
const bindingHasUnnecessary = bindValue<boolean>(mod.id, uiBindingNames.HasUnnecessary, false);

// Custom infomode item for display options.
export const DisplayOptions = () =>
{
    // Get translated text.
    const { translate } = useLocalization();
    const tooltipDisplayOption: string = translate(UITranslationKey.DisplayOptionTooltip) || "Choose a display option.";
    const tooltipUnnecessary:   string = translate(UITranslationKey.UnnecessaryTooltip  ) || "Choose a display option.";
    const removeUnnecessary:    string = translate(UITranslationKey.RemoveUnnecessary   ) || "Remove Unnecessary";

    // Get whether or not selected district has unnecessary.
    const hasUnnecessary: boolean = useValue(bindingHasUnnecessary);

    // Handle button click.
    function onButtonClick()
    {
        trigger("audio", "playSound", ModuleResolver.instance.UISound.toggleInfoMode, 1);
        trigger(mod.id, uiEventNames.RemoveUnnecessaryClicked);
    }

    // A row with four check boxes.
    // An optional row with one check box and a button.
    return (
        <>
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
            {
                hasUnnecessary &&
                (
                    <ModuleResolver.instance.Tooltip
                        direction="right"
                        tooltip={<ModuleResolver.instance.FormattedParagraphs children={tooltipUnnecessary} />}
                        theme={ModuleResolver.instance.TooltipClasses}
                        children=
                        {
                            <div className={styles.resourceLocatorDisplayOptions}>
                                <DisplayOptionCheckBox displayOption={DisplayOption.Unnecessary} />
                                <button className={styles.resourceLocatorRemoveUnnecessaryButton} onClick={() => onButtonClick()}>{removeUnnecessary}</button>
                            </div>
                        }
                    />
                )
            }
        </>
    );
}
