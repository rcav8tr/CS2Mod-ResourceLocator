# Resource Locator
New info view to locate buildings where resources are required, produced, sold, and stored.
Locate cargo vehicles transporting resources.

# Description
With this info view, you can:
- View where your city's resources are located according to how the resources are being used:  required, produced, sold, and stored.
- View detailed information for each resource, including how it is obtained and how it is used.
- View storage amounts for buildings that require, produce, sell, and store each resource.
- View production/surplus/deficit rates for buildings that produce or consume each resource.
- View individual districts.
- View individual resources.
- View which cargo vehicles are transporting resources.
- View amount of cargo in transit.

### District Selector
If your city has districts:
- The **District Selector** is visible.
- If **Entire City** is selected, resource locations (buildings and vehicles) are for the Entire City.
- If a single district is selected, resource locations (buildings and vehicles) are for only the selected district.
  The vehicle owner's property determines the vehicle's district.
- Districts are listed alphabetically.
- The default is **Entire City** every time a game is started.

If your city has no districts:
- The **District Selector** is hidden.
- Resource locations (buildings and vehicles) are for the Entire City.

### Display Option
- The Requires, Produces, Sells, and Stores options show buildings that require, produce, sell, or store a **selected resource**.
- These options match the game's building information when you click on a building.
- The color of each building indicates that the corresponding selected resource is being used by that building according to the selected **Display Option**.
- Only one **Display Option** can be selected at a time.
- **Display Option** does not affect **Cargo Vehicles** (see below).

### Color Option
- Use multiple colors or one color for the resources.
- Use **Multiple** when you have many or all resources selected and you want to differentiate between them.
  Most resource colors match the resource icon.
- Use **One** when you have one or a few resources selected and you want to more easily identify their locations.
  You can choose the one color to use.

### Resource Selection
- You can select all or deselect all resources.
- You can select and deselect individual resources.
- To view only one resource, deselect all resources and then select only the one resource you want view.
- The **Building Color** check box also controls whether or not vehicle color is set.
- The **Resource Selections** are grouped and ordered the same as on the Production tab of the City Economy view.

### Detailed Resource Information
Hover the cursor over a resource to get detailed information about that resource:
- Where this resource comes from, including other resources required to produce this resource.
- Which other resources are produced from this resource.
- Other uses of this resource (e.g. Electricity, Heating, Upkeep, etc.)
- Whether this resource can be imported/exported or the resource is available only locally.
- Current amount stored in buildings that require, produce, sell, or store this resource.
  Storage is for the **selected district**.
  If the **Options** settings are turned on, resources produced in special case buildings are included in: Storage - Produces.
  If the **Options** settings are turned on, resources stored in special case buildings are included in: Storage - Stores.
- For each **Display Option**, current amount stored is also shown in an orange bar behind the resource name.
  The resource with the largest amount stored has a full bar and the other bars are sized relative to that one.
- Current production rate and surplus/deficit rate of this resource.
  Rates are for the Entire City, even if a district is selected.
  The simulation must run for a bit for the rates to be updated by the game.
- For the Produces **Display Option**, the production, surplus, and deficit rates are also shown in the blue, green, and red bars behind the resource name.
  The resource with the largest production, surplus, or deficit has a full bar and the other bars are sized relative to that one.
- Most of the above information is also available on the Production tab of the City Economy view.
  Note that the data on the Production tab is updated only once every 45 game minutes and is smoothed over multiple updates.
  The Resource Locator data is updated much more often.
  So the data might not match exactly between Resource Locator and the Production tab.
- Current amount in transit on **Cargo Vehicles** (see below).
  **In Transit** amount excludes resources being transported between two outside connections.
- Number of companies in the **selected district** that require, produce, sell, or store the resource according to the selected **Display Option**.
  Number of companies excludes **Special Case Buildings** (see below) because they do not have companies.

### Buildings With A Single Resource
- If a building requires, produces, sells, or stores only one resource, the building color is set according to that resource.
- A building can sell only one resource.

### Buildings With Multiple Resources. 
- Many buildings require more than one resource.
  Most buildings produce only one resource (exception is Recycling Center).
  If a building requires or produces more than one resource, the building color is set according to the top **selected resource**.
  For example, Steel requires both Coal and Metals.
  If the **Display Option** is Requires and both Coal and Metals resources are selected,
  then for a building that produces Steel, the color is set for Coal because Coal is higher in the list than Metals.
- Most buildings store only one resource (exception is cargo transport buildings).
  If a building stores more than one resource, the color is set according to the top **selected resource** currently stored in the building.
- Even though different port storage buildings can hold different resources, the color for all is set the same as the port gate.
- To view these buildings in their other color(s), deselect the top resource of the building.

### Special Case Buildings
Most buildings that require, produce, sell, and store resources are commercial, industrial, and office companies.
Some resources are produced in buildings other than companies.
Some resources are stored in buildings other than industrial warehouses.

The special case buildings are:
- Recycling Center - Produces from garbage: Metals, Plastics, Textiles, Paper
- Coal Power Plant - Stores for its own use: Coal
- Gas Power Plant - Stores for its own use: Petrochemicals
- Medical Facility - Stores for its own use: Pharmaceuticals
- Emergency Shelter - Stores for its own use: Food
- Cargo Transport Station - Stores multiple resources that can be used by the city and imported/exported.
  Buildings include:  Cargo Train Terminal, Cargo Harbor, Airport with the Cargo Terminal upgrade, International Airport, Port, and Storage Yards.

See **Options** for whether or not special case buildings are included.

### Excluded Buildings
Abandoned, condemned, deleted, and destroyed buildings are excluded.

### Cargo Vehicles
- Vehicle color is set only for cargo vehicles, not for any other vehicles.
- Vehicle color is set according to the **selected resource** being transported by the vehicle.
- Vehicle color is not set if the vehicle is empty.
- Only cargo ships and cargo planes can transport more than one resource at a time.
  All other cargo vehicles can transport only one resource at a time.
- If a vehicle is transporting only one resource, vehicle color is set according to that resource.
- If a vehicle is transporting more than one resource, vehicle color is set according to the top **selected resource** that the vehicle is transporting.
- The selected **Display Option** does not affect how vehicle color is set.
- A vehicle is included only if the vehicle's district is the **selected district**.
  The vehicle's district is determined by the vehicle owner's property.
- Cargo vehicles traveling between two outside connections are excluded.
- The Reach Stacker, Fishing Boat, and Oil Tanker do not actually carry any resource.
  Therefore, the vehicle color is not set.

# Options
The following settings are available on the Options screen.

### Include Special Case Buildings
You can choose to include or exclude (default) special case buildings.
The special case buildings are listed above.

When a special case building is included:
- Building color is set according to the selected **Display Option** and selected resources.
- Resources produced at the building are included in **Storage - Produces**.
- Resources stored at the building are included in **Storage - Stores**.
- Vehicle color is set according to the **selected resources**.
- Vehicles owned by the building are included in **In Transit** amount.

# Compatibility
The mod is translated into all the languages supported by the base game.

There are no known compatibility issues with any other mods.

This mod can be safely disabled or unsubscribed at any time.

To receive an invitation to the Discord server for mod discussion, go to:  [https://discord.gg/HTav7ARPs2](https://discord.gg/HTav7ARPs2)

# Possible Future Enhancements
Here are some possible future enhancements that were thought about during development but not included initially:
- Allow the player to locate buildings for **selected resources** by showing an icon above each building.
- By default, the game turns on all resources each time the infoview is displayed.
  Save the **selected resources** to be used the next time the infoview is displayed.
  Possibly do this as a separate mod for all infoviews, not just for the infoview for this mod.

# Acknowledgements
The following mods were used extensively in the development of this mod:
- Scene Explorer by krzychu124
- Extended Tooltip by Mimonsi
