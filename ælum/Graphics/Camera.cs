using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class Camera
{
    private Vector2 position_;
    private float scale_ = 50;
    public int pixelSize { get; private set; } = -1;

    public float aspectRatio => renderTargets_[0] != null ? renderTargets_[0].Width / (float) renderTargets_[0].Height : 1;
    
    private readonly List<RenderTarget2D> renderTargets_ = new List<RenderTarget2D>();

    public RenderTarget2D RT(int idx){return renderTargets_[idx];}

    public Camera(int renderTargetsAmount, int pixelSize = 1)
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

    public void LerpCenterTo(Vector2 position, float t)
    {
        Center = MathUtils.Lerp(Center, position, t);
    }

    public Vector2 Center
    {
        get => position_+new Vector2(scale_ / 2 * aspectRatio, scale_ / 2f);
        set => position_ = value-new Vector2(scale_ / 2 * aspectRatio, scale_ / 2f);
    }

    public void SetPixelSize(int size, bool recenter = true)
    {
        Vector2 oldCenter = recenter ? Center : Vector2.Zero; //skip getting center

        int newPixelSize = MathUtils.ClampInt(size, 1, 10000);
        if(pixelSize == newPixelSize) return;
        pixelSize = newPixelSize;
        UpdateRenderTargets();
        if (recenter)
            Center = oldCenter;
    }

    public void UpdateRenderTargets()
    {
        SetRenderTargets();
        scale_ = (float) renderTargets_[0].Height / Core.PPU;
    }

    private void SetRenderTargets()
    {
        int width = Core.Viewport.Width / pixelSize;
        int height = Core.Viewport.Height / pixelSize;
        for (var i = 0; i < renderTargets_.Count; i++)
        {
            Debug.WriteLine($"Initting RenderTarget on: {i}");
            renderTargets_[i]?.Dispose();
            renderTargets_[i] = new RenderTarget2D(Core.GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth16);
        }
    }

    public void UpdateBeforeDrawing()
    {
        if (renderTargets_[0].Width != Core.Viewport.Width / pixelSize || renderTargets_[0].Height != Core.Viewport.Height / pixelSize)
            SetPixelSize(pixelSize);
    }
    
    public RectF GetCullRect(float overscan = 0)
    {
        return new RectF(position_.X - overscan, position_.Y - overscan, (scale_*aspectRatio+overscan*2), (scale_+overscan*2));
    }
    
    public Matrix GetGlobalViewMatrix()
    {
        float x = Core.SnapToPixel(position_.X);
        float y = Core.SnapToPixel(position_.Y);
        return Matrix.CreateOrthographicOffCenter(
            x, x + scale_ * aspectRatio * DEBUGMULT, //FIXME TODO
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
        float top = renderTargets_[0].Height + y * Core.PPU * INVDEBUGMULT; // we sum height to invert Y coords
        float left = x * Core.PPU * INVDEBUGMULT;
        return Matrix.CreateOrthographicOffCenter(left, left+2, top+2, top, -10, 10);
    }

    public Vector2 WorldMousePosition
    {
        get
        {
            Vector2 screenPos = Input.MousePosition;
            Vector2 screenToWorldScaled = new Vector2(
                screenPos.X/Core.Viewport.Width*scale_*aspectRatio,
                (Core.Viewport.Height-screenPos.Y)/Core.Viewport.Height*scale_
                );
            return position_ + screenToWorldScaled;
        }
    }

    public Vector2 WorldToScreenPosition(Vector2 pos) //FIXME
    {
        float y = (pos.Y-position_.Y) / scale_;
        float x = (pos.X-position_.X) / scale_ * aspectRatio;
        return new Vector2(x,y);
    }

    public float GetSpriteZ(Vector2 position)
    {
        Vector2 wts = WorldToScreenPosition(position);
        return wts.Y*1.9f + wts.X*0.01f;
//        float ScreenYPos = (position.Y-entity.Position.Y) / Core.mainCam.RT(0).Height +
//            (Core.mainCam.position.X-entity.Position.X) / Core.mainCam.RT(0).Width*0.0001f
    }
}