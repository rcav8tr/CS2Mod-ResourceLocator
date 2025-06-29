import { infoviewTypes          } from "cs2/bindings";
import { ModuleRegistryExtend   } from "cs2/modding";

import { ColorOptions           } from "colorOptions";
import { DisplayOptions         } from "displayOptions";
import { DistrictSelector       } from "districtSelector";
import { Heading                } from "heading";
import { InfomodeItem           } from "infomodeItem";
import   mod                      from "../mod.json";
import { SelectDeselect         } from "selectDeselect";
import { RLBuildingType         } from "uiConstants";

// InfomodeItem extension.
export const InfomodeItemExtend: ModuleRegistryExtend = (Component: any) =>
{
    return (props) =>
    {
        // Get building type name from the infomode.
        const infomode: infoviewTypes.Infomode = props.infomode;
        const buildingTypeName: string = infomode.id;

        // Check if building type is for this mod based on the name.
        if (buildingTypeName && buildingTypeName.startsWith(mod.id))
        {
            // Building type is for this mod.

            // Get the building type from the building type name.
            const buildingTypeNameSuffix: string = buildingTypeName.substring(mod.id.length);
            const buildingType = RLBuildingType[buildingTypeNameSuffix as keyof typeof RLBuildingType];

            // Check for special cases.
            if (buildingType === RLBuildingType.District      ) { return (<DistrictSelector />); }
            if (buildingType === RLBuildingType.DisplayOption ) { return (<DisplayOptions   />); }
            if (buildingType === RLBuildingType.ColorOption   ) { return (<ColorOptions     />); }
            if (buildingType === RLBuildingType.SelectDeselect) { return (<SelectDeselect   />); }
            if (buildingType === RLBuildingType.MaxValues     ) { return (<></>               ); }

            // Check for special case for headings.
            if (buildingType === RLBuildingType.HeadingMaterials      ||
                buildingType === RLBuildingType.HeadingMaterialGoods  ||
                buildingType === RLBuildingType.HeadingImmaterialGoods)
            {
                return (<Heading buildingType={buildingType} />);
            }

            // Not a special case.
            // Return this mod's custom infomode item.
            return (<InfomodeItem infomode={infomode} buildingType={buildingType} />);
        }

        // Building type is not for this mod.
        // Return original InfomodeItem unchanged.
        return (<Component {...props} />);
    }
}
