using System;
using System.IO;
using System.Windows.Forms;

public struct ParsedFile : IEquatable<ParsedFile>
{
   public readonly string atlasName;
   public readonly string fileAbs;

   public ParsedFile(string fileAbs, string atlasName = null)
   {
      this.fileAbs = fileAbs;
      this.atlasName = atlasName;
   }

   public string FileDir => Path.GetDirectoryName(fileAbs);
   public string FileName => Path.GetFileName(fileAbs);
   public bool IsAtlas => atlasName != null;



   //#### boilerplate ####
   public static bool operator ==(ParsedFile a, ParsedFile b)
   {
      return a.Equals(b);
   }

   public static bool operator !=(ParsedFile a, ParsedFile b)
   {
      return !(a == b);
   }

   public bool Equals(ParsedFile other)
   {
      return other.atlasName == atlasName && other.fileAbs == fileAbs;
   }

   public override bool Equals(object obj)
   {
      if (ReferenceEquals(null, obj)) return false;
      return obj is ParsedFile && Equals((ParsedFile) obj);
   }

   public override int GetHashCode()
   {
      return fileAbs.GetHashCode();
   }
}

