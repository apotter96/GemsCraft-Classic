﻿using System;
using System.IO;

namespace GemsCraft.Plugins
{
    public abstract class PluginConfig
    {
        public virtual void Save(IPlugin plugin, String file)
        {
            // Append plugin directory
            String realFile = "plugins/" + plugin.Name + "/" + file;
            PluginConfigSerializer.Serialize(realFile, this);
        }

        public virtual PluginConfig Load(IPlugin plugin, String file, Type type)
        {
            // Append plugin directory
            String realFile = "plugins/" + plugin.Name + "/" + file;

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