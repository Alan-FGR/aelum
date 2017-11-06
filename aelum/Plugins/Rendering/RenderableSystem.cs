using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace aelum
{
   public abstract class RenderableConsumer : Consumer<RenderableSystem, RenderablePlugin>
   {
      public void Render(RectF worldRect, RenderTarget2D target)
      {
         foreach (RenderableSystem system in Systems)
         {
            foreach (RenderablePlugin plugin in system.GetFromRect(worldRect))
            {
               
            }
         }
      }
   }

   public abstract class RenderableSystem : SpatialPluginSystem<RenderablePlugin>{}

   public abstract class RenderablePlugin : SpatialPlugin{
      protected RenderablePlugin(Node node) : base(node){}
   }



   public class Camera : Plugin
   {
      public class CameraBuffer
      {
         public RenderTarget2D renderTarget;
         public RenderableConsumer consumer;
         public CameraBuffer(RenderableConsumer consumer)
         {
            renderTarget = new RenderTarget2D(Engine.Graphics.Device, Engine.Graphics.Viewport.Width, Engine.Graphics.Viewport.Height);
            this.consumer = consumer;
         }
         public void Render(RectF camRect)
         {
            consumer.Render(camRect, renderTarget);
         }
      }
      
      private readonly List<CameraBuffer> cameraBuffers_ = new List<CameraBuffer>();
      private RectF viewPortDestRect_ = new RectF();

      public void AddSystemToRender(RenderableConsumer systemConsumer)
      {
         cameraBuffers_.Add(new CameraBuffer(systemConsumer));
      }

      public void RenderAllBuffers()
      {
         foreach (CameraBuffer buffer in cameraBuffers_)
         {
            buffer.Render(new RectF(Node.Position, Vector2.One*10));
         }
      }
      
      public Camera(Node node) : base(node)
      {
      }
   }







}