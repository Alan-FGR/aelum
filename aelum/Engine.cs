using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace aelum
{
   
public class FinalBufferRenderData
{
    public Texture2D texture;
    public BlendState blendState = BlendState.AlphaBlend;
    public Effect effect = null;
    public Color color = Color.White;
    public bool pixelPerfect = true;

    public FinalBufferRenderData(Texture2D texture)
    {
        this.texture = texture;
    }
}

partial class Engine
{
    ///<summary>Object that represents the framebuffer to render all rendertargets into</summary>
    private class FinalBuffer
    {
        private readonly SpriteBatch spriteBatch_;
        private readonly BasicEffect basicEffect_;
        private readonly Viewport viewport_;

        internal FinalBuffer(GraphicsDevice device)
        {
            spriteBatch_ = new SpriteBatch(device);
            basicEffect_ = new BasicEffect(device);
            viewport_ = new Viewport();
        }

        public void RenderToFinalBuffer(FinalBufferRenderData data)
        {
            Rectangle destRect = viewport_.Bounds;
            if (data.pixelPerfect)
            {
                //destRect = //TODO
            }

            spriteBatch_.Begin(SpriteSortMode.Immediate, data.blendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, data.effect);
            spriteBatch_.Draw(data.texture, destRect, data.color);
            spriteBatch_.End();
        }
    }
}

public class Camera
{
    public float scale;
    
    public class CameraRenderTarget
    {
        public float pixelSize = 1;
        public RenderTarget2D renderTarget;
    }

}

partial class Engine : Game
{
    
    public static Scene scene;



    private static FinalBuffer finalBuffer_;

    public Engine()
    {
        finalBuffer_ = new FinalBuffer(GraphicsDevice);
    }
}

}