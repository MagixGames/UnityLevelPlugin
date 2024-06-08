using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    // Leave the same; spatial
    public class SublevelDataExporter : SpatialExporter
    {
        public SublevelDataExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public SublevelDataExporter(ExporterContext context) : base(context) { }
    }
}
