using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class PlayerScript : Script {
    private float speed_;
    private Sheet.ID currentProjectile;
    public PlayerScript(Entity entity, float speed) : base(entity)
    {
        speed_ = speed;
        currentProjectile = Sheet.ID.small_projectile;
    }
    public override void Update()
    {
        float movement = Keys.W.IsDown() ? speed_ : Keys.S.IsDown() ? -speed_ : 0;
        float multiplier = Keys.LeftShift.IsDown() ? 2 : 1;
        entity.Position += new Vector2(0,movement * multiplier * Core.lastDT);

        if (Input.LMB.WasPressed())
        {
            var bullet = new Entity(entity.Position, Core.mainCam.WorldMousePosition - entity.Position);
            bullet.persistent = false; //we don't save projectiles
            new Quad(bullet, new QuadData(currentProjectile));
//            new Sprite(bullet, new SpriteData(Vector2.Zero, Color.White, currentProjectile.GetRect().UvToPixels()));
            new Projectile(bullet, 60, 1);
        }
        DebugHelper.AddDebugLine(entity.Position, Core.mainCam.WorldMousePosition, Color.Green);
    }
}

public class Projectile : Script
{
    private float speed_;
    private float lifeTime_;

    public Projectile(Entity entity, float speed, int damage) : base(entity)
    {
        speed_ = speed;
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
    }
    public override void Update()
    {
        entity.Rotation += Core.lastDT*speed_;
    }
}


class TestGame : Core
{
    static void Main() { using (var game = new TestGame()) game.Run(); }

    private Entity player_;

    public TestGame()
    {
        //create our player
        player_ = new Entity(new Vector2(8,15));
        new Quad(player_, new QuadData(Sheet.ID.player));
        new PlayerScript(player_, 25);
        new LightOccluder(player_, LightOccluder.OccluderShape.Horizontal, 2f);

        //create some rotating lights
        clearColor = Color.Black;
        lightsBlendMode = BlendState.Additive;
        LightProjector.blendState_ = BlendState.Additive;
        LightOccluder.SHADOW_BIAS = 0;
        for (int i = 0; i < 16; i++)
        {   
            Texture2D lightTexture = Content.Load<Texture2D>("light");
            var light = new Entity(new Vector2(Randy.Range(1f,35f),Randy.Range(1f,25f)));
            new LightProjector(light, 12, -8, lightTexture, Randy.NextSaturatedColor());
            new Rotate(light, Randy.Range(-3f,3f));
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

        //subscribe event, dropping sprites onto scene creates entities with the sprite
        UI.RootRect.OnDrop += WorldDropFromUI;

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

