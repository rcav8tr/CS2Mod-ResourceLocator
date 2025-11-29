using Colossal.UI.Binding;

namespace ResourceLocator
{
    /// <summary>
    /// Info for a single resource info needed by the UI.
    /// </summary>
    public class ResourceInfo : IJsonWritable
    {
        // Building type.
        public RLBuildingType BuildingType { get; set; }

        // Storage amount data.
        public int StorageAmountRequires    { get; set; }
        public int StorageAmountProduces    { get; set; }
        public int StorageAmountSells       { get; set; }
        public int StorageAmountStores      { get; set; }
        public int StorageAmountInTransit   { get; set; }

        // Rate data.
        public bool RateValid               { get; set; }
        public int RateProduction           { get; set; }
        public int RateSurplus              { get; set; }

        // Company count data.
        public int CompanyCountRequires     { get; set; }
        public int CompanyCountProduces     { get; set; }
        public int CompanyCountSells        { get; set; }
        public int CompanyCountStores       { get; set; }

        // Miscellaneous.
        public bool HasWeight               { get; set; }

        /// <summary>
        /// Write resource info to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
			writer.TypeBegin(ModAssemblyInfo.Name + ".ResourceInfo");
			writer.PropertyName("buildingType");
			writer.Write((int)BuildingType);
			writer.PropertyName("storageAmountRequires");
			writer.Write(StorageAmountRequires);
			writer.PropertyName("storageAmountProduces");
			writer.Write(StorageAmountProduces);
			writer.PropertyName("storageAmountSells");
			writer.Write(StorageAmountSells);
			writer.PropertyName("storageAmountStores");
			writer.Write(StorageAmountStores);
			writer.PropertyName("storageAmountInTransit");
			writer.Write(StorageAmountInTransit);
			writer.PropertyName("rateValid");
			writer.Write(RateValid);
			writer.PropertyName("rateProduction");
			writer.Write(RateProduction);
			writer.PropertyName("rateSurplus");
			writer.Write(RateSurplus);
			writer.PropertyName("companyCountRequires");
			writer.Write(CompanyCountRequires);
			writer.PropertyName("companyCountProduces");
			writer.Write(CompanyCountProduces);
			writer.PropertyName("companyCountSells");
			writer.Write(CompanyCountSells);
			writer.PropertyName("companyCountStores");
			writer.Write(CompanyCountStores);
			writer.PropertyName("hasWeight");
			writer.Write(HasWeight);
			writer.TypeEnd();
        }
    }
}
