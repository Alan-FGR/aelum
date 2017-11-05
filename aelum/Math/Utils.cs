using Microsoft.Xna.Framework;

namespace aelum.Math
{

   class Utils
   {
      #region Trig

      public static float DirectionToAngle(Vector2 direction)
      {
         return (float)System.Math.Atan2(-direction.X, direction.Y);
      }

      public static Vector2 AngleToDirection(float radians)
      {
         return new Vector2(
               (float)-System.Math.Sin(radians),
               (float)System.Math.Cos(radians)
         );
      }

      #endregion
   }

}
