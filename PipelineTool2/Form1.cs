using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipelineTool2
{
   public partial class Form1 : Form
   {
      private NotifyIcon notifIcon = new NotifyIcon();

      Dictionary<string, AssetImporter> importers_ = new Dictionary<string, AssetImporter>
      {
         {"ATLAS SPRITE", new AssetImporter(".png")}
      };

      public Form1()
      {
         InitializeComponent();

         Output.outputBox = rtfbox_output;

         notifIcon.Icon = Icon;
         notifIcon.Text = "ælum Pipeline Tool";
         notifIcon.ContextMenu = new ContextMenu(new []{new MenuItem("Quit", OnNotifQuitClick), });
         notifIcon.Visible = true;
         notifIcon.Click+=NotifIconOnClick;

         #region Help RTF Copypaste

         testbox_help.Rtf = @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 Calibri;}{\f1\fnil\fcharset2 Symbol;}}
{\*\generator Msftedit 5.41.21.2510;}\viewkind4\uc1\pard\sa200\sl276\slmult1\lang22\b\f0\fs28 Paths (in this page):\fs22\par
\pard{\pntext\f1\'B7\tab}{\*\pn\pnlvlblt\pnf1\pnindent0{\pntxtb\'B7}}\fi-360\li720\sa200\sl276\slmult1 Sources: \b0 The folder containing all your assets\b\par
{\pntext\f1\'B7\tab}Output:\b0  The folder where import assets should be copied\b\par
{\pntext\f1\'B7\tab}Path to FXC:\b0  Path to the folder containing a version of the DirectX Effects 2.0 compiler (fxc.exe)\b\par
\pard\sa200\sl276\slmult1\fs28 User Interface:\par
\pard{\pntext\f1\'B7\tab}{\*\pn\pnlvlblt\pnf1\pnindent0{\pntxtb\'B7}}\fi-360\li720\sa200\sl276\slmult1\fs22 Auto Build:\b0  When this is ticked, assets will be rebuilt when they are modified on disk\fs28\par
\b\fs22{\pntext\f1\'B7\tab}Build New: \b0 Builds new files, these are files that were modified while application was running\fs28\par
\b\fs22{\pntext\f1\'B7\tab}Build All: \b0 Builds all files listed in the ""Assets"" tab (can be used to force rebuild)\fs28\par
\pard\sa200\sl276\slmult1\b General Usage:\par
\b0\fs22 Select the paths required for the program, your assets will show in the ""Assets"" tab, the importer is automatically detected from the file extension, you may however change the importer to be used at any time. Clicking the individual ""Build"" button will build only that single asset.\par
\b About Naming Processors (Namers):\par
In a nutshell:\line\tab Input values:\line\tab\tab string \b0 path\b\line\tab\tab string \b0 file\b\line\tab\tab string \b0 folder\b\line\tab Output:\line\tab\tab string\par
Detailed information:\par
\b0 Think of these as shaders but for filenames and in C#.\line You get these input variables:\par
\pard{\pntext\f1\'B7\tab}{\*\pn\pnlvlblt\pnf1\pnindent0{\pntxtb\'B7}}\fi-360\li720\sa200\sl276\slmult1 'file' which contains the filename without extension\par
{\pntext\f1\'B7\tab}'path' which contains the full path to the file\par
{\pntext\f1\'B7\tab}'folder' which is the folder where the file is (last in path)\par
\pard\sa200\sl276\slmult1 You have to return a string that will be used as the name your asset resource.\par
\b NOTE: \b0 The ""Output"" tab shows important information on errors and progress, please always check it before submitting a bug report.\par
}
 ";
         #endregion
         
         folderpicker_input.InitPicker();
         folderpicker_output.InitPicker();
         folderpicker_fxc.InitPicker();

         folderpicker_input.SetPath("D:\\FNA\\Aelum\\TestGame\\Assets");
         
         RefreshInputFiles();

         foreach (var atlas in atlases)
         {
            CreateGroupBoxForPath(atlas, true);
         }

         foreach (var pathFile in pathsAndFiles)
         {
            CreateGroupBoxForPath(pathFile, false);
         }
      }

      private void CreateGroupBoxForPath(KeyValuePair<string, List<string>> pathFile, bool atlas)
      {
         var gb = new GroupBox();
         gb.Text = atlas ? pathFile.Key : pathFile.Key.Replace(folderpicker_input.Path,"ROOT");
         gb.Parent = layout_paths;
         gb.Width = 420;
         gb.MinimumSize = new Size(420, 0);
         gb.AutoSize = true;
         gb.AutoSizeMode = AutoSizeMode.GrowAndShrink;
         gb.Padding = new Padding(0);
         gb.Margin = new Padding(8, 8, 0, 8);

         var elo = new FlowLayoutPanel();
         elo.Parent = gb;
         elo.Location = new Point(0, 19);
         elo.FlowDirection = FlowDirection.TopDown;
         elo.AutoSize = true;
         elo.AutoSizeMode = AutoSizeMode.GrowAndShrink;
         elo.Padding = new Padding(0);
         elo.Margin = new Padding(0);

         foreach (string file in pathFile.Value)
         {
            var fe = new FileEntry(pathFile.Key, file, Path.Combine(folderpicker_input.Path,pathFile.Key));
            fe.Parent = elo;
            fe.Location = new Point(5, 0);
            fe.Margin = new Padding(8, 1, 0, 1);
         }
      }

      FilesManager pathsAndFiles;
      FilesManager atlases;

      struct ParsedFile
      {
         public readonly bool atlas;
         public readonly string key;

         public ParsedFile(bool atlas, string key, string file)
         {
            this.atlas = atlas;
            this.key = key;
         }
      }

      class FilesManager : Dictionary<string, List<string>>
      {
         public new List<string> this[string key]
         {
            get
            {
               if (!ContainsKey(key))
                  base[key] = new List<string>();
               return base[key];
            }
         }
      }

      void RefreshInputFiles()
      {
         if (!folderpicker_input.IsValid)
            return;

         pathsAndFiles = new FilesManager();
         atlases = new FilesManager();

         var allfiles = Directory.GetFiles(folderpicker_input.Path, "*.*", SearchOption.AllDirectories);
         
         foreach (string file in allfiles)
         {
            var parsedFile = ParseFile(file);
            
            if (parsedFile.atlas)
            {
               atlases[parsedFile.key].Add(file);
            }
            else
            {
               pathsAndFiles[parsedFile.key].Add(Path.GetFileName(file));
            }
         }
      }
      
      private ParsedFile ParseFile(string file)
      {
         var dir = Path.GetDirectoryName(file);
         Uri inputPath = new Uri(folderpicker_input.Path);
         string relPath = inputPath.MakeRelativeUri(new Uri(dir)).OriginalString;

         if (relPath.ToLower().Contains("atlas"))
         {
            //get last folder with 'atlas' keyword
            var folders = relPath.Split('/');
            for (var i = folders.Length - 1; i >= 0; i--)
            {
               string folder = folders[i];
               if (folder.ToLower().Contains("atlas"))
               {
                  return new ParsedFile(true, folder, file);
               }
            }
         }
         return new ParsedFile(false, dir, Path.GetFileName(file));
      }

      private void OnNotifQuitClick(object sender, EventArgs eventArgs)
      {
         Hide();
         Application.Exit();
      }

      private void NotifIconOnClick(object sender, EventArgs eventArgs)
      {
         Show();
      }

      private bool notifTipShown = false;
      private void Form1_FormClosing(object sender, FormClosingEventArgs e)
      {
         if (e.CloseReason == CloseReason.UserClosing)
         {
            Hide();
            if (!notifTipShown)
            {
               notifIcon.ShowBalloonTip(0, "Pipeline Tool in Tray", "Right click the icon to quit.", ToolTipIcon.Info);
               notifTipShown = true;
            }
            e.Cancel = true;
         }
      }
   }
}
