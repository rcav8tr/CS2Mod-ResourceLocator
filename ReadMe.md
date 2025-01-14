﻿# Resource Locator
New info view to locate buildings where resources are required, produced, sold, and stored.

# Description
With this info view, you can see where your city's resources are located according to how the resources are being used:  required, produced, sold, and stored.
You can select individual districts and the resources to view.

### District Selector
If your city has districts:
- The District Selector is visible.
- You can show resource locations for the Entire City or a single district.
- Districts are listed alphabetically.
- The default is Entire City every time a game is started.

If your city has no districts:
- The District Selector is hidden.
- Resource locations are shown for the Entire City.

### Display Option
- The Requires, Produces, Sells, and Stores options will show buildings that require, produce, sell, or store a selected resource.
- These options match the game's building information.
- Only one Display Option can be selected at a time.

### Resource Selection
- You can select all or deselect all resources.
- You can select and deselect individual resources.
- To show only one resource, deselect all resources and then select only the one resource you want see.
- The Resource Selections are grouped and ordered the same as on the Production tab of the City Economy view.

### Resource Information
Hover the cursor over a resource to get additional information about that resource:
- Where this resource comes from, including other resources required to produce this resource.
- Which other resources are produced from this resource.
- Other uses of this resource (e.g. Electricity, Heating, Upkeep, etc.)
- Whether this resource can be imported/exported or the resource is available only locally.
- The current production of this resource.
- The current surplus or deficit of this resource.
- When the Produces Display Option is selected, the production, surplus, and deficit are shown in 3 bars behind the resource name.
- All of the above is the same information available on the Production tab of the City Economy view.

### Building Colors
- The color of each building indicates that the corresponding selected resource is being used by that building according to the Display Option.
- Most resource colors match the resource icon.

### Buildings With A Single Resource
- If a building requires, produces, sells, or stores only one resource, the building color will be set according to that resource.
- A building can sell only one resource.

### Buildings With Multiple Resources. 
- Many buildings require more than one resource.
- Most buildings produce only one resource (exception is Recycling Center).
- Most buildings store only one resource (exception is cargo transport buildings).
- If a building requires, produces, or stores more than one resource, the building color is set according to the top selected resource.
  For example, Steel requires both Coal and Metals.
  If the Display Option is Requires and both Coal and Metals resources are selected,
  then a building that produces Steel will be colored for Coal because Coal is higher in the list than Metals.
- To view these buildings in their other color(s), the top resource of the building must be deselected.

### Special Cases
Most buildings that require, produce, sell, and store resources are commercial, industrial, and office.
Here are some special cases of other buildings:
- The Recycling Center produces multiple resources including:  Metals, Plastics, Textiles, and Paper.
- Cargo transport buildings can store multiple resources.
  This includes Cargo Train Terminal, Cargo Harbor, Airport with the Cargo Terminal upgrade, and International Airport.
- Coal power plants store Coal.
- Gas power plants store Petrochemicals.
- Some healthcare buildings store Pharmaceuticals.
- Emergency Shelters store Food.

### Excluded Buildings
Hidden, abandoned, condemned, deleted, and destroyed buildings are excluded.

# Compatibility
The mod is translated into all the languages supported by the base game.

There are no known compatibility issues with any other mods.

This mod can be safely disabled or unsubscribed at any time.

# Possible Future Enhancements
Here are some possible future enhancements that were thought about during development but not included initially:
- Allow the player to locate buildings for selected resources by showing an icon above each building.
- By default, the game turns on all resources each time the infoview is displayed.
  Save the selected resources to be used the next time the infoview is displayed.
  Possibly do this as a separate mod for all infoviews, not just for the infoview for this mod.

# Acknowledgements
The following mods were used extensively in the development of this mod:
- Scene Explorer by krzychu124
- Extended Tooltip by Mimonsi
