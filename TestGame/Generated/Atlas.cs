public static class Atlas { public const int big_projectile = 220811766;
 public const int enemy_projectile = 1605139876;
 public const int enemy2 = 1253124800;
 public const int enemyA = 943948607;
 public const int medium_projectile = -1060590801;
 public const int MISSING_SPRITE = 0;
 public const int player = 1875821083;
 public const int powerup_projectile = -1450232676;
 public const int small_projectile = -99237163;
public static void RegisterPipelineAssets() {
 Sheet.Sprites.Add(big_projectile, new RectF(0f,0.125f,0.265625f,0.1328125f));
 Sheet.Sprites.Add(enemy_projectile, new RectF(0f,0.265625f,0.265625f,0.1328125f));
 Sheet.Sprites.Add(enemy2, new RectF(0.2734375f,0.125f,0.234375f,0.234375f));
 Sheet.Sprites.Add(enemyA, new RectF(0.515625f,0f,0.234375f,0.234375f));
 Sheet.Sprites.Add(medium_projectile, new RectF(0.2734375f,0.3671875f,0.203125f,0.109375f));
 Sheet.Sprites[MISSING_SPRITE] = new RectF(0.515625f,0.2421875f,0.125f,0.125f);
 Sheet.Sprites.Add(player, new RectF(0f,0f,0.421875f,0.1171875f));
 Sheet.Sprites.Add(powerup_projectile, new RectF(0.7578125f,0f,0.1171875f,0.1171875f));
 Sheet.Sprites.Add(small_projectile, new RectF(0f,0.40625f,0.0859375f,0.0859375f));
}}