using Frosty.Core;
using FrostySdk.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin;

// Built in frosty options stuff
public class ULOptions : OptionsExtension
{
    // notes: TD = To Dupe
    public static string ULE_StandardMeshTD_DEFAULT = "s2/objects/props/objectsets/_rebelalliance/gasflask_01/gasflask_01_mesh";
    public static string ULE_StandardMesh_ColorParam_DEFAULT = "_CS";
    public static string ULE_StandardMesh_NormalParam_DEFAULT = "_NAM_texcoord0";

    [Category("Import Options")]
    [DisplayName("Standard Mesh To Dupe Off")]
    [Description("Use this mesh path to dupe off. Supports CS and NAM only.")]
    public string ULE_StandardMeshTD { get; set; } = "s2/objects/props/objectsets/_rebelalliance/gasflask_01/gasflask_01_mesh";
    [Category("Import Options")]
    [DisplayName("Standard Mesh To Dupe Off")]
    [Description("Use this mesh path to dupe off. Supports CS and NAM only.")]
    public string ULE_StandardMesh_ColorParam { get; set; } = "_CS";
    [Category("Import Options")]
    [DisplayName("Standard Mesh To Dupe Off")]
    [Description("Use this mesh path to dupe off. Supports CS and NAM only.")]
    public string ULE_StandardMesh_NormalParam { get; set; } = "_NAM_texcoord0";

    [Category("Import Options")]
    [DisplayName("Emissive Mesh To Dupe Off")]
    [Description("DOES NOT SAVE AND WILL NOT WORK.")]
    public string ULE_EmissiveMeshTD { get; set; } = "PLACEHOLDER";

    public override void Load()
    {
        ULE_StandardMeshTD = Config.Get<string>("ULE_StandardMeshTD", ULE_StandardMeshTD_DEFAULT);
        ULE_StandardMesh_ColorParam = Config.Get<string>("ULE_StandardMesh_ColorParam", ULE_StandardMesh_ColorParam_DEFAULT);
        ULE_StandardMesh_NormalParam = Config.Get<string>("ULE_StandardMesh_NormalParam", ULE_StandardMesh_NormalParam_DEFAULT);
    }

    public override void Save()
    {
        Config.Add("ULE_StandardMeshTD", ULE_StandardMeshTD);
        Config.Add("ULE_StandardMesh_ColorParam", ULE_StandardMesh_ColorParam);
        Config.Add("ULE_StandardMesh_NormalParam", ULE_StandardMesh_NormalParam);
    }
}
