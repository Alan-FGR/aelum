using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace aelum
{

   class Program
   {
      static void Main(string[] args)
      {
         Scene s = new Scene();

         Node n = s.CreateNode(Vector2.Zero);

         QuadSystem bgQuads = s.CreateSystem<QuadSystem>();
         bgQuads.AddPlugin(new Quad(n));
      }
   }


   class Scene
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

   abstract class PluginSystemUntyped
   {
   }

   abstract class PluginSystem<TPlugin> : PluginSystemUntyped where TPlugin : Plugin
   {
      protected List<TPlugin> plugins;

      internal void AddPlugin(TPlugin plugin)
      {
         plugins.Add(plugin);
      }
   }

   abstract class SpatialPluginSystem<TPlugin> : PluginSystem<TPlugin> where TPlugin : SpatialPlugin<TPlugin>
   {

   }

   abstract class SpatialPlugin<T> : Plugin where T : SpatialPlugin<T>
   {
      private SpatialPluginSystem<T> system_;

      protected SpatialPlugin(Node node) : base(node)
      {
      }
   }

   class RenderableSystem<T> : PluginSystem<T> where T : RenderablePlugin
   {

   }

   class RenderablePlugin : Plugin
   {
      public RenderablePlugin(Node node) : base(node)
      {
      }
   }


   class QuadSystem : PluginSystem<Quad>
   {

   }

   class Quad : Plugin
   {
      public Quad(Node node) : base(node)
      {
      }
   }

   public class Node
   {

      #region Constructors

      internal Node(Vector2 position, float rotation, List<Plugin> plugins = null) // def constr
      {
#if ORIGIN_SHIFT
        LastPosition = new Vector2(float.MinValue, float.MaxValue);
        #endif

         position_ = position;
         rotation_ = rotation;

         //        if (plugins != null)
         //        {
         //            foreach (Plugin data in plugins)
         //            {
         ////                Plugin.CreateFromData(this, data); TODO
         //            }
         //        }

         InformSpatialChange();
      }
      // overloads for convenience, REMEMBER to always call def from them
      //    public Node(Vector2 position) : this(position, 0){} TODO
      //    public Node(Vector2 position, Vector2 direction) : this(position, MathUtils.DirectionToAngle(direction)){}
      //    public Node() : this(Vector2.Zero, 0){}

      #endregion


      #region Spatial

      private Vector2 position_;
      private float rotation_;

      public Vector2 Position
      {
         get => position_;
         set
         {
#if ORIGIN_SHIFT      
            LastPosition = position_;
#endif
            position_ = value;
            InformSpatialChange();
         }
      }

      public float Rotation
      {
         get => rotation_;
         set
         {
            rotation_ = value;
            InformSpatialChange();
         }
      }

      public Vector2 Direction
      {
         get => Math.Utils.AngleToDirection(rotation_);
         set => Rotation = Math.Utils.DirectionToAngle(value);
      }


      public void SetPositionAndRotation(Vector2 position, float rotation)
      {
#if ORIGIN_SHIFT      
        LastPosition = position_;
#endif
         position_ = position;
         rotation_ = rotation;
         InformSpatialChange();
      }

      public void SetPositionAndDirection(Vector2 position, Vector2 direction)
      {
#if ORIGIN_SHIFT      
        LastPosition = position_;
#endif
         position_ = position;
         Direction = direction; //this will call Rotation which will call InformSpatialChange
      }

      private void InformSpatialChange()
      {
#if ORIGIN_SHIFT
        EntityChunkRegionSystem.UpdateChunkSystemForEntity(this);
#endif
         foreach (Plugin plugin in plugins)
            plugin.EntityChanged();
      }

      #endregion


      #region Plugins

      private List<Plugin> plugins;

      ///<summary> Internal call only </summary>
      internal void RegisterPlugin(Plugin plugin)
      {
         plugins.Add(plugin);
      }

      public void RemovePlugin(Plugin plugin)
      {
         if (plugins.Contains(plugin))
         {
            plugins.Remove(plugin);
            plugin.FinalizeComponent();
         }
      }

      public T GetPlugin<T>() where T : Plugin
      {
         foreach (Plugin c in plugins)
         {
            T cast = c as T;
            if (cast != null) return cast;
         }
         return null;
      }

      public bool GetPlugin<T>(out T plugin) where T : Plugin
      {
         plugin = GetPlugin<T>();
         return plugin != null;
      }
      
      #endregion


      // uncommon members
      public bool persistent = true
         ; // not all entities should be saved to disk, e.g.: parented entities are saved by their parents, bullets

   }

   public abstract class Plugin
   {
      public Node Node { get; }

      public virtual void FinalizeComponent()
      {
      }

      public virtual void EntityChanged()
      {
      }

      protected Plugin(Node node)
      {
         Node = node;
         node.RegisterPlugin(this);
      }
   }

}