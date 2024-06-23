using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityLevelPlugin.Export;

namespace UnityLevelPlugin
{
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
            ReadJustStartOfElement(name);
            attr = ReadAttributes();
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
        public T ReadEnum<T>(string name) => (T)Enum.Parse(typeof(T), _reader.ReadElementContentAsString());

        public Vector3 ReadElementVector3(string name)
        {
            Vector3 vec = new Vector3();
            ReadStartElement(name);
            vec.X = ReadElementFloat(nameof(Vector3.X));
            vec.Y = ReadElementFloat(nameof(Vector3.Y));
            vec.Z = ReadElementFloat(nameof(Vector3.Z));
            ReadEndElement();
            return vec;
        }
        public Quaternion ReadElementQuaterion(string name)
        {
            Quaternion vec = new Quaternion();
            ReadStartElement(name);
            vec.X = ReadElementFloat(nameof(Quaternion.X));
            vec.Y = ReadElementFloat(nameof(Quaternion.Y));
            vec.Z = ReadElementFloat(nameof(Quaternion.Z));
            vec.W = ReadElementFloat(nameof(Quaternion.W));
            ReadEndElement();
            return vec;
        }

        public ULTransform ReadElementTransform(string name)
        {
            ULTransform t = new ULTransform();
            ReadStartElement(name);
            t.Translation = ReadElementVector3(nameof(ULTransform.Translation));
            t.Rotation = ReadElementQuaterion(nameof(ULTransform.Rotation));
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
}
