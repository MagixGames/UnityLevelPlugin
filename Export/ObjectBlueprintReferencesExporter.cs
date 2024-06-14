using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using UnityLevelPlugin.Tools;

namespace UnityLevelPlugin.Export
{
    public class ObjectBlueprintReferencesExporter : Exporter
    {
        public ULEObjectBlueprintReferences bps;

        public override void Export(EbxAssetEntry entry)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            bps = new ULEObjectBlueprintReferences();
            foreach (var reference in from item in asset.Objects 
                                      where item is ObjectReferenceObjectData 
                                      select (ObjectReferenceObjectData)item)
            {
                if (reference.Blueprint == null || (App.AssetManager.GetEbxEntry(reference.Blueprint.External.FileGuid) == null))
                {
                    continue;
                }
                ULObjectInstance instance = new ULObjectInstance();
                instance.objectBlueprint = new ULObjectBlueprint();
                instance.transforms = new List<ULTransform>()
                {
                    ULTransform.FromLinearTransform(reference.BlueprintTransform) //+ context.currentOffset.Peek()
                };
                instance.objectVariations = new List<string>
                {
                    (reference.ObjectVariation != 2) ? "Default" : App.AssetManager.GetEbxEntry(reference.ObjectVariation.External.FileGuid).Name
                };
                if (ReadObjectBlueprint(App.AssetManager.GetEbxEntry(reference.Blueprint.External.FileGuid), out var mesh))
                {
                    instance.objectBlueprint.meshPath = mesh.Filename + ".fbx";
                }
                else
                {
                    return;
                }

                bps.instances.Add(instance);
            }
        }
        public bool ReadObjectBlueprint(EbxAssetEntry entry, out EbxAssetEntry mesh)
        {
            List<EbxAssetEntry> meshReferences = ULTools.FindAllReferencesOfType(entry, "MeshAsset");
            meshReferences.ForEach(context.plugin.AddMesh);

            if (meshReferences.Count == 0)
            {
                mesh = null;
                return false;
            }
            mesh = meshReferences.First();

            return true;
        }

        public ObjectBlueprintReferencesExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public ObjectBlueprintReferencesExporter(ExporterContext context) : base(context) { }
    }
}
