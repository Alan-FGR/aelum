using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace aelum
{
   public class Scene
   {
      private List<Node> nodes;
      private List<PluginSystemUntyped> systems;

      public TSystem CreateSystem<TSystem>() where TSystem : PluginSystemUntyped, new()
      {
         TSystem system = new TSystem();
         systems.Add(system);
         return system;
      }

      public Node CreateNode(Vector2 position, float rotation = 0)
      {
         Node newNode = new Node(position, rotation);
         nodes.Add(newNode);
         return newNode;
      }

   }
}