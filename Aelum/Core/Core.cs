using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FarseerPhysics.Dynamics;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework.Content;

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
   private readonly BasicEffect backBufferEffect_;
   


   
   
   
   
   // actual systems and managers TODO - ALL OF THIS SUCKS
   public static World physWorld;
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
      LightProjector.LoadContent();

      pixel = new Texture2D(base.GraphicsDevice, 1, 1);
      pixel.SetData(new[] { Color.White });

      //UI
      SpriteFont font = global::Content.Manager.Load<SpriteFont>("Font");
      font.Spacing = 0;
      font.LineSpacing = 10;
      UI.Init(Graphics.Device, font, 2);

      // rendering
      mainCam = new Camera(2, 2);
      backBufferBatch_ = new SpriteBatch(Graphics.Device);
      backBufferEffect_ = new BasicEffect(Graphics.Device);

      Window.ClientSizeChanged += (o, e) => { UI.ScreenResize(); mainCam.UpdateRenderTargets(); };
      
      // physics
      physWorld = new World(Vector2.One);

      // debug
      if (DEBUG) new DebugHelper(physWorld);

   }

   protected override void Update(GameTime gameTime)
   {
      lastGameTime = gameTime;
      lastDT = (float)lastGameTime.ElapsedGameTime.TotalSeconds;

      // update our input
      OnBeforeInputUpdate?.Invoke();
      Input.Update();
      //UI
      UI.UpdateUI();

      // update our game logic
      OnBeforeLogicUpdate?.Invoke();
      Behavior.UpdateAll(lastDT);

      // update physics stuff
      OnBeforePhysicsUpdate?.Invoke();
      physWorld.Step(Math.Min((float)gameTime.ElapsedGameTime.TotalMilliseconds * 0.001f, 1f / 30f));//TODO
      DynamicBody.UpdateAllBodies();

      OnEndUpdate?.Invoke();
      base.Update(gameTime);
   }

   protected override void Draw(GameTime gameTime)
   {
      //lastDrawDT = gameTime.ElapsedGameTime.TotalSeconds.ToFloat();

      OnBeforeDraw?.Invoke();

      mainCam.UpdateBeforeDrawing();

      float cullOverScan = Keys.Z.IsDown() ? -3 : 0;
      Matrix globalMatrix = mainCam.GetGlobalViewMatrix();

      Graphics.Device.SetRenderTarget(mainCam.RT(0));
      Graphics.Device.SetStatesToDefault();
      Graphics.Device.Clear(clearColor);

      backBufferEffect_.Projection = globalMatrix;
      backBufferEffect_.Texture = atlas;
      backBufferEffect_.TextureEnabled = true;
      backBufferEffect_.CurrentTechnique.Passes[0].Apply();

      //THIS ALL SUCKS!! WE NEED A PROPER COMPONENT_SYSTEMS HANDLING SYSTEM BASED ON EVENTS!

      //render quads (and possibly meshes made of quads)
      Quad.DrawAllInRect(mainCam.GetCullRect(cullOverScan));

      //render sprite components
      //TODO use private spritebatcher
      Sprite.DrawAllInRect(backBufferBatch_, mainCam.GetCullRect(cullOverScan), mainCam.GetSpritesViewMatrix());

      //render 2d lighting
      //        var lights = LightProjector.DrawAllInRect(mainCam.GetCullRect(20), globalMatrix);


      //render UI
      Texture2D uiRender = UI.DrawUI();

      //debug rendering
      if (DEBUG) DebugHelper.instance.DrawDebug(mainCam);


      //render opaque stuff (quads, sprites, etc)
      RenderToScreen(mainCam.RT(0), BlendState.Opaque);

      //render lights and shadows
      //        RenderToScreen(lights.texture, lightsBlendMode);

      //render UI
      backBufferBatch_.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
      backBufferBatch_.Draw(uiRender, Graphics.Viewport.Size().FittingMultiple(UI.PixelSize).FromSize(), Color.White);
      backBufferBatch_.End();


      if (DEBUG) RenderToScreen(DebugHelper.instance.DbgRenderTarget, BlendState.NonPremultiplied, new Color(1, 1, 1, 0.75f));


      //audio TODO move from here
      SoundPlayer.CullSoundsInRect(mainCam.GetCullRect());

   }

   private void RenderToScreen(Texture2D texture, BlendState blendState = null, Color? color = null, Effect effect = null)
   {
      if (blendState == null) blendState = BlendState.AlphaBlend;

      Graphics.Device.SetRenderTarget(null);

      //round dimensions to fit exact pixel size (the biggest multiple that fits on screen) TODO REM
      //        int pixelRoundedWidth = Viewport.Width/mainCam.pixelSize*mainCam.pixelSize;
      //        int pixelRoundedHeight = Viewport.Height/mainCam.pixelSize*mainCam.pixelSize;
      //        Rectangle roundedRectangle = new Rectangle(0, 0, pixelRoundedWidth, pixelRoundedHeight);
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
