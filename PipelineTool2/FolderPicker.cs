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
   public partial class FolderPicker : UserControl
   {
      public FolderPicker()
      {
         InitializeComponent();
      }

      public void InitPicker()
      {
         SetPath("NOT SELECTED");
      }

      public string Path { get; private set; }

      [Browsable(true)]
      public string Purpose { get; set; }

      public event Action<FolderPicker> OnSetValidPath;

      public bool IsValid => Directory.Exists(Path);

      public void SetPath(string path)
      {
         Path = path;
         path_label.Text = Purpose+": "+path;

         if (IsValid)
         {
            BackColor = Color.Transparent;
            OnSetValidPath?.Invoke(this);
         }
         else
         {
            Output.LogError(Purpose+" path not valid");
            BackColor = Color.Red;
         }
      }

      private void decent_folder_browser()
      {
         SaveFileDialog sf = new SaveFileDialog();
         sf.FileName = sf.Title = "Pick a Folder";
         sf.InitialDirectory = Directory.GetCurrentDirectory();
         if(sf.ShowDialog() == DialogResult.OK)
            SetPath(System.IO.Path.GetDirectoryName(sf.FileName));
      }

      private void browse_button_Click(object sender, EventArgs e)
      {
         decent_folder_browser();
      }
   }
}
