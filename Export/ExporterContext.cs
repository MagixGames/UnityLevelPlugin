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
        public Stack<ULTransform> currentOffset;
        public UnityLevelPlugin plugin;

        public ExporterContext(UnityLevelPlugin pUlPlugin) 
        {
            currentOffset = new Stack<ULTransform>();
            currentOffset.Push(new ULTransform());
            plugin = pUlPlugin;
        }

        public void PushTransform(ULTransform transform)
        {
            currentOffset.Push(new ULTransform());
            //if (currentOffset.Count == 0)
            //{
            //    currentOffset.Push(transform);
            //    return;
            //}
            //currentOffset.Push(currentOffset.Peek() + transform);
        }
        public void PopTransform() => currentOffset.Pop();
    }
}
