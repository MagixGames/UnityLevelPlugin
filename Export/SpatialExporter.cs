using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Export
{
    public abstract class SpatialExporter : Exporter
    {
        public ULEBSpatial spatial;

        public SpatialExporter(UnityLevelPlugin plugin) : base(plugin) { }
        public SpatialExporter(ExporterContext context) : base(context) { }

        // Base export
        // Contains code for base "spatial" based things first, as everything is parent->children
        public override void Export(EbxAssetEntry entry)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            spatial = new ULEBSpatial();
            spatial.name = entry.Filename;

            #region -- SubWorldReferences -- 
            foreach (var subWorldReference in
                asset.Objects.Where((i) => i is SubWorldReferenceObjectData).ToList().ConvertAll((i) => (SubWorldReferenceObjectData)i))
            {
                if (App.AssetManager.GetEbxEntry(subWorldReference.BundleName) == null)
                {
                    continue;
                }
                SublevelDataExporter subWorldExporter = new SublevelDataExporter(context);
                context.PushTransform(ULTransform.FromLinearTransform(subWorldReference.BlueprintTransform));
                subWorldExporter.Export(App.AssetManager.GetEbxEntry(subWorldReference.BundleName));
                subWorldExporter.spatial.transform = ULTransform.FromLinearTransform(subWorldReference.BlueprintTransform);
                spatial.children.Add(subWorldExporter.spatial);
                context.currentOffset.Pop();
            }
            #endregion
            #region -- LayerDataReferences -- 
            foreach (var layerDataReference in
                asset.Objects.Where((i) => i is LayerReferenceObjectData).ToList().ConvertAll((i) => (LayerReferenceObjectData)i))
            {
                if (layerDataReference.Blueprint == null || (App.AssetManager.GetEbxEntry(layerDataReference.Blueprint.External.FileGuid) == null))
                {
                    continue;
                }
                LayerDataExporter layerDataExporter = new LayerDataExporter(context);
                context.PushTransform(ULTransform.FromLinearTransform(layerDataReference.BlueprintTransform));
                layerDataExporter.Export(App.AssetManager.GetEbxEntry(layerDataReference.Blueprint.External.FileGuid));
                layerDataExporter.spatial.transform = ULTransform.FromLinearTransform(layerDataReference.BlueprintTransform);
                spatial.children.Add(layerDataExporter.spatial);
                context.currentOffset.Pop();
            }
            #endregion
            #region -- SpatialPrefabReferences -- 
            foreach (var prefabReference in
                asset.Objects.Where((i) => i is SpatialPrefabReferenceObjectData).ToList().ConvertAll((i) => (SpatialPrefabReferenceObjectData)i))
            {
                if (prefabReference.Blueprint == null || (App.AssetManager.GetEbxEntry(prefabReference.Blueprint.External.FileGuid) == null))
                {
                    continue;
                }
                SpatialPrefabExporter spatialPrefabExporter = new SpatialPrefabExporter(context);
                context.PushTransform(ULTransform.FromLinearTransform(prefabReference.BlueprintTransform));
                spatialPrefabExporter.Export(App.AssetManager.GetEbxEntry(prefabReference.Blueprint.External.FileGuid));
                spatialPrefabExporter.spatial.transform = ULTransform.FromLinearTransform(prefabReference.BlueprintTransform);
                spatial.children.Add(spatialPrefabExporter.spatial);
                context.currentOffset.Pop();
            }
            #endregion

            #region -- StaticModelGroup --
            StaticModelGroupExporter staticModelGroupExporter = new StaticModelGroupExporter(context);
            staticModelGroupExporter.Export(entry);
            spatial.staticModelGroup = staticModelGroupExporter.group;
            #endregion
            #region -- ObjectReferences --
            ObjectBlueprintReferencesExporter objectBlueprintReferencesExporter = new ObjectBlueprintReferencesExporter(context);
            objectBlueprintReferencesExporter.Export(entry);
            spatial.objectBlueprintReferences = objectBlueprintReferencesExporter.bps;
            #endregion
        }
    }
}
