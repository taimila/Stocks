// SPDX-FileCopyrightText: 2023 Nick Logozzo
// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Xml;

// Borrowed from https://github.com/NickvisionApps/Denaro/blob/main/NickvisionMoney.GNOME/Helpers/Builder.cs
public class Builder
{
    public static Gtk.Builder FromFile(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        using var reader = new StreamReader(stream!);

        var uiContents = reader.ReadToEnd();
        var xml = new XmlDocument();
        xml.LoadXml(uiContents);
        
        var elements = xml.GetElementsByTagName("*");

        foreach (XmlElement element in elements)
        {
            if (element.HasAttribute("translatable"))
            {
                element.RemoveAttribute("translatable");

                if (element.HasAttribute("context"))
                {
                    var context = element.GetAttribute("context");
                }
                else
                {
                    element.InnerText = _(element.InnerText);
                }
            }
        }

        return Gtk.Builder.NewFromString(xml.OuterXml, -1);
    }
}