using System;

public static class PipelineAssets
{
    public static T LoadAsset<T>(string name) where T : IDisposable
    {
        //THIS is cached for us, so we don't need to, but we could if it's slow
        return Content.Manager.Load<T>(name);
    }
}