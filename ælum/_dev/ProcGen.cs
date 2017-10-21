
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

//class Program { static void Main() { using (var game = new ProcGen()) game.Run(); } }
/*
 * class ProcGen : Game
{
    GraphicsDeviceManager graphics;
    SpriteBatch spriteBatch;
    GraphicsDevice device;
 
    public ProcGen()
    {
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }
 
    protected override void Initialize()
    {
        graphics.PreferredBackBufferWidth = 500;
        graphics.PreferredBackBufferHeight = 500;
        graphics.IsFullScreen = false;
        graphics.ApplyChanges();
 
        base.Initialize();
    }
 
    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        device = graphics.GraphicsDevice;
    }
 
    protected override void UnloadContent()
    {
    }
 
    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            this.Exit();
 
        base.Update(gameTime);
    }
 
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
 
        base.Draw(gameTime);
    }
}
 */

class ProcGen : Game
{
    GraphicsDeviceManager graphics;
    SpriteBatch spriteBatch;
    GraphicsDevice device;

    private Texture2D texture2D_;
 
    public ProcGen()
    {
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }
 
    protected override void Initialize()
    {
        graphics.PreferredBackBufferWidth = 500;
        graphics.PreferredBackBufferHeight = 500;
        graphics.IsFullScreen = false;
        graphics.ApplyChanges();
 
        base.Initialize();
    }
 
    int size = 64;


    interface ITileDef
    {
        Color GetMapColor();
        //List<colliderData> GetCollider();
        //RectF GetAtlasRect();
    }

    struct Ground : ITileDef
    {
        public enum GroundType
        {
            None, Dirt, Grass
        }

        public GroundType type;

        public Ground(GroundType type)
        {
            this.type = type;
        }

        public Color GetMapColor()
        {
            if(type == GroundType.Dirt)
                return Color.SaddleBrown;
            if(type == GroundType.Grass)
                return Color.Green;

            return Color.Red; //no color spec
        }
    }
    


    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        device = graphics.GraphicsDevice;


        ITileDef[,] map = new ITileDef[size,size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
                map[x,y] = new Ground(x<3?Ground.GroundType.Grass : Ground.GroundType.Dirt);
        }

        texture2D_ = new Texture2D(device,size,size);
        
        Color[] colors = new Color[size*size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            colors[x*size+y] = map[x,y].GetMapColor();
        }

        texture2D_.SetData(colors);

    }
 
    protected override void UnloadContent()
    {
    }
 
    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            this.Exit();
 
        base.Update(gameTime);
    }
 
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null);
        //spriteBatch.Draw(texture2D_,Vector2.Zero, Color.White);

        spriteBatch.Draw(texture2D_, new Rectangle(0,0,size*4, size*4), Color.White);

        spriteBatch.End();

 
        base.Draw(gameTime);
    }
}






