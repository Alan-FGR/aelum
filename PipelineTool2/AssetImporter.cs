using System.Collections.Generic;

public abstract class AssetImporter
{
   public readonly string assetName;
   public readonly string[] extensions;

   protected AssetImporter(string assetName, params string[] extensions)
   {
      this.assetName = assetName;
      this.extensions = extensions;
   }

   public abstract void Import(string input, string output);
}

public static class Importers
{
   private static AssetImporter[] importers_;
   static Importers()
   {
      importers_ = new[]
      {
         new TextureImporter(),
      };
   }



   public static List<string> GetImportersForExtension(string ext)
   {
      List<string> retList = new List<string>();
      foreach (AssetImporter importer in importers_)
         foreach (string importerExtension in importer.extensions)
            if (ext == importerExtension && !retList.Contains(importer.assetName))
               retList.Add(importer.assetName);
      if(retList.Count == 0)
         retList.Add("Unused");
      retList.Add("Copy");
      return retList;
   }

}

class TextureImporter : AssetImporter
{
   public TextureImporter() : base("Texture", ".png"){}

   public override void Import(string input, string output)
   {
      Output.Log($"importing {input} as {output}");
   }
}