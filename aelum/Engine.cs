using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace aelum
{

//   public class FinalBufferRenderData
//   {
//      public Texture2D texture;
//      public BlendState blendState = BlendState.AlphaBlend;
//      public Effect effect = null;
//      public Color color = Color.White;
//      public bool pixelPerfect = true;
//
//      public FinalBufferRenderData(Texture2D texture)
//      {
//         this.texture = texture;
//      }
//   }
//
//   partial class Engine
//   {
//      ///<summary>Object that represents the framebuffer to render all rendertargets into</summary>
//      private class FinalBuffer
//      {
//         private readonly SpriteBatch spriteBatch_;
//         private readonly BasicEffect basicEffect_;
//         private readonly Viewport viewport_;
//
//         internal FinalBuffer(GraphicsDevice device)
//         {
//            spriteBatch_ = new SpriteBatch(device);
//            basicEffect_ = new BasicEffect(device);
//            viewport_ = new Viewport();
//         }
//
//         public void RenderToFinalBuffer(FinalBufferRenderData data)
//         {
//            Rectangle destRect = viewport_.Bounds;
//            if (data.pixelPerfect)
//            {
//               //destRect = //TODO
//            }
//
//            spriteBatch_.Begin(SpriteSortMode.Immediate, data.blendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, data.effect);
//            spriteBatch_.Draw(data.texture, destRect, data.color);
//            spriteBatch_.End();
//         }
//      }
//   }
//
//   public class Camera
//   {
//      public class CameraRenderTarget
//      {
////         private List<RenderableSystem<RenderablePlugin>> renderableSystems_ = new List<RenderableSystem<RenderablePlugin>>();
////         private RenderTarget2D renderTarget_;
////         public float pixelSize = 1;
////
////         public void RenderAllSystems()
////         {
////            foreach (RenderableSystem<RenderablePlugin> system in renderableSystems_)
////            {
////               // render all systems (typically 1) into this rendertarget
////
////            }
////         }
//      }
//
//      public class CameraAudioListener
//      {
//         private AudioSystem<AudioPlugin> audioSystems;
//         public float listenerVolume;
//      }
//
//      public float scale;
//      private List<CameraRenderTarget> renderTargets_ = new List<CameraRenderTarget>();
//      private List<CameraAudioListener> audioListeners_ = new List<CameraAudioListener>();
//
//      public void RenderAllTargets()
//      {
//         foreach (CameraRenderTarget renderTarget in renderTargets_)
//         {
//            // render all rendertargets into their respective buffers
////            renderTarget.RenderAllSystems();
//         }
//      }
//
//   }

   

   partial class Engine : Game
   {

      public static Engine instance;

      public static class Graphics
      {
         public static GraphicsDevice Device => instance.device_;
         public static Viewport Viewport => instance.device_.Viewport;
      }

      private GraphicsDevice device_;

      public Engine()
      {
         instance = this;
         //         finalBuffer_ = new FinalBuffer(GraphicsDevice);
      }
   }

}