using Microsoft.Xna.Framework.Graphics;

public interface IRenderableSystem
{
   void Draw(Camera camera, RenderTarget2D renderTarget);
   void DrawDebug();
}