import { bindValue, useValue, trigger   } from "cs2/api";
import { Color                          } from "cs2/bindings";
import { useLocalization                } from "cs2/l10n";

import { ColorOptionCheckBox            } from "colorOptionCheckBox";
import   styles                           from "colorOptions.module.scss";
import   mod                              from "../mod.json";
import { ModuleResolver                 } from "moduleResolver";
import { uiBindingNames, uiEventNames   } from "uiBindings";
import { ColorOption                    } from "uiConstants";
import { UITranslationKey               } from "uiTranslationKey";

// Define binding for one color.
const bindingOneColor = bindValue<Color >(mod.id, uiBindingNames.OneColor, {r: 255, g: 0, b: 0, a: 1});

// Custom infomode item for color options.
export const ColorOptions = () =>
{
    // Translations.
    const { translate } = useLocalization();
    const labelColor   = (translate(UITranslationKey.ColorOptionColor  ) || "Color") + ":";
    const labelTooltip = (translate(UITranslationKey.ColorOptionTooltip) || "Choose Multiple or One color.");

    // Get one color from data binding.
    const valueOneColor: Color = useValue(bindingOneColor);

    // Handle one color change.
    function onColorChanaged(newColor: Color)
    {
        trigger(mod.id, uiEventNames.OneColorChanged, newColor);
    }

    // A row with a label, two check boxes, and a color field.
    return (
        <ModuleResolver.instance.Tooltip
            direction="right"
            tooltip={<ModuleResolver.instance.FormattedParagraphs children={labelTooltip} />}
            theme={ModuleResolver.instance.TooltipClasses}
            children=
            {
                <div className={styles.resourceLocatorColorOptions}>
                    <div className={styles.resourceLocationColorLabel}>{labelColor}</div>
                    <ColorOptionCheckBox colorOption={ColorOption.Multiple} />
                    <ColorOptionCheckBox colorOption={ColorOption.One     } />
                    <ModuleResolver.instance.ColorField
                        focusKey={ModuleResolver.instance.FOCUS_DISABLED}
                        value={valueOneColor}
                        onChange={(newColor: Color) => onColorChanaged(newColor)}
                        alpha={false}
                    />
                </div>
            }
        />
    );
}
