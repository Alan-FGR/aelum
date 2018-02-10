using System;
using System.Collections.Generic;

public class SystemsManager<TSystem> : List<TSystem> where TSystem : new()
{
   public TSystem Default => this[0];

   public SystemsManager()
   {
      Add(new TSystem());
   }

   public void Reset()
   {
      Clear();
      Add(new TSystem());
   }

   public TSystem CreateSystem()
   {
      TSystem system = new TSystem();
      Add(system);
      return system;
   }
   
   private static void NotSupported(){throw new Exception("Overload not supported");}
   public SystemsManager(int capacity) : this(){NotSupported();}
   public SystemsManager(IEnumerable<TSystem> collection) : this(){NotSupported();}
}

public interface IComponentSystem<T>
{
   void OnComponentCtor(T component);
   void OnComponentFinalize(T component);
   List<T> GetAllComponents();
}

public abstract class ManagedComponentSystem<T> : IComponentSystem<T>
{
   private readonly List<T> components_ = new List<T>();

   public void OnComponentCtor(T component)
   {
      components_.Add(component);
   }

   public void OnComponentFinalize(T component)
   {
      components_.Remove(component);
   }

   public List<T> GetAllComponents()
   {
      return components_;
   }

   public IEnumerable<T> LoopInverse()
   {
      for (var i = components_.Count - 1; i >= 0; i--)
         yield return components_[i];
   }
}

// component that has a management systems
public abstract class ManagedComponent<T, TSystem> : Component
   where T : ManagedComponent<T, TSystem>
   where TSystem : IComponentSystem<T>, new()
{
   public static readonly SystemsManager<TSystem> Systems;

   static ManagedComponent()
   {
      Systems = new SystemsManager<TSystem>();
   }

   private readonly byte systemIndex_ = 0;
   /// <summary> The system this component belongs to </summary>
   public TSystem System => Systems[systemIndex_];

   //protected ManagedComponent(Entity entity) : base(entity){}
   
   protected ManagedComponent(Entity entity, byte system = 0) : base(entity)
   {
      systemIndex_ = system;
      Systems[system].OnComponentCtor((T)this);
   }

   public override void FinalizeComponent()
   {
      Systems[systemIndex_].OnComponentFinalize((T)this);
   }
}
