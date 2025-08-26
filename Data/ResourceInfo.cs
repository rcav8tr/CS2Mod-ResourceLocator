using Colossal.UI.Binding;

namespace ResourceLocator
{
    /// <summary>
    /// Info for a single resource info needed by the UI.
    /// </summary>
    public class ResourceInfo : IJsonWritable
    {
        // Building type,
        public RLBuildingType BuildingType { get; set; }

        // Storage data.
        public int StorageRequires  { get; set; }
        public int StorageProduces  { get; set; }
        public int StorageSells     { get; set; }
        public int StorageStores    { get; set; }
        public int StorageInTransit { get; set; }

        // Rate data.
        public int RateProduction   { get; set; }
        public int RateSurplus      { get; set; }
        public int RateDeficit      { get; set; }

        public ResourceInfo
        (
            RLBuildingType buildingType,
            int storageRequires,
            int storageProduces,
            int storageSells,
            int storageStores,
            int storageInTransit,
            int rateProduction,
            int rateSurplus,
            int rateDeficit
        )
        {
            BuildingType     = buildingType;
            StorageRequires  = storageRequires;
            StorageProduces  = storageProduces;
            StorageSells     = storageSells;
            StorageStores    = storageStores;
            StorageInTransit = storageInTransit;
            RateProduction   = rateProduction;
            RateSurplus      = rateSurplus;
            RateDeficit      = rateDeficit;
        }

        /// <summary>
        /// Write resource info to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
			writer.TypeBegin(ModAssemblyInfo.Name + ".ResourceInfo");
			writer.PropertyName("buildingType");
			writer.Write((int)BuildingType);
			writer.PropertyName("storageRequires");
			writer.Write(StorageRequires);
			writer.PropertyName("storageProduces");
			writer.Write(StorageProduces);
			writer.PropertyName("storageSells");
			writer.Write(StorageSells);
			writer.PropertyName("storageStores");
			writer.Write(StorageStores);
			writer.PropertyName("storageInTransit");
			writer.Write(StorageInTransit);
			writer.PropertyName("rateProduction");
			writer.Write(RateProduction);
			writer.PropertyName("rateSurplus");
			writer.Write(RateSurplus);
			writer.PropertyName("rateDeficit");
			writer.Write(RateDeficit);
			writer.TypeEnd();
        }
    }
}
