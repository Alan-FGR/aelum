using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Core
{
   private List<Scene> scenes_;
}

class Scene
{
   private List<Node> nodes_;
   private List<ComponentSystem> systems_;

   public T GetSystem<T>() where T : ComponentSystem
   {
      foreach (ComponentSystem system in systems_)
         if (system is T)
            return system as T;
      return null;
   }

   public T AddSystem<T>() where T : ComponentSystem, new()
   {
      T system = new T();
      systems_.Add(system);
      return system;
   }

}

class Node
{
   private Scene scene_;
   private List<WeakReference<Component>> componentsWeakRefs_;

   public T AddComponent<T>() where T : Component, new()
   {
      
   }

}

class ComponentSystem
{
   private List<Component> components_;
}

class Component
{
   private ComponentSystem system_;
   private Node node_;
}

class Program
{
   static void Main(string[] args)
   {

   }
}

