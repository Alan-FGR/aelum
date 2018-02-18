public class AssetImporter
{
   public readonly string[] extensions;

   public AssetImporter(params string[] extensions)
   {
      this.extensions = extensions;
   }
}