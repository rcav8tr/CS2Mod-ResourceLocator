using Colossal.UI.Binding;
using System.Collections.Generic;

namespace ResourceLocator
{
    /// <summary>
    /// All resource infos needed by UI.
    /// </summary>
    public class ResourceInfos : List<ResourceInfo>, IJsonWritable
    {
        /// <summary>
        /// Write resource infos to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
			writer.ArrayBegin(this.Count);
			foreach (ResourceInfo resourceInfo in this)
			{
				resourceInfo.Write(writer);
			}
			writer.ArrayEnd();
        }
    }
}
