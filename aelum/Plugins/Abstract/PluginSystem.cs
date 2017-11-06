using System.Collections.Generic;

namespace aelum
{
   public abstract class PluginSystemUntyped{}

   public abstract class PluginSystem<TPlugin> : PluginSystemUntyped where TPlugin : Plugin
   {
      protected List<TPlugin> plugins;

      internal void AddPlugin(TPlugin plugin)
      {
         plugins.Add(plugin);
      }
   }
}