using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipelineTool2
{
   public partial class FileEntry : UserControl
   {
      public readonly ParsedFile parsedFile;

      private bool pendingChange_;
      public bool PendingChange
      {
         get => pendingChange_;
         private set {
            pendingChange_ = value;
            build_button.Text = "Build" + (pendingChange_ ? "*" : "");
            build_button.Font = new Font(build_button.Font, pendingChange_ ? FontStyle.Bold : 0);
         }
      }
      
      public FileEntry(ParsedFile pf)
      {
         InitializeComponent();

         parsedFile = pf;

         if (parsedFile.IsAtlas)
         {
            //this is a hack
            file_checkbox_label.Text = parsedFile.fileAbs.Split(new [] {parsedFile.atlasName}, StringSplitOptions.None).Last();
         }
         else
         {
            file_checkbox_label.Text = Path.GetFileName(parsedFile.fileAbs);
         }

         file_checkbox_label.Checked = true;

         Output.Log("File Added", parsedFile.fileAbs);

      }

      public void UpdateEntry()
      {
         if (!File.Exists(parsedFile.fileAbs))
         {
            Output.Log("File Removed", parsedFile.fileAbs);
            Dispose();
         }
         else
         {
            Output.Log("File Changed", parsedFile.fileAbs);
            PendingChange = true;
         }
      }

   }
}
