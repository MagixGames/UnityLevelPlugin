using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Managers;
using FrostySdk.Resources;
using MeshSetPlugin;
using MeshSetPlugin.Resources;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using TexturePlugin;
using LinearTransform = FrostySdk.Ebx.LinearTransform;
using Matrix = SharpDX.Matrix;
using Vector3 = System.Numerics.Vector3;
using FrostySdk.IO;
using MagixTools;
using System.Threading;
using FrostyCore;
using UnityLevelPlugin.Export;
using UnityLevelPlugin.Tools;

namespace UnityLevelPlugin
{
    public class ExportUnityLevelButton : DataExplorerContextMenuExtension
    {
        public override string ContextItemName => "Export Level (UnityLevelPlugin)";
        //public override ImageSource Icon => null; // new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/HavokFileType.png") as ImageSource;

        public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
        {
            try
            {
                UnityLevelPlugin pl = new UnityLevelPlugin("E:\\Workspace\\Frostbite\\UnityLevelEditor\\Test");
                pl.Export();
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Error importing asset onto '{App.SelectedAsset.Name}'");
#if DEVELOPER___DEBUG
                App.Logger.Log(ex.Message);
                App.Logger.Log(ex.StackTrace);
#endif
            }
        });
    }

    public class UnityLevelPlugin
    {
        internal readonly DirectoryInfo outputFolder;
        internal FBXExporter meshExporter;
        internal TextureExporter textureExporter;
        internal HashSet<string> exportedObjects;
        internal HashSet<EbxAssetEntry> meshesToExport;
        internal EbxAssetEntry levelDataAssetEntry;

        public Dictionary<uint, string> ObjectVariablesTable;
        private FrostyTaskWindow _task;

        private UnityXmlWriter xmlWriter;
        private LevelDataExporter levelDataExporter;
        private List<ULMeshData> meshes;

        public UnityLevelPlugin(string outputFolder)  
        { 
            this.outputFolder = new DirectoryInfo(outputFolder);
        }

        public void Export()
        {
            textureExporter = new TextureExporter();
            meshes = new List<ULMeshData>();
            meshesToExport = new HashSet<EbxAssetEntry>();
            exportedObjects = new HashSet<string>();
            levelDataAssetEntry = App.SelectedAsset;
            FrostyTaskWindow.Show("Shits happening", "cool stuff", (task) => 
            {
                try
                {
                    _task = task;
                    meshExporter = new FBXExporter(task);
                    ObjectVariablesTable = App.AssetManager.EnumerateEbx("ObjectVariation").ToDictionary((entry) => (uint)Utils.HashString(entry.Name, true), (entry) => entry.Name);

                    //LevelData level = (LevelData)App.AssetManager.GetEbx(levelDataAssetEntry).RootObject;
                    //EbxAsset asset = App.AssetManager.GetEbx(levelDataAssetEntry);
                    levelDataExporter = new LevelDataExporter(this);
                    levelDataExporter.Export(levelDataAssetEntry);

                    MeshVariationDb.LoadVariations(task);
                    foreach (EbxAssetEntry mesh in meshesToExport)
                    {
                        ExportMesh(mesh);
                    }
                    ExportLevelData();
                }
                catch (Exception ex)
                {
                    App.Logger.Log(ex.Message);
                    App.Logger.Log(ex.StackTrace);
                    //App.Logger.Log(Debug.last.ToString());
                }
            });
        }

        public void ExportLevelData()
        {
            string outputFile = ToFile("leveldata.fbul");
            using (FileStream stream = new FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.Write))
            {
                xmlWriter = new UnityXmlWriter(stream);
                xmlWriter.WriteStartElement("LevelData");

                xmlWriter.WriteElement("LevelDataPath", new FileInfo(outputFile).Directory.FullName.Replace('\\', '/')); // <LevelDataPath>
                xmlWriter.WriteList("MeshData", meshes, (m, n) => m.Write(xmlWriter, n));
                levelDataExporter.spatial.Write(xmlWriter, "rootLevel");

                xmlWriter.WriteEndElement();
                xmlWriter.End();
            }
        }


        public string ToFile(string path)
        {
            return ULTools.ToFile(outputFolder, path);
        }

        public void AddMesh(EbxAssetEntry asset)
        {
            if (exportedObjects.Add(asset.Name))
            {
                meshesToExport.Add(asset);
            }
        }
        public void ExportTexture(EbxAssetEntry asset) // (res)Texture & (ebx)TextureAsset
        {
            _task.Update(asset.Name);
            try
            {
                if (asset.Type.Contains("RenderTextureAsset")) return;
                textureExporter.Export(
                    App.AssetManager.GetResAs<Texture>(
                        App.AssetManager.GetResEntry(
                            (ulong)(((dynamic)App.AssetManager.GetEbx(asset).RootObject).Resource))),
                    ULTools.ToFile(outputFolder, "Textures/" + asset.Filename + ".png"), "*.png");
            } catch { }
        }
        public void ExportMesh(EbxAssetEntry entry) // (res)MeshSet & (ebx)MeshAsset
        {
            _task.Update(entry.Name);
            if (new FileInfo(ULTools.ToFile(outputFolder, "Meshes/" + entry.Filename + ".fbx")).Exists)
            {
                goto skipExport;
            }
            MeshSetPlugin.Resources.MeshSet mesh = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(
                    App.AssetManager.GetResEntry(
                        (ulong)(((dynamic)App.AssetManager.GetEbx(entry).RootObject).MeshSetResource)));
            meshExporter.ExportFBX(App.AssetManager.GetEbx(entry).RootObject, ULTools.ToFile(outputFolder, "Meshes/" + entry.Filename), "2017", "Meters", false, true, "", "binary",mesh);
            EbxAsset asset = App.AssetManager.GetEbx(entry);
        skipExport:
            ULMeshData meshData = new ULMeshData();
            meshData.textureParameters = new List<ULTextureParameter>();
            meshData.meshFileName = entry.Filename + ".fbx";

            var varDb = MeshVariationDb.GetVariations(entry.Guid);
            if (varDb == null || varDb.Variations == null)
            {
                App.Logger.Log("Mesh has no MeshVariation entry: " + entry.Name);
                return;
            }
            
            foreach (KeyValuePair<uint, MeshVariation> pair in varDb.Variations)
            {
                foreach (var mat in pair.Value.Materials)
                {
                    //FrostySdk.Ebx.MeshMaterial material = asset.GetObject(mat.MaterialGuid);
                    List<TextureShaderParameter> textureParams = ((List<object>)mat.TextureParameters).ConvertAll((o) => (TextureShaderParameter)o);
                    foreach (TextureShaderParameter textureParam in textureParams)
                    {
                        ULTextureParameter texParameter = new ULTextureParameter();
                        var textureEntry = App.AssetManager.GetEbxEntry(textureParam.Value.External.FileGuid);
                        if (textureEntry == null)
                        {
                            App.Logger.Log("Texture entry was null. Mesh: " + entry.Name);
                            continue;
                        }
                        texParameter.fileName = textureEntry.Filename + ".png";
                        texParameter.parameter = textureParam.ParameterName.ToString();
                        if (exportedObjects.Add(textureEntry.Name) && (!new FileInfo(ULTools.ToFile(outputFolder, "Textures/" + textureEntry.Filename + ".png")).Exists))
                            ExportTexture(textureEntry);
                        meshData.textureParameters.Add(texParameter);
                    }
                }
            }

            meshes.Add(meshData);
        }
    }
}
