
public class BehaviorSystem : ManagedComponentSystem<Behavior>
{
   public void Update()
   {
      foreach (Behavior behavior in LoopInverse())
         behavior.Update();
   }
}

public abstract class Behavior : ManagedComponent<Behavior, BehaviorSystem> {
   protected Behavior(Entity entity) : base(entity) {}
   public abstract void Update();

   public static void UpdateAllBehaviorSystems()
   {
      foreach (BehaviorSystem behaviorSystem in Systems)
      {
         behaviorSystem.Update();
      }
   }
}