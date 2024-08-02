using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using MagixTools;
using MeshSetPlugin;
using MeshSetPlugin.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityLevelPlugin.Export;
using UnityLevelPlugin.Tools;

namespace UnityLevelPlugin.Import;

public partial class Importer
{
    public string ToFile(string path)
    {
        return ULTools.ToFile(context.plugin.outputFolder, path);
    }

    public void ImportUserAdded()
    {
        FBXImporter fbxImporter = new FBXImporter(App.Logger);

        // TEMP
        // TEMP
        // TEMP
        FBXExporter fbxExporter = new FBXExporter(task);
        // TEMP
        // TEMP
        // TEMP

        var meshDuper = new DuplicationPlugin.DuplicationTool.MeshExtension();
        var textureDuper = new DuplicationPlugin.DuplicationTool.TextureExtension();
        var havokDuper = new DuplicationPlugin.DuplicationTool.HavokAssetExtension();
        var ebxDuper = new DuplicationPlugin.DuplicationTool.DuplicateAssetExtension();

        var meshPath = levelDataAssetEntry.Name + "/Meshes/";
        var texturesPath = levelDataAssetEntry.Name + "/Textures/";
        var collisionPath = levelDataAssetEntry.Name + "/Collision/";
        var objectBlueprintPath = levelDataAssetEntry.Name + "/Objects/";

        var meshImportSettings = new FrostyMeshImportSettings();

        // Universal mesh :DDD
        // no longer a universal mesh D:
        //var rigidMeshToDupe = App.AssetManager.GetEbxEntry(Config.Get<string>("ULE_StandardMeshTD", ULOptions.ULE_StandardMeshTD_DEFAULT));
        var emissiveMeshToDupe = App.AssetManager.GetEbxEntry("fx/vehicles/abilities/seismiccharge/seismiccanister_mesh");
        var transparentMeshToDupe = App.AssetManager.GetEbxEntry("characters/hero/bobafett/bobafett_01/bobafett_01_cape_mesh");
        var textureToDupe = App.AssetManager.GetEbxEntry(@"Objects/Props/ObjectSets/_RebelAlliance/Box_M_01/T_Box_M_01_A_CS");
        var havokToDupe = App.AssetManager.GetEbxEntry(@"Objects/Props/ObjectSets/_RebelAlliance/Box_M_01/Box_M_01_A_Physics_Win32");
        var obToDupe = App.AssetManager.GetEbxEntry(@"Objects/Props/ObjectSets/_RebelAlliance/Box_M_01/Box_M_01_B");

        // duping objects first :DDD
        Dictionary<string, PointerRef> objectBlueprints = new Dictionary<string, PointerRef>();

        foreach (ULMeshData meshData in context.plugin.meshes)
        {
            #region -- Preprocess the textures --

            Dictionary<string, string> textureParams = meshData.textureParameters.ToDictionary(k => k.parameter, k => k.fileName);
            Dictionary<string, EbxAssetEntry> textureParamsRefs = new Dictionary<string, EbxAssetEntry>();
            Dictionary<string, string> textureParamsLookup = new Dictionary<string, string>();

            #endregion

            #region -- Mesh --
            fbxImporter = new FBXImporter(App.Logger);
            string meshFileName = meshData.meshFileName.Replace(".fbx", "") + "_mesh";
            var meshEntry = App.AssetManager.GetEbxEntry(meshPath + meshFileName);
            if (meshEntry != null)
            {
                goto skipDupeMesh;
            }
            if (textureParams.ContainsKey("Transparency"))
            {
                meshEntry = meshDuper.DuplicateAsset(transparentMeshToDupe, meshPath + meshFileName, false, typeof(RigidMeshAsset));
                textureParamsLookup.Add("Transparency", "Transparency");
                textureParamsLookup.Add("Color", "BaseColor");
                textureParamsLookup.Add("Normals", "Normal");
            }
            else
            {
                meshEntry = meshDuper.DuplicateAsset(emissiveMeshToDupe, meshPath + meshFileName, false, typeof(RigidMeshAsset));
                textureParamsLookup.Add("Emissive", "_Emissive");
                textureParamsLookup.Add("Color", "_BaseColor");
                textureParamsLookup.Add("Normals", "_Normal");
            }
        skipDupeMesh:
            // gets the meshset resource
            MeshSetPlugin.Resources.MeshSet meshSet = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(
                    App.AssetManager.GetResEntry(
                        (ulong)(((dynamic)App.AssetManager.GetEbx(meshEntry).RootObject).MeshSetResource)));
            task.Status = "importing mesh just got meshset";
            fbxImporter.ImportFBXModified(ToFile($"Meshes/{meshData.meshFileName}"), meshSet,
                App.AssetManager.GetEbx(meshEntry), meshEntry);
            // RETARDED IDEA ALERT
            // RETARDED IDEA ALERT 
            // CREDIT FOR THE RETARDED IDEA: @FEGEEWATERS

            // we export and reimport the same fucking mesh (retarded (yes i needed to say it twice))
            meshEntry = App.AssetManager.GetEbxEntry(meshEntry.Name);
            MeshSetPlugin.Resources.MeshSet meshSet2 = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(
                    App.AssetManager.GetResEntry(
                        (ulong)(((dynamic)App.AssetManager.GetEbx(meshEntry).RootObject).MeshSetResource)));
            
            fbxExporter.ExportFBX(App.AssetManager.GetEbx(meshEntry).RootObject, ToFile("Meshes/" + meshData.meshFileName + ".temp.fbx"), "2017", "Meters", true, false, "", "binary", meshSet2);

            fbxImporter.ImportFBX(ToFile($"Meshes/{meshData.meshFileName}.temp.fbx"), meshSet2,
                App.AssetManager.GetEbx(meshEntry), meshEntry, new FrostyMeshImportSettings());
            File.Delete(ToFile("Meshes/" + meshData.meshFileName + ".temp.fbx"));

            // RETARDED IDEA ALERT
            // RETARDED IDEA ALERT 

            meshEntry = App.AssetManager.GetEbxEntry(meshEntry.Name);
            task.Status = "Finished mesh, going on to color texture";

            #endregion

            #region -- Textures --

            //EbxAssetEntry colorTextureEntry = null;
            //EbxAssetEntry normalsTextureEntry = null;

            foreach (var textureParam in textureParams)
            {
                if (!string.IsNullOrEmpty(textureParam.Value) && textureParam.Value.Contains(".png"))
                {
                    string textureFileName = textureParam.Value.Replace(".png", "") + (!textureParam.Value.Contains(textureParam.Key) ? textureParam.Key : string.Empty);
                    var entry = App.AssetManager.GetEbxEntry(texturesPath + textureFileName);
                    if (entry == null)
                    {
                        entry = textureDuper.DuplicateAsset(textureToDupe, texturesPath + textureFileName, false, typeof(TextureAsset));
                        ULTools.ImportTexture(entry, ToFile($"Textures/{textureParam.Value}"));
                    }
                    textureParamsRefs.Add(textureParam.Key, entry);
                }
            }
            
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
                material.Shader.TextureParameters.Clear();
                //if (colorTextureEntry != null)
                //{
                //    material.Shader.TextureParameters.Add(new TextureShaderParameter()
                //    {
                //        ParameterName = Config.Get<string>("ULE_StandardMesh_ColorParam", ULOptions.ULE_StandardMesh_ColorParam_DEFAULT),// "_CS",
                //        Value = EbxTools.DeepRef(colorTextureEntry, typeof(TextureAsset))
                //    });
                //    meshAsset.AddDependency(colorTextureEntry.Guid);
                //}
                //if (normalsTextureEntry != null)
                //{
                //    material.Shader.TextureParameters.Add(new TextureShaderParameter()
                //    {
                //        ParameterName = Config.Get<string>("ULE_StandardMesh_NormalParam", ULOptions.ULE_StandardMesh_NormalParam_DEFAULT),//"_NAM_texcoord0",
                //        Value = EbxTools.DeepRef(normalsTextureEntry, typeof(TextureAsset))
                //    });
                //    meshAsset.AddDependency(normalsTextureEntry.Guid);
                //}
                foreach (var param in textureParamsRefs)
                {
                    material.Shader.TextureParameters.Add(new TextureShaderParameter()
                    {
                        ParameterName = textureParamsLookup[param.Key],//"_NAM_texcoord0",
                        Value = EbxTools.DeepRef(param.Value, typeof(TextureAsset))
                    });
                    meshAsset.AddDependency(param.Value.Guid);
                }
                if (textureParams.TryGetValue("EmissiveIntensity", out string intensity))
                {
                    // its always the first so lets not do extra work here :)
                    if (material.Shader.VectorParameters[0].ParameterName != "EmissiveIntensity")
                    {
                        continue; // error ??
                    }
                    material.Shader.VectorParameters[0].Value = new Vec4() { x = Convert.ToSingle(intensity), y = 0, z = 0, w = 0 };
                }
                //foreach (var param in material.Shader.TextureParameters)
                //{
                //    if (param.ParameterName == "_CS")
                //    {
                //        if (colorTextureEntry == null)
                //        {
                //            param.Value = new PointerRef();
                //        }
                //        param.Value = EbxTools.DeepRef(colorTextureEntry, typeof(TextureAsset));
                //    }
                //    else if (param.ParameterName == "_NAM_texcoord0")
                //    {
                //        param.Value = EbxTools.DeepRef(normalsTextureEntry, typeof(TextureAsset));
                //    }
                //}
            }
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
            var collisionParam = meshData.textureParameters.FirstOrDefault((i) => i.parameter == "COLLISION");
            string originalCollisionFileName = string.Empty;
            if (!string.IsNullOrEmpty(collisionParam.fileName))
            {
                originalCollisionFileName = meshData.textureParameters.First((i) => i.parameter == "COLLISION").fileName;
            }
            //var collisionEntry = havokDuper.DuplicateAsset(havokToDupe, collisionPath + collisionFileName, false, typeof(HavokAsset));
            //HavokInterface.ImportHavok(ToFile($"Collision/{originalCollisionFileName}"), collisionEntry);
            #endregion

            #region -- Object Blueprint making --

            //EbxAsset objectBlueprint = EbxTools.CreateEbxAsset(nameof(ObjectBlueprint));

            // check if it exists so no errors ;)
            string obPath = objectBlueprintPath + meshData.meshFileName.Replace(".fbx", "");
            var obEntry = App.AssetManager.GetEbxEntry(obPath);
            if (obEntry != null)
            {
                App.AssetManager.RevertAsset(obEntry);
            }
            obEntry = ebxDuper.DuplicateAsset(obToDupe, obPath, false, typeof(ObjectBlueprint));
            var obAsset = App.AssetManager.GetEbx(obEntry);
            ObjectBlueprint ob = (ObjectBlueprint)obAsset.RootObject;
            //App.Logger.Log(ob.Object.Internal.GetType().FullName);
            var entityData = (StaticModelEntityData)ob.Object.Internal;
            entityData.Mesh = EbxTools.RefToFile(meshAsset);

            if (!string.IsNullOrEmpty(originalCollisionFileName))
            {
                List<PointerRef> havokPtrList = HavokInterface.BuildCompound(ToFile($"Collision/{originalCollisionFileName}"), obAsset, havokToDupe, collisionPath.TrimEnd('/'), $"{meshData.meshFileName.Replace(".fbx", "")}_Physics_Win32");
                ((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies.Clear();
                ((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies.AddRange(havokPtrList);
            }
            //((StaticModelPhysicsComponentData)entityData.Components[1].Internal).PhysicsBodies
            //    .ForEach((o) => ((RigidBodyData)o.Internal).Asset = new PointerRef(collisionEntry.Guid));

            App.AssetManager.ModifyEbx(obEntry.Name, obAsset);
            objectBlueprints.Add(meshData.meshFileName, EbxTools.RefToFile(obAsset));

            #endregion
        }


        foreach (ULObjectInstance inst in levelSpatial.objectBlueprintReferences.instances)
        {
            foreach (ULTransform trans in inst.transforms)
            {
                var reference = (ObjectReferenceObjectData)CreateRootClass(nameof(ObjectReferenceObjectData));
                reference.BlueprintTransform = trans.ToLinearTransform();
                reference.Blueprint = objectBlueprints[inst.objectBlueprint.meshPath];
                reference.Flags = 13040047; // magic numbers <3
                reference.LightmapScaleWithSize = true;
                reference.LightmapResolutionScale = 1;
            }
        }
    }
}
