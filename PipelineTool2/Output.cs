using System.Diagnostics;
using System.Windows.Forms;
using PipelineTool2;

public static class Output
{
   static RichTextBox outputBox => Form1.Instance.rtfbox_output;

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