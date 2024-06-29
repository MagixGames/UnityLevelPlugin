using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Import;

public class ImporterContext
{
    public UnityLevelPlugin plugin;

    public ImporterContext(UnityLevelPlugin pUlPlugin)
    {
        plugin = pUlPlugin;
    }
}
