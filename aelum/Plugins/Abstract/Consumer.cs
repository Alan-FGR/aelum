using System;
using System.Collections.Generic;

namespace aelum
{
   public abstract class Consumer<TSystem, TPlugin> where TPlugin : Plugin where TSystem : PluginSystem<TPlugin>
   {
//      public List<WeakReference<PluginSystem<TPlugin>>> systems_ = new List<WeakReference<PluginSystem<TPlugin>>>();
//      public void AddSystemWeakRef(PluginSystem<TPlugin> system)
//      {
//         systems_.Add(new WeakReference<PluginSystem<TPlugin>>(system));
//      }
      public List<TSystem> Systems = new List<TSystem>(); //TODO weak ref
   }


}