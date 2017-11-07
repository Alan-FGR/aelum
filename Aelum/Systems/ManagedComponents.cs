using System.Collections.Generic;

public abstract class ComponentSystem<T, TSystem>
   where T : ManagedComponent<T, TSystem>
   where TSystem : ComponentSystem<T, TSystem>, new()
{
   private readonly List<T> components_ = new List<T>();
   public List<T> Components => components_;

   public void AddComponent(T component)
   {
      components_.Add(component);
   }

   public void RemoveComponent(T component)
   {
      components_.Remove(component);
   }
}

// component that has a management system
public abstract class ManagedComponent<T, TSystem> : Component
   where T : ManagedComponent<T, TSystem>
   where TSystem : ComponentSystem<T, TSystem>, new()
{
   public static readonly TSystem SYSTEM = new TSystem();

   protected ManagedComponent(Entity entity) : base(entity)
   {
      SYSTEM.AddComponent((T)this);
   }
   public override void FinalizeComponent()
   {
      SYSTEM.RemoveComponent((T)this);
   }
}
