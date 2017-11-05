using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
    }
}



class Engine
{
    public static Scene scene;
}

class Scene
{
    private List<Node> nodes;
    private List<PluginSystemUntyped> systems;
}

abstract class PluginSystemUntyped
{
    
}

abstract class PluginSystem<T> : PluginSystemUntyped where T : Plugin
{
    private List<T> plugins;
}

class Node
{
    private List<Plugin> plugins;
}

abstract class Plugin
{
    private Node node;
}



