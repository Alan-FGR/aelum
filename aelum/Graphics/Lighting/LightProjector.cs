using System;
using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class LightProjector : ManagedChunkComponent<LightProjector, LightSystem>
{
   private RenderTarget2D lightProjectorRT_;

//   public struct Result
//   {
//      public Texture2D texture;
//      public Effect lastBlurEffect;
//   }

   static LightProjector()
   {
      Camera.DEFAULT_RENDER_PATH.Enqueue(new Camera.RenderLayer(DEFAULT_SYSTEM,1), 500);
   }
   
   public LightProjector(Entity entity, byte system = 0) : base(entity, system)
   {}
   
   public LightProjector(Entity entity, float size, float centerOffset, Texture2D lightTexture, Color? color = null) : this(entity)
   {
      cfg.size = size;
      cfg.centerOffset = centerOffset;
      cfg.lightTexture = lightTexture;
      cfg.lightColor = color ?? Color.White;
      InitProjectorRT();
   }
   
   [MessagePackObject]
   public struct LightProjectorConfig
   {
      [Key(0)] public float size;
      [Key(1)] public float centerOffset;
      [IgnoreMember] public Texture2D lightTexture;
      [Key(2)] public string TextureName => lightTexture.Name;
      [Key(3)] public Color lightColor;

      //TODO FIX ALL THIS SHIT
      [SerializationConstructor]
      public LightProjectorConfig(float size, float centerOffset, string TextureName, Color lightColor) //WTH!
      {
         this.size = size;
         this.centerOffset = centerOffset;
         this.lightTexture = Content.Manager.Load<Texture2D>(TextureName); //WTFH!! TODO
         this.lightColor = lightColor;
      }

      public LightProjectorConfig(float size, float centerOffset, Texture2D lightTexture, Color lightColor)
      {
         this.size = size;
         this.centerOffset = centerOffset;
         this.lightTexture = lightTexture;
         this.lightColor = lightColor;
      }
   }
   public LightProjectorConfig cfg;

   public LightProjector(Entity entity, byte[] serialData) : this(entity)
   {
      cfg = MessagePackSerializer.Deserialize<LightProjectorConfig>(serialData);
      InitProjectorRT();
   }

   public void InitProjectorRT()
   {
      lightProjectorRT_?.Dispose();
      lightProjectorRT_ = new RenderTarget2D(Graphics.Device, Core.mainCam.MainRenderTarget.Width / System.shadowsQuality, Core.mainCam.MainRenderTarget.Height / System.shadowsQuality);
   }
   
   public void Accumulate(SpriteBatch accumulationBatch)
   {
      accumulationBatch.Draw(lightProjectorRT_, Vector2.Zero, cfg.lightColor);
   }

   public virtual void RenderProjector(Effect shadowsEffect, int occludersSegmentsCount)
   {
      Graphics.Device.SetRenderTarget(lightProjectorRT_);
      Graphics.Device.Clear(Color.Black);

      //set projector corners
      float sinT = (float)Math.Sin(entity.Rotation + Math.PI / 4);
      float cosT = (float)Math.Cos(entity.Rotation + Math.PI / 4);

      Vector3 fwdCenter = MathUtils.AngleToDirection(entity.Rotation).ToVector3() * -cfg.centerOffset;

      Vector3 localCorner0 = fwdCenter + new Vector3(cfg.size * -sinT, cfg.size * cosT, 0);
      Vector3 localCorner1 = fwdCenter + new Vector3(cfg.size * cosT, cfg.size * sinT, 0);
      Vector3 localCorner2 = fwdCenter + new Vector3(cfg.size * sinT, cfg.size * -cosT, 0);
      Vector3 localCorner3 = fwdCenter + new Vector3(cfg.size * -cosT, cfg.size * -sinT, 0);

      shadowsEffect.Parameters["Origin"].SetValue(entity.Position); //set shadows extrude origin
      shadowsEffect.Parameters["Texture"].SetValue(cfg.lightTexture);
      shadowsEffect.Parameters["projCorner0"].SetValue(entity.Position.ToVector3(-9) + localCorner0);
      shadowsEffect.Parameters["projCorner1"].SetValue(entity.Position.ToVector3(-9) + localCorner1);
      shadowsEffect.Parameters["projCorner2"].SetValue(entity.Position.ToVector3(-9) + localCorner2);
      shadowsEffect.Parameters["projCorner3"].SetValue(entity.Position.ToVector3(-9) + localCorner3);

      shadowsEffect.CurrentTechnique.Passes[0].Apply();

      //Core.GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;

      Graphics.Device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, occludersSegmentsCount * 4 + 4, 0, occludersSegmentsCount * 2 + 2);

   }

   public override void FinalizeComponent()
   {
      lightProjectorRT_.Dispose();
      base.FinalizeComponent();
   }

   public override ComponentData GetSerialData()
   {
      return new ComponentData(ComponentTypes.LightProjector, MessagePackSerializer.Serialize(cfg));
   }

   
   
}