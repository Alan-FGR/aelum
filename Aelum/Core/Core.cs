using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FarseerPhysics.Dynamics;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework.Content;
using Priority_Queue;

//TODO offset half a pixel when origin is centered on objects of odd pixel dimension??? OR NOT? =/

public static class Graphics
{
   // convenience acessors
   public static GraphicsDeviceManager Manager => Core.instance.graphicsDeviceManager;
   public static GraphicsDevice Device => Manager.GraphicsDevice;
   public static GraphicsAdapter Adapter => Device.Adapter;
   public static DisplayMode Mode => Adapter.CurrentDisplayMode;
   public static Viewport Viewport => Device.Viewport;
   public static PresentationParameters Presentation => Device.PresentationParameters;

   // configs and vars
   public static int PixelsPerUnit { get; private set; } = 16;
   public static float PixelsToWorld { get; private set; } = 1f / 16;
   internal static void SetPixelsPerUnit(int ppu) //TODO maybe move and use acessor?
   {
      PixelsPerUnit = ppu;
      PixelsToWorld = 1f / ppu;
   }
}

public static class Content
{
   public static ContentManager Manager => Core.instance.contentManager;
   //TODO add pipeline stuff here
}

public abstract class Core : Game
{
   internal static Core instance;

   internal GraphicsDeviceManager graphicsDeviceManager;
   internal ContentManager contentManager;

   // stuff to actually render to window/screen
   private readonly SpriteBatch backBufferBatch_;
   


   
   
   
   
   // actual systems and managers TODO - ALL OF THIS SUCKS
   public static Texture2D atlas;//TODO move
   public static Texture2D pixel;

   
   public static bool DEBUG = true;
   public static int ATLAS_TO_WORLD { get; private set; }
   public Color clearColor = Color.Black;
   protected BlendState lightsBlendMode = BlendStateExtra.Multiply;

   // game arch shit
   public static Camera mainCam;
   public static GameTime lastGameTime { get; private set; } = new GameTime(TimeSpan.Zero, TimeSpan.Zero);
   public static float lastDT = 1 / 60f;

   // hooks
   public Action OnBeforeInputUpdate;
   public Action OnBeforeLogicUpdate;
   public Action OnBeforePhysicsUpdate;
   public Action OnEndUpdate;
   public Action OnBeforeDraw; // don't use for logic other that purely visual and unimportant or pre processing

   //audio
   //    private Song BGM;
   //    public MediaPlayer BGMPlayer { get; private set; } holy jesus, this is static? 0_o



   public Core(int pixelsPerUnit = 16)
   {
      instance = this;
      
      // serialization
      CompositeResolver.RegisterAndSetAsDefault(SzMessagePackResolver.Instance, StandardResolver.Instance);

      // general
      IsFixedTimeStep = true;
      IsMouseVisible = true;
      Window.AllowUserResizing = true;
      
      // init graphics
      Graphics.SetPixelsPerUnit(pixelsPerUnit);
      graphicsDeviceManager = new GraphicsDeviceManager(this);
      graphicsDeviceManager.SynchronizeWithVerticalRetrace = true;
      graphicsDeviceManager.PreferredBackBufferWidth = 1340;
      graphicsDeviceManager.PreferredBackBufferHeight = 720;

      
      
      
      
      // init content
      contentManager = Content;
      contentManager.RootDirectory = "Content";
      atlas = Content.Load<Texture2D>("Atlas");
      ATLAS_TO_WORLD = atlas.Width / Graphics.PixelsPerUnit;
      LightProjector.DEFAULT_SYSTEM.LoadContent();//TODO HIGH PRIORITY 

      pixel = new Texture2D(GraphicsDevice, 1, 1);
      pixel.SetData(new[] { Color.White });

      //UI
      SpriteFont font = global::Content.Manager.Load<SpriteFont>("Font");
      font.Spacing = 0;
      font.LineSpacing = 10;
      UI.Init(Graphics.Device, font, 2);

      // rendering
      mainCam = new Camera(2);
      
      backBufferBatch_ = new SpriteBatch(Graphics.Device);

      Window.ClientSizeChanged += (o, e) => { UI.ScreenResize();}; //TODO
      
   }
   
   protected override void Update(GameTime gameTime)
   {
      lastGameTime = gameTime;
      lastDT = (float)lastGameTime.ElapsedGameTime.TotalSeconds;

      // update our input
      OnBeforeInputUpdate?.Invoke();
      Input.Update();

      // update the UI
      UI.UpdateUI();

      // update our game logic
      OnBeforeLogicUpdate?.Invoke();
      Behavior.SYSTEM.Update();

      // update physics stuff
      OnBeforePhysicsUpdate?.Invoke();
      Physics.World.Step(lastDT);
      DynamicBody.UpdateAllBodies();

      OnEndUpdate?.Invoke();
      base.Update(gameTime);
   }
   
   protected override void Draw(GameTime gameTime)
   {
      OnBeforeDraw?.Invoke();

      mainCam.Render();

      //render 2d lighting
//      var lights = LightProjector.DrawAllInRect(mainCam.GetCullRect(20), globalMatrix);

      //render UI
//      Texture2D uiRender = UI.DrawUI();

      //debug rendering
      Texture2D debugRender = Dbg.RenderDebug(mainCam);
      
      //render opaque stuff (quads, sprites, etc)
      RenderToScreen(mainCam.MainRenderTarget, BlendState.Opaque);
      RenderToScreen(mainCam.GetRenderTarget(1).renderTarget, lightsBlendMode);
      
//      RenderToScreen(LightProjector.DEFAULT_SYSTEM.GetAllComponents()[0].lightProjectorRT_);


      //render lights and shadows
//      RenderToScreen(lights.texture, lightsBlendMode);


      //render UI
//      backBufferBatch_.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
//      backBufferBatch_.Draw(uiRender, Graphics.Viewport.Size().FittingMultiple(UI.PixelSize).FromSize(), Color.White);
//      backBufferBatch_.End();


      if (DEBUG) RenderToScreen(debugRender, BlendState.NonPremultiplied, new Color(1, 1, 1, 0.5f));


      //audio TODO move from here FIXME
      SoundPlayer.DEFAULT_SYSTEM.CullSoundsInRect(mainCam.GetCullRect());

   }

   private void RenderToScreen(Texture2D texture, BlendState blendState = null, Color? color = null, Effect effect = null)
   {
      if (blendState == null) blendState = BlendState.AlphaBlend;

      Graphics.Device.SetRenderTarget(null);
      
      Rectangle roundedRectangle = Graphics.Viewport.Size().FittingMultiple(mainCam.pixelSize).FromSize();

      backBufferBatch_.Begin(SpriteSortMode.Immediate, blendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
      backBufferBatch_.Draw(texture, roundedRectangle, color ?? Color.White);
      backBufferBatch_.End();
   }

   public static float SnapToPixel(float coord)
   {
      return Math.Round(coord * Graphics.PixelsPerUnit).ToFloat() * Graphics.PixelsToWorld;
   }

   public static Vector2 SnapToPixel(Vector2 coord)
   {
      return new Vector2(SnapToPixel(coord.X), SnapToPixel(coord.Y));
   }

}

//todo
//generic grid system
//editable blueprints using tile system
//save and load blueprints properly
//blueprint objects that use multiple tiles
