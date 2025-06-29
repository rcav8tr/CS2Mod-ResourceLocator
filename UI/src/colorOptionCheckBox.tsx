import { bindValue, useValue, trigger   } from "cs2/api";
import { useLocalization                } from "cs2/l10n";

import   styles                           from "colorOptionCheckBox.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames, uiEventNames   } from "uiBindings";
import { ColorOption                    } from "uiConstants";
import { UITranslationKey               } from "uiTranslationKey";

// Define binding for color option.
const bindingColorOption = bindValue<number>(mod.id, uiBindingNames.ColorOption, ColorOption.Multiple);

// Props for ColorOptionCheckBox.
export interface ColorOptionCheckBoxProps
{
    colorOption: ColorOption;
}

// A color option check box.
export const ColorOptionCheckBox = ({ colorOption }: ColorOptionCheckBoxProps) =>
{
    // Check if this color option is checked.
    const valueColorOption: ColorOption = useValue(bindingColorOption);
    const isChecked: boolean = colorOption === valueColorOption;

    // Get translated label for this color option.
    const translationKey: string = (colorOption === ColorOption.Multiple ? UITranslationKey.ColorOptionMultiple : UITranslationKey.ColorOptionOne);
    const { translate } = useLocalization();
    const label: string = translate(translationKey) + (colorOption === ColorOption.One ? ":" : "");

    // Handle check box click.
    function onCheckBoxClick()
    {
        trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
        trigger(mod.id, uiEventNames.ColorOptionClicked, colorOption);
    }

    // Function to join classes.
    function joinClasses(...classes: any) { return classes.join(" "); }

    // A check box with a label.
    // A click anywhere on the enclosing container is considered a click on the check box.
    return (
        <div className={styles.resourceLocatorColorOptionCheckBoxContainer} onClick={() => onCheckBoxClick()}>
            <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.toggle,
                                        ModuleResolver.instance.InfomodeItemClasses.checkbox,
                                        styles.resourceLocatorColorOptionCheckBox,
                                        (isChecked ? "checked" : "unchecked"))}>
                <div className={joinClasses(ModuleResolver.instance.CheckboxClasses.checkmark,
                                            (isChecked ? "checked" : ""))}></div>
            </div>
            <div className={styles.resourceLocatorColorOptionCheckBoxLabel}>
                {label}
            </div>
        </div>
    );
}
