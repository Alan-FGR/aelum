using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class PlayerScript : Script {
    private const float SPEED = 25;

    //when you're not saving any data for script and it's persistent, this constructor is used when deserializing
    public PlayerScript(Entity entity) : base(entity){}

    public override void Update()
    {
        float movement = Keys.W.IsDown() ? SPEED : Keys.S.IsDown() ? -SPEED : 0;
        float multiplier = Keys.LeftShift.IsDown() ? 2 : 1;
        entity.Position += new Vector2(0,movement * multiplier * Core.lastDT);

        if (Input.LMB.WasPressed())
        {
            var bullet = new Entity(entity.Position, Core.mainCam.WorldMousePosition - entity.Position);
            new Quad(bullet, new QuadData(Sheet.ID.small_projectile)); //add quad to render bullet
            new Projectile(bullet, 60); // add projectile script to bullet
        }
        DebugHelper.AddDebugLine(entity.Position, Core.mainCam.WorldMousePosition, Color.Green);
    }
}

public class Projectile : Script
{
    private readonly float speed_;
    private float lifeTime_;

    public Projectile(Entity entity, float speed) : base(entity)
    {
        speed_ = speed;
        entity.persistent = false; //we don't save projectiles
    }

    public override void Update()
    {
        Vector2 lastPos = entity.Position;
        entity.Position += entity.Direction * speed_ * Core.lastDT;
        lifeTime_ += Core.lastDT;
        DebugHelper.AddDebugLine(lastPos, entity.Position, Color.Yellow);
        var hit = Core.physWorld.RayCastSingle(lastPos, entity.Position);
        if (hit.Key != null)
        {
            hit.Key.GetPhysicalBody().entity.Destroy();
            entity.Destroy();
        }
        else if(lifeTime_ > 5)
            entity.Destroy();
    }
}

public class Rotate : Script
{
    private float speed_;
    public Rotate(Entity entity, float speed) : base(entity)
    {
        speed_ = speed;
        StoreScriptData("speed",speed_);//we store the data for this script, so it persists in save file
    }
    //constructor used for deserialization
    public Rotate(Entity entity) : base(entity){}

    protected override void AfterDeserialization()
    {
        speed_ = RetrieveScriptData<float>("speed");//we retrieve data stored when deserializing
    }
    public override void Update()
    {
        entity.Rotation += Core.lastDT*speed_;
    }
}


class TestGame : Core
{
    static void Main() { using (var game = new TestGame()) game.Run(); }
    
    public TestGame()
    {
        //create our player
        Entity player = new Entity(new Vector2(8,15));
        new Quad(player, new QuadData(Sheet.ID.player));
        new PlayerScript(player);
        new LightOccluder(player, LightOccluder.OccluderShape.Horizontal, 2f);


        //we add a light for the player ship
        Texture2D lightTexture = Content.Load<Texture2D>("light");
        Entity headlight = new Entity(player.Position+Vector2.UnitX, Vector2.UnitX); //pointing right (unitx)
        new LightProjector(headlight, 20, -15, lightTexture, Color.White);

        //we parent the light to the player entity
        new EntityContainer(player).AddChild(headlight);


        //create some rotating lights
        clearColor = Color.Black;
        lightsBlendMode = BlendState.Additive;
        LightProjector.blendState_ = BlendState.Additive;
        LightOccluder.SHADOW_BIAS = 0;
        for (int i = 1; i < 4; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                var light = new Entity(new Vector2(i*9,j*23));
                new LightProjector(light, 20, -15, lightTexture, Randy.NextSaturatedColor());
                new Rotate(light, Randy.Range(-3f,3f));
            }
        }

        //subscribe event
        OnBeforePhysicsUpdate += SpawnEnemy;

        //list all sprites in some UI
        UI.Layout spritesLo = UI.RootRect.AddChild(new UI.Layout(0,0,40,40));
        spritesLo.AddLayoutter(new UI.Layout.ExpandToParentSize(UI.Layout.Mode.Horizontal));
        spritesLo.AddLayoutter(new UI.Layout.AlignRelativeToParent(1,1));
        spritesLo.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));
        foreach (Sheet.ID id in System.Enum.GetValues(typeof(Sheet.ID)).Cast<Sheet.ID>())
            spritesLo.AddChild(new SpriteUIRect(0, 0, 56, 32, id));

        //some help UI
        UI.RootRect.AddChild(new UI.TextRect(0,0,200,10,
            "Controls: W,A,LMB. Drag and drop sprites from bottom onto scene, press 1 to save scene, 2 to load"
            ));

        //subscribe event, dropping sprites onto scene creates entities with the sprite
        UI.RootRect.OnDrop += WorldDropFromUI;


        //saving and loading scene
        OnBeforeLogicUpdate += () =>
        {
            if (Keys.D1.WasPressed())
                Entity.SaveAll("savedata");
            else if (Keys.D2.WasPressed())
            {
                Entity.DestroyAll();
                Entity.LoadAll("savedata");
            }
        };


    }
    
    private float interval_ = 3;
    public void SpawnEnemy()
    {
        interval_ += Core.lastDT;
        if (interval_ > 3)
        {
            interval_ = 0;
            Vector2 position = new Vector2(Randy.Range(20, 40), Randy.Range(6, 20));
            Sheet.ID spriteId = Sheet.ID.enemy1;
            CreateWorldSprite(position, spriteId);
        }
    }

    private static void CreateWorldSprite(Vector2 position, Sheet.ID spriteId)
    {
        var entity = new Entity(position);
        new StaticBody(entity).CreateCollider(new rectangleColliderData(Vector2.One * 2));
        new Quad(entity, new QuadData(spriteId));
        new LightOccluder(entity, LightOccluder.OccluderShape.Cross, 2);
    }

    class SpriteUIRect : UI.DraggableButton
    {
        public readonly Sheet.ID sprite;
        
        public SpriteUIRect(int x, int y, int w, int h, Sheet.ID sprite) : base(x, y, w, h, null)
        {
            this.sprite = sprite;
            var rptRect = sprite.GetRect().UvToPixels();
            AddChild(new UI.Image(2, 2, rptRect.Width, rptRect.Height, atlas, Color.White, rptRect));
        }

        public override void DrawDraggingImage(Point mousePosition)
        {
            Rectangle sourceRectangle = sprite.GetRect().UvToPixels();
            UIBatch.Draw(atlas,
                mousePosition.ToVector2()-new Vector2(sourceRectangle.Width/2, sourceRectangle.Height/2),
                sourceRectangle, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
    }

    private void WorldDropFromUI(UI.IDraggable obj)
    {
        CreateWorldSprite(mainCam.WorldMousePosition, (obj as SpriteUIRect).sprite);
    }

}

