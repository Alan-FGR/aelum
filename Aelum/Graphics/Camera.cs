using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Priority_Queue;

public class CameraRenderLayer
{
   public IRenderableSystem system;
   public int renderTarget;
}

public class Camera
{
   private Vector2 position_;
   private float scale_ = 50;
   public int pixelSize { get; private set; } = -1;

   public float AspectRatio => RenderTarget != null ? RenderTarget.Width / (float)RenderTarget.Height : 1;

   private readonly List<RenderTarget2D> renderTargets_ = new List<RenderTarget2D>();

   public RenderTarget2D RenderTarget => renderTargets_[0];
   
   public RenderTarget2D RenderTargets(int idx) { return renderTargets_[idx]; }

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

//   public SimplePriorityQueue<>

   public void Render()
   {
      
   }

   public void LerpCenterTo(Vector2 position, float t)
   {
      Center = MathUtils.Lerp(Center, position, t);
   }

   public Vector2 Center
   {
      get => position_ + new Vector2(scale_ / 2 * AspectRatio, scale_ / 2f);
      set => position_ = value - new Vector2(scale_ / 2 * AspectRatio, scale_ / 2f);
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

   public void UpdateRenderTargets()
   {
      SetRenderTargets();
      scale_ = (float)RenderTarget.Height / Graphics.PixelsPerUnit;
   }

   private void SetRenderTargets()
   {
      int width = Graphics.Viewport.Width / pixelSize;
      int height = Graphics.Viewport.Height / pixelSize;
      for (var i = 0; i < renderTargets_.Count; i++)
      {
         Debug.WriteLine($"Initting RenderTarget on: {i}");
         renderTargets_[i]?.Dispose();
         renderTargets_[i] = new RenderTarget2D(Graphics.Device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth16);
      }
   }

   public void UpdateBeforeDrawing()
   {
      if (RenderTarget.Width != Graphics.Viewport.Width / pixelSize || RenderTarget.Height != Graphics.Viewport.Height / pixelSize)
         SetPixelSize(pixelSize);
   }

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

   public static float DEBUGMULT = 1;//5;
   public static float INVDEBUGMULT = 1;//1/5f;

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

}