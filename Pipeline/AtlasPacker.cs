#region MIT License

/*
 * Copyright (c) 2017 alangamedev.com
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a 
 * copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation 
 * the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the Software 
 * is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
 * 
 */

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using PipelineTool;
using sspack;


public class CsGenExporter : IMapExporter
{
    public string MapExtension => "cs";
    public float atlasSize;
    public Func<string, string> namerFunc;

    public struct IdRect
    {
        public string varName;
        public Rectangle rect;
    }
    
    public void Save(string filename, Dictionary<string, Rectangle> map)
    {
//        Packer.GetSpriteName = (spriteName,fullpath) =>
//        {
//            if (fullpath.Contains("PLAYABLE")) return "Plr_"+spriteName; 
//            if (fullpath.Contains("CHARACTER")) return "Char_"+spriteName;
//            if (fullpath.Contains("WALLS")) return "Wall_"+spriteName;
//            if (fullpath.Contains("BLUEPRINT")) return "BPS_"+spriteName;
//            return "Obj_"+spriteName;
//        };

        // copy the files list and sort alphabetically
        string[] keys = new string[map.Count];
        map.Keys.CopyTo(keys, 0);
        List<string> outputFiles = new List<string>(keys);
        outputFiles.Sort();

        List<IdRect> IDs = new List<IdRect>();
        bool hasMissing = false;
        foreach (var fullpath in outputFiles)
        {
            // get the destination rectangle
            Rectangle destination = map[fullpath];

            string spriteName;
            if (fullpath.Contains("MISSING_SPRITE"))
            {
                spriteName = "MISSING_SPRITE";
                hasMissing = true;
            }
            else
                spriteName = namerFunc(fullpath);
                
            //sanitize string
            const string regexPattern = @"[^a-zA-Z0-9]";
            spriteName = Regex.Replace(spriteName, regexPattern, "_");
            IDs.Add(new IdRect {varName = spriteName, rect = destination});
        }

        if (!hasMissing)
        {
            Dbg.Write("No missing sprite image provided! Please make an image called MISSING_SPRITE.png in your atlas directory for meaningful visual debugging helper");
            IDs.Add(new IdRect {varName = "MISSING_SPRITE", rect = new Rectangle(0,0,32,32)});
        }


        using (StreamWriter writer = new StreamWriter(filename))
        {
            writer.Write("public static class Atlas {");

            foreach (IdRect idRect in IDs)
            {
                float x = idRect.rect.X/atlasSize;
                float y = idRect.rect.Y/atlasSize;
                float w = idRect.rect.Width/atlasSize;
                float h = idRect.rect.Height/atlasSize;
                if (idRect.varName == "MISSING_SPRITE")
                    writer.WriteLine($" public const int {idRect.varName} = 0;");
                else
                    writer.WriteLine($" public const int {idRect.varName} = {idRect.varName.GetHashCode()};");
            }

            writer.WriteLine("public static void RegisterPipelineAssets() {");

            foreach (IdRect idRect in IDs)
            {
                float x = idRect.rect.X/atlasSize;
                float y = idRect.rect.Y/atlasSize;
                float w = idRect.rect.Width/atlasSize;
                float h = idRect.rect.Height/atlasSize;
                if (idRect.varName == "MISSING_SPRITE")//TODO don't use add
                    writer.WriteLine($" Sheet.Sprites[{idRect.varName}] = new RectF({x}f,{y}f,{w}f,{h}f);");
                else
                    writer.WriteLine($" Sheet.Sprites.Add({idRect.varName}, new RectF({x}f,{y}f,{w}f,{h}f));");
            }

            writer.Write("}}");
        }


        #region oldshit
        /*
        using (StreamWriter writer = new StreamWriter(filename))
        {
            writer.Write(@"
            using System.Collections.Generic;

            public static class Sheet
            {
                public static readonly Dictionary<ID, RectF> Sprites = new Dictionary<ID, RectF>();

                public enum ID
                {
            ");
            foreach (IdRect idRect in IDs)
            {
                writer.WriteLine("        "+idRect.varName+",");
            }

            writer.Write(@"
            }

            static Sheet()
            {
            ");

            foreach (IdRect idRect in IDs)
            {
                float x = idRect.rect.X/atlasSize;
                float y = idRect.rect.Y/atlasSize;
                float w = idRect.rect.Width/atlasSize;
                float h = idRect.rect.Height/atlasSize;

                writer.WriteLine($"        Sprites[ID.{idRect.varName}] = new RectF({x}f,{y}f,{w}f,{h}f);");
            }

            writer.Write(@"
            }
            public static RectF Get(ID id)
            {
                return Sprites[id];
            }
            }
            ");
        }
        */
        #endregion
    }
}

public class Packer
{
    public static int Pack(string atlasDir, string imageOutputDir, string codeOutputDir, Func<string,string> namingFunc)
	{
        string imgFile = Path.Combine(imageOutputDir, "Atlas.png");
        string codeFile = Path.Combine(codeOutputDir, "Atlas.cs");

		// find all images
		List<string> images = new List<string>();
        foreach (var file in Directory.GetFiles(atlasDir, "*.*", SearchOption.AllDirectories)){
            FileInfo info = new FileInfo(file);
            if(info.Extension == ".png")
                images.Add(info.FullName);
        }
        
		// PACKIT!
        var imagePacker = new ImagePacker();
        Bitmap outputImage;
		Dictionary<string, Rectangle> outputMap;
		int result = imagePacker.PackImage(images, true, true, MAXIMGSIZE, MAXIMGSIZE, 1, true, out outputImage, out outputMap);
		if (result != 0)
		{
			Dbg.Write("There was an error making the image sheet.");
			return result;
		}
        
		if (File.Exists(imgFile)) File.Delete(imgFile);
        IImageExporter imageExporter = new PngImageExporter();
		imageExporter.Save(imgFile, outputImage);

		if (File.Exists(codeFile)) File.Delete(codeFile);
	    CsGenExporter mapExporter = new CsGenExporter();
	    mapExporter.namerFunc = namingFunc;
	    mapExporter.atlasSize = outputImage.Width;
        mapExporter.Save(codeFile, outputMap);
        
		return 200;
	}
    public const int MAXIMGSIZE = 8096;
}
