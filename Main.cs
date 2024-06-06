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
        private readonly DirectoryInfo outputFolder;
        private FBXExporter meshExporter;
        private TextureExporter textureExporter;
        private HashSet<string> exportedObjects;
        private HashSet<EbxAssetEntry> meshesToExport;
        private EbxAssetEntry levelDataAssetEntry;

        private Dictionary<uint, string> ObjectVariablesTable;
        private FrostyTaskWindow _task;

        private UnityXmlWriter xmlWriter;
        // exported data
        private List<ULObjectInstance> staticModelGroup;
        private List<ULMeshData> meshes;

        public UnityLevelPlugin(string outputFolder)  
        { 
            this.outputFolder = new DirectoryInfo(outputFolder);
        }

        public void Export()
        {
            textureExporter = new TextureExporter();
            staticModelGroup = new List<ULObjectInstance>();
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
                    EbxAsset asset = App.AssetManager.GetEbx(levelDataAssetEntry);

                    //foreach (PointerRef ptr in level.Objects)
                    foreach (object o in asset.Objects)
                    {
                        //EbxAssetEntry ebx = App.AssetManager.GetEbxEntry(ptr.External.FileGuid);
                        //if (ebx == null)
                        //{
                        //    continue;
                        //}
                        //switch (App.AssetManager.GetEbx(ebx).RootObject)
                        switch (o)
                        {
                            case LayerReferenceObjectData obj: ReadBlueprint(obj.Blueprint); break;
                            case SubWorldReferenceObjectData obj: ReadBlueprint(App.AssetManager.GetEbxEntry(obj.BundleName)); break;
                            case StaticModelGroupEntityData obj: ReadStaticModelGroup(obj, levelDataAssetEntry); break;
                        }
                    }

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
            using (FileStream stream = File.OpenWrite(outputFile))
            {
                xmlWriter = new UnityXmlWriter(stream);
                xmlWriter.WriteStartElement("LevelData");

                xmlWriter.WriteElement("LevelDataPath", new FileInfo(outputFile).Directory.FullName.Replace('\\','/')); // <LevelDataPath>
                WriteMeshData(); // <MeshData>
                WriteStaticModelGroups(); // <StaticModelGroups>

                xmlWriter.WriteEndElement();
                xmlWriter.End();
            }
        }

        public void WriteMeshData()
        {
            xmlWriter.WriteStartElement("MeshData",
                ("count", meshes.Count)
            );

            foreach (var mesh in meshes)
            {
                mesh.Write(xmlWriter, "mesh");
            }

            xmlWriter.WriteEndElement();
        }

        public void WriteStaticModelGroups()
        {
            xmlWriter.WriteStartElement("StaticModelGroup", 
                ("count", staticModelGroup.Count)
            );

            foreach (var instance in staticModelGroup)
            {
                instance.Write(xmlWriter, "member");
            }

            xmlWriter.WriteEndElement();
        }

        public void ExportObjectBlueprint(EbxAssetEntry asset)
        {
            List<EbxAssetEntry> list = (from refGuid in asset.EnumerateDependencies()
                                        select App.AssetManager.GetEbxEntry(refGuid) into refEntry
                                        where TypeLibrary.IsSubClassOf(refEntry.Type, "MeshAsset")
                                        select refEntry).ToList();
            list.ForEach((entry) => AddMesh(entry));
        }

        public void ReadStaticModelGroup(StaticModelGroupEntityData modelGroup, EbxAssetEntry rootAsset)
        {
            ResAssetEntry resEntry = App.AssetManager.GetResEntry(rootAsset.Name + "/staticmodelgroup_physics_win32");
            if (resEntry == null)
            {
                App.Logger.Log("Static model group not found: " + rootAsset.Name + "/staticmodelgroup_physics_win32");
                //return;
            }
            //HavokPhysicsData physics = ((resEntry == null) ? new HavokPhysicsData() : GetLevelEditorHavok(resEntry.Name));
            List<ULTransform> physics = ((resEntry == null) ? new List<ULTransform>() : GetPhysicsDataOfObjectBlueprint(resEntry.Name));
            App.Logger.Log(physics.Count.ToString());
            //App.Logger.Log(physics..ToString());
            if (resEntry == null)
            {
                return;
            }
            int index = 0;
            foreach (StaticModelGroupMemberData member in modelGroup.MemberDatas)
            {
                ULObjectInstance instance = new ULObjectInstance();
                instance.objectVariations = new List<string>();
                instance.transforms = new List<ULTransform>();
                EbxAssetEntry memberType = App.AssetManager.GetEbxEntry(member.MemberType.External.FileGuid);
                EbxAssetEntry mesh = App.AssetManager.GetEbxEntry(member.MeshAsset.External.FileGuid);
                AddMesh(mesh);
                ExportObjectBlueprint(memberType);

                member.InstanceObjectVariation.ForEach((value) => instance.objectVariations.Add((value == 0) ? "Default" : ObjectVariablesTable[value]));
                
                if (member.PhysicsPartCountPerInstance > 0)
                {
                    // PhysicsPartRange
                    for (int i = 0; i < ((member.PhysicsPartRange.Last - member.PhysicsPartRange.First + 1) / member.PhysicsPartCountPerInstance); i++)
                    {
                        //Console.WriteLine(physics.GetTransform(index)); // debug
                        //instance.transforms.Add(ULTransform.FromMatrix4x4(physics.GetTransform(index++)));
                        if (index >= physics.Count)
                        {
                            App.Logger.Log($"Physics count way lower than expected! Expected(<{physics.Count}) Got({i})");
                            App.Logger.Log($"Asset: " + rootAsset.Name);
                            return;
                        }
                        instance.transforms.Add(physics[index++]);
                        //Console.WriteLine(member.PhysicsPartRange.First);
                        //Console.WriteLine(index);
                        instance.objectBlueprint = new ULObjectBlueprint(mesh.Filename + ".fbx");
                    }
                }
                else
                {
                    List<LinearTransform> transforms = member.InstanceTransforms;
                    foreach (var trans in transforms)
                    {
                        //instance.transforms.Add(ULTransform.FromMatrix4x4(physics.GetTransform(i)));
                        instance.transforms.Add(ULTransform.FromLinearTransform(trans));
                        instance.objectBlueprint = new ULObjectBlueprint(mesh.Filename + ".fbx");
                    }
                }

                staticModelGroup.Add(instance);
            }
        }

        // has object blueprints under it that are relative to the spatial prefab reference
        public void ReadSpatialPrefabReference(SpatialPrefabReferenceObjectData reference, EbxAssetEntry rootAsset)
        {
            EbxAssetEntry entry = App.AssetManager.GetEbxEntry(reference.Blueprint.External.FileGuid);
            if (entry == null)
            {
                return;
            }
            EbxAsset asset = App.AssetManager.GetEbx(entry);

            foreach (var objBp in asset.Objects.Where((o) => o.GetType() == typeof(ObjectReferenceObjectData)))
            {
                ReadObjectReference((ObjectReferenceObjectData)objBp, entry, ULTransform.FromLinearTransform(reference.BlueprintTransform));
            }
        }


        // @TEMP just add it to the statics
        // @TEMP just add it to the statics
        // @TEMP just add it to the statics  
        public void ReadObjectReference(ObjectReferenceObjectData data, EbxAssetEntry rootAsset)
        {
            if (data == null || data.Blueprint == null || data.BlueprintTransform == null)
            {
                return;
            }
            ULObjectInstance instance = new ULObjectInstance();
            instance.objectBlueprint = new ULObjectBlueprint();
            instance.transforms = new List<ULTransform>()
            {
                ULTransform.FromLinearTransform(data.BlueprintTransform)
            };
            instance.objectVariations = new List<string>
            {
                (data.ObjectVariation != 2) ? "Default" : App.AssetManager.GetEbxEntry(data.ObjectVariation.External.FileGuid).Name
            };
            if (ReadObjectBlueprint(App.AssetManager.GetEbxEntry(data.Blueprint.External.FileGuid), out var mesh))
            {
                instance.objectBlueprint.meshPath = mesh.Filename + ".fbx";
            }
            else
            {
                return;
            }

            staticModelGroup.Add(instance);
        }
        public void ReadObjectReference(ObjectReferenceObjectData data, EbxAssetEntry rootAsset, ULTransform offset)
        {
            ULObjectInstance instance = new ULObjectInstance();
            instance.objectBlueprint = new ULObjectBlueprint();
            instance.transforms = new List<ULTransform>()
            {
                ULTransform.FromLinearTransform(data.BlueprintTransform) + offset
            };
            instance.objectVariations = new List<string>
            {
                (data.ObjectVariation != 2) ? "Default" : App.AssetManager.GetEbxEntry(data.ObjectVariation.External.FileGuid).Name
            };
            if (ReadObjectBlueprint(App.AssetManager.GetEbxEntry(data.Blueprint.External.FileGuid), out var mesh))
            {
                instance.objectBlueprint.meshPath = mesh.Filename + ".fbx";
            }
            else
            {
                return;
            }

            staticModelGroup.Add(instance);
        }

        public bool ReadObjectBlueprint(EbxAssetEntry entry, out EbxAssetEntry mesh)
        {
            List<EbxAssetEntry> meshReferences = FindAllReferencesOfType(entry, "MeshAsset");
            meshReferences.ForEach((e) => AddMesh(e));

            if (meshReferences.Count == 0)
            {
                mesh = null;
                return false;
            }
            mesh = meshReferences.First();

            return true;
        }

        private void ReadBlueprint(PointerRef ptr) => ReadBlueprint(App.AssetManager.GetEbxEntry(ptr.External.FileGuid));
        private void ReadBlueprint(EbxAssetEntry entry)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            Console.WriteLine(entry.Name);

            //foreach (PointerRef ptr in level.Objects)
            // switched to going through the objects instead bc references were inconsistent, sometimes in the file and sometimes out of file
            // so this makes it consistent
            foreach (object o in asset.Objects)
            {
                Console.WriteLine(o.GetType().Name);
                switch (o)
                {
                    case LayerReferenceObjectData obj:
                        {
                            if (obj.Blueprint == null)
                            {
                                break;
                            }
                            ReadBlueprint(obj.Blueprint);
                        }
                        break;
                    case SubWorldReferenceObjectData obj:
                        {
                            if (obj.BundleName == null)
                            {
                                break;
                            }
                            ReadBlueprint(App.AssetManager.GetEbxEntry(obj.BundleName));
                        }
                        break;
                    case StaticModelGroupEntityData obj: ReadStaticModelGroup(obj, entry); break;
                    case ObjectReferenceObjectData obj: ReadObjectReference(obj, entry); break;
                    case SpatialPrefabReferenceObjectData obj: ReadSpatialPrefabReference(obj, entry); break;
                }
            }
        }

        public List<ULTransform> GetPhysicsDataOfObjectBlueprint(string fullPath)
        {
            // Every object will have the same naming convention of just having "_Physics_Win32" at the end
            // so to speed it up we can just do that
            //return App.AssetManager.GetResAs<HavokPhysicsData>(App.AssetManager.GetResEntry(fullPath + ((fullPath.EndsWith("_physics_win32")) ? "" : "_Physics_Win32")));
            string outFile = ToFile("havokinfo");
            HavokInterface.ExportTranslationInformation(outFile, App.AssetManager.GetResEntry(fullPath + ((fullPath.EndsWith("_physics_win32")) ? "" : "_Physics_Win32")));
            Thread.Sleep(100);
            outFile = outFile + ".txt";
            string fileText = File.ReadAllText(outFile);
            string[] lines = fileText.Split('\n');
            lines[lines.Length - 1] = lines[0]; // fix crash
            List<ULTransform> result = new List<ULTransform>();
            foreach (string line in lines)
            {
                float[] m = line.Split(';').Select(s => Convert.ToSingle(s)).ToArray();
                result.Add(ULTransform.FromMatrix4x4(
                              //new Matrix4x4(m[0], m[1], m[2], m[3],
                              //              m[4], m[5], m[6], m[7],
                              //              m[8], m[9], m[10], m[11],
                              //              m[12], m[13], m[14], m[15]
                              //              )));
                              new Matrix4x4(m[0], m[1], m[2], 0,
                                            m[4], m[5], m[6], 0,
                                            m[8], m[9], m[10], 0,
                                            m[3], m[7], m[11], 0
                                            )));
            }
            return result;

            //StaticModelPhysicsComponentData componentData = (StaticModelPhysicsComponentData)App.AssetManager.GetEbx(
            //                                                (from refGuid in bp.Objects
            //                                                 select App.AssetManager.GetEbxEntry(refGuid.External.FileGuid) into refEntry
            //                                                 where TypeLibrary.IsSubClassOf(refEntry.Type, "StaticModelPhysicsComponentData")
            //                                                 select refEntry).First()).RootObject;
            //RigidBodyData bodyData = componentData.PhysicsBodies
        }

        public List<EbxAssetEntry> FindAllReferencesOfType(EbxAssetEntry entry, string typeName)
        {
            return (from refGuid in entry.EnumerateDependencies()
                    select App.AssetManager.GetEbxEntry(refGuid) into refEntry
                    where TypeLibrary.IsSubClassOf(refEntry.Type, typeName)
                    select refEntry).ToList();
        }

        //public HavokPhysicsData GetLevelEditorHavok(string fullPath)
        //{
        //    return App.AssetManager.GetResAs<HavokPhysicsData>(App.AssetManager.GetResEntry(fullPath + ((fullPath.EndsWith("_physics_win32")) ? "" : "_Physics_Win32")));
        //}

        public string ToFile(string path)
        {
            string outPath = Path.Combine(outputFolder.FullName, path);

            try 
            { 
                Directory.CreateDirectory(new FileInfo(outPath).Directory.FullName); // ensure it exists
            } catch (Exception _) { }
            
            return outPath;
        }

        public static T GetEbxFileData<T>(PointerRef ptr)
        {
            return (T) App.AssetManager.GetEbx(App.AssetManager.GetEbxEntry(ptr.External.FileGuid)).RootObject;
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
            textureExporter.Export(
                App.AssetManager.GetResAs<Texture>(
                    App.AssetManager.GetResEntry(
                        (ulong)(((dynamic)App.AssetManager.GetEbx(asset).RootObject).Resource))),
                ToFile("Textures/" + asset.Filename + ".png"), "*.png");
        }

        public void ExportMesh(EbxAssetEntry entry) // (res)MeshSet & (ebx)MeshAsset
        {
            _task.Update(entry.Name);
            if (new FileInfo(ToFile("Meshes/" + entry.Filename + ".fbx")).Exists)
            {
                goto skipExport;
            }
            MeshSetPlugin.Resources.MeshSet mesh = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(
                    App.AssetManager.GetResEntry(
                        (ulong)(((dynamic)App.AssetManager.GetEbx(entry).RootObject).MeshSetResource)));
            meshExporter.ExportFBX(App.AssetManager.GetEbx(entry).RootObject, ToFile("Meshes/" + entry.Filename), "2017", "Meters", false, true, "", "binary",mesh);
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
                        if (exportedObjects.Add(textureEntry.Name) && (!new FileInfo(ToFile("Textures/" + textureEntry.Filename + ".png")).Exists))
                            ExportTexture(textureEntry);
                        meshData.textureParameters.Add(texParameter);
                    }
                }
            }

            meshes.Add(meshData);
        }
    }
}
