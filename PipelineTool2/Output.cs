using System.Diagnostics;
using System.Windows.Forms;

public static class Output
{
   public static RichTextBox outputBox;

   public static void Log(string message, string details = "")
   {
      if(outputBox != null)
         outputBox.AppendText(message+"\n"+details+"\n\n"); //todo rtf formatting
//      else
         Debug.WriteLine(message+", "+details);
   }

   public static void LogError(string error)
   {
      if(outputBox != null)
         outputBox.AppendText("ERROR! "+error+"\n\n"); //todo color
//      else
         Debug.WriteLine("ERROR: "+error);
   }

}