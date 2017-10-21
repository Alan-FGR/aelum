using System.Collections.Generic;

public abstract class ManagedComponent<T> : Component // component that has a management system
{
    private static List<ManagedComponent<T>> systemComponents_ = new List<ManagedComponent<T>>();
    
    public ManagedComponent(Entity entity) : base(entity)
    {
        systemComponents_.Add(this);
    }
    
    public override void FinalizeComponent()
    {
        systemComponents_.Remove(this);
    }
    protected static List<ManagedComponent<T>> GetAllComponents()
    {
        return systemComponents_;
    }
}

public abstract class Behavior : ManagedComponent<Behavior>
{
    public Behavior(Entity entity) : base(entity)
    {
    }

    public abstract void Update();

    public static void UpdateAll(float deltaTime)
    {
        for (var i = GetAllComponents().Count - 1; i >= 0; i--)
        {
            ManagedComponent<Behavior> managedComponent = GetAllComponents()[i];
            ((Behavior) managedComponent).Update();
        }
    }
}