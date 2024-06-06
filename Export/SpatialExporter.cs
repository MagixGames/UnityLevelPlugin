using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public abstract class SpatialExporter : Exporter
    {
        public ULEBSpatial spatial;

        // Base export
        // Contains code for base "spatial" based things first, as everything is parent->children
        public override void Export(EbxAssetEntry entry)
        {

        }
    }
}
