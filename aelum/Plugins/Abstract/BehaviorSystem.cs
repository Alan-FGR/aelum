namespace aelum
{
   public abstract class BehaviorSystem : PluginSystem<Behavior>
   {
      public void UpdatePlugins()
      {
         foreach (Behavior behavior in plugins)
         {
            behavior.Update();
         }
      }
   }

   public abstract class Behavior : Plugin
   {
      protected Behavior(Node node) : base(node)
      {}

      public void Update()
      {}
   }
}