using System;
using PipelineTool2;

public static class Folders
{
   public static string Input = Form1.Instance.folderpicker_input.Path;
   public static string Output = Form1.Instance.folderpicker_output.Path;
   public static string Fxc = Form1.Instance.folderpicker_fxc.Path;

   public static string MakePathRelative(string dir)
   {
      string relPath = new Uri(Input).MakeRelativeUri(new Uri(dir)).OriginalString;
      return relPath;
   }
}