using Microsoft.Xna.Framework;

namespace aelum
{
   class Program
   {
      static void Main(string[] args)
      {
         Scene s = new Scene();

         Node n = s.CreateNode(Vector2.Zero);

         QuadSystem bgQuads = s.CreateSystem<R>()
         bgQuads.AddPlugin(new QuadRenderer(n));
      }
   }
}