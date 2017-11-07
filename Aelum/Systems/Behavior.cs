
public class BehaviorSystem : ComponentSystem<Behavior, BehaviorSystem>
{
   public void Update()
   {
      for (var i = Components.Count-1; i >= 0; i--)
         Components[i].Update();
   }
}

public abstract class Behavior : ManagedComponent<Behavior, BehaviorSystem> {
   protected Behavior(Entity entity) : base(entity) {}
   public abstract void Update();
}