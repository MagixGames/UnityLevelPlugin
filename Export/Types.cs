using Frosty.Core.Controls.Editors;
using Frosty.Core.Viewport;
using FrostySdk.Ebx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UnityLevelPlugin.Tools;
using Matrix = SharpDX.Matrix;

// Bunch of structs basically
namespace UnityLevelPlugin.Export
{
    #region -- Types --
    public struct ULTransform
    {
        // TEMP ?

        public Vector3 Translation;
        public Quaternion Rotation;
        public Vector3 Scale;
#if FROSTY
        public ULTransform()
        {
            Translation = new Vector3();
            Rotation = new Quaternion();
            Scale = new Vector3(1);
        }
        public ULTransform(Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            Translation = translation;
            Rotation = rotation;
            Scale = scale;
        }
        public override string ToString()
        {
            return Translation.ToString() + Rotation.ToString() + Scale.ToString();
        }

        public static ULTransform operator +(ULTransform left, ULTransform right)
        {
            return new ULTransform(left.Translation + right.Translation, left.Rotation + right.Rotation, left.Scale * right.Scale);
        }


        private static Matrix ToSharpDXMatrix(Matrix4x4 matrix) => new Matrix(
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        private static System.Numerics.Vector3 ToNumericsVec3(SharpDX.Vector3 vec) => new System.Numerics.Vector3(vec.X, vec.Y, vec.Z);

        public static ULTransform FromMatrix4x4(Matrix4x4 matrix)
        {
            matrix = new Matrix4x4(
                matrix.M11, -matrix.M12, -matrix.M13, matrix.M14,
                -matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                -matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                -matrix.M41, matrix.M42, matrix.M43, matrix.M44
                );
            //var matrix2 = new Matrix4x4(
            //    matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            //    matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            //    matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            //    matrix.M31, matrix.M32, matrix.M33, matrix.M44
            //    );
            //matrix = Matrix4x4.Transpose( matrix );

            ULTransform trns = new ULTransform();
            trns.Translation = new Vector3();
            trns.Rotation = new Quaternion();
            trns.Scale = new Vector3();

            Matrix4x4.Decompose(matrix, out System.Numerics.Vector3 scale, out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 translation);

            trns.Translation.X = translation.X;
            trns.Translation.Y = translation.Y;
            trns.Translation.Z = translation.Z;

            trns.Scale.X = scale.X;
            trns.Scale.Y = scale.Y;
            trns.Scale.Z = scale.Z;

            trns.Rotation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);

            return trns;
        }

        public static ULTransform FromMatrixLinearTransform(Matrix4x4 m)
        {
            LinearTransform trns = new LinearTransform();
            trns.right = new FrostySdk.Ebx.Vec3() { x = m.M11, y = m.M12, z = m.M13 };
            trns.up = new FrostySdk.Ebx.Vec3() { x = m.M21, y = m.M22, z = m.M23 };
            trns.forward = new FrostySdk.Ebx.Vec3() { x = m.M31, y = m.M32, z = m.M33 };
            trns.trans = new FrostySdk.Ebx.Vec3() { x = m.M41, y = m.M42, z = m.M43 };
            return FromLinearTransform(trns);
        }

        public static ULTransform FromLinearTransform(FrostySdk.Ebx.LinearTransform lt)
        {
            Matrix4x4 matrix = new Matrix4x4(
                    lt.right.x, lt.right.y, lt.right.z, 0.0f,
                    lt.up.x, lt.up.y, lt.up.z, 0.0f,
                    lt.forward.x, lt.forward.y, lt.forward.z, 0.0f,
                    lt.trans.x, lt.trans.y, lt.trans.z, 1.0f
                    );
            return ULTransform.FromMatrix4x4(matrix);
        }

        public LinearTransform ToLinearTransform()
        {
            float val = (float)(Math.PI / 180.0);
            LinearTransform transform = new LinearTransform();
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(Rotation);
            m *= Matrix4x4.CreateScale(Scale.X, Scale.Y, Scale.Z);

            transform.right.x = m.M11;
            transform.right.y = -m.M12;
            transform.right.z = -m.M13;

            transform.up.x = -m.M21;
            transform.up.y = m.M22;
            transform.up.z = m.M23;

            transform.forward.x = -m.M31;
            transform.forward.y = m.M32;
            transform.forward.z = m.M33;

            transform.trans.x = -Translation.X;
            transform.trans.y = Translation.Y;
            transform.trans.z = Translation.Z;


            // for the negatations
            //matrix = new Matrix4x4(
            //    matrix.M11, -matrix.M12, -matrix.M13, matrix.M14,
            //    -matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            //    -matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            //    -matrix.M41, matrix.M42, matrix.M43, matrix.M44
            //    );

            return transform;
        }
#endif
    }
    public struct ULObjectInstance
    {
        public List<ULTransform> transforms;
        public List<string> objectVariations;
        public ULObjectBlueprint objectBlueprint;

#if FROSTY
        public ULObjectInstance()
        {
            transforms = new List<ULTransform>();
            objectVariations = new List<string>();
            objectBlueprint = new ULObjectBlueprint();
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteTransformsList(nameof(transforms), transforms);
            writer.WriteList(nameof(objectVariations), objectVariations);
            objectBlueprint.Write(writer, nameof(objectBlueprint));
            writer.WriteEndElement();
        }

        public static ULObjectInstance Read(UnityXmlReader reader, string name)
        {
            ULObjectInstance inst = new ULObjectInstance();
            reader.ReadStartElement(name);
            inst.transforms = reader.ReadList(nameof(transforms), (n) => reader.ReadElementTransform(n));
            inst.objectVariations = reader.ReadList(nameof(objectVariations), (n) => reader.ReadElementString(n));
            inst.objectBlueprint = ULObjectBlueprint.Read(reader, nameof(objectBlueprint));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULObjectBlueprint
    {
        public string meshPath;
        public string originalPath;

#if FROSTY
        public ULObjectBlueprint(string meshPath, string originalPath)
        {
            this.meshPath = meshPath;
            this.originalPath = originalPath;
        }
#endif

        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(meshPath), meshPath);
            writer.WriteElement(nameof(originalPath), originalPath);
            writer.WriteEndElement();
        }
        public static ULObjectBlueprint Read(UnityXmlReader reader, string name)
        {
            ULObjectBlueprint inst = new ULObjectBlueprint();
            reader.ReadStartElement(name);
            inst.meshPath = reader.ReadElementString(nameof(meshPath));
            inst.originalPath = reader.ReadElementString(nameof(originalPath));
            reader.ReadEndElement();
            return inst;
        }
    } 
    public struct ULStaticModelGroupObjectInstance
    {
        public List<ULTransform> transforms;
        public List<string> objectVariations;
        public ULStaticModelGroupMemberData objectData;

#if FROSTY
        public ULStaticModelGroupObjectInstance()
        {
            transforms = new List<ULTransform>();
            objectVariations = new List<string>();
            objectData = new ULStaticModelGroupMemberData();
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteTransformsList(nameof(transforms), transforms);
            writer.WriteList(nameof(objectVariations), objectVariations);
            objectData.Write(writer, nameof(objectData));
            writer.WriteEndElement();
        }

        public static ULStaticModelGroupObjectInstance Read(UnityXmlReader reader, string name)
        {
            ULStaticModelGroupObjectInstance inst = new ULStaticModelGroupObjectInstance();
            reader.ReadStartElement(name);
            inst.transforms = reader.ReadList(nameof(transforms), (n) => reader.ReadElementTransform(n));
            inst.objectVariations = reader.ReadList(nameof(objectVariations), (n) => reader.ReadElementString(n));
            inst.objectData = ULStaticModelGroupMemberData.Read(reader, nameof(objectData));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULStaticModelGroupMemberData
    {
        public string meshPath;
        public string meshFullPath;
        public string memberTypePath;
        public uint org_HealthStateEntityManagerId;
        public uint org_PhysicsPartCountPerInstance;
        public uint org_PartComponentCount;

        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(meshPath), meshPath);
            writer.WriteElement(nameof(meshFullPath), meshFullPath);
            writer.WriteElement(nameof(memberTypePath), memberTypePath);
            writer.WriteElement(nameof(org_HealthStateEntityManagerId), org_HealthStateEntityManagerId);
            writer.WriteElement(nameof(org_PhysicsPartCountPerInstance), org_PhysicsPartCountPerInstance);
            writer.WriteElement(nameof(org_PartComponentCount), org_PartComponentCount);
            writer.WriteEndElement();
        }
        public static ULStaticModelGroupMemberData Read(UnityXmlReader reader, string name)
        {
            ULStaticModelGroupMemberData inst = new ULStaticModelGroupMemberData();
            reader.ReadStartElement(name);
            inst.meshPath = reader.ReadElementString(nameof(meshPath));
            inst.meshFullPath = reader.ReadElementString(nameof(meshFullPath));
            inst.memberTypePath = reader.ReadElementString(nameof(memberTypePath));
            inst.org_HealthStateEntityManagerId = reader.ReadElementUInt32(nameof(org_HealthStateEntityManagerId));
            inst.org_PhysicsPartCountPerInstance = reader.ReadElementUInt32(nameof(org_PhysicsPartCountPerInstance));
            inst.org_PartComponentCount = reader.ReadElementUInt32(nameof(org_PartComponentCount));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULTextureParameter
    {
        public string parameter;
        public string fileName;

#if FROSTY
        public ULTextureParameter(string parameter, string fileName)
        {
            this.parameter = parameter;
            this.fileName = fileName;
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(parameter), parameter);
            writer.WriteElement(nameof(fileName), fileName);
            writer.WriteEndElement();
        }
        public static ULTextureParameter Read(UnityXmlReader reader, string name)
        {
            ULTextureParameter inst = new ULTextureParameter();
            reader.ReadStartElement(name);
            inst.parameter = reader.ReadElementString(nameof(parameter));
            inst.fileName = reader.ReadElementString(nameof(fileName));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULObjectVariation
    {
        public string parameter;
        public string fileName;

#if FROSTY
        public ULObjectVariation(string parameter, string fileName)
        {
            this.parameter = parameter;
            this.fileName = fileName;
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(parameter), parameter);
            writer.WriteElement(nameof(fileName), fileName);
            writer.WriteEndElement();
        }
        public static ULObjectVariation Read(UnityXmlReader reader, string name)
        {
            ULObjectVariation inst = new ULObjectVariation();
            reader.ReadStartElement(name);
            inst.parameter = reader.ReadElementString(nameof(parameter));
            inst.fileName = reader.ReadElementString(nameof(fileName));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULMeshVariationInfo
    {
        public string parameter;
        public string fileName;
#if FROSTY
        public ULMeshVariationInfo(string parameter, string fileName)
        {
            this.parameter = parameter;
            this.fileName = fileName;
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(parameter), parameter);
            writer.WriteElement(nameof(fileName), fileName);
            writer.WriteEndElement();
        }
        public static ULTextureParameter Read(UnityXmlReader reader, string name)
        {
            ULTextureParameter inst = new ULTextureParameter();
            reader.ReadStartElement(name);
            inst.parameter = reader.ReadElementString(nameof(parameter));
            inst.fileName = reader.ReadElementString(nameof(fileName));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULMeshData
    {
        public List<ULTextureParameter> textureParameters;
        public string meshFileName;

#if FROSTY
        public ULMeshData()
        {
            textureParameters = new List<ULTextureParameter>();
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteList(nameof(textureParameters), textureParameters, (i, n) => i.Write(writer, n));
            writer.WriteElement(nameof(meshFileName), meshFileName);
            writer.WriteEndElement();
        }
        public static ULMeshData Read(UnityXmlReader reader, string name)
        {
            ULMeshData inst = new ULMeshData();
            reader.ReadStartElement(name);
            inst.textureParameters = reader.ReadList(nameof(textureParameters), (n) => ULTextureParameter.Read(reader, n));
            inst.meshFileName = reader.ReadElementString(nameof(meshFileName));
            reader.ReadEndElement();
            return inst;
        }
    }

    #endregion

    #region -- Per Data Type Export Data --

    public struct ULEStaticModelGroup
    {
        public List<ULStaticModelGroupObjectInstance> members;

#if FROSTY
        public ULEStaticModelGroup()
        {
            members = new List<ULStaticModelGroupObjectInstance>();
        }
#endif
        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteList(nameof(members), members, (i, n) => i.Write(writer, n));
            writer.WriteEndElement();
        }
        public static ULEStaticModelGroup Read(UnityXmlReader reader, string name)
        {
            ULEStaticModelGroup inst = new ULEStaticModelGroup();
            reader.ReadStartElement(name);
            inst.members = reader.ReadList(nameof(members), (n) => ULStaticModelGroupObjectInstance.Read(reader, n));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULEObjectBlueprintReferences
    {
        public List<ULObjectInstance> instances;

#if FROSTY
        public ULEObjectBlueprintReferences()
        {
            instances = new List<ULObjectInstance>();
        }
#endif

        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteList(nameof(instances), instances, (i, n) => i.Write(writer, n));
            writer.WriteEndElement();
        }
        public static ULEObjectBlueprintReferences Read(UnityXmlReader reader, string name)
        {
            ULEObjectBlueprintReferences inst = new ULEObjectBlueprintReferences();
            reader.ReadStartElement(name);
            inst.instances = reader.ReadList(nameof(instances), (n) => ULObjectInstance.Read(reader, n));
            reader.ReadEndElement();
            return inst;
        }
    }

    // Qualified:
    //      LevelData
    //      SubLevelData
    //      LayerData
    //      DetachedSubLevelData (?)
    public struct ULEBSpatial
    {
        public string name;
        public ULTransform transform;
        public ULEStaticModelGroup staticModelGroup;
        public ULEObjectBlueprintReferences objectBlueprintReferences;
        public List<ULEBSpatial> children;
#if FROSTY
        public ULEBSpatial()
        {
            transform = new ULTransform();
            staticModelGroup = new ULEStaticModelGroup();
            objectBlueprintReferences = new ULEObjectBlueprintReferences();
            children = new List<ULEBSpatial>();
        }
#endif

        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(name), this.name);
            writer.WriteTransform(nameof(transform), transform);
            staticModelGroup.Write(writer, nameof(staticModelGroup));
            objectBlueprintReferences.Write(writer, nameof(objectBlueprintReferences));
            writer.WriteList(nameof(children), children, (i, n) => i.Write(writer, n));
            writer.WriteEndElement();
        }
        public static ULEBSpatial Read(UnityXmlReader reader, string name)
        {
            ULEBSpatial inst = new ULEBSpatial();
            reader.ReadStartElement(name);
            inst.name = reader.ReadElementString(nameof(name));
            inst.transform = reader.ReadElementTransform(nameof(transform));
            inst.staticModelGroup = ULEStaticModelGroup.Read(reader, nameof(staticModelGroup));
            inst.objectBlueprintReferences = ULEObjectBlueprintReferences.Read(reader, nameof(objectBlueprintReferences));
            inst.children = reader.ReadList(nameof(children), (i) => Read(reader, i));
            reader.ReadEndElement();
            return inst;
        }
    }

    #endregion
}
