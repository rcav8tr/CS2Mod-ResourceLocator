import { bindValue, useValue, trigger   } from "cs2/api";
import { useLocalization                } from "cs2/l10n";

import   styles                           from "displayOptionCheckBox.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames, uiEventNames   } from "uiBindings";
import { DisplayOption                  } from "uiConstants";
import { UITranslationKey               } from "uiTranslationKey";

// Props for DisplayOptionCheckBox.
export interface DisplayOptionCheckBoxProps
{
    displayOption: DisplayOption;
}

// Define binding for display option.
const bindingDisplayOption = bindValue<number>(mod.id, uiBindingNames.DisplayOption, DisplayOption.Requires);

// A display option check box.
export const DisplayOptionCheckBox = ({ displayOption }: DisplayOptionCheckBoxProps) =>
{
    // Check if this display option is checked.
    const valueDisplayOption: DisplayOption = useValue(bindingDisplayOption);
    const isChecked: boolean = displayOption === valueDisplayOption;

    // Get translated label for this display option.
    let translationKey: string;
    switch (displayOption)
    {
        case DisplayOption.Requires: translationKey = UITranslationKey.DisplayOptionRequires; break;
        case DisplayOption.Produces: translationKey = UITranslationKey.DisplayOptionProduces; break;
        case DisplayOption.Sells:    translationKey = UITranslationKey.DisplayOptionSells;    break;
        case DisplayOption.Stores:   translationKey = UITranslationKey.DisplayOptionStores;   break;
    }
    const { translate } = useLocalization();
    const label: string = translate(translationKey) || translationKey;

    // Handle check box click.
    function onCheckBoxClick()
    {
        trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
        trigger(mod.id, uiEventNames.DisplayOptionClicked, displayOption);
    }

    // Function to join classes.
    function joinClasses(...classes: any) { return classes.join(" "); }

    // A check box with a label.
    // A click anywhere on the enclosing container is considered a click on the check box.
    return (
        <div className={styles.resourceLocatorDisplayOptionCheckBoxContainer} onClick={() => onCheckBoxClick()}>
            <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.toggle,
                                        ModuleResolver.instance.InfomodeItemClasses.checkbox,
                                        styles.resourceLocatorDisplayOptionCheckBox,
                                        (isChecked ? "checked" : "unchecked"))}>
                <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.checkmark,
                                            (isChecked ? "checked" : ""))}></div>
            </div>
            <div className={styles.resourceLocatorDisplayOptionCheckBoxLabel}>
                {label}
            </div>
        </div>
    );
}
