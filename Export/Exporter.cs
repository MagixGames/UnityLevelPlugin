using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    // Generally will also have a struct data type in ExportData.cs which is whats built on Export()
    public abstract class Exporter
    {
        public ExporterContext context;
        public Exporter(UnityLevelPlugin plugin)
        {
            context = new ExporterContext(plugin);
        }
        public Exporter(ExporterContext context)
        {
            this.context = context;
        }

        public abstract void Export(EbxAssetEntry entry);
    }
}
