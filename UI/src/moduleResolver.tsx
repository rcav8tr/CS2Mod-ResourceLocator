import { BalloonDirection, Color, FocusKey, Theme, UniqueFocusKey   } from "cs2/bindings";
import { InputAction                                                } from "cs2/input";
import { getModule                                                  } from "cs2/modding";
import { FormattedParagraphsProps, TooltipProps                     } from "cs2/ui";


type ColorFieldProps =
{
    focusKey?:          FocusKey;
    disabled?:          boolean;
    value?:             Color;
    className?:         string;
    selectAction?:      InputAction;
    alpha?:             boolean;
    popupDirection?:    BalloonDirection;
    hideHint?:          boolean,
    onChange?:          (e: Color) => void;
    onClick?:           (e: any) => void;
    onMouseEnter?:      (e: any) => void;
    onMouseLeave?:      (e: any) => void;
    onClosePicker?:     (e: any) => void;
}

// Provide access to modules from index.js.
export class ModuleResolver
{
    // Define instance.
    private static _instance: ModuleResolver = new ModuleResolver();
    public static get instance(): ModuleResolver { return this._instance }

    // Define component modules.
    private _focusDisabled: any;
    private _tooltip: any;
    private _formattedParagraphs: any;
    private _colorfield: any;

    // Define code modules.
    private _uiSound: any;
    private _loc: any;

    // Define SCSS modules.
    private _tooltipClasses: any;
    private _infomodeItemClasses: any;
    private _transparentButtonClasses: any;
    private _checkboxClasses: any;
    private _dropdownClasses: any;
    private _colorLegendClasses: any;

    // Provide access to component modules.
    public get FOCUS_DISABLED():        UniqueFocusKey                                     { return this._focusDisabled         ?? (this._focusDisabled         = getModule("game-ui/common/focus/focus-key.ts",                                "FOCUS_DISABLED"        )); }
    public get Tooltip():               (props: TooltipProps)               => JSX.Element { return this._tooltip               ?? (this._tooltip               = getModule("game-ui/common/tooltip/tooltip.tsx",                               "Tooltip"               )); }
    public get FormattedParagraphs():   (props: FormattedParagraphsProps)   => JSX.Element { return this._formattedParagraphs   ?? (this._formattedParagraphs   = getModule("game-ui/common/text/formatted-paragraphs.tsx",                     "FormattedParagraphs"   )); }
    public get ColorField():            (props: ColorFieldProps)            => JSX.Element { return this._colorfield            ?? (this._colorfield            = getModule("game-ui/common/input/color-picker/color-field/color-field.tsx",    "ColorField"            )); }

    // Provide access to code modules.
    public get UISound()                { return this._uiSound  ?? (this._uiSound   = getModule("game-ui/common/data-binding/audio-bindings.ts",    "UISound"   )); }
    public get Loc()                    { return this._loc      ?? (this._loc       = getModule("game-ui/common/localization/loc.generated.ts",     "Loc"       )); }

    // Provide access to SCSS modules.
    public get TooltipClasses():            Theme | any { return this._tooltipClasses           ?? (this._tooltipClasses            = getModule("game-ui/common/tooltip/tooltip.module.scss",                                                                   "classes")); }
    public get InfomodeItemClasses():       Theme | any { return this._infomodeItemClasses      ?? (this._infomodeItemClasses       = getModule("game-ui/game/components/infoviews/active-infoview-panel/components/infomode-item/infomode-item.module.scss",   "classes")); }
    public get TransparentButtonClasses():  Theme | any { return this._transparentButtonClasses ?? (this._transparentButtonClasses  = getModule("game-ui/game/themes/transparent-button.module.scss",                                                           "classes")); }
    public get CheckboxClasses():           Theme | any { return this._checkboxClasses          ?? (this._checkboxClasses           = getModule("game-ui/common/input/toggle/checkbox/checkbox.module.scss",                                                    "classes")); }
    public get DropdownClasses():           Theme | any { return this._dropdownClasses          ?? (this._dropdownClasses           = getModule("game-ui/menu/themes/dropdown.module.scss",                                                                     "classes")); }
    public get ColorLegendClasses():        Theme | any { return this._colorLegendClasses       ?? (this._colorLegendClasses        = getModule("game-ui/common/charts/legends/color-legend.module.scss",                                                       "classes")); }
}