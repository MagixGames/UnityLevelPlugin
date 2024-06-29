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
using Frosty.Core.Controls;

namespace UnityLevelPlugin;

public class ExportUnityLevelButton : DataExplorerContextMenuExtension
{
    public override string ContextItemName => "Export Level (UnityLevelPlugin)";
    //public override ImageSource Icon => null; // new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/HavokFileType.png") as ImageSource;

    public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
    {
        try
        {
            // todo: change to save file .fbul
            FrostySaveFileDialog ofd = new FrostySaveFileDialog("Select output file", "*.fbul (Frostbite UnityLevelEditorOut)|*.fbul", "FBUL", "out.fbul");
            if (ofd.ShowDialog())
            {
                // @TODO
                // fix later :D
                UnityLevelPlugin pl = new UnityLevelPlugin(new FileInfo(ofd.FileName).Directory.FullName);
                pl.Export();
            }
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
public class ImportUnityLevelButton : DataExplorerContextMenuExtension
{
    public override string ContextItemName => "Import Level (UnityLevelPlugin)";
    //public override ImageSource Icon => null; // new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/HavokFileType.png") as ImageSource;

    public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
    {
        try
        {
            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Select FBUL", "*.fbule (Frostbite UnityLevel Export)|*.fbule", "FBULE");
            if (ofd.ShowDialog())
            {
                UnityLevelPlugin pl = new UnityLevelPlugin(new FileInfo(ofd.FileName).Directory.FullName);
                pl.Import();
            }
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
    private UnityXmlReader xmlReader;
    private LevelDataExporter levelDataExporter;
    private List<ULMeshData> meshes;

    private ULEBSpatial importedSpatial;

    public UnityLevelPlugin(string outputFolder)  
    { 
        this.outputFolder = new DirectoryInfo(outputFolder);
    }

    #region -- Export -- 
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
                List<TextureShaderParameter> textureParams = ((List<dynamic>)mat.TextureParameters).ConvertAll((o) => (TextureShaderParameter)o);
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
    #endregion

    #region -- Import

    /// <summary>
    /// THIS ONLY DOES IMPORTING OF CUSTOM STUFF RIGHT NOW. WILL SUPPORT MORE LATER
    /// </summary>
    public void Import()
    {
        textureExporter = new TextureExporter();
        meshesToExport = new HashSet<EbxAssetEntry>();
        exportedObjects = new HashSet<string>();
        levelDataAssetEntry = App.SelectedAsset;
        FBXImporter fbxImporter = new FBXImporter(App.Logger);

        var meshDuper = new DuplicationPlugin.DuplicationTool.MeshExtension();
        var textureDuper = new DuplicationPlugin.DuplicationTool.TextureExtension();
        var havokDuper = new DuplicationPlugin.DuplicationTool.HavokAssetExtension();
        var ebxDuper = new DuplicationPlugin.DuplicationTool.DuplicateAssetExtension();

        var meshPath = levelDataAssetEntry.Name + "/Meshes/";
        var texturesPath = levelDataAssetEntry.Name + "/Textures/";
        var collisionPath = levelDataAssetEntry.Name + "/Collision/";
        var objectBlueprintPath = levelDataAssetEntry.Name + "/Objects/";

        var meshImportSettings = new FrostyMeshImportSettings();

        FrostyTaskWindow.Show("Shits IMPORTING !! YAYYAYAYYAY", "Preeeeeetty cool stuff", (task) =>
        {
            try
            {
                _task = task;

                if (!MeshVariationDb.IsLoaded)
                {
                    MeshVariationDb.LoadVariations(task);
                }

                // doing it all in here for rn for simplicity
                ImportLevelData();

                var rigidMeshToDupe = App.AssetManager.GetEbxEntry(@"objects/props/objectsets/tatooine/cup_01/cup_01_mesh");
                var textureToDupe = App.AssetManager.GetEbxEntry(@"Objects/Props/ObjectSets/_RebelAlliance/Box_M_01/T_Box_M_01_A_CS");
                var havokToDupe = App.AssetManager.GetEbxEntry(@"Objects/Props/ObjectSets/_RebelAlliance/Box_M_01/Box_M_01_A_Physics_Win32");
                var obToDupe = App.AssetManager.GetEbxEntry(@"Objects/Props/ObjectSets/_RebelAlliance/Box_M_01/Box_M_01_B");

                // duping objects first :DDD
                Dictionary<string, PointerRef> objectBlueprints = new Dictionary<string, PointerRef>();

                foreach (ULMeshData meshData in meshes) 
                {
                    #region -- Mesh --
                    string meshFileName = meshData.meshFileName.Replace(".fbx", "") + "_mesh";
                    var meshEntry = meshDuper.DuplicateAsset(rigidMeshToDupe, meshPath + meshFileName, false, typeof(RigidMeshAsset));

                    // gets the meshset resource
                    MeshSetPlugin.Resources.MeshSet meshSet = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(
                            App.AssetManager.GetResEntry(
                                (ulong)(((dynamic)App.AssetManager.GetEbx(meshEntry).RootObject).MeshSetResource)));

                    task.Status = "importing mesh just got meshset";
                    fbxImporter.ImportFBXModified(ToFile($"Meshes/{meshData.meshFileName}"), meshSet, 
                        App.AssetManager.GetEbx(meshEntry), meshEntry, rigidMeshToDupe, out int lodNum);
                    task.Status = "Finished mesh, going on to color texture";

                    #endregion

                    #region -- Textures --

                    // Color texture

                    string orgColorTextureFile = meshData.textureParameters.First((i) => i.parameter == "Color").fileName;
                    string colorTextureFileName = orgColorTextureFile.Replace(".png", "") + "_CS";
                    
                    var colorTextureEntry = textureDuper.DuplicateAsset(textureToDupe, texturesPath + colorTextureFileName, false, typeof(TextureAsset));
                    task.Status = "checkpoint";
                    ULTools.ImportTexture(colorTextureEntry, ToFile($"Textures/{orgColorTextureFile}"));
                    task.Status = "Finished color texture";
                    // NAM texture

                    string orgNormalsTextureFile = meshData.textureParameters.First((i) => i.parameter == "Normals").fileName;
                    string normalsTextureFileName = orgNormalsTextureFile.Replace(".png", "") + "_NAM";
                    var normalsTextureEntry = textureDuper.DuplicateAsset(textureToDupe, texturesPath + normalsTextureFileName, false, typeof(TextureAsset));
                    ULTools.ImportTexture(normalsTextureEntry, ToFile($"Textures/{orgNormalsTextureFile}"));

                    // write references to mesh var DB

                    EbxAsset meshAsset = App.AssetManager.GetEbx(meshEntry);
                    meshAsset = App.AssetManager.GetEbx(meshEntry);
                    RigidMeshAsset rigidMesh = (RigidMeshAsset)meshAsset.RootObject;

                    foreach (var material in rigidMesh.Materials.Select((n) => (FrostySdk.Ebx.MeshMaterial)n.Internal))
                    {
                        foreach (var item in material.Shader.VectorParameters)
                        {
                            item.Value = new Vec4() { x = 0, y = 0, z = 0, w = 0 };
                        }
                        foreach (var param in material.Shader.TextureParameters)
                        {
                            if (param.ParameterName == "_CS")
                            {
                                param.Value = EbxTools.DeepRef(colorTextureEntry, typeof(TextureAsset));
                            }
                            else if (param.ParameterName == "_NAM_texcoord0")
                            {
                                param.Value = EbxTools.DeepRef(normalsTextureEntry, typeof(TextureAsset));
                            }
                        }
                    }
                    meshAsset.AddDependency(colorTextureEntry.Guid);
                    meshAsset.AddDependency(normalsTextureEntry.Guid);
                    App.AssetManager.ModifyEbx(meshEntry.Name, meshAsset);
                    meshEntry = App.AssetManager.GetEbxEntry(meshEntry.Name);

                    // Edit ShaderBlockDepots for the texture stuff
                    #region -- SBD shinanegains -- 
                    if (ProfilesLibrary.DataVersion == (int)ProfileVersion.StarWarsBattlefrontII)
                    {
                        dynamic ebxData = rigidMesh;
                        ResAssetEntry shaderBlock = App.AssetManager.GetResEntry(meshEntry.Name.ToLower() + "_mesh/blocks");
                        ShaderBlockDepot sbd = App.AssetManager.GetResAs<ShaderBlockDepot>(shaderBlock);

                        for (int lodIndex = 0; lodIndex < meshSet.Lods.Count; lodIndex++)
                        {
                            MeshSetLod lod = meshSet.Lods[lodIndex];
                            MeshSetPlugin.Resources.ShaderBlockEntry sbe = sbd.GetSectionEntry(lodIndex);

                            int index = 0;
                            foreach (MeshSetSection section in lod.Sections)
                            {
                                dynamic material = ebxData.Materials[section.MaterialId].Internal;
                                MeshSetPlugin.Resources.ShaderPersistentParamDbBlock texturesBlock = sbe.GetTextureParams(index);
                                MeshSetPlugin.Resources.ShaderPersistentParamDbBlock paramsBlock = sbe.GetParams(index);
                                index++;

                                foreach (dynamic param in material.Shader.BoolParameters)
                                {
                                    string paramName = param.ParameterName;
                                    bool value = param.Value;

                                    paramsBlock.SetParameterValue(paramName, value);
                                }
                                foreach (dynamic param in material.Shader.VectorParameters)
                                {
                                    string paramName = param.ParameterName;
                                    dynamic vec = param.Value;

                                    paramsBlock.SetParameterValue(paramName, new float[] { vec.x, vec.y, vec.z, vec.w });
                                }
                                foreach (dynamic param in material.Shader.ConditionalParameters)
                                {
                                    string value = param.Value;
                                    PointerRef assetRef = param.ConditionalAsset;

                                    if (assetRef.Type == PointerRefType.External)
                                    {
                                        EbxAsset asset = App.AssetManager.GetEbx(App.AssetManager.GetEbxEntry(assetRef.External.FileGuid));
                                        dynamic conditionalAsset = asset.RootObject;

                                        string conditionName = conditionalAsset.ConditionName;
                                        byte idx = (byte)conditionalAsset.Branches.IndexOf(value);

                                        paramsBlock.SetParameterValue(conditionName, idx);
                                    }
                                }
                                foreach (dynamic param in material.Shader.TextureParameters)
                                {
                                    string paramName = param.ParameterName;
                                    PointerRef value = param.Value;

                                    texturesBlock.SetParameterValue(paramName, value.External.ClassGuid);
                                }

                                texturesBlock.IsModified = true;
                                paramsBlock.IsModified = true;
                            }
                        }

                        ulong resRid = ((dynamic)ebxData).MeshSetResource;
                        ResAssetEntry resEntry = App.AssetManager.GetResEntry(resRid);

                        App.AssetManager.ModifyRes(sbd.ResourceId, sbd);
                        meshEntry.LinkAsset(resEntry);
                    }
                    #endregion

                    #endregion

                    #region -- Collision (Havok) --
                    // Collision
                    string originalCollisionFileName = meshData.textureParameters.First((i) => i.parameter == "COLLISION").fileName;
                    string collisionFileName = meshData.textureParameters.First((i) => i.parameter == "COLLISION").fileName.Replace(' ', '_').Replace(".obj", "") + "_Physics_Win32";

                    //var collisionEntry = havokDuper.DuplicateAsset(havokToDupe, collisionPath + collisionFileName, false, typeof(HavokAsset));
                    //HavokInterface.ImportHavok(ToFile($"Collision/{originalCollisionFileName}"), collisionEntry);
                    #endregion

                    #region -- Object Blueprint making --

                    //EbxAsset objectBlueprint = EbxTools.CreateEbxAsset(nameof(ObjectBlueprint));

                    // just duping an object blueprint for this test because making one is time consuming and fuck that

                    var obEntry = ebxDuper.DuplicateAsset(obToDupe, objectBlueprintPath + meshData.meshFileName.Replace(".fbx", ""), false, typeof(ObjectBlueprint));
                    var obAsset = App.AssetManager.GetEbx(obEntry);
                    ObjectBlueprint ob = (ObjectBlueprint)obAsset.RootObject;
                    //App.Logger.Log(ob.Object.Internal.GetType().FullName);
                    var entityData = (StaticModelEntityData)ob.Object.Internal;
                    entityData.Mesh = EbxTools.RefToFile(meshAsset);

                    List<PointerRef> havokPtrList = HavokInterface.MassImportHavok([ToFile($"Collision/{originalCollisionFileName}")], obAsset, havokToDupe, collisionPath + $"{meshData.meshFileName.Replace(".fbx", "")}");
                    // remove any havok rn
                    //((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies = ((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies
                    //    .Where((n) => !n.Internal.GetType().Name.Contains("Havok"))
                    //    .ToList();
                    ((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies.Clear();
                    ((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies.AddRange(havokPtrList);
                    //((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies
                    //    .ForEach((o) => ((RigidBodyData)o.Internal).Asset = new PointerRef(collisionEntry.Guid));

                    App.AssetManager.ModifyEbx(obEntry.Name, obAsset);
                    objectBlueprints.Add(meshData.meshFileName, EbxTools.RefToFile(obAsset));

                    #endregion

                }

                // create layer data
                EbxAsset customLayerData = EbxTools.CreateEbxAsset(nameof(LayerData));
                LayerData ld = (LayerData)customLayerData.RootObject;
                ld.Enabled = true;

                foreach (ULObjectInstance inst in importedSpatial.objectBlueprintReferences.instances)
                {
                    foreach (ULTransform trans in inst.transforms)
                    {
                        var reference = (ObjectReferenceObjectData)EbxTools.CreateNewClass(nameof(ObjectReferenceObjectData), customLayerData);
                        reference.BlueprintTransform = trans.ToLinearTransform();
                        reference.Blueprint = objectBlueprints[inst.objectBlueprint.meshPath];
                        reference.Flags = 13040047; // magic numbers <3
                        reference.LightmapScaleWithSize = true;
                        reference.LightmapResolutionScale = 1;
                        ld.Objects.Add(new PointerRef(reference));
                    }
                }

                var ldEntry = App.AssetManager.AddEbx(levelDataAssetEntry.Name + $"/UL_ImportedContent", customLayerData);

                var levelDataAsset = App.AssetManager.GetEbx(levelDataAssetEntry);

                LayerReferenceObjectData layerRef = EbxTools.CreateNewClass(nameof(LayerReferenceObjectData), levelDataAsset);
                layerRef.Blueprint = EbxTools.RefToFile(customLayerData);
                layerRef.LightmapResolutionScale = 1;
                layerRef.LightmapScaleWithSize = true;
                layerRef.Flags = 7601219; // another magic number <3
                ((Blueprint)levelDataAsset.RootObject).Objects.Add(new PointerRef(layerRef));
                App.AssetManager.ModifyEbx(levelDataAssetEntry.Name, levelDataAsset);
            }
            catch (Exception ex) { App.Logger.Log(ex.Message); App.Logger.Log(ex.StackTrace); }
        });
    }

    public void ImportLevelData()
    {
        string outputFile = ToFile("out.fbule");
        importedSpatial = new ULEBSpatial();
        using (FileStream stream = new FileStream(outputFile, FileMode.Open, FileAccess.Read))
        {
            xmlReader = new UnityXmlReader(stream);
            //xmlWriter.WriteStartElement("LevelData");

            //xmlWriter.WriteElement("LevelDataPath", new FileInfo(outputFile).Directory.FullName.Replace('\\', '/')); // <LevelDataPath>
            //xmlWriter.WriteList("MeshData", meshes, (m, n) => m.Write(xmlWriter, n));
            //levelDataExporter.spatial.Write(xmlWriter, "rootLevel");

            //xmlWriter.WriteEndElement();
            //xmlWriter.End();

            //xmlReader.ReadStartElement("LevelData");
            meshes = xmlReader.ReadList("MeshData", (n) => ULMeshData.Read(xmlReader, n));
            importedSpatial = ULEBSpatial.Read(xmlReader, "root");
            //xmlReader.ReadEndElement();
        }
    }

    #endregion
}
