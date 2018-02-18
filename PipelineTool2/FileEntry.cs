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
      public FileEntry(string path, string file, string relativeTo)
      {
         InitializeComponent();
         file_checkbox_label.Text = file.Replace(relativeTo, "");
      }
   }
}
