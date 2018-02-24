using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PipelineTool2;

public class PipelineGroupBox : GroupBox
{
   public readonly string path;
   public readonly string atlas;
   private readonly FlowLayoutPanel layout_;

   public PipelineGroupBox(ParsedFile pf)
   {
      path = pf.IsAtlas ? null : pf.FileDir;
      atlas = pf.atlasName;

      Text = atlas ?? path.Replace(Folders.Input,"ROOT");

      Width = 420;
      MinimumSize = new Size(420, 0);
      AutoSize = true;
      AutoSizeMode = AutoSizeMode.GrowAndShrink;
      Padding = new Padding(0);
      Margin = new Padding(8, 8, 0, 8);

      layout_ = new FlowLayoutPanel();
      layout_.Parent = this;
      layout_.Location = new Point(0, 19);
      layout_.FlowDirection = FlowDirection.TopDown;
      layout_.AutoSize = true;
      layout_.AutoSizeMode = AutoSizeMode.GrowAndShrink;
      layout_.Padding = new Padding(0);
      layout_.Margin = new Padding(0);
      
   }

   public void CreateOrUpdateFileEntry(ParsedFile pf)
   {
      foreach (Control control in Controls[0].Controls)
      {
         var fe = control as FileEntry;
         if (fe != null)
         {
            if (fe.parsedFile == pf)
            {
               fe.UpdateEntry();
               return;
            }
         }
      }

      var newFileEntry = new FileEntry(pf);
      newFileEntry.Parent = layout_;
      newFileEntry.Location = new Point(5, 0);
      newFileEntry.Margin = new Padding(8, 1, 0, 1);
   }

}