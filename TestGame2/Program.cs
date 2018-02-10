using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

class TestGame : Core
{
   static void Main() { using (var game = new TestGame()) game.Run(); }

   public static SoundEffect ExplosionSound;

   protected override void Initialize()
   {
      Atlas.RegisterPipelineAssets();

      

      //create our player
      Entity player = new Entity(new Vector2(8,15));
//        new Quad(player, new QuadData(Atlas.player));
      new Sprite(player, new SpriteData(Vector2.One*8, Color.White, Atlas.hero_3x4.GetSprite().UvToPixels()));
      new PlayerScript(player);
      new LightOccluder(player, LightOccluder.OccluderShape.Horizontal, 2f);
      new SoundPlayer(player, Sound.laser); //jesus christ :(

      //we add a light for the player ship
      Texture2D lightTexture = Content.Load<Texture2D>(Other.light);
      Entity headlight = new Entity(player.Position+Vector2.UnitX, Vector2.UnitX); //pointing right (unitx)
      new LightProjector(headlight, 20, -15, lightTexture, Color.White);

      //we parent the light to the player entity
      new EntityContainer(player).AddChild(headlight);


      //create some rotating lights
      clearColor = Color.DarkGray;
      lightsBlendMode = BlendState.Additive;
      LightProjector.Systems.Default.BlendState = BlendState.Additive;
      LightOccluder.Systems.Default.shadowBias = 0;
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
        
      //audio
      MediaPlayer.Play(Music.Cache.bgm);
      MediaPlayer.Volume = 0.4f;


      //list all sprites in some UI
      UI.Layout spritesLo = UI.RootRect.AddChild(new UI.Layout(0,0,40,40));
      spritesLo.AddLayoutter(new UI.Layout.ExpandToParentSize(UI.Layout.Mode.Horizontal));
      spritesLo.AddLayoutter(new UI.Layout.AlignRelativeToParent(1,1));
      spritesLo.AddLayoutter(new UI.Layout.LayoutChildren(UI.Layout.Mode.Horizontal));

      foreach (var sprite in Sheet.Sprites)
      {
         spritesLo.AddChild(new SpriteUIRect(0, 0, 56, 32, sprite.Key));
      }

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
      interval_ += lastDT;
      if (interval_ > 3)
      {
         interval_ = 0;
         Vector2 position = new Vector2(Randy.Range(20, 40), Randy.Range(6, 20));
         CreateWorldSprite(position, Atlas.enemyA);
      }
   }

   private static void CreateWorldSprite(Vector2 position, int spriteId)
   {
      var entity = new Entity(position);
      new StaticBody(entity).CreateCollider(new rectangleColliderData(Vector2.One * 2));
      new QuadComponent(entity, new QuadData(spriteId));
      new LightOccluder(entity, LightOccluder.OccluderShape.Cross, 2);
   }

   class SpriteUIRect : UI.DraggableButton
   {
      public readonly int sprite;
        
      public SpriteUIRect(int x, int y, int w, int h, int sprite) : base(x, y, w, h, null)
      {
         this.sprite = sprite;
         var rptRect = Sheet.Get(sprite).UvToPixels();
         AddChild(new UI.Image(2, 2, rptRect.Width, rptRect.Height, atlas, Color.White, rptRect));
      }

      public override void DrawDraggingImage(Point mousePosition)
      {
         Rectangle sourceRectangle = Sheet.Get(sprite).UvToPixels();
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

