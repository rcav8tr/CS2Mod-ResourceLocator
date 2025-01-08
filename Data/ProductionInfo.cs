using Colossal.UI.Binding;

namespace ResourceLocator
{
    /// <summary>
    /// Info for a single production info needed by the UI.
    /// </summary>
    public class ProductionInfo : IJsonWritable
    {
        // Building type and production data.
        public RLBuildingType buildingType { get; set; }
        public int production { get; set; }
        public int surplus {  get; set; }
        public int deficit {  get; set; }

        public ProductionInfo(RLBuildingType buildingType, int production, int surplus, int deficit)
        {
            this.buildingType = buildingType;
            this.production   = production;
            this.surplus      = surplus;
            this.deficit      = deficit;
        }

        /// <summary>
        /// Write production info to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
			writer.TypeBegin(ModAssemblyInfo.Name + ".ProductionInfo");
			writer.PropertyName("buildingType");
			writer.Write((int)buildingType);
			writer.PropertyName("production");
			writer.Write(production);
			writer.PropertyName("surplus");
			writer.Write(surplus);
			writer.PropertyName("deficit");
			writer.Write(deficit);
			writer.TypeEnd();
        }
    }
}
