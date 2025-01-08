import { useLocalization    } from "cs2/l10n";

import   styles               from "heading.module.scss";
import { RLBuildingType     } from "uiConstants";
import { UITranslationKey   } from "uiTranslationKey";

// Props for Heading.
export interface HeadingProps
{
    buildingType: RLBuildingType;
}

// Custom infomode item for headings.
export const Heading = ({ buildingType }: HeadingProps) =>
{
    // Get translation for heading text.
    const { translate } = useLocalization();
    const headingText: string = "" + (
        buildingType === RLBuildingType.HeadingMaterials       ? translate(UITranslationKey.Materials      ) :
        buildingType === RLBuildingType.HeadingMaterialGoods   ? translate(UITranslationKey.MaterialGoods  ) :
        buildingType === RLBuildingType.HeadingImmaterialGoods ? translate(UITranslationKey.ImmaterialGoods) :
        "Unhandled heading type.");

    // A horizontal line with the heading text below.
    return (
        <>
            <hr className={styles.resourceLocatorHeadingLine} />
            <div className={styles.resourceLocatorHeadingText}>{headingText}</div>
        </>
    );
}