using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Xml;
using UnityLevelPlugin.Export;

namespace UnityLevelPlugin;

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
    public void WriteElement<T>(string name, T value) => WriteElement(name, value.ToString());
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
        if (list == null)
        {
            WriteStartElement(name, ("count", "0"));
            _writer.WriteEndElement();
            return;
        }
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
        WriteQuaterion(nameof(transform.Rotation), transform.Rotation);
        WriteVec3(nameof(transform.Scale), transform.Scale);
        _writer.WriteEndElement();
    }

    public void WriteVec3(string name, Vector3 v)
    {
        _writer.WriteStartElement(name);
        WriteElement(nameof(v.X), v.X);
        WriteElement(nameof(v.Y), v.Y);
        WriteElement(nameof(v.Z), v.Z);
        _writer.WriteEndElement();
    }
    public void WriteQuaterion(string name, Quaternion v)
    {
        _writer.WriteStartElement(name);
        WriteElement(nameof(v.X), v.X);
        WriteElement(nameof(v.Y), v.Y);
        WriteElement(nameof(v.Z), v.Z);
        WriteElement(nameof(v.W), v.W);
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
