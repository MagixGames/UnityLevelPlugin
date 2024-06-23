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

            bool hasObjectBlueprintReferences = false;
            foreach (object obj in asset.Objects)
            {
                switch (obj)
                {
                    case SubWorldReferenceObjectData subWorldReference:
                        {
                            if (App.AssetManager.GetEbxEntry(subWorldReference.BundleName) == null)
                            {
                                continue;
                            }
                            SublevelDataExporter subWorldExporter = new SublevelDataExporter(context);
                            subWorldExporter.Export(App.AssetManager.GetEbxEntry(subWorldReference.BundleName));
                            subWorldExporter.spatial.transform = ULTransform.FromLinearTransform(subWorldReference.BlueprintTransform);
                            spatial.children.Add(subWorldExporter.spatial);
                            break;
                        }
                    case LayerReferenceObjectData layerDataReference:
                        {
                            if (layerDataReference.Blueprint == null || (App.AssetManager.GetEbxEntry(layerDataReference.Blueprint.External.FileGuid) == null))
                            {
                                continue;
                            }
                            LayerDataExporter layerDataExporter = new LayerDataExporter(context);
                            layerDataExporter.Export(App.AssetManager.GetEbxEntry(layerDataReference.Blueprint.External.FileGuid));
                            layerDataExporter.spatial.transform = ULTransform.FromLinearTransform(layerDataReference.BlueprintTransform);
                            spatial.children.Add(layerDataExporter.spatial);
                            break;
                        }
                    case SpatialPrefabReferenceObjectData prefabReference:
                        {
                            if (prefabReference.Blueprint == null || (App.AssetManager.GetEbxEntry(prefabReference.Blueprint.External.FileGuid) == null))
                            {
                                continue;
                            }
                            SpatialPrefabExporter spatialPrefabExporter = new SpatialPrefabExporter(context);
                            spatialPrefabExporter.Export(App.AssetManager.GetEbxEntry(prefabReference.Blueprint.External.FileGuid));
                            spatialPrefabExporter.spatial.transform = ULTransform.FromLinearTransform(prefabReference.BlueprintTransform);
                            if (!(spatialPrefabExporter.spatial.children.Count == 0 &&
                                spatialPrefabExporter.spatial.staticModelGroup.members.Count == 0 &&
                                spatialPrefabExporter.spatial.objectBlueprintReferences.instances.Count == 0))
                            {
                                spatial.children.Add(spatialPrefabExporter.spatial);
                            }
                            break;
                        }
                    case ObjectReferenceObjectData _:
                        {
                            hasObjectBlueprintReferences = true;
                            break;
                        }
                }
            }


            #region -- StaticModelGroup --
            StaticModelGroupExporter staticModelGroupExporter = new StaticModelGroupExporter(context);
            staticModelGroupExporter.Export(entry);
            spatial.staticModelGroup = staticModelGroupExporter.group;
            #endregion
            #region -- ObjectReferences --
            if (hasObjectBlueprintReferences)
            {
                ObjectBlueprintReferencesExporter objectBlueprintReferencesExporter = new ObjectBlueprintReferencesExporter(context);
                objectBlueprintReferencesExporter.Export(entry);
                spatial.objectBlueprintReferences = objectBlueprintReferencesExporter.bps;
            }
            #endregion

        }
    }
}
