using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public class SpatialPrefabExporter : SpatialExporter
    {
        public SpatialPrefabExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public SpatialPrefabExporter(ExporterContext context) : base(context) { }
    }
}
