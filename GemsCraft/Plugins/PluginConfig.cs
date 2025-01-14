﻿using System;
using System.IO;

namespace GemsCraft.Plugins
{
    public abstract class PluginConfig
    {
        public virtual void Save(IPlugin plugin, string file)
        {
            // Append plugin directory
            string realFile = "plugins/" + plugin.Name + "/" + file;
            PluginConfigSerializer.Serialize(realFile, this);
        }

        public virtual PluginConfig Load(IPlugin plugin, string file, Type type)
        {

            // Append plugin directory
            string realFile = "plugins/" + plugin.Name + "/" + file;

            if (!Directory.Exists("plugins/" + plugin.Name))
            {
                Directory.CreateDirectory("plugins/" + plugin.Name);
            }

            if (!File.Exists(realFile))
            {
                Save(plugin, file);
            }

            return PluginConfigSerializer.Deserialize(realFile, type);
        }
    }
}
