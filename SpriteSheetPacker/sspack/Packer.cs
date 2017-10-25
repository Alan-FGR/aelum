#region MIT License

/*
 * Copyright (c) 2017 alangamedev.com
 * Copyright (c) 2009-2010 Nick Gravelyn (nick@gravelyn.com), Markus Ewald (cygon@nuclex.org)
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using sspack;


public class CsGenExporter : IMapExporter
{
    public string MapExtension => "cs";

    public struct IdRect
    {
        public string id;
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

        if (Packer.GetSpriteName == null)
        {
            Console.WriteLine("no custom spritename function provided, using default");
            Packer.GetSpriteName = (spriteName,fullpath) =>
            {
                return spriteName;
            };
        }

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
                spriteName = Packer.GetSpriteName(Path.GetFileNameWithoutExtension(fullpath), fullpath);
                
            //sanitize string
            const string regexPattern = @"[^a-zA-Z0-9]";
            spriteName = Regex.Replace(spriteName, regexPattern, "_");
            IDs.Add(new IdRect {id = spriteName, rect = destination});
        }

        if (!hasMissing)
        {
            Console.WriteLine("No missing sprite image provided! Please make an image called MISSING_SPRITE.png in your atlas directory for meaningful visual debugging helper");
            IDs.Add(new IdRect {id = "MISSING_SPRITE", rect = new Rectangle(0,0,32,32)});
        }

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
                writer.WriteLine("        "+idRect.id+",");
            }

            writer.Write(@"
    }

    static Sheet()
    {
");

            foreach (IdRect idRect in IDs)
            {

                float x = idRect.rect.X/Packer.IMGSIZEF;
                float y = idRect.rect.Y/Packer.IMGSIZEF;
                float w = idRect.rect.Width/Packer.IMGSIZEF;
                float h = idRect.rect.Height/Packer.IMGSIZEF;

                writer.WriteLine($"        Sprites[ID.{idRect.id}] = new RectF({x}f,{y}f,{w}f,{h}f);");
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
    }
}

public class Packer
{
    public static int Pack(string atlasDir, string imageOutputDir, string codeOutputDir, Func<string,string,string> namingFunc = null)
	{
	    SOURCESROOT = atlasDir;
        IMGFILE = imageOutputDir + "\\atlas.png";
        MAPFILE = codeOutputDir + "\\SpritesDefs.cs";

		Exporters.Load();
		IImageExporter imageExporter = new PngImageExporter();
		IMapExporter mapExporter = new CsGenExporter();
            
		// compile a list of images
		List<string> images = new List<string>();
        
        string[] allfiles = Directory.GetFiles(SOURCESROOT, "*.*", SearchOption.AllDirectories);
        foreach ( var file in allfiles){
            FileInfo info = new FileInfo(file);
            if(info.Extension == ".png")
                images.Add(info.FullName);
        }

		// make sure no images have the same name if we're building a map
		for (int i = 0; i < images.Count; i++)
		{
			string str1 = Path.GetFileNameWithoutExtension(images[i]);

			for (int j = i + 1; j < images.Count; j++)
			{
				string str2 = Path.GetFileNameWithoutExtension(images[j]);

				if (str1 == str2)
				{
					Console.WriteLine("Two images have the same name: {0} = {1}", images[i], images[j]);
					return -1;
				}
			}
		}

        var imagePacker = new ImagePacker();
        
        Bitmap outputImage;
		Dictionary<string, Rectangle> outputMap;

		// pack the image, generating a map only if desired
		int result = imagePacker.PackImage(images, true, true, IMGSIZE, IMGSIZE, 1, true, out outputImage, out outputMap);
		if (result != 0)
		{
			Console.WriteLine("There was an error making the image sheet.");
			return result;
		}

        IMGSIZEF = outputImage.Width;
        
		if (File.Exists(IMGFILE)) File.Delete(IMGFILE);
		imageExporter.Save(IMGFILE, outputImage);

		if (File.Exists(MAPFILE)) File.Delete(MAPFILE);
        mapExporter.Save(MAPFILE, outputMap);
        
		return 0;
	}

    public static float IMGSIZEF;
    public const int IMGSIZE = 8096;
    public static string IMGFILE = "..\\..\\TestGame\\Content\\atlas.png";
    public static string MAPFILE = "..\\..\\..\\ælum\\Generated\\SpriteSheet.cs";
    public static string SOURCESROOT = "..\\..\\TestGame\\_ATLAS";
    public static Func<string, string, string> GetSpriteName;
}
