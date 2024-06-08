using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public class LevelDataExporter : SpatialExporter
    {
        public LevelDataExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public LevelDataExporter(ExporterContext context) : base(context) { }
    }
}
