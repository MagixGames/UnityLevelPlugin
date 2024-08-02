using Frosty.Core;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TexturePlugin;
using UnityLevelPlugin.Export;
using UnityLevelPlugin.Tools;

namespace UnityLevelPlugin.Import;

public partial class Importer
{
    public ImporterContext context;
    public ULEBSpatial levelSpatial;
    
    public EbxAsset addedLayerAsset;
    public LayerData addedLayer;
    public EbxAssetEntry levelDataAssetEntry;

    internal FrostyTaskWindow task;

    public Importer(UnityLevelPlugin plugin)
    {
        context = new ImporterContext(plugin);
        levelSpatial = plugin.importedSpatial;
    }
    public Importer(ImporterContext context)
    {
        this.context = context;
    }

    public dynamic CreateClass(string className)
    {
        return EbxTools.CreateNewClass(className, addedLayerAsset);
    }

    public dynamic CreateRootClass(string className)
    {
        dynamic obj = CreateClass(className);
        addedLayer.Objects.Add(new PointerRef(obj));
        return obj;
    }

    public void Import()
    {
        levelDataAssetEntry = App.SelectedAsset;

        FrostyTaskWindow.Show("Importing level data . . .", "Preeeeeetty cool stuff's going on", (task) =>
        {
            this.task = task;

            if (!MeshVariationDb.IsLoaded)
            {
                MeshVariationDb.LoadVariations(task);
            }

            // create layer data
            EbxAsset customLayerData = EbxTools.CreateEbxAsset(nameof(LayerData));
            LayerData ld = (LayerData)customLayerData.RootObject;
            ld.Enabled = true;

            addedLayerAsset = customLayerData;
            addedLayer = ld;

            task.Update("Starting to import...");

            // Importers

            ImportUserAdded();
            ImportComponents();
            ImportStaticModelGroups();


            // revert if it exists already
            var ldEntry = App.AssetManager.GetEbxEntry(levelDataAssetEntry.Name + $"/UL_ImportedContent");
            if (ldEntry != null) // already exists
            {
                App.AssetManager.ModifyEbx(ldEntry.Name, customLayerData);
            }
            else
            {
                ldEntry = App.AssetManager.AddEbx(levelDataAssetEntry.Name + $"/UL_ImportedContent", customLayerData);

                var levelDataAsset = App.AssetManager.GetEbx(levelDataAssetEntry);

                LayerReferenceObjectData layerRef = EbxTools.CreateNewClass(nameof(LayerReferenceObjectData), levelDataAsset);
                layerRef.Blueprint = EbxTools.RefToFile(customLayerData);
                layerRef.LightmapResolutionScale = 1;
                layerRef.LightmapScaleWithSize = true;
                layerRef.Flags = 7601219; // another magic number <3
                ((Blueprint)levelDataAsset.RootObject).Objects.Add(new PointerRef(layerRef));
                App.AssetManager.ModifyEbx(levelDataAssetEntry.Name, levelDataAsset);
            }

            App.Logger.Log("Done importing! Have a good day :D ");
        });
    }
}