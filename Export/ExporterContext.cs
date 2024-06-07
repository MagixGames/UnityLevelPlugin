using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public class ExporterContext
    {
        public Stack<ULTransform> currentOffset;

        public ExporterContext() 
        {
            currentOffset = new Stack<ULTransform>();
        }
    }
}
