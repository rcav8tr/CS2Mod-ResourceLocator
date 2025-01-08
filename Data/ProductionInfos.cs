using Colossal.UI.Binding;
using System.Collections.Generic;

namespace ResourceLocator
{
    /// <summary>
    /// All production infos needed by UI.
    /// </summary>
    public class ProductionInfos : List<ProductionInfo>, IJsonWritable
    {
        /// <summary>
        /// Write production infos to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
			writer.ArrayBegin(this.Count);
			foreach (ProductionInfo productionInfo in this)
			{
				productionInfo.Write(writer);
			}
			writer.ArrayEnd();
        }
    }
}
