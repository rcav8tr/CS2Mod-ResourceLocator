import { bindValue, useValue, trigger   } from "cs2/api";
import { useLocalization                } from "cs2/l10n";

import   styles                           from "dataOptionCheckBox.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames, uiEventNames   } from "uiBindings";
import { DataOption                     } from "uiConstants";
import { UITranslationKey               } from "uiTranslationKey";

// Props for DataOptionCheckBox.
export interface DataOptionCheckBoxProps
{
    dataOption: DataOption;
}

// Define binding for data option.
const bindingDataOption = bindValue<number>(mod.id, uiBindingNames.DataOption, DataOption.Latest);

// A data option check box.
export const DataOptionCheckBox = ({ dataOption }: DataOptionCheckBoxProps) =>
{
    // Check if this data option is checked.
    const valueDataOption: DataOption = useValue(bindingDataOption);
    const isChecked: boolean = dataOption === valueDataOption;

    // Get translated label for this data option.
    let translationKey: string;
    switch (dataOption)
    {
        case DataOption.Latest:  translationKey = UITranslationKey.DataOptionLatest;  break;
        case DataOption.Average: translationKey = UITranslationKey.DataOptionAverage; break;
    }
    const { translate } = useLocalization();
    const label: string = translate(translationKey) || translationKey;

    // Handle check box click.
    function onCheckBoxClick()
    {
        trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
        trigger(mod.id, uiEventNames.DataOptionClicked, dataOption);
    }

    // A check box with a label.
    // A click anywhere on the enclosing container is considered a click on the check box.
    return (
        <div className={styles.resourceLocatorDataOptionCheckBoxContainer} onClick={() => onCheckBoxClick()}>
            <div className={`${ModuleResolver.instance.CheckboxClasses.toggle}
                             ${ModuleResolver.instance.InfomodeItemClasses.checkbox}
                             ${styles.resourceLocatorDataOptionCheckBox}
                             ${(isChecked ? "checked" : "unchecked")}`}>
                <div className={`${ModuleResolver.instance.CheckboxClasses.checkmark}
                                 ${(isChecked ? "checked" : "")}`}></div>
            </div>
            <div className={styles.resourceLocatorDataOptionCheckBoxLabel}>
                {label}
            </div>
        </div>
    );
}
