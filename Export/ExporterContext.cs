using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public class ExporterContext
    {
        public UnityLevelPlugin plugin;

        public ExporterContext(UnityLevelPlugin pUlPlugin) 
        {
            plugin = pUlPlugin;
        }
    }
}
