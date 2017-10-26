﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Microsoft.CSharp;
using Timer = System.Windows.Forms.Timer;

public static class Dbg
{
    public static TextBox UiBox;
    static ConcurrentQueue<string> dbgQueue_ = new ConcurrentQueue<string>();

    public static void Write(string text)
    {
        Debug.WriteLine(text);
        Console.WriteLine(text);
        UiBox?.AppendText(text+"\r\n");
    }

    public static void SafeWrite(string text)
    {
        dbgQueue_.Enqueue(text);
    }

    public static void Tick()
    {
        for (int i = 0; i < 128; i++)
        {
            if (dbgQueue_.IsEmpty) return;
            if (dbgQueue_.TryDequeue(out string item))
            {
                Write(item);
            }
        }
    }
}

public class FileChangesBuffer
{
    private readonly object lock_ = new object();

    private readonly List<string> accumulator_ = new List<string>();
    private readonly List<string> inputFlow_ = new List<string>();

    private readonly FileSystemWatcher fsw_;

    public FileChangesBuffer(string path)
    {
        Dbg.Write("creating watcher for path "+path);
        fsw_ = new FileSystemWatcher(path);
        fsw_.IncludeSubdirectories = true;
        fsw_.EnableRaisingEvents = true;

        fsw_.NotifyFilter = NotifyFilters.Attributes|NotifyFilters.CreationTime|
                            NotifyFilters.DirectoryName|NotifyFilters.FileName|NotifyFilters.LastAccess|
                            NotifyFilters.LastWrite|NotifyFilters.Security|NotifyFilters.Size;

        fsw_.Changed += ProcessChange;
        fsw_.Created += ProcessChange;
        fsw_.Deleted += ProcessChange;
        Dbg.Write($"watcher for {path} created");
    }

    ~FileChangesBuffer(){fsw_?.Dispose();} // take a walk on the safe side ;)
    public void Stop()
    {
        Dbg.Write("stopping watcher for "+fsw_.Path);
        fsw_.EnableRaisingEvents = false;
        fsw_.Dispose();
    }

    private void ProcessChange(object sender, FileSystemEventArgs e) //MT
    {
        Dbg.SafeWrite("detected file change "+e.FullPath);
        RegisterChange(e.FullPath);
    }

    void RegisterChange(string file) //MT
    {
        lock (lock_)
        {
            Dbg.SafeWrite("enqueueing changed file "+file);
            inputFlow_.Add(file);
        }
    }

    public string[] ConsumeAccumulatedChanges() //only if results are ready
    {
        lock (lock_)
        {
            if (inputFlow_.Count == 0)
            {
                if (accumulator_.Count > 0)
                {
                    Dbg.Write("collecting results available for "+fsw_.Path);
                    var retArray = accumulator_.ToArray();
                    accumulator_.Clear();
                    return retArray;
                }
                return new string[]{};
            }
            Dbg.Write("postponing results; processing pending changes for "+fsw_.Path);
            accumulator_.AddRange(inputFlow_);
            inputFlow_.Clear();
            return new string[]{};
        }
    }
}

[DesignerCategory("")] // we don't want useless tools
public class PipelineTool : Form
{
    //text IDS
    public const string ATLAS = "Atlas";
    public const string SOUND = "Sound";
    public const string MUSIC = "Music";
    public const string FONTS = "Fonts";
    public const string SHADR = "Shaders";
    public const string MESHS = "Meshes";
    public const string OTHER = "Other"; //just copy
    public static string[] PTHS_IDS = {ATLAS,SOUND,MUSIC,FONTS,SHADR,MESHS,OTHER,};

    public const string DXCPL = "DX Compiler";
    public const string XNBCP = "XNB Compiler";
    public const string FFMPG = "FFMpeg";
    public const string BLEND = "Blender";
    public static string[] BINS_IDS = {DXCPL,XNBCP,FFMPG,BLEND,};

    public const string OUTBN = "OutputBinaries";
    public const string OUTCD = "OutputCode";
    public static string[] OUTS_IDS = {OUTBN,OUTCD,};

    //misc
    public const string SAVEF = "ToolConf";

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new PipelineTool());
    }

    public static SaneToggleButton MasterSwitch;

    public static Dictionary<string,object> GlobalData = new Dictionary<string, object>();
    public static Action GlobalSave;
    public static Action GlobalLoad;

    public static Action GlobalTick;

    public class DirectoryWidget : SanePanel
    {
        //UI members
        protected SaneLabel label_;
        protected SaneButton button_;
        
        //events
        public Action PathChanged;

        //data members
        protected string id_;
        protected string path_;
        public void SetPath(string path)
        {
            path_ = path;
            label_.Text = $"{id_}: {path_}";
            PathChanged?.Invoke();
        }

        public DirectoryWidget(Control parent, string id, int width = 14, int height = 1) : base(parent, width, height)
        {
            id_ = id;
            label_ = new SaneLabel(this, id);
            label_.SaneCoords.SaneScale(12, 1);
            button_ = new SaneButton(this, "Browse...");
            button_.SaneClick += Browse;
            button_.SaneCoords.SanePosition(label_.SaneCoords.Width, 0);
            GlobalSave += SaveData;
            GlobalLoad += LoadData;
        }

        public virtual void Browse(SaneButton b)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Directory.GetCurrentDirectory();
            fbd.Description = "Sorry for this terrible UI. Blame Microsoft :P";
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                SetPath(fbd.SelectedPath);
            }
        }

        public virtual void LoadData()
        {
            if (GlobalData.TryGetValue(id_, out object obj))
                SetPath(obj as string);
        }

        public virtual void SaveData()
        {
            GlobalData[id_] = path_;
        }

    }

    public class FileBrowserWidget : DirectoryWidget
    {
        public FileBrowserWidget(Control parent, string id, int width = 14, int height = 1) : base(parent, id, width, height)
        {}

        public override void Browse(SaneButton b)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                SetPath(ofd.FileName);
            }
        }
    }

    class NamerProcessor
    {
        public NamerProcessor(string code)
        {
            string source = 
                "namespace DYNAMN { public class DYNAMC { public string DYNAMF(string path, string file) { "+code+"; } } } ";
            Dictionary<string, string> providerOptions = new Dictionary<string, string>{{"CompilerVersion", "v3.5"}};
            CSharpCodeProvider provider = new CSharpCodeProvider(providerOptions);
            CompilerParameters compilerParams = new CompilerParameters {GenerateInMemory = true, GenerateExecutable = false};
            CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, source);
            if (results.Errors.Count > 0)
            {
                string err = "Errors compiling code:\r\n";
                foreach (CompilerError error in results.Errors)
                    err += error.ErrorText+"\r\n";
                throw new Exception(err);
            }
            instance_ = results.CompiledAssembly.CreateInstance("DYNAMN.DYNAMC");
            methodInfo_ = instance_.GetType().GetMethod("DYNAMF");
        }
        
        private MethodInfo methodInfo_;
        private object instance_;

        public string RunProcessor(string input)
        {
            return methodInfo_.Invoke(instance_, new object[]{Path.GetFullPath(input),Path.GetFileNameWithoutExtension(input)}) as string;
        }
    }

    public class AssetDirectoryMonitorWidget : DirectoryWidget
    {
        //ui
        private SaneToggleButton switch_;
        private SaneTextBox namerBox_;
        private SaneTextBox namerPreviewBox_;

        private FileChangesBuffer monitorBuffer_;
        private int ticks_;

        public AssetDirectoryMonitorWidget(Control parent, string id, SaneTabs namerTabs, int width = 14, int height = 1) : base(parent, id, width, height)
        {
            label_.SaneCoords.SanePosition(2, 0);
            label_.SaneCoords.SaneScale(10, 1);

            switch_ = new SaneToggleButton(this);
            switch_.SaneClick += b => { StateChanged(); };

            //shader editor
            var page = namerTabs.NewPage(id);
            new SaneLabel(page, id+" Naming Processor");
            var minihelp = new SaneLabel(page, "vars: file, path", 8);
            minihelp.SaneCoords.SanePosition(6, 0);
            minihelp.TextAlign = ContentAlignment.BottomRight;
            minihelp.Font = new Font(FontFamily.GenericMonospace, 9);
            namerBox_ = new SaneTextBox(page,14,12);
            namerBox_.SaneCoords.SanePosition(0, 1);
            namerBox_.Text = "return file;";
            namerBox_.Font = new Font(FontFamily.GenericMonospace, 10);
            var previewNamerButton = new SaneButton(page,"Preview Results (Output Below)",8);
            previewNamerButton.SaneCoords.SanePosition(3, 13);
            previewNamerButton.SaneClick += button => { PreviewNamer(); };
            namerPreviewBox_ = new SaneTextBox(page,14,6);
            namerPreviewBox_.SaneCoords.SanePosition(0, 14);
            namerPreviewBox_.ReadOnly = true;

            PathChanged += StateChanged;
            GlobalTick += Tick;
        }

        void Tick()
        {
            // delayed subscribe so when user changes namer code auto compilation stops
            if (ticks_ == 0) namerBox_.TextChanged += (sender, args) =>{MasterSwitch.Toggled = false;};

            ticks_++;
            if (MasterSwitch.Toggled && switch_.Toggled)
            {
                AutoBuild();
            }
        }

        void AutoBuild()
        {
            switch_.Text = "Toggle "+ticks_ % 10;
            BuildInternal(GetMonitorResults());
        }

        public void BuildAll(bool ignoreToggle = false)
        {
            if (ignoreToggle)
            {
                BuildInternal(GetAllFilesInPath());
            }
            else
            {
                if (switch_.Toggled)
                {
                    BuildInternal(GetAllFilesInPath());
                }
            }
        }

        private void BuildInternal(string[] filesToBuild)
        {
            if (filesToBuild.Length == 0) return;
            
            Dbg.Write("building "+id_+" from "+path_);
            foreach (string s in filesToBuild)
            {
                Dbg.Write(s);
            }
        }

        string[] GetAllFilesInPath()
        {
            if(Directory.Exists(path_))
                return Directory.GetFiles(path_, "*.*", SearchOption.AllDirectories);
            Dbg.Write(id_+" path is not valid "+path_);
            return new string[]{};
        }

        void StateChanged()
        {
            if (switch_.Toggled)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        void StartMonitoring()
        {
            StopMonitoring();
            if(Directory.Exists(path_))
                monitorBuffer_ = new FileChangesBuffer(path_);
            else
                Dbg.Write("can't monitor path for "+id_+" - "+path_);
        }

        void StopMonitoring()
        {
            monitorBuffer_?.Stop();
            monitorBuffer_ = null;
        }

        string[] GetMonitorResults()
        {
            if (monitorBuffer_ != null)
            {
                return monitorBuffer_.ConsumeAccumulatedChanges();
            }
            Dbg.Write("no valid monitor for "+id_+", disabling monitoring");
            switch_.Toggled = false;
            StateChanged();
            return new string[]{};
        }

        public override void SaveData()
        {
            GlobalData[id_] = new Tuple<string, bool, string>(path_, switch_.Toggled, namerBox_.Text);
        }

        public override void LoadData()
        {
            if (GlobalData.TryGetValue(id_, out object obj))
            {
                var data = obj as Tuple<string, bool, string>;
                SetPath(data.Item1);
                switch_.Toggled = data.Item2;
                namerBox_.Text = data.Item3;
                StateChanged();
            }
        }


        //namer processing
        void PreviewNamer()
        {
            namerPreviewBox_.Clear();
            string[] filesInPath = GetAllFilesInPath();
            if (filesInPath.Length == 0)
            {
                namerPreviewBox_.AppendText("NO VALID FILE IN PATH!");
                return;
            }

            try
            {
                NamerProcessor proc = new NamerProcessor(namerBox_.Text);
                foreach (string file in filesInPath)
                {
                    string processed = proc.RunProcessor(file);
                    namerPreviewBox_.AppendText(processed+"\t\t\t\tINPUT: "+file+"\r\n");
                }
            }
            catch (Exception e)
            {
                namerPreviewBox_.AppendText(e.Message);
            }
        }


    }

    private List<AssetDirectoryMonitorWidget> allMonitorWidgets_ = new List<AssetDirectoryMonitorWidget>();
    private Timer ticker_;

    public PipelineTool()
    {
        Text = "Pipeline Tool";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        

        //right column area (FIRST FOR DBG)
        var tabs = new SaneTabs(this, 15, 21);
        tabs.SaneCoords.SanePosition(15, 0);

        var dbgPage = tabs.NewPage("Info");
        var dbgBox = new SaneTextBox(dbgPage, 14, 20);
        dbgBox.ReadOnly = true;
        Dbg.UiBox = dbgBox;
        Dbg.Write("initializing");

        var helpPage = tabs.NewPage("Help");
        var help = new SaneTextBox(helpPage, 14, 20);
        help.Text = 
            @"

#####################################

About Naming Processors:
    In a nutshell:
        Input values:
            string path
            string file
        Output:
            string

### More information:
Think of these as shaders but for filenames and in C#.
You get two input variables: 'file' which contains the filename without extension, and 'path' which contains the full path to the file so you can check which folder they're on.
You have to return a string that will be used as the name your asset resource.

#####################################

";
        help.ReadOnly = true;
        help.WordWrap = true;



        //master controls
        MasterSwitch = new SaneToggleButton(this,3);
        MasterSwitch.Text = "Master Switch";
        
        var rebuildEnabled = new SaneButton(this, "Rebuild Enabled", 3);
        rebuildEnabled.SaneCoords.SanePosition(8, 0);
        rebuildEnabled.SaneClick += button =>
        {foreach (AssetDirectoryMonitorWidget widget in allMonitorWidgets_)
            widget.BuildAll();};
        
        var rebuildEverything = new SaneButton(this, "Rebuild Everything", 3);
        rebuildEverything.SaneCoords.SanePosition(11, 0);
        rebuildEverything.SaneClick += button =>
        {foreach (AssetDirectoryMonitorWidget widget in allMonitorWidgets_)
            widget.BuildAll(true);};



        //left column area

        int c = 2;
        new SaneLabel(this, "Source Paths").SaneCoords.SanePosition(1,c++);
        foreach (string s in PTHS_IDS)
        {
            var monitor = new AssetDirectoryMonitorWidget(this, s, tabs);
            monitor.SaneCoords.SanePosition(0, c++);
            allMonitorWidgets_.Add(monitor);
        }

        c++;
        new SaneLabel(this, "Binaries").SaneCoords.SanePosition(1,c++);
        foreach (string s in BINS_IDS)
            new FileBrowserWidget(this, s).SaneCoords.SanePosition(0, c++);

        c++;
        new SaneLabel(this, "Output Directories").SaneCoords.SanePosition(1,c++);
        foreach (string s in OUTS_IDS)
            new DirectoryWidget(this, s).SaneCoords.SanePosition(0, c++);
        
        SaneCoords SaneCoords = new SaneCoords(this);
        SaneCoords.SaneScale(30, 22);

        CenterToScreen();

        LoadGlobalData();

        FormClosing += (sender, args) => { 
            ticker_.Stop();
            SaveGlobalData();
            };

        // ticker
        ticker_ = new Timer();
        ticker_.Tick += (sender, args) => { GlobalTick?.Invoke(); };
        ticker_.Interval = 500;//todo 500
        ticker_.Start();

        GlobalTick += Dbg.Tick;
    }

    private static void LoadGlobalData()
    {
        try
        {
            Dbg.Write("loading saved configs");
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(SAVEF, FileMode.Open, FileAccess.Read, FileShare.Read);
            try{GlobalData = formatter.Deserialize(stream) as Dictionary<string, object>;}catch{}
            stream.Close();
            try{MasterSwitch.Toggled = (bool)GlobalData["MS"];}catch{}
            Dbg.Write("invoking load data event");
            GlobalLoad?.Invoke();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void SaveGlobalData()
    {
        Dbg.Write("preparing to save");
        GlobalSave?.Invoke();
        GlobalData["MS"] = MasterSwitch.Toggled;
        IFormatter formatter = new BinaryFormatter();
        Dbg.Write("opening stream");
        Stream stream = new FileStream(SAVEF, FileMode.Create, FileAccess.Write, FileShare.None);
        formatter.Serialize(stream, GlobalData);
        Dbg.Write("finished, closing stream...");
        stream.Close();
        Dbg.Write("stream closed");
    }
}
