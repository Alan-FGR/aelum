using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Priority_Queue;

public class Camera
{
   
   #region Members
   
   private Vector2 position_;
   private float scale_ = 50;
   public int pixelSize { get; private set; } = -1;
   
   //Rendering
   public float AspectRatio => MainRenderTarget != null ? MainRenderTarget.Width / (float)MainRenderTarget.Height : 1;
   public RenderTarget2D MainRenderTarget => renderTargets_[0].renderTarget;
   public CameraRenderTarget GetRenderTarget(int idx) { return renderTargets_[idx]; }

   private readonly List<CameraRenderTarget> renderTargets_ = new List<CameraRenderTarget>(); //Camera render buffers
   private readonly SimplePriorityQueue<RenderLayer> renderLayers_ = new SimplePriorityQueue<RenderLayer>();

   public static readonly SimplePriorityQueue<RenderLayer> DEFAULT_RENDER_PATH = new SimplePriorityQueue<RenderLayer>();
   
   //Debug stuff
   public static float DEBUGMULT = 1;//5;
   public static float INVDEBUGMULT = 1;//1/5f;

   #endregion

   #region Constructors

   public Camera(int pixelSize = 1, int renderTargetsAmount = 2)
   {
      for (int i = 0; i < renderTargetsAmount; i++)
      {
         renderTargets_.Add(new CameraRenderTarget(Color.Black));
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

   public class CameraRenderTarget
   {
      public Color clearColor;
      public int cameraTargetPixelSize;
      public RenderTarget2D renderTarget;
      public BlendState blendMode = BlendState.Opaque;

      public CameraRenderTarget(Color clearColor, int cameraTargetPixelSize = 1)
      {
         this.clearColor = clearColor;
         this.cameraTargetPixelSize = cameraTargetPixelSize;
      }

      public void PrepareForDrawing(Point baseSize, bool clear = true)
      {
         if (renderTarget == null || renderTarget.Dimensions() != baseSize.DividedBy(cameraTargetPixelSize))
            InitRT(baseSize);

         Graphics.Device.SetRenderTarget(renderTarget);
         Graphics.Device.SetStatesToDefault();
         Graphics.Device.Clear(clearColor);
      }

      public static int inc = 0; //todo remove this
      public void InitRT(Point baseSize)
      {
         Debug.WriteLine("Initting RenderTarget");
         renderTarget?.Dispose();
         renderTarget = new RenderTarget2D(Graphics.Device, baseSize.X / cameraTargetPixelSize, baseSize.Y / cameraTargetPixelSize, false, SurfaceFormat.Color, DepthFormat.Depth16, 0, RenderTargetUsage.PreserveContents);
         renderTarget.Name = inc++.ToString();
      }
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
         system_.Draw(camera, camera.GetRenderTarget(renderTargetIndex).renderTarget);
      }
   }

   #endregion

   #region Rendering

   /// <summary> Adding layers will invalidate the default layers (fallback) </summary>
   public void AddRenderLayer(IRenderableSystem system, int priority = 0, int layer = 0)
   {
      renderLayers_.Enqueue(new RenderLayer(system, layer), priority);
   }

   public List<Tuple<Texture2D, BlendState>> Render()
   {
      UpdateBeforeDrawing();

      var viewportSize = Graphics.Viewport.Size().DividedBy(pixelSize);

      List<Tuple<Texture2D, BlendState>> retList = new List<Tuple<Texture2D, BlendState>>();

      //render all targets
      for (var index = 0; index < renderTargets_.Count; index++)
      {
         CameraRenderTarget target = renderTargets_[index];
         
         target.PrepareForDrawing(viewportSize);
         
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

         retList.Add(new Tuple<Texture2D, BlendState>(target.renderTarget, target.blendMode));

      }

      return retList;

      //      backBufferEffect_.Projection = globalMatrix;
      //      backBufferEffect_.Texture = atlas;
      //      backBufferEffect_.TextureEnabled = true;
      //      backBufferEffect_.CurrentTechnique.Passes[0].Apply();

   }

   public void UpdateBeforeDrawing()
   {
      if (MainRenderTarget.Width != Graphics.Viewport.Width / pixelSize || MainRenderTarget.Height != Graphics.Viewport.Height / pixelSize)
         SetPixelSize(pixelSize);
   }

   public void UpdateRenderTargets()
   {
      var viewportSize = Graphics.Viewport.Size().DividedBy(pixelSize);
      foreach (CameraRenderTarget rt in renderTargets_)
      {
         rt.InitRT(viewportSize);
      }

      scale_ = (float)MainRenderTarget.Height / Graphics.PixelsPerUnit;
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
      float top = MainRenderTarget.Height + y * Graphics.PixelsPerUnit * INVDEBUGMULT; // we sum height to invert Y coords
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