﻿using System;
using System.IO;
using System.Xml.Serialization;

namespace GemsCraft.Plugins
{
    public class PluginConfigSerializer
    {
        public static void Serialize(String configFile, PluginConfig config)
        {
            XmlSerializer xs = new XmlSerializer(config.GetType());
            StreamWriter writer = File.CreateText(configFile);
            xs.Serialize(writer, config);
            writer.Flush();
            writer.Close();
        }

        public static PluginConfig Deserialize(String configFile, Type type)
        {
            XmlSerializer xs = new XmlSerializer(type);
            StreamReader reader = File.OpenText(configFile);
            PluginConfig c = (PluginConfig)xs.Deserialize(reader);
            reader.Close();
            return c;
        }
    }
}
