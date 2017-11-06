namespace aelum
{
   public abstract class Plugin
   {
      public Node Node { get; }

      public virtual void FinalizePlugin(){}

      public virtual void EntityChanged(){}

      protected Plugin(Node node)
      {
         Node = node;
         node.RegisterPlugin(this);
      }
   }
}