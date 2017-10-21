#region MIT License

/*
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

public static class C
{
    public const int IMGSIZE = 2048;
    public const string IMGFILE = "..\\..\\..\\TestGame\\Content\\atlas.png";
    public const string MAPFILE = "..\\..\\..\\ælum\\Generated\\SpriteSheet.cs";
//    public const string SOURCESROOT = "..\\..\\..\\TestGame\\Content\\_WORKING\\ATLAS";
    public const string SOURCESROOT = "..\\..\\..\\TestGame\\_ATLAS";

    public static float IMGSIZEF;
}

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
        // copy the files list and sort alphabetically
        string[] keys = new string[map.Count];
        map.Keys.CopyTo(keys, 0);
        List<string> outputFiles = new List<string>(keys);
        outputFiles.Sort();

        List<IdRect> IDs = new List<IdRect>();
        foreach (var fullpath in outputFiles)
        {
            // get the destination rectangle
            Rectangle destination = map[fullpath];

            string imagename = Path.GetFileNameWithoutExtension(fullpath);
                
            //sanitize string
            const string regexPattern = @"[^a-zA-Z0-9]";
            imagename = Regex.Replace(imagename, regexPattern, "_");

            if (fullpath.Contains("PLAYABLE")) imagename = "Plr_"+imagename; 
            else if (fullpath.Contains("CHARACTER")) imagename = "Char_"+imagename;
            else if (fullpath.Contains("WALLS")) imagename = "Wall_"+imagename;
            else if (fullpath.Contains("BLUEPRINT")) imagename = "BPS_"+imagename;
            else imagename = "Obj_"+imagename;
            
            IDs.Add(new IdRect {id = imagename, rect = destination});
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

                float x = idRect.rect.X/C.IMGSIZEF;
                float y = idRect.rect.Y/C.IMGSIZEF;
                float w = idRect.rect.Width/C.IMGSIZEF;
                float h = idRect.rect.Height/C.IMGSIZEF;

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

public class Program
{
	static int Main(string[] args)
	{
		return Launch(args);
	}

	public static int Launch(string[] args)
    {
		// make sure we have our list of exporters
		Exporters.Load();

		// try to find matching exporters
		IImageExporter imageExporter = new PngImageExporter();
		IMapExporter mapExporter = new CsGenExporter();
            
		// compile a list of images
		List<string> images = new List<string>();
        
        string[] allfiles = System.IO.Directory.GetFiles(C.SOURCESROOT, "*.*", System.IO.SearchOption.AllDirectories);
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

        // generate our output
		ImagePacker imagePacker = new ImagePacker();
        
		Bitmap outputImage;
		Dictionary<string, Rectangle> outputMap;

		// pack the image, generating a map only if desired
		int result = imagePacker.PackImage(images, true, true, C.IMGSIZE, C.IMGSIZE, 1, true, out outputImage, out outputMap);
		if (result != 0)
		{
			Console.WriteLine("There was an error making the image sheet.");
			return result;
		}

        C.IMGSIZEF = outputImage.Width;

		// try to save using our exporters
		if (File.Exists(C.IMGFILE)) File.Delete(C.IMGFILE);
		imageExporter.Save(C.IMGFILE, outputImage);

		if (File.Exists(C.MAPFILE)) File.Delete(C.MAPFILE);
        mapExporter.Save(C.MAPFILE, outputMap);

		return 0;
	}
    
}
