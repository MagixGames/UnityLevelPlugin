using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityLevelPlugin.Export;

namespace UnityLevelPlugin.Import;

public abstract class Importer
{
    public ImporterContext context;
    public Importer(UnityLevelPlugin plugin)
    {
        context = new ImporterContext(plugin);
    }
    public Importer(ImporterContext context)
    {
        this.context = context;
    }

    public abstract void Import(EbxAssetEntry entry, dynamic data);
}