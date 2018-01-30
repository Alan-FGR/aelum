using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

public class SoundPlayer : ChunkedComponent<SoundPlayer, SoundSystem>
{

   private string effectId_;
   private SoundEffectInstance effectInstance_;

   private bool inRange = false;
   private bool lastInRange = false; //was it in range last tick?

   public SoundPlayer(Entity entity, string effectId) : base(entity)
   {
      effectId_ = effectId;
      effectInstance_ = PipelineAssets.LoadAsset<SoundEffect>(effectId).CreateInstance();
   }

   internal void ResetCullState()
   {
      lastInRange = inRange;
      inRange = false;
   }

   internal void SetInRange()
   {
      inRange = true;
   }

   internal void ProcessChange()
   {
      if (lastInRange != inRange)
      {
         if (inRange) // became hearable / was unculled
         {
//                if(effectInstance_.IsLooped)
//                    effectInstance_.Play();
         }
         else //was culled out
         {
            effectInstance_.Stop();
         }
      }
      if (inRange)
      {
         float vol = SoundSystem.GetVolumeForPosition(entity.Position);
         float pan = SoundSystem.GetPanForPosition(entity.Position.X);
         effectInstance_.Volume = vol;
         effectInstance_.Pan = MathHelper.Clamp(pan, -1, 1);
//            DebugHelper.AddDebugText($"{vol}, {pan},\n {effectInstance_.Volume}, {effectInstance_.Pan}", entity.Position, Color.White);
      }
   }

   public void Play()
   {
      if (inRange)
      {
         effectInstance_.Stop(true);
         effectInstance_.Play();
      }
   }



   //TODO save more data
   public SoundPlayer(Entity entity, byte[] serialData) : this(entity, MessagePackSerializer.Deserialize<string>(serialData))
   {
   }

   public override ComponentData GetSerialData()
   {
      return new ComponentData(ComponentTypes.SoundPlayer, MessagePackSerializer.Serialize(effectId_));
   }

}