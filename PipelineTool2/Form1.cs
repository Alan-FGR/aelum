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
      public static Form1 Instance { get; private set; }

      private NotifyIcon notifIcon = new NotifyIcon();

      private FileSystemWatcher watcher_;
      private int ticksSinceLastFileChange = 0;
      private Timer timer_;
      
      Dictionary<string, AssetImporter> importers_ = new Dictionary<string, AssetImporter>
      {
         {"ATLAS SPRITE", new AssetImporter(".png")}
      };
      
      public Form1()
      {
         InitializeComponent();

         Instance = this;
         
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

         folderpicker_input.OnSetValidPath += picker => RefreshInputFiles();
         folderpicker_input.InitPicker();
         folderpicker_output.InitPicker();
         folderpicker_fxc.InitPicker();

         folderpicker_input.SetPath("D:\\FNA\\Aelum\\TestGame\\Assets");
         
         timer_ = new Timer();
         timer_.Tick += (sender, args) => Update();
         timer_.Interval = 1000; //todo
         timer_.Start();
      }

      void Update()
      {
         if (ticksSinceLastFileChange == 2)//todo
         {
            if (auto_build_checkbox.Checked)
            {
               Output.Log("Auto building assets...");
               //todo
            }
         }
         ticksSinceLastFileChange++;

      }

      void RefreshInputFiles()
      {
         if (!folderpicker_input.IsValid)
         {
            Output.LogError("Input path is not valid");
            return;
         }

         layout_paths.Controls.Clear();

         var allfiles = Directory.GetFiles(folderpicker_input.Path, "*.*", SearchOption.AllDirectories);
         
         foreach (string file in allfiles)
         {
            AddOrUpdateFile(file);
         }

         watcher_ = new FileSystemWatcher(folderpicker_input.Path);
         watcher_.SynchronizingObject = this;
         watcher_.IncludeSubdirectories = true;
         watcher_.EnableRaisingEvents = true;

         watcher_.NotifyFilter = NotifyFilters.Attributes|NotifyFilters.CreationTime|
                             NotifyFilters.DirectoryName|NotifyFilters.FileName|NotifyFilters.LastAccess|
                             NotifyFilters.LastWrite|NotifyFilters.Security|NotifyFilters.Size;

         watcher_.Changed += WatcherOnChanged;
         watcher_.Created += WatcherOnChanged;
         watcher_.Deleted += WatcherOnChanged;
         watcher_.Renamed += WatcherOnChanged;
         watcher_.Error += (sender, args) => Output.LogError("File system watching error! "+args);
      }

      private void AddOrUpdateFile(string file)
      {
         var parsedFile = ParseFile(file);
         var box = GetOrCreateGroupBoxFor(parsedFile);
         box.CreateOrUpdateFileEntry(parsedFile);
      }

      private void WatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
      {
         ticksSinceLastFileChange = 0;

         RenamedEventArgs rea = fileSystemEventArgs as RenamedEventArgs;

         if (File.Exists(fileSystemEventArgs.FullPath) || fileSystemEventArgs.ChangeType == WatcherChangeTypes.Deleted)
         {
            if (rea != null)
               AddOrUpdateFile(rea.OldFullPath);
            AddOrUpdateFile(fileSystemEventArgs.FullPath);
         }
         else if (fileSystemEventArgs.ChangeType != WatcherChangeTypes.Changed &&
                  fileSystemEventArgs.ChangeType != WatcherChangeTypes.All &&
                  Directory.Exists(fileSystemEventArgs.FullPath))
         {
            RefreshInputFiles(); //if dir created/renamed/deleted, we refresh the whole shebang
         }
      }

      PipelineGroupBox GetOrCreateGroupBoxFor(ParsedFile pf)
      {
         foreach (Control control in layout_paths.Controls)
         {
            var pgb = control as PipelineGroupBox;
            if (pgb != null)
            {
               if (pf.atlasName != null)
               {
                  if (pgb.atlas == pf.atlasName)
                     return pgb;
               }
               else if (pgb.path == pf.FileDir)
                  return pgb;
            }
         }

         //if we can't find suitable box, we create one
         var gb = new PipelineGroupBox(pf);
         gb.Parent = layout_paths;
         return gb;
      }
      
      private ParsedFile ParseFile(string file)
      {
         var dir = Path.GetDirectoryName(file);

         var relPath = Folders.MakePathRelative(dir);

         if (relPath.ToLower().Contains("atlas"))
         {
            //get last folder with 'atlas' keyword
            var folders = relPath.Split('/');
            for (var i = folders.Length - 1; i >= 0; i--)
            {
               string folder = folders[i];
               if (folder.ToLower().Contains("atlas"))
               {
                  return new ParsedFile(file, folder);
               }
            }
         }
         return new ParsedFile(file);
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
