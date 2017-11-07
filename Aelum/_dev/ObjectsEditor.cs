using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
/*
public class ObjectsEditor : Game
{
    private GraphicsDeviceManager Graphics;
    private ContentManager ContentManager;
    private SpriteBatch sb;

    private static Texture2D texture;

    public static List<ObjectSpec> catalog = new List<ObjectSpec>();
    
    public Dictionary<string, List<int>> ObjectCats = new Dictionary<string, List<int>>();

    public enum Orientation
    {
        left, up, right, down, omni
    }

    public enum ItemType
    {
        Food, Medicine, Ammo, Gun, Tool, Misc
    }

    public enum ObjectTypeTag
    {
        deco,
        table,
        wallDeco,
        bed,
        rug,
        window,
        bedside,
    }

    public enum ObjectStyleTag
    {
        residential,
        industrial,
        commercial,
        alley,
        street,
        forest,
        rural,
        hospital
    }
    
    [MessagePackObject]
    public struct SpriteDef
    {
        [Key(0)] public Orientation wallSide;
        [Key(1)] public Sheet.ID atlasId;

        // contains items
        [Key(2)]public List<ItemType> itemsContained;

        [SerializationConstructor]
        public SpriteDef(Orientation wallSide, Sheet.ID atlasId, List<ItemType> itemsContained)
        {
            this.wallSide = wallSide;
            this.atlasId = atlasId;
            this.itemsContained = itemsContained;
        }

        public SpriteDef(Sheet.ID atlasId) : this()
        {
            this.atlasId = atlasId;
        }
    }

    [MessagePackObject]
    public class ObjectSpec
    {
        [SerializationConstructor]
        public ObjectSpec(List<SpriteDef> sprites, List<ObjectTypeTag> typeTags, List<ObjectStyleTag> styleTags)
        {
            index = catalog.Count;
            catalog.Add(this);
            this.sprites = sprites;
            this.typeTags = typeTags;
            this.styleTags = styleTags;
        }

        public ObjectSpec() : this(new List<SpriteDef>(), new List<ObjectTypeTag>(), new List<ObjectStyleTag>())
        {
        }

        [IgnoreMember] public int index;

        [Key(0)] public List<SpriteDef> sprites;
        [Key(1)] public List<ObjectTypeTag> typeTags;
        [Key(2)] public List<ObjectStyleTag> styleTags;
    }

    public class OTTDND : UI.DraggableButton
    {
        public ObjectTypeTag tag;
        public OTTDND(int x, int y, int w, int h, ObjectTypeTag t) : base(x, y, w, h, null)
        {
            tag = t;
        }
    }

    public class OSTDND : UI.DraggableButton
    {
        public ObjectStyleTag tag;
        public OSTDND(int x, int y, int w, int h, ObjectStyleTag t) : base(x, y, w, h, null)
        {
            tag = t;
        }
    }

    public class DraggableSprite : UI.DraggableButton
    {
        public Sheet.ID spriteId;
        public DraggableSprite(int x, int y, int w, int h, Sheet.ID sprite) : base(x, y, w, h)
        {
            spriteId = sprite;
        }
    }

    public class DropNewSlot : UI.UIRect, UI.IDropOver
    {
        private ObjectSpec obj;
        public DropNewSlot(int x, int y, int w, int h, ObjectSpec o) : base(x, y, w, h, null)
        {
            obj = o;
        }

        public void Dropped(UI.IDraggable element)
        {
            if (element is OTTDND)
            {
                obj.typeTags.Add(((OTTDND) element).tag);
            }
            else if (element is OSTDND)
            {
                obj.styleTags.Add(((OSTDND) element).tag);
            }
            else if (element is DraggableSprite)
            {
                obj.sprites.Add(new SpriteDef(((DraggableSprite)element).spriteId));
            }

            PopulateCatalog();
            PopulateSprites();

        }
    }

    public ObjectsEditor()
    {
        // general
        IsFixedTimeStep = true;
        IsMouseVisible = true;
        
        // init graphics
        Graphics = new GraphicsDeviceManager(this);
        Graphics.SynchronizeWithVerticalRetrace = true;
        Graphics.PreferredBackBufferWidth = 1200;
        Graphics.PreferredBackBufferHeight = 700;
        Window.AllowUserResizing = true;
        
        // init content
        ContentManager = Content;
        ContentManager.RootDirectory = "Content";

        SpriteFont font = ContentManager.Load<SpriteFont>("font1");
        font.Spacing = 0;
        font.LineSpacing = 10;

        texture = Content.Load<Texture2D>("atlas");
        
        sb = new SpriteBatch(Graphics.GraphicsDevice);

        UI.Init(Graphics.GraphicsDevice, font);
        Window.ClientSizeChanged += (o, e) => {UI.ScreenResize();};



        CompositeResolver.RegisterAndSetAsDefault(
            BuiltinResolver.Instance,
            DynamicEnumAsStringResolver.Instance,
            StandardResolver.Instance
            );
        


        var menu = UI.RootRect.AddChild(new UI.Layout(10, 10, 1180, 30));
        menu.AddChild(new UI.TextButton(5, 5, 40, 20, "ADD")).OnClick += button =>
        {
            new ObjectSpec();
            PopulateCatalog();
        };


        var ottlo = menu.AddChild(new UI.Layout(50, 5, 350, 20, null, UI.Layout.Mode.Horizontal));
        ottlo.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));
        foreach (ObjectTypeTag t in Enum.GetValues(typeof(ObjectTypeTag)).Cast<ObjectTypeTag>())
        {
            ottlo.AddChild(new OTTDND(0, 0, 32, 32, t)).AddChild(new UI.TextButton(0,0,32,10,t.ToString())).IgnoreInput=true;
        }

        var osttlo = menu.AddChild(new UI.Layout(410, 5, 400, 20, null, UI.Layout.Mode.Horizontal));
        osttlo.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));
        foreach (ObjectStyleTag t in Enum.GetValues(typeof(ObjectStyleTag)).Cast<ObjectStyleTag>())
        {
            osttlo.AddChild(new OSTDND(0, 0, 48, 32, t)).AddChild(new UI.TextButton(0,0,48,10,t.ToString())).IgnoreInput=true;
        }



        PopulateCatalog();
        PopulateSprites();



    }

    private static UI.Layout catalogLayout;
    static float catScr = 0;
    private static void PopulateCatalog()
    {
        catScr = catalogLayout?.scrollAmount ?? 0;
        catalogLayout?.Remove();

        catalogLayout = UI.RootRect.AddChild(new UI.Layout(10, 50, 1180, 450));
        catalogLayout.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));

        catalogLayout.scrollAmount = catScr;
        catalogLayout.lerpedScroll = catScr;

        foreach (ObjectSpec obj in catalog)
        {
            var itemLayout = catalogLayout.AddChild(new UI.Layout(0, 0, 40, 32));
            itemLayout.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Vertical));

            var indd = itemLayout.AddChild(new DropNewSlot(0, 0, 32, 16, obj));
            var itemName = indd.AddChild(new UI.TextButton(0, 0, 32, 16, obj.index.ToString())).IgnoreInput=true;

            foreach (ObjectTypeTag tag in obj.typeTags)
            {
                itemLayout.AddChild(new UI.TextButton(0, 0, 32, 16, tag.ToString(), Color.LimeGreen));
            }

            foreach (ObjectStyleTag tag in obj.styleTags)
            {
                itemLayout.AddChild(new UI.TextButton(0, 0, 32, 16, tag.ToString(), Color.Yellow));
            }

            foreach (SpriteDef sd in obj.sprites)
            {
                var spr = sd.atlasId.GetRect();
                Rectangle atlasRect = new Rectangle(
                    (texture.Width * spr.X).Settle(),
                    (texture.Height * spr.Y).Settle(),
                    (texture.Width * spr.width).Settle(),
                    (texture.Height * spr.height).Settle()
                );
                UI.DraggableButton b = new UI.DraggableButton(0, 0, atlasRect.Width, atlasRect.Height);
                var spriteSlot = itemLayout.AddChild(b);

                b.OnClick += button =>
                {
                    obj.sprites.Remove(sd);
                    PopulateCatalog();
                    PopulateSprites();
                };

                spriteSlot.AddChild(new UI.Image(0, 0, atlasRect.Width, atlasRect.Height, texture, Color.White, atlasRect));
            }
        
            var newSpriteSlot = itemLayout.AddChild(new DropNewSlot(0, 0, 32, 32, obj));
        }
    }

    private static UI.Layout spritesLayout_;
    private static float sprScr;
    private static void PopulateSprites()
    {

        sprScr = spritesLayout_?.scrollAmount ?? 0;

        spritesLayout_?.Remove();
        spritesLayout_ = UI.RootRect.AddChild(new UI.Layout(10, 510, 1180, 180, null, UI.Layout.Mode.Horizontal));
        spritesLayout_.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal,false));

        spritesLayout_.scrollAmount = sprScr;
        spritesLayout_.lerpedScroll = sprScr;

        int i = 0;
        foreach (Sheet.ID id in Enum.GetValues(typeof(Sheet.ID)).Cast<Sheet.ID>())
        {

            if(id.ToString().Split('_')[0] != "Obj") continue;

            foreach (ObjectSpec spec in catalog)
            {
                foreach (SpriteDef def in spec.sprites)
                {
                    if(def.atlasId == id)
                        goto skip;
                }
            }

            var spr = id.GetRect();

            Rectangle atlasRect = new Rectangle(
                (texture.Width * spr.X).Settle(),
                (texture.Height * spr.Y).Settle(),
                (texture.Width * spr.width).Settle(),
                (texture.Height * spr.height).Settle()
            );

            var but = spritesLayout_.AddChild(new DraggableSprite(i%2*34, i/2*34, atlasRect.Width, atlasRect.Height, id));

            var img = but.AddChild(new UI.Image(0, 0, atlasRect.Width, atlasRect.Height, texture, Color.White,
                atlasRect));


            i++;

            skip:;

        }

        //spritesLayout_.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));
    }


    protected override void Update(GameTime gameTime)
    {
        Input.Update();
        base.Update(gameTime);

//        if (Keys.Down.WasPressed()) UI.defaultSpacing--;
//        if (Keys.Up.WasPressed()) UI.defaultSpacing++;
        if (Keys.Left.WasPressed()) UI.defaultMargins--;
        if (Keys.Right.WasPressed()) UI.defaultMargins++;

        if (Keys.S.WasPressed())
        {
            Save();   
        }

        if (Keys.L.WasPressed())
        {
            Load();
            PopulateCatalog();
            PopulateSprites();
        }

        UI.UpdateUI();
    }

    void Save()
    {
        var s = MessagePackSerializer.ToJson(catalog);
        File.WriteAllText("../../../objectsData", s);
    }

    void Load()
    {
        catalog.Clear();

        List<ObjectSpec> loaded =
            MessagePackSerializer.Deserialize<List<ObjectSpec>>(
                MessagePackSerializer.FromJson(File.ReadAllText("../../../objectsData")));
        PopulateCatalog();
        PopulateSprites();
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        
        Texture2D uiRender = UI.DrawUI();
        
        Graphics.GraphicsDevice.SetRenderTarget(null);
        sb.Begin();
        sb.Draw(uiRender, Graphics.GraphicsDevice.Viewport.Size().FittingMultiple(UI.pixelSize).FromSize(), Color.White);
        sb.End();

    }
}*/