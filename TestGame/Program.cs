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
        currentProjectile = Sheet.ID.Obj_small_projectile;
    }
    public override void Update()
    {
        float movement = Keys.W.IsDown() ? speed_ : Keys.S.IsDown() ? -speed_ : 0;
        float multiplier = Keys.LeftShift.IsDown() ? 2 : 1;
        entity.Position += new Vector2(0,movement * multiplier * Core.lastDT);

        if (Input.LMB.WasPressed())
        {
            var bullet = new Entity(entity.Position, Core.mainCam.WorldMousePosition - entity.Position);
            new Quad(bullet, new QuadData(currentProjectile));
            new Projectile(bullet, 60, 1);
        }
        DebugHelper.AddDebugLine(entity.Position, Core.mainCam.WorldMousePosition, Color.Green);
    }
}

public class Projectile : Script
{
    private float speed_;
    private int damage_;
    private float lifeTime_;

    public Projectile(Entity entity, float speed, int damage) : base(entity)
    {
        damage_ = damage;
        speed_ = speed;
    }

    public override void Update()
    {
        entity.Position += entity.Direction * speed_ * Core.lastDT;
        lifeTime_ += Core.lastDT;

        var hit = Core.physWorld.RayCastSingle(entity.LastPosition, entity.Position);
        if (hit.Key != null)
        {
            var body = (hit.Key.Body.UserData as PhysicalBody); // THIS SUCKCKCKCKCKCSSSSSSSSSSSS
            body.entity.Dispose();
            entity.Dispose();
        }
        else if(lifeTime_ > 5)
            entity.Dispose();
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
        new Quad(player_, new QuadData(Sheet.ID.Obj_player));
        new PlayerScript(player_, 25);

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
            var enemy = new Entity(new Vector2(Randy.Range(20, 40), Randy.Range(6, 20)));
            new StaticBody(enemy).CreateCollider(new rectangleColliderData((Vector2.One * 2).ToVec2F()));
            new Quad(enemy, new QuadData(Sheet.ID.Obj_enemy1));
        }
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
        var e = new Entity(mainCam.WorldMousePosition);
        new StaticBody(e).CreateCollider(new rectangleColliderData((Vector2.One * 2).ToVec2F()));
        new Quad(e, new QuadData((obj as SpriteUIRect).sprite));
    }

}

