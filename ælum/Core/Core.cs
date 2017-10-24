using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FarseerPhysics.Dynamics;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

//TODO offset half a pixel when origin is centered on objects of odd pixel dimension??? OR NOT? =/

abstract partial class Core
{
    // convenience
    public static GraphicsDeviceManager Graphics;
    public static GraphicsDevice GraphicsDevice => Graphics.GraphicsDevice;
    public static GraphicsAdapter Adapter => GraphicsDevice.Adapter;
    public static DisplayMode DisplayMode => Adapter.CurrentDisplayMode;
    public static Viewport Viewport => GraphicsDevice.Viewport;
    public static PresentationParameters Presentation => GraphicsDevice.PresentationParameters;
    public static ContentManager ContentManager;
}

public abstract partial class Core : Game
{
    // actual systems and managers TODO - ALL OF THIS SUCKS
    public static SpriteBatch spriteBatch;
    readonly BasicEffect basicEffect;
    public static World physWorld;
    public static Texture2D atlas;//TODO move
    public static Texture2D pixel;

    // configs and vars
    public const int PPU = 16; //pixels per world/physWorld unit
    public const float PX_TO_WORLD = 1f / PPU; // pixels to world
    public static bool DEBUG = true;
    public static int ATLAS_TO_WORLD { get; private set; }
    public Color clearColor = Color.Black;
    protected BlendState lightsBlendMode = BlendStateExtra.Multiply;
    
    // game arch shit
    public static Camera mainCam;
    public static GameTime lastGameTime { get; private set; } = new GameTime(TimeSpan.Zero, TimeSpan.Zero);
    public static float lastDT = 1/60f;

    // hooks
    public Action OnBeforeInputUpdate;
    public Action OnBeforeLogicUpdate;
    public Action OnBeforePhysicsUpdate;
    public Action OnEndUpdate;
    public Action OnBeforeDraw; // don't use for logic other that purely visual and unimportant or pre processing

    //audio
//    private Song BGM;
//    public MediaPlayer BGMPlayer { get; private set; } holy jesus, this is static? 0_o
    


    public Core()
    {
        // general
        IsFixedTimeStep = true;
        IsMouseVisible = true;
        
        CompositeResolver.RegisterAndSetAsDefault(SzMessagePackResolver.Instance, StandardResolver.Instance);

        // init graphics
        Graphics = new GraphicsDeviceManager(this);
        Graphics.SynchronizeWithVerticalRetrace = true;
        Graphics.PreferredBackBufferWidth = 1340;
        Graphics.PreferredBackBufferHeight = 720;
        Window.AllowUserResizing = true;
        
        // init content
        ContentManager = Content;
        ContentManager.RootDirectory = "Content";
        atlas = Content.Load<Texture2D>("atlas");
        ATLAS_TO_WORLD = atlas.Width / PPU;
        LightProjector.LoadContent();

        pixel = new Texture2D(base.GraphicsDevice, 1, 1);
        pixel.SetData(new []{Color.White});

        //UI
        SpriteFont font = ContentManager.Load<SpriteFont>("Font");
        font.Spacing = 0;
        font.LineSpacing = 10;
        UI.Init(GraphicsDevice, font, 2);
        
        // rendering
        mainCam = new Camera(2, 2);
        spriteBatch = new SpriteBatch(GraphicsDevice);
        basicEffect = new BasicEffect(GraphicsDevice);
        
        Window.ClientSizeChanged += (o, e) => {UI.ScreenResize();mainCam.UpdateRenderTargets();};

        //DBG
        //mainCam.CenterAt(Vector2.One*8);
        //GraphicsDeviceState.SetDefaultState(new GraphicsDeviceState(BlendState.Opaque, SamplerState.AnisotropicWrap));

        // physics
        physWorld = new World(Vector2.One);
        
        // debug
        if(DEBUG) new DebugHelper(physWorld);

    }

    protected override void Update(GameTime gameTime)
    {
        lastGameTime = gameTime;
        lastDT = (float) lastGameTime.ElapsedGameTime.TotalSeconds;

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
        physWorld.Step(Math.Min((float)gameTime.ElapsedGameTime.TotalMilliseconds*0.001f, 1f/30f));//TODO
        DynamicBody.UpdateAllBodies();

        OnEndUpdate?.Invoke();
        base.Update(gameTime);
    }
    
    protected override void Draw(GameTime gameTime)
    {
        //lastDrawDT = gameTime.ElapsedGameTime.TotalSeconds.ToFloat();

        OnBeforeDraw?.Invoke();

        mainCam.UpdateBeforeDrawing();

        float cullOverScan = Keys.Z.IsDown() ? -3 : 3;
        Matrix globalMatrix = mainCam.GetGlobalViewMatrix();

        GraphicsDevice.SetRenderTarget(mainCam.RT(0));
        GraphicsDevice.SetStatesToDefault();
        GraphicsDevice.Clear(clearColor);
        
        basicEffect.Projection = globalMatrix;
        basicEffect.Texture = atlas;
        basicEffect.TextureEnabled = true;
        basicEffect.CurrentTechnique.Passes[0].Apply();
        
        //THIS ALL SUCKS!! WE NEED A PROPER COMPONENT_SYSTEMS HANDLING SYSTEM BASED ON EVENTS!

        //render quads (and possibly meshes made of quads)
        Quad.DrawAllInRect(mainCam.GetCullRect(cullOverScan));

        //render sprite components
        //TODO use private spritebatcher
        Sprite.DrawAllInRect(spriteBatch, mainCam.GetCullRect(cullOverScan), mainCam.GetSpritesViewMatrix());

        //render 2d lighting
        var lights = LightProjector.DrawAllInRect(mainCam.GetCullRect(20), globalMatrix);


        //render UI
        Texture2D uiRender = UI.DrawUI();

        //debug rendering
        if(DEBUG) DebugHelper.instance.DrawDebug(mainCam);

        
        //render opaque stuff (quads, sprites, etc)
        RenderToScreen(mainCam.RT(0), BlendState.Opaque);
        
        //render lights and shadows
        RenderToScreen(lights.texture, lightsBlendMode);

        //render UI
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
        spriteBatch.Draw(uiRender, Viewport.Size().FittingMultiple(UI.PixelSize).FromSize(), Color.White);
        spriteBatch.End();


        if (DEBUG) RenderToScreen(DebugHelper.instance.dbgRT, BlendState.NonPremultiplied, new Color(1,1,1,0.75f));


        //audio TODO move from here
        SoundPlayer.CullSoundsInRect(mainCam.GetCullRect());

    }

    private void RenderToScreen(Texture2D texture, BlendState blendState = null, Color? color = null, Effect effect = null)
    {
        if (blendState == null) blendState = BlendState.AlphaBlend;

        GraphicsDevice.SetRenderTarget(null);

        //round dimensions to fit exact pixel size (the biggest multiple that fits on screen) TODO REM
//        int pixelRoundedWidth = Viewport.Width/mainCam.pixelSize*mainCam.pixelSize;
//        int pixelRoundedHeight = Viewport.Height/mainCam.pixelSize*mainCam.pixelSize;
//        Rectangle roundedRectangle = new Rectangle(0, 0, pixelRoundedWidth, pixelRoundedHeight);
        Rectangle roundedRectangle = Viewport.Size().FittingMultiple(mainCam.pixelSize).FromSize();

        spriteBatch.Begin(SpriteSortMode.Immediate, blendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
        spriteBatch.Draw(texture, roundedRectangle, color ?? Color.White);
        spriteBatch.End();
    }

    public static float SnapToPixel(float coord)
    {
        return Math.Round(coord*PPU).ToFloat()*PX_TO_WORLD;
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
