using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public class LayerDataExporter : SpatialExporter
    {
        public LayerDataExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public LayerDataExporter(ExporterContext context) : base(context) { }
    }
}
