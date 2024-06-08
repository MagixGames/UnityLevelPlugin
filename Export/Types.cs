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
        public Vector3 Translation;
        public Vector3 Rotation;
        public Vector3 Scale;
        public ULTransform()
        {
            Translation = new Vector3();
            Rotation = new Vector3();
            Scale = new Vector3(1);
        }
        public ULTransform(Vector3 translation, Vector3 rotation, Vector3 scale)
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
            ULTransform trns = new ULTransform();
            trns.Translation = new Vector3();
            trns.Rotation = new Vector3();
            trns.Scale = new Vector3();

            Matrix4x4.Decompose(matrix, out System.Numerics.Vector3 scale, out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 translation);
            //System.Numerics.Vector3 euler = ToNumericsVec3(SharpDXUtils.ExtractEulerAngles(ToSharpDXMatrix(matrix)));
            System.Numerics.Vector3 euler = rotation.ToEuler();
            trns.Translation.X = translation.X;
            trns.Translation.Y = translation.Y;
            trns.Translation.Z = translation.Z;

            trns.Scale.X = scale.X;
            trns.Scale.Y = scale.Y;
            trns.Scale.Z = scale.Z;

            trns.Rotation.X = euler.X;
            trns.Rotation.Y = euler.Y;
            trns.Rotation.Z = euler.Z;

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

        public static ULTransform FromLinearTransform(FrostySdk.Ebx.LinearTransform linTrans)
        {
            Matrix4x4 matrix = new Matrix4x4(
                    linTrans.right.x, linTrans.right.y, linTrans.right.z, 0.0f,
                    linTrans.up.x, linTrans.up.y, linTrans.up.z, 0.0f,
                    linTrans.forward.x, linTrans.forward.y, linTrans.forward.z, 0.0f,
                    linTrans.trans.x, linTrans.trans.y, linTrans.trans.z, 1.0f
                    );

            ULTransform trns = new ULTransform();

            Matrix4x4.Decompose(matrix, out System.Numerics.Vector3 scale, out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 translation);
            //System.Numerics.Vector3 euler = ToNumericsVec3(SharpDXUtils.ExtractEulerAngles(ToSharpDXMatrix(matrix)));
            System.Numerics.Vector3 euler = rotation.ToEuler();

            trns.Translation.X = translation.X;
            trns.Translation.Y = translation.Y;
            trns.Translation.Z = translation.Z;

            trns.Scale.X = scale.X;
            trns.Scale.Y = scale.Y;
            trns.Scale.Z = scale.Z;

            trns.Rotation.X = euler.X;
            trns.Rotation.Y = euler.Y;
            trns.Rotation.Z = euler.Z;

            return trns;
        }

        public LinearTransform ToLinearTransform()
        {
            float val = (float)(Math.PI / 180.0);
            LinearTransform transform = new LinearTransform();
            Matrix4x4 m = Matrix4x4.CreateRotationX(Rotation.X * val) * Matrix4x4.CreateRotationY(Rotation.Y * val) * Matrix4x4.CreateRotationZ(Rotation.Z * val);
            m *= Matrix4x4.CreateScale(Scale.X, Scale.Y, Scale.Z);

            transform.trans.x = transform.Translate.x;
            transform.trans.y = transform.Translate.y;
            transform.trans.z = transform.Translate.z;

            transform.right.x = m.M11;
            transform.right.y = m.M12;
            transform.right.z = m.M13;

            transform.up.x = m.M21;
            transform.up.y = m.M22;
            transform.up.z = m.M23;

            transform.forward.x = m.M31;
            transform.forward.y = m.M32;
            transform.forward.z = m.M33;
            return transform;
        }
    }
    public struct ULObjectInstance
    {
        public List<ULTransform> transforms;
        public List<string> objectVariations;
        public ULObjectBlueprint objectBlueprint;

        public ULObjectInstance()
        {
            transforms = new List<ULTransform>();
            objectVariations = new List<string>();
            objectBlueprint = new ULObjectBlueprint();
        }
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
        //public string texturePath;

        public ULObjectBlueprint(string meshPath/*, string texturePath*/)
        {
            this.meshPath = meshPath;
            //this.texturePath = texturePath;
        }

        public void Write(UnityXmlWriter writer, string name)
        {
            writer.WriteStartElement(name);
            writer.WriteElement(nameof(meshPath), meshPath);
            writer.WriteEndElement();
        }
        public static ULObjectBlueprint Read(UnityXmlReader reader, string name)
        {
            ULObjectBlueprint inst = new ULObjectBlueprint();
            reader.ReadStartElement(name);
            inst.meshPath = reader.ReadElementString(nameof(meshPath));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULTextureParameter
    {
        public string parameter;
        public string fileName;

        public ULTextureParameter(string parameter, string fileName)
        {
            this.parameter = parameter;
            this.fileName = fileName;
        }
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

        public ULObjectVariation(string parameter, string fileName)
        {
            this.parameter = parameter;
            this.fileName = fileName;
        }
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

        public ULMeshVariationInfo(string parameter, string fileName)
        {
            this.parameter = parameter;
            this.fileName = fileName;
        }
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

        public ULMeshData()
        {
            textureParameters = new List<ULTextureParameter>();
        }
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
        public List<ULObjectInstance> members;

        public ULEStaticModelGroup()
        {
            members = new List<ULObjectInstance>();
        }
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
            inst.members = reader.ReadList(nameof(members), (n) => ULObjectInstance.Read(reader, n));
            reader.ReadEndElement();
            return inst;
        }
    }
    public struct ULEObjectBlueprintReferences
    {
        public List<ULObjectInstance> instances;

        public ULEObjectBlueprintReferences()
        {
            instances = new List<ULObjectInstance>();
        }

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
    public struct ULEBSpatial
    {
        public string name;
        public ULTransform transform;
        public ULEStaticModelGroup staticModelGroup;
        public ULEObjectBlueprintReferences objectBlueprintReferences;
        public List<ULEBSpatial> children;

        public ULEBSpatial()
        {
            transform = new ULTransform();
            staticModelGroup = new ULEStaticModelGroup();
            objectBlueprintReferences = new ULEObjectBlueprintReferences();
            children = new List<ULEBSpatial>();
        }

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
