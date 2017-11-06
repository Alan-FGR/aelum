using System.Collections.Generic;

namespace aelum
{
   public abstract class SpatialPluginSystem<TPlugin> : PluginSystem<TPlugin> where TPlugin : SpatialPlugin
   {
      public List<TPlugin> GetFromRect(RectF worldRect)
      {
         return plugins;//TODO
      }
   }

   public abstract class SpatialPlugin : Plugin
   {
      protected SpatialPlugin(Node node) : base(node){}
   }
}