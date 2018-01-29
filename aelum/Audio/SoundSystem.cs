using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

public class SoundSystem : ChunkedComponentSystem<SoundPlayer, SoundSystem>
{
   private const float SOUNDS_DIST = 16;
   static SoundSystem()
   {
      //todo init/set default systems
   }

   public static float GetPanForPosition(float x)
   {
      return (x-Core.mainCam.Center.X)/SOUNDS_DIST/2; //todo review
   }

   public static float GetVolumeForPosition(Vector2 pos)
   {
      return (SOUNDS_DIST - Vector2.Distance(Core.mainCam.Center, pos) + SOUNDS_DIST/2)/SOUNDS_DIST; //todo review
   }

   public void CullSoundsInRect(RectF rect)
   {
      //TODO store in range list to check when stuff gets in/outta range
      //mark all as culled
      foreach (SoundPlayer player in GetAllComponents())
         player.ResetCullState();

      //unmark the ones in rect
      foreach (SoundPlayer player in GetComponentsInRect(rect))
         player.SetInRange();

      //process state changes
      foreach (SoundPlayer player in GetAllComponents())
         player.ProcessChange();

   }

   public static void PlayOneShotAt(SoundEffect sound, Vector2 position)
   {
      sound.Play(GetVolumeForPosition(position), 0, GetPanForPosition(position.X));
   }

}