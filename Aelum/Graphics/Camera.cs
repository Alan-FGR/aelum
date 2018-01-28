using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Priority_Queue;

public class Camera
{
   
   #region Members
   
   private Vector2 position_;
   private float scale_ = 50;
   public int pixelSize { get; private set; } = -1;
   
   //Rendering
   public float AspectRatio => RenderTarget != null ? RenderTarget.Width / (float)RenderTarget.Height : 1;
   public CameraRenderTarget RenderTarget => renderTargets_[0];
   public CameraRenderTarget GetRenderTarget(int idx) { return renderTargets_[idx]; }

   private readonly List<CameraRenderTarget> renderTargets_ = new List<CameraRenderTarget>(); //Camera render buffers
   private readonly SimplePriorityQueue<RenderLayer> renderLayers_ = new SimplePriorityQueue<RenderLayer>();

   public static readonly SimplePriorityQueue<RenderLayer> DEFAULT_RENDER_PATH = new SimplePriorityQueue<RenderLayer>();
   
   //Debug stuff
   public static float DEBUGMULT = 1;//5;
   public static float INVDEBUGMULT = 1;//1/5f;

   #endregion

   #region Constructors

   public Camera(int pixelSize = 1, int renderTargetsAmount = 1)
   {
      for (int i = 0; i < renderTargetsAmount; i++)
      {
         renderTargets_.Add(null);
      }

      SetPixelSize(pixelSize, false); //this also inits targets

#if ORIGIN_SHIFT
        Entity.OnOriginShift += v =>
        {
            position_ += v;
        };
#endif
   }

   #endregion

   #region Rendering Objects

   public class CameraRenderTarget : RenderTarget2D
   {
      public CameraRenderTarget(GraphicsDevice graphicsDevice, int width, int height) : base(graphicsDevice, width, height){}
      public CameraRenderTarget(GraphicsDevice graphicsDevice, int width, int height, bool mipMap, SurfaceFormat preferredFormat, DepthFormat preferredDepthFormat) : base(graphicsDevice, width, height, mipMap, preferredFormat, preferredDepthFormat){}
      public CameraRenderTarget(GraphicsDevice graphicsDevice, int width, int height, bool mipMap, SurfaceFormat preferredFormat, DepthFormat preferredDepthFormat, int preferredMultiSampleCount, RenderTargetUsage usage) : base(graphicsDevice, width, height, mipMap, preferredFormat, preferredDepthFormat, preferredMultiSampleCount, usage){}

      public Color ClearColor = Color.Green;
   }

   public class RenderLayer
   {
      private readonly IRenderableSystem system_;
      public int renderTargetIndex; //TODO FIXME
      public RenderLayer(IRenderableSystem system, int renderTargetIndex)
      {
         system_ = system;
         this.renderTargetIndex = renderTargetIndex;
      }
      public void Draw(Camera camera)
      {
         system_.Draw(camera, camera.GetRenderTarget(renderTargetIndex));
      }
   }

   #endregion

   #region Rendering

   /// <summary> Adding layers will invalidate the default layers (fallback) </summary>
   public void AddRenderLayer(IRenderableSystem system, int priority = 0, int layer = 0)
   {
      renderLayers_.Enqueue(new RenderLayer(system, layer), priority);
   }

   public void Render()
   {
      UpdateBeforeDrawing();

      float cullOverScan = Keys.Z.IsDown() ? -3 : 0; //TODO FIXME DBG
      Matrix globalMatrix = GetGlobalViewMatrix();
      
      //render all targets
      for (var index = 0; index < renderTargets_.Count; index++)
      {
         CameraRenderTarget target = renderTargets_[index];
         Graphics.Device.SetRenderTarget(RenderTarget);
         Graphics.Device.SetStatesToDefault();
         Graphics.Device.Clear(target.ClearColor);
         
         var layers = renderLayers_.Count > 0 ? renderLayers_ : DEFAULT_RENDER_PATH;

         //can't get ordered ienumerator :( TODO make extension or subclass
         var queue = new SimplePriorityQueue<RenderLayer>();
         foreach (RenderLayer layer in layers)
         {
            queue.Enqueue(layer, layers.GetPriority(layer));
         }

         while (queue.TryDequeue(out var command))
         {
            if(command.renderTargetIndex == index) //TODO optimize
               command.Draw(this);
         }
      }
      
      //      backBufferEffect_.Projection = globalMatrix;
      //      backBufferEffect_.Texture = atlas;
      //      backBufferEffect_.TextureEnabled = true;
      //      backBufferEffect_.CurrentTechnique.Passes[0].Apply();

   }

   public void UpdateBeforeDrawing()
   {
      if (RenderTarget.Width != Graphics.Viewport.Width / pixelSize || RenderTarget.Height != Graphics.Viewport.Height / pixelSize)
         SetPixelSize(pixelSize);
   }

   private void SetRenderTargets()
   {
      int width = Graphics.Viewport.Width / pixelSize;
      int height = Graphics.Viewport.Height / pixelSize;
      for (var i = 0; i < renderTargets_.Count; i++)
      {
         Debug.WriteLine($"Initting RenderTarget on: {i}");
         renderTargets_[i]?.Dispose();
         renderTargets_[i] = new CameraRenderTarget(Graphics.Device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth16);
      }
   }

   public void UpdateRenderTargets()
   {
      SetRenderTargets();
      scale_ = (float)RenderTarget.Height / Graphics.PixelsPerUnit;
   }

   #endregion

   #region Geometrics

   public RectF GetCullRect(float overscan = 0)
   {
      return new RectF(position_.X - overscan, position_.Y - overscan, (scale_ * AspectRatio + overscan * 2), (scale_ + overscan * 2));
   }

   public Matrix GetGlobalViewMatrix()
   {
      float x = Core.SnapToPixel(position_.X);
      float y = Core.SnapToPixel(position_.Y);
      return Matrix.CreateOrthographicOffCenter(
         x, x + scale_ * AspectRatio * DEBUGMULT, //FIXME TODO
         y, y + scale_ * DEBUGMULT,
         -10, 10f);
   }

   public Matrix GetSpritesViewMatrix() //TODO invert y
   {
      /* client space              viewport space
       * 
       *  0,0 ------- 1,0          -1,1 ------- 1,1
       *   |           |             |           |
       *   |  [1,1]/2  |             |    0,0    |
       *   |           |             |           |
       *  0,1 ------- 1,1          -1,-1------- 1,-1
       */
      float x = Core.SnapToPixel(position_.X);
      float y = Core.SnapToPixel(position_.Y);
      float top = RenderTarget.Height + y * Graphics.PixelsPerUnit * INVDEBUGMULT; // we sum height to invert Y coords
      float left = x * Graphics.PixelsPerUnit * INVDEBUGMULT;
      return Matrix.CreateOrthographicOffCenter(left, left + 2, top + 2, top, -10, 10);
   }

   public void SetPixelSize(int size, bool recenter = true)
   {
      Vector2 oldCenter = recenter ? Center : Vector2.Zero; //skip getting center

      int newPixelSize = MathUtils.ClampInt(size, 1, 10000);
      if (pixelSize == newPixelSize) return;
      pixelSize = newPixelSize;
      UpdateRenderTargets();
      if (recenter)
         Center = oldCenter;
   }

   #endregion

   #region Positional

   public Vector2 Center
   {
      get => position_ + new Vector2(scale_ / 2 * AspectRatio, scale_ / 2f);
      set => position_ = value - new Vector2(scale_ / 2 * AspectRatio, scale_ / 2f);
   }

   public void LerpCenterTo(Vector2 position, float t)
   {
      Center = MathUtils.Lerp(Center, position, t);
   }

   public Vector2 WorldMousePosition
   {
      get
      {
         Vector2 screenPos = Input.MousePosition;
         Vector2 screenToWorldScaled = new Vector2(
            screenPos.X / Graphics.Viewport.Width * scale_ * AspectRatio,
            (Graphics.Viewport.Height - screenPos.Y) / Graphics.Viewport.Height * scale_
         );
         return position_ + screenToWorldScaled;
      }
   }

   public Vector2 WorldToScreenPosition(Vector2 pos) //FIXME
   {
      float y = (pos.Y - position_.Y) / scale_;
      float x = (pos.X - position_.X) / scale_ * AspectRatio;
      return new Vector2(x, y);
   }

   #endregion

}