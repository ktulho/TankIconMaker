﻿using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using RT.Util.Xml;
using D = System.Drawing;
using W = System.Windows.Media;

namespace TankIconMaker
{
    /// <summary>
    /// Enables <see cref="XmlClassify"/> to save color properties as strings of a human-editable form.
    /// </summary>
    sealed class colorTypeOptions : XmlClassifyTypeOptions,
        IXmlClassifySubstitute<W.Color, string>,
        IXmlClassifySubstitute<D.Color, string>
    {
        public W.Color FromSubstitute(string instance)
        {
            return (this as IXmlClassifySubstitute<D.Color, string>).FromSubstitute(instance).ToColorWpf();
        }

        public string ToSubstitute(W.Color instance)
        {
            return ToSubstitute(instance.ToColorGdi());
        }

        D.Color IXmlClassifySubstitute<D.Color, string>.FromSubstitute(string instance)
        {
            try
            {
                if (!instance.StartsWith("#") || (instance.Length != 7 && instance.Length != 9))
                    throw new Exception();
                int alpha = instance.Length == 7 ? 255 : int.Parse(instance.Substring(1, 2), NumberStyles.HexNumber);
                int r = int.Parse(instance.Substring(instance.Length == 7 ? 1 : 3, 2), NumberStyles.HexNumber);
                int g = int.Parse(instance.Substring(instance.Length == 7 ? 3 : 5, 2), NumberStyles.HexNumber);
                int b = int.Parse(instance.Substring(instance.Length == 7 ? 5 : 7, 2), NumberStyles.HexNumber);
                return D.Color.FromArgb(alpha, r, g, b);
            }
            catch
            {
                return D.Color.Black; // XmlClassify doesn't currently let us specify "no value", so just use a random color
            }
        }

        public string ToSubstitute(D.Color instance)
        {
            return instance.A == 255 ? "#{0:X2}{1:X2}{2:X2}".Fmt(instance.R, instance.G, instance.B) : "#{0:X2}{1:X2}{2:X2}{3:X2}".Fmt(instance.A, instance.R, instance.G, instance.B);
        }
    }

    /// <summary>
    /// Filters lists of <see cref="MakerBase"/> objects before XmlClassify attempts to decode them, removing all
    /// entries pertaining to maker types that no longer exist in the assembly and hence can't possibly be instantiated.
    /// </summary>
    sealed class listMakerBaseOptions : XmlClassifyTypeOptions, IXmlClassifyProcessXml
    {
        public void XmlPreprocess(XElement xml)
        {
            foreach (var item in xml.Nodes().OfType<XElement>().Where(e => e.Name == "item").ToArray())
            {
                var type = item.Attribute("type");
                if (type == null)
                    item.Remove();
                else if (!Program.MakerConstructors.Any(c => c.DeclaringType.Name == type.Value || c.DeclaringType.FullName == type.Value))
                    item.Remove();
            }
        }

        public void XmlPostprocess(XElement xml)
        {
        }
    }
}