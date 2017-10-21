using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class _UIDev : Game
{
    private GraphicsDeviceManager Graphics;
    private ContentManager ContentManager;
    private SpriteBatch sb;

    public _UIDev()
    {
        // general
        IsFixedTimeStep = true;
        IsMouseVisible = true;
        
        // init graphics
        Graphics = new GraphicsDeviceManager(this);
        Graphics.SynchronizeWithVerticalRetrace = true;
        Graphics.PreferredBackBufferWidth = 1340;
        Graphics.PreferredBackBufferHeight = 700;
        Window.AllowUserResizing = true;
        
        // init content
        ContentManager = Content;
        ContentManager.RootDirectory = "Content";
        SpriteFont font = ContentManager.Load<SpriteFont>("font1");

        font.Spacing = 0;
        font.LineSpacing = 10;

        Texture2D texture = Content.Load<Texture2D>("tile");
        
        sb = new SpriteBatch(Graphics.GraphicsDevice);

        UI.Init(Graphics.GraphicsDevice, font);
        Window.ClientSizeChanged += (o, e) => {UI.ScreenResize();};

        var c1 = UI.RootRect.AddChild(new UI.Layout(150, 50, 300, 300));

        c1.AddLayoutter(new UI.Layout.ExpandToParentSize(UI.Layout.Mode.Vertical));
        c1.AddLayoutter(new UI.Layout.AlignRelativeToParent(1,0));
        c1.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Vertical));

        GetValue(c1, texture);

        c1.AddChild(new UI.UIRect(100,100,100,100));

        GetValue(c1, texture).Visible = true;


        c1.AddChild(new UI.TextRect(100, 100, 200, 100, "Lorem@<>#[fo]{}(ba)*&%$!?,.:; ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum."));
        c1.AddChild(new UI.TextRect(100, 100, 100, 100, "mytest  asdf sd f asd f as df as df as df asdf mytest sdf sd f sdfa mytest mytest mytest mytest dsf d fs df sdfsdf ds   s s s s ss  s sst mytest mytest mytest"));
        c1.AddChild(new UI.TextRect(100, 100, 100, 100, "mytest  s s s ss  s sst mytest mytest mytest"));
        c1.AddChild(new UI.TextRect(100, 100, 100, 100, "mytest mytest sdf sd f sdfa mytest mytest mytest mytest dsf d fs df sdfsdf ds   s s s s ss  s sst mytest mytest mytest"));
        c1.AddChild(new UI.TextRect(100, 100, 100, 100, "mytest mytest sdf sd f sdfa mytest f sd f sdfa mytest mytest mytest mytest dsf d fs df sd f sdfa mytest mytest mytest mytest dsf d fs dmytest mytest mytest dsf d fs df sdfsdf ds   s s s s ss  s sst mytest mytest mytest"));
        c1.AddChild(new UI.TextRect(100, 100, 100, 100, "mytest mytest sdf sd f sdfa mytest mytest mytest mytest dsf d fs df sdfsdf ds   f sd f sdfa mytest mytest mytest mytest dsf d fs df sd f sdfa mytest mytest mytest mytest dsf d fs df sd f sdfa mytest mytest mytest mytest dsf d fs df sd f sdfa mytest mytest mytest mytest dsf d fs df sd f sdfa mytest mytest mytest mytest dsf d fs ds s s s ss  s sst mytest mytest mytest"));
        c1.AddChild(new UI.UIRect(100,100,100,100));
        c1.AddChild(new UI.UIRect(100,100,100,100));
        c1.AddChild(new UI.UIRect(100,100,100,100));
        c1.AddChild(new UI.UIRect(100,100,100,100));
        c1.AddChild(new UI.UIRect(100,100,100,100));
        c1.AddChild(new UI.UIRect(100,100,100,100));
        c1.AddChild(new UI.UIRect(100,100,100,100));

    }

    private static UI.Layout GetValue(UI.Layout c1, Texture2D texture)
    {
        var cc1 = c1.AddChild(new UI.Layout(0, 0, 100, 60));
        cc1.Visible = true;

        cc1.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));

        //cc1.AddLayoutter(new UI.Layout.CenterX());


        var a = cc1.AddChild(new UI.TextButton(25, 25, 50, 50, "buttontest"));
        a.AddChild(new UI.Image(10, 10, 20, 20, texture));
        a.AddChild(new UI.Label(30, 30, 10, 10, "labell"));
        a.AddChild(new UI.TextRect(0, 0, 36, 10, "rect"));


        cc1.AddChild(new UI.SwappableButton(25, 25, 50, 50)).AddChild(new UI.Image(1, 1, 48, 48, texture)).IgnoreInput =true;

        cc1.AddChild(new UI.SwappableButton(25, 25, 50, 50)).AddChild(new UI.Image(1, 1, 20, 20, texture)).IgnoreInput =true;
        cc1.AddChild(new UI.SwappableButton(25, 25, 50, 50)).AddChild(new UI.Image(1, 1, 30, 30, texture)).IgnoreInput =true;
        cc1.AddChild(new UI.SwappableButton(25, 25, 50, 50));
        cc1.AddChild(new UI.Button(25, 25, 50, 50));
        cc1.AddChild(new UI.Image(25, 25, 50, 50, texture)).IgnoreInput=false;
        cc1.AddChild(new UI.DraggableButton(25, 25, 50, 50));
        cc1.AddChild(new UI.UIRect(25, 25, 50, 50));
        cc1.AddChild(new UI.UIRect(25, 25, 50, 50));
        cc1.AddChild(new UI.UIRect(25, 25, 50, 50));
        cc1.AddChild(new UI.UIRect(25, 25, 50, 50));
        cc1.AddChild(new UI.UIRect(25, 25, 50, 50));
        cc1.AddChild(new UI.UIRect(25, 25, 50, 50));

        return cc1;
    }

    protected override void Update(GameTime gameTime)
    {
        Input.Update();
        base.Update(gameTime);

//        if (Keys.Down.WasPressed()) UI.defaultSpacing--;
//        if (Keys.Up.WasPressed()) UI.defaultSpacing++;
        if (Keys.Left.WasPressed()) UI.defaultMargins--;
        if (Keys.Right.WasPressed()) UI.defaultMargins++;

        UI.UpdateUI();
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        
        Texture2D uiRender = UI.DrawUI();
        
        Graphics.GraphicsDevice.SetRenderTarget(null);
        sb.Begin();
        sb.Draw(uiRender, Graphics.GraphicsDevice.Viewport.Size().FittingMultiple(UI.PixelSize).FromSize(), Color.White);
        sb.End();

    }
}