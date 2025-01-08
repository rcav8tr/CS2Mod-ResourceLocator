import { trigger        } from "cs2/api";

import   styles           from "displayOptionCheckBox.module.scss";
import   mod              from "../mod.json";
import { ModuleResolver } from "moduleResolver";
import { uiEventNames   } from "uiBindings";
import { DisplayOption  } from "uiConstants";

// Props for DisplayOptionCheckBox.
export interface DisplayOptionCheckBoxProps
{
    displayOption: DisplayOption;
    isChecked: boolean;
    label: string | null;
}

// A display option check box.
export const DisplayOptionCheckBox = ({ displayOption, isChecked, label }: DisplayOptionCheckBoxProps) =>
{
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
