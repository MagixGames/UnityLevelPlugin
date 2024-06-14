using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Experimental;
using UnityEditor.MemoryProfiler;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;
using static UnityEngine.Mesh;

//[ScriptedImporter(1, "fbul")] // Frostbite -> Unity Level

public class fbImporter// : ScriptedImporter
{
    public const string TEXTURES_PATH = "Resources/SWBF2/Textures";
    public const string MESHES_PATH = "Resources/SWBF2/Meshes";
    public const string PREFABS_PATH = "Resources/SWBF2/Prefabs";
    public const string COMBINED_MESHES_PATH = "Resources/SWBF2/Meshes/Combined";

    public DirectoryInfo levelDataDir;
    public List<ULMeshData> meshData;
    public ULEBSpatial levelSpatial;

    public Dictionary<string, GameObject> prefabs;
    public HashSet<string> importedTextures;
    public string debug;
    public string debug2;
    public string defaultTexture = String.Empty;

    [MenuItem("Frostbite/ImportLevel")]
    public static void Import()
    {
        fbImporter fbImporter = new fbImporter();
        fbImporter.OnImportAsset(EditorUtility.OpenFilePanel("Open FBUL", "", "fbul"));
    }

    public/* override*/ void OnImportAsset(string path/*AssetImportContext ctx*/)
    {
        FileInfo file = new FileInfo(path);
        prefabs = new Dictionary<string, GameObject>();
        importedTextures = new HashSet<string>();
        ReadData(file);
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/SWBF2"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "SWBF2");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/SWBF2/Textures"))
        {
            AssetDatabase.CreateFolder("Assets/Resources/SWBF2", "Textures");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/SWBF2/Meshes"))
        {
            AssetDatabase.CreateFolder("Assets/Resources/SWBF2", "Meshes");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/SWBF2/Meshes/Combined"))
        {
            AssetDatabase.CreateFolder("Assets/Resources/SWBF2/Meshes", "Combined");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/SWBF2/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources/SWBF2", "Prefabs");
        }
        //ImportTexture(GetOriginalDirFile("Textures/" + meshData[0].textureParameters[0].fileName));
        try
        {
            ImportMeshes();
            PlaceLevelObjects();
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
        //File.Delete(ctx.assetPath);
    }

    public void ImportMeshes()
    {
        foreach (var mData in meshData/*.GetRange(0,2)*/)
        {
            // Import mesh 
            debug2 = mData.meshFileName;
            if (prefabs.ContainsKey(mData.meshFileName))
            {
                continue;
            }
            if (AssetImporter.GetAtPath($"Assets/{MESHES_PATH}/{mData.meshFileName}") != null)
            {
                //prefabs.Add(mData.meshFileName, "Assets/" + PREFABS_PATH + $"/{mData.meshFileName.Replace(".fbx", "")}.prefab");//AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"Assets/{PREFABS_PATH}/{Path.GetFileNameWithoutExtension(mData.meshFileName)}.prefab"));
                prefabs.Add(mData.meshFileName, AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"Assets/{PREFABS_PATH}/{Path.GetFileNameWithoutExtension(mData.meshFileName)}.prefab"));
                continue;
            }
            //Mesh mesh = (Mesh)
            ImportMesh(GetOriginalDirFile("Meshes/" + mData.meshFileName));
            Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath($"Assets/{MESHES_PATH}/{mData.meshFileName}").Where((obj) => obj.GetType() == typeof(Mesh)).Cast<Mesh>().ToArray();
            string texture = string.Empty;
            foreach (var param in mData.textureParameters)
            {
                if ((param.parameter.Contains("CS", StringComparison.Ordinal) && !param.parameter.Contains("NM")) || param.parameter.Contains("BaseColor") || param.parameter == "color" )
                {
                    texture = param.fileName;
                    break; 
                }
            }
            string prefabName = mData.meshFileName.Replace(".fbx", "");
            GameObject gameObject = new GameObject(prefabName);

            #region Material
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            if (!string.IsNullOrEmpty(texture))
            {
                if (importedTextures.Add(texture))
                {
                    renderer.material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(
                        ImportTexture(GetOriginalDirFile("Textures/" + texture)));
                    if (string.IsNullOrEmpty(defaultTexture))
                    {
                        defaultTexture = GetMaterialFromTexture(texture).Replace(".mat", "");
                    }
                }
                else
                {
                    string materialPath = GetMaterialFromTexture(texture).Replace(".mat", "");
                    renderer.material = Resources.Load<Material>(materialPath);
                    if (string.IsNullOrEmpty(defaultTexture))
                    {
                        defaultTexture = materialPath;
                    }
                }
            }
            else
            {
                //if (!string.IsNullOrEmpty(defaultTexture))
                //{
                //    renderer.material = Resources.Load<Material>(defaultTexture);
                //}
                renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            }
            #endregion
            Mesh combinedMesh = new Mesh();
            bool writeCombinedMesh = false;

            if (meshes.Length > 1)
            {
                CombineInstance[] combine = new CombineInstance[meshes.Length];
                for (int i = 0; i < combine.Length; i++)
                {
                    combine[i].mesh = meshes[i];
                    //combine[i].subMeshIndex = i;
                }
                writeCombinedMesh = true;
                combinedMesh.name = prefabName + "_combined";
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combinedMesh.CombineMeshes(combine, true, false, false);
                combinedMesh.Optimize();
                AssetDatabase.CreateAsset(combinedMesh, $"Assets/{COMBINED_MESHES_PATH}/{combinedMesh.name}.asset");
                //AssetDatabase.AddObjectToAsset(combinedMesh, $"Assets/{MESHES_PATH}/{mData.meshFileName}");
                gameObject.AddComponent<MeshFilter>().mesh = combinedMesh;
                //AssetDatabase.AddObjectToAsset(combinedMesh, gameObject);
                //for (int i = 0; i < meshes.Length; i++)
                //{
                //    MeshFilter filter = gameObject.AddComponent<MeshFilter>();
                //    filter.mesh = meshes[i];
                //}
                //AssetDatabase.Create
                //AssetDatabase.DeleteAsset(relativeMeshPath);
                //EditorUtility.CopySerialized(combinedMesh, mesh);
                //AssetDatabase.SaveAssets();
                //AssetDatabase.Refresh();
            }
            else
            {
                MeshFilter filter = gameObject.AddComponent<MeshFilter>();
                filter.mesh = meshes[0];
            }
            string prefabPath = "Assets/" + PREFABS_PATH + $"/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
            //if (writeCombinedMesh)
            //{
            //}
            //BinaryFormatter formatter = new BinaryFormatter();
            //formatter
            //SavePrefab(gameObject, "Assets/" + PREFABS_PATH + $"/{prefabName}.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            //prefabs.Add(mData.meshFileName, "Assets/" + PREFABS_PATH + $"/{prefabName}.prefab");
            prefabs.Add(mData.meshFileName, prefab);
            UnityEngine.Object.DestroyImmediate(gameObject, true);
        }

        //Thread.Sleep(100);
        //foreach (var prefab in prefabs.Values)
        //{
        //    UnityEngine.Object.DestroyImmediate(prefab, true);
        //}
    }

    public void SavePrefab(GameObject obj, string path)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        FileStream file = File.Create(EditorRelative(path));
        formatter.Serialize(file, obj);
        file.Close();
    }

    public GameObject LoadGameObject(string path)
    {
        if (AssetImporter.GetAtPath(path) != null)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream file = File.Open(EditorRelative(path), FileMode.Open);
            GameObject loadedGameObject = (GameObject)formatter.Deserialize(file);
            file.Close();

            // Instantiate the loaded GameObject
            return GameObject.Instantiate(loadedGameObject);
        }
        return null;
    }

    public string EditorRelative(string path)
    {
        return Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
    }

    public void PlaceLevelObjects()
    {
        GameObject rootObject = new GameObject("RootLevel");
        //rootObject.transform.localRotation = Quaternion.Euler(0, -180, 0);
        rootObject.transform.localScale = new Vector3(-1, 1, 1);
        PlaceSpatial(levelSpatial, rootObject);
    }

    public void DEBUG_WriteRootStaticModelMatrices()
    {
        //// Static objects
        //foreach (var inst in levelSpatial.staticModelGroup.members)
        //{
        //    new Transform()
        //    SpawnObjectInstance(obj, inst);
        //}
    }


    public void PlaceSpatial(ULEBSpatial spatial, GameObject parent)
    {
        GameObject obj = new GameObject(spatial.name); 
        obj.transform.SetParent(parent.transform/*, false*/);
        //obj.transform.localPosition = new Vector3(-spatial.transform.Translation.x, spatial.transform.Translation.y, spatial.transform.Translation.z);
        
        obj.transform.localPosition = spatial.transform.Translation;
        obj.transform.localRotation = Quaternion.Euler(spatial.transform.Rotation);
        obj.transform.localScale = spatial.transform.Scale;

        // Children
        foreach (var child in spatial.children)
        {
            PlaceSpatial(child, obj);
        }
        // Static objects
        foreach (var inst in spatial.staticModelGroup.members)
        {
            SpawnObjectInstance(obj, inst);
        }
        // Object blueprints
        foreach (var inst in spatial.objectBlueprintReferences.instances)
        {
            SpawnObjectInstance(obj, inst);
        }
    }

    public void SpawnObjectInstance(GameObject parent, ULObjectInstance inst)
    {
        GameObject prefab;
        if (prefabs.TryGetValue(inst.objectBlueprint.meshPath, out GameObject o))
        {
            prefab = o; //PrefabUtility.LoadPrefabContents(path);
                        //prefab = LoadGameObject(path);
        }
        else
        {
            return;
        }

        // Fix direction
        //for (int i = 0; i < inst.transforms.Count; i++)
        //{
        //    inst.transforms[i] = new ULTransform(
        //        new Vector3(-inst.transforms[i].Translation.x, inst.transforms[i].Translation.y, inst.transforms[i].Translation.z),
        //        inst.transforms[i].Rotation,
        //        inst.transforms[i].Scale
        //        );
        //}
        foreach (var trans in inst.transforms)
        {
            //GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            GameObject obj = UnityEngine.Object.Instantiate(prefab, parent.transform/*, false*/); // SET LAST TO FALSE LATER ON
            obj.transform.localPosition = trans.Translation;
            obj.transform.right = trans.right / trans.Scale.x;
            obj.transform.up = trans.up / trans.Scale.y;
            obj.transform.forward = trans.forward / trans.Scale.z;

            //obj.transform.localEulerAngles = trans.Rotation;
            obj.transform.localScale = trans.Scale;
        }
        UnityEngine.Object.DestroyImmediate(prefab);
    }


    public string GetOriginalDirFile(string path)
    {
        return Path.Combine(levelDataDir.FullName, path);
    }

    public string GetMeshPath(string fileName) => Path.Combine(Application.dataPath, MESHES_PATH + $"/{fileName}").Replace('\\', '/').Replace(Application.dataPath, "Assets").Replace(".png", "").Replace("Assets/Resources/", "");
    public string GetMaterialFromTexture(string fileName) => (Path.ChangeExtension(Path.Combine(Application.dataPath, TEXTURES_PATH + $"/{fileName}").Replace('\\', '/').Replace(Application.dataPath, "Assets").Replace(".png", "").Replace("Assets/Resources/", ""), "").TrimEnd('.') + "_material.mat").Replace(Application.dataPath, "Assets");

    public string RelativePathToResourcesImport(string path) => Path.GetFileNameWithoutExtension(path.Replace("Assets/Resources/", "")); 

    public void RefreshAndEnsureImported(string relativePath)
    {
        AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
        Refresh();

        AssetImporter assetImporter = AssetImporter.GetAtPath(relativePath);
        //string resourcesPath = RelativePathToResourcesImport(relativePath);
        //Thread.Sleep(10);
        while (assetImporter == null)
        {
            assetImporter = AssetImporter.GetAtPath(relativePath);
            Thread.Sleep(5);
        }
    }

    public void Refresh()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }

    #region Import Mesh & Import Texture

    // this shit will never work
    // https://github.com/eastskykang/UnityMeshImporter/blob/master/Runtime/Scripts/MeshImporter.cs
    public /*string*/ Mesh ImportMesh(string path)
    {
        string fileName = Path.GetFileName(path);
        string newPath = Path.Combine(Application.dataPath, MESHES_PATH + $"/{fileName}").Replace('\\', '/');
        File.Copy(path, newPath, true);

        string meshPath = newPath.Replace(Application.dataPath, "Assets");// .Replace(".fbx", "").Replace("Assets/Resources/", "");
        string relativeMeshPath = newPath.Replace(Application.dataPath, "Assets");
        RefreshAndEnsureImported(relativeMeshPath);

        //var mesh = Resources.Load<Mesh>(meshPath);
        var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(relativeMeshPath);
        //var mesh = (Mesh)AssetDatabase.LoadAssetAtPath(relativeMeshPath, typeof(Mesh));
        //var mesh = (Mesh)AssetDatabase.LoadAssetAtPath(relativeMeshPath, typeof(Mesh));
        if (mesh == null)
        {
            Debug.Log("IM GOING TO GO FUCKING INSANE THIS SHIT ASS FUCKING GAME ENIGNE ");
        }
        if (((mesh.uv2 != null && mesh.uv2.Length > 0) && (mesh.uv2.Length == mesh.vertexCount))
            //&& (mesh.uv2[0] != mesh.uv2[1])
            )
        {
            // Weird issue where some meshes have UVs as 0,0 so small check here to avoid that
            if (mesh.uv2[0] != mesh.uv2[1])
                mesh.uv = mesh.uv2;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Combine meshes
        //if (mesh.subMeshCount > 1)
        //{
        //    CombineInstance[] combine = new CombineInstance[mesh.subMeshCount];
        //    for (int i = 0; i < mesh.subMeshCount; i++)
        //    {
        //        combine[i].mesh = mesh;
        //        combine[i].subMeshIndex = i;
        //    }
        //    Mesh combinedMesh = new Mesh();
        //    combinedMesh.CombineMeshes(combine, true, false);
        //    mesh = combinedMesh;
        //    //AssetDatabase.DeleteAsset(relativeMeshPath);
        //    EditorUtility.CopySerialized(combinedMesh, mesh);
        //    AssetDatabase.SaveAssets();
        //    AssetDatabase.Refresh();
        //}


        // overwrite the old one
        //AssetDatabase.DeleteAsset(relativeMeshPath);
        //AssetDatabase.CreateAsset(mesh, relativeMeshPath);

        //return relativeMeshPath;
        return mesh;
        //return new Mesh();
    }
    public string /*Material*/ ImportTexture(string path)
    {
        string fileName = Path.GetFileName(path);
        string newPath = Path.Combine(Application.dataPath, TEXTURES_PATH + $"/{fileName}").Replace('\\', '/');
        File.Copy(path, newPath, true);
        var material = new Material(Shader.Find("Standard")); // Standard   HDRP/Lit
        string texturePath = newPath.Replace(Application.dataPath, "Assets").Replace(".png", "").Replace("Assets/Resources/", "");
        RefreshAndEnsureImported(newPath.Replace(Application.dataPath, "Assets"));
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath.Replace(Application.dataPath, "Assets"));// Resources.Load<Texture2D>(texturePath);
        material.SetTexture("_MainTex", texture); // _MainTex           _BaseColorMap
        string materialFilePath = (Path.ChangeExtension(newPath, "").TrimEnd('.') + "_material.mat").Replace(Application.dataPath, "Assets");
        AssetDatabase.CreateAsset(material, materialFilePath);
        Refresh();
        //return material;
        return materialFilePath;
    }
    #endregion

    #region Read Level Data

    public void ReadData(FileInfo file)
    {
        UnityXmlReader reader = new UnityXmlReader(file.OpenRead());
        //levelDataDir = new DirectoryInfo(reader.ReadElement("LevelDataPath"));
        reader.ReadElement("LevelDataPath");
        levelDataDir = file.Directory;
        ReadMeshData(reader);
        ReadLevelData(reader);
    }

    public void ReadMeshData(UnityXmlReader reader)
    {
        meshData = reader.ReadList("MeshData", (n) => ULMeshData.Read(reader, n)/*, "mesh"*/);
    }
    public void ReadLevelData(UnityXmlReader reader)
    {
        levelSpatial = ULEBSpatial.Read(reader, "rootLevel");
    }

    #endregion
}
