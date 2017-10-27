using Microsoft.Xna.Framework.Audio;
public static class Sound {
public static class Cache {
  public static SoundEffect explosion = PipelineAssets.LoadAsset<SoundEffect>("explosion.wav");
  public static SoundEffect laser = PipelineAssets.LoadAsset<SoundEffect>("laser.wav");
  public static SoundEffect noise = PipelineAssets.LoadAsset<SoundEffect>("noise.wav");
}
 public static string explosion = "explosion.wav";
 public static string laser = "laser.wav";
 public static string noise = "noise.wav";
}
