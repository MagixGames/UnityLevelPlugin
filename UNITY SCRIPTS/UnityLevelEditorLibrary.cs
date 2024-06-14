#region __usings
using System.IO;
using System.Xml;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
#endregion
#region UnityXmlWriter
public class UnityXmlWriter
{
    private XmlWriter _writer;

    public UnityXmlWriter(Stream outStream)
    {
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
        };
        _writer = XmlWriter.Create(outStream, settings);
        _writer.WriteStartDocument();
    }


    public void WriteStartElement(string s) => _writer.WriteStartElement(s);
    public void WriteEndElement() => _writer.WriteEndElement();
    public void WriteAttributeString(string name, string value) => _writer.WriteAttributeString(name, value);
    public void WriteValue(object o) => _writer.WriteValue(o);

    #region -- Extensions --

    public void WriteElement(string name, string value)
    {
        _writer.WriteStartElement(name);
        _writer.WriteValue(value);
        _writer.WriteEndElement();
    }
    public void WriteElement(string name, float value) => WriteElement(name, value.ToString());
    public void WriteElement(string name, ULTransform value) => WriteTransform(name, value);
    public void WriteElement(string name, string value, params (string, object)[] attributes)
    {
        _writer.WriteStartElement(name);
        foreach (var attr in attributes)
        {
            _writer.WriteAttributeString(attr.Item1, attr.Item2.ToString());
        }
        _writer.WriteValue(value);
        _writer.WriteEndElement();
    }
    public void WriteStartElement(string name, params (string, object)[] attributes)
    {
        _writer.WriteStartElement(name);
        foreach (var attr in attributes)
        {
            _writer.WriteAttributeString(attr.Item1, attr.Item2.ToString());
        }
    }

    public void WriteList<T>(string name, List<T> list)
    {
        WriteStartElement(name, ("count", list.Count.ToString()));
        foreach (var obj in list)
        {
            WriteElement("item", obj.ToString());
        }
        _writer.WriteEndElement();
    }
    public void WriteList<T>(string name, List<T> list, Action<T, string> writer)
    {
        WriteStartElement(name, ("count", list.Count.ToString()));
        foreach (var obj in list)
        {
            writer(obj, "item");
        }
        _writer.WriteEndElement();
    }
    public void WriteTransformsList(string name, List<ULTransform> transforms)
    {
        WriteStartElement(name, ("count", transforms.Count.ToString()));
        foreach (var obj in transforms)
        {
            WriteElement("item", obj);
        }
        _writer.WriteEndElement();
    }

    public void WriteTransform(string name, ULTransform transform)
    {
        _writer.WriteStartElement(name);
        WriteVec3(nameof(transform.Translation), transform.Translation);
        WriteVec3(nameof(transform.Rotation), transform.Rotation);
        WriteVec3(nameof(transform.Scale), transform.Scale);
        _writer.WriteEndElement();
    }

    public void WriteVec3(string name, Vector3 v)
    {
        _writer.WriteStartElement(name);
        WriteElement(nameof(v.x), v.x);
        WriteElement(nameof(v.y), v.y);
        WriteElement(nameof(v.z), v.z);
        _writer.WriteEndElement();
    }

    #endregion

    public void End()
    {
        _writer.WriteEndDocument();
        _writer.Flush();
        _writer.Close();
    }
}
#endregion
#region UnityXmlReader
public class UnityXmlReader
{
    private XmlReader _reader;

    public UnityXmlReader(Stream inStream)
    {
        XmlReaderSettings settings = new XmlReaderSettings
        {
            ConformanceLevel = ConformanceLevel.Fragment,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true,
            DtdProcessing = DtdProcessing.Ignore
        };
        _reader = XmlReader.Create(inStream, settings);
        _reader.MoveToContent();
        _reader.ReadStartElement();
    }

    public void ReadStartElement(string name) => _reader.ReadStartElement(name);
    public void ReadStartElement(string name, out Dictionary<string, string> attr)
    {
        //_reader.ReadStartElement(name);
        //attr = ReadAttributes();
        ReadJustStartOfElement(name);
        attr = ReadAttributes();
        //_reader.MoveToElement();
        ReadStartElement(name);
    }
    public void ReadEndElement() => _reader.ReadEndElement();

    #region -- Reading Types --

    public int ReadElementInt32(string name) => Convert.ToInt32(_reader.ReadElementContentAsString());
    public short ReadElementInt16(string name) => Convert.ToInt16(_reader.ReadElementContentAsString());
    public byte ReadElementByte(string name) => Convert.ToByte(_reader.ReadElementContentAsString());
    public float ReadElementFloat(string name) => Convert.ToSingle(_reader.ReadElementContentAsString());
    public string ReadElementString(string name) => _reader.ReadElementContentAsString();
    public string ReadElement(string name) => _reader.ReadElementContentAsString();

    public Vector3 ReadElementVector3(string name)
    {
        Vector3 vec = new Vector3();
        ReadStartElement(name);
        vec.x = ReadElementFloat(nameof(Vector3.x));
        vec.y = ReadElementFloat(nameof(Vector3.y));
        vec.z = ReadElementFloat(nameof(Vector3.z));
        ReadEndElement();
        return vec;
    }

    public ULTransform ReadElementTransform(string name)
    {
        ULTransform t = new ULTransform();
        ReadStartElement(name);
        t.right = ReadElementVector3(nameof(ULTransform.right));
        t.up = ReadElementVector3(nameof(ULTransform.up));
        t.forward = ReadElementVector3(nameof(ULTransform.forward));
        t.Translation = ReadElementVector3(nameof(ULTransform.Translation));
        t.Rotation = ReadElementVector3(nameof(ULTransform.Rotation));
        t.Scale = ReadElementVector3(nameof(ULTransform.Scale));
        ReadEndElement();
        return t;
    }

    #region -- Lists --

    public List<T> ReadList<T>(string name, Func<string, T> read, string itemName = "item")
    {
        List<T> list;
        Dictionary<string, string> attrs;
        ReadStartElement(name, out attrs);
        int count = Convert.ToInt32(attrs["count"]);
        list = new List<T>(count);
        if (count == 0)
        {
            return list;
        }
        for (int i = 0; i < count; i++)
        {
            list.Add(read(itemName));
        }
        ReadEndElement();
        return list;
    }

    #endregion

    #endregion

    #region -- Helpers --

    public Dictionary<string, string> ReadAttributes(int expectedAmount = -1)
    {
        Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!_reader.HasAttributes) return attributes;

        while (_reader.MoveToNextAttribute())
        {
            attributes.Add(_reader.Name, _reader.Value);
        }
        _reader.MoveToElement();

        if (expectedAmount != -1 && expectedAmount != attributes.Count)
        {
            throw new XmlException("Inconsistent amount of attributes");
        }

        return attributes;
    }
    public void ReadJustStartOfElement(string expectedName)
    {
        if (_reader.NodeType == XmlNodeType.Element && _reader.Name == expectedName)
        {
            return;
        }
        _reader.Read();
        if (_reader.NodeType != XmlNodeType.Element)
        {
            throw new XmlException("Not start of element");
        }
        if (_reader.Name != expectedName)
        {
            throw new XmlException("Not the expected name");
        }
    }
    #endregion
}
#endregion
#region Structs
#region -- Types --
public struct ULTransform
{
    public Vector3 right;
    public Vector3 up;
    public Vector3 forward;
    public Vector3 Translation;
    public Vector3 Rotation;
    public Vector3 Scale;
}
public struct ULObjectInstance
{
    public List<ULTransform> transforms;
    public List<string> objectVariations;
    public ULObjectBlueprint objectBlueprint;

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
#endregion