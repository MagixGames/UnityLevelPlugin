using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using UnityLevelPlugin.Export;
using UnityLevelPlugin.Tools;

namespace UnityLevelPlugin.Import;

internal class StaticModelGroupImporter : Importer
{
    public StaticModelGroupImporter(UnityLevelPlugin plugin) : base(plugin)
    {
    }

    public StaticModelGroupImporter(ImporterContext context) : base(context)
    {
    }

    public override void Import(EbxAssetEntry entry, dynamic data)
    {
        ULStaticModelGroupObjectInstance inst = (ULStaticModelGroupObjectInstance)data;

        EbxAsset asset = App.AssetManager.GetEbx(entry);

        StaticModelGroupEntityData group = (StaticModelGroupEntityData)asset.Objects.FirstOrDefault((o) => o is StaticModelGroupEntityData);

        if (group == null)
        {
            group = new StaticModelGroupEntityData()
            {
                Flags = 25511358,
                ClientRuntimeComponentCount = 2,
                ServerRuntimeComponentCount = 2,
                Enabled = true,
                NetworkIdCount = 1268, // dk what this number correlates to so /shrug
            };
        }
        group.Components.Clear();
        StaticModelGroupPhysicsComponentData physics = EbxTools.CreateNewClass("StaticModelGroupPhysicsComponentData", asset);
        group.Components.Add(new PointerRef(physics));

    }
}
