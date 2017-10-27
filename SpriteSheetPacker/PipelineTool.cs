using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
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


public partial class PipelineTool
{
    public abstract class Importer
    {
        protected readonly string[] extensions;
        protected readonly string exportExtension;

        //TODO this is mostly stateless, staticize most stuff

        protected bool ImportsExtension(string path)
        {
            if (extensions.Length == 0)
                return true;
            string extension = Path.GetExtension(path).Replace(".","");
            if (extension != null && extensions.Contains(extension.ToLower()))
                return true;
            return false;
        }

        protected bool ExportsExtension(string path)
        {
            string extension = Path.GetExtension(path).Replace(".","");
            if (extension == exportExtension)
                return true;
            return false;
        }
    
        protected Importer(string[] extensions, string exportExtension)
        {
            this.extensions = extensions;
            this.exportExtension = exportExtension;
        }

        public Dictionary<string, string> GetCorrespondenceList(string inputPath, string outPath, Func<string,string> namer)
        {
            var retDict = new Dictionary<string, string>();
            var allSourceFiles = Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in allSourceFiles)
            {
                if (ImportsExtension(file))
                {
                    var finalFileName = namer(file);
                    var finalFile = Path.Combine(outPath, finalFileName);

                    if (exportExtension != null)
                        finalFile += "."+exportExtension;
                    else
                        finalFile += Path.GetExtension(file);

                    retDict[file] = finalFile;
                }
            }
            return retDict;
        }

        public List<string> GetAllFilesInDest(string outPath)
        {
            //we only care about files we can import
            List<string> retList = new List<string>();
            var all = Directory.GetFiles(outPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in all)
            {
                if(ExportsExtension(file))
                    retList.Add(file);
            }
            return retList;
        }

        public List<string> GetFilesInDestWithNoCorrespondent(Dictionary<string, string> correspondences, List<string> allFilesInDest)
        {
            List<string> retList = new List<string>();

            List<string> finalDestFiles = correspondences.Values.ToList();
            foreach (string destFile in allFilesInDest) //for all files in destination folder
            {
                if (!finalDestFiles.Contains(destFile)) //if it's not in the list of files we want in dest
                {
                    //then we add to list (typically it should be removed)
                    retList.Add(destFile);
                }
            }
            return retList;
        }

        public virtual void Import(string inputPath, string[] changedFilesFull, string outputBins, string outputCode, Func<string, string> namer)
        {
            Dictionary<string, string> correspondenceList = GetCorrespondenceList(inputPath, outputBins, namer);
            List<string> allFilesInDest = GetAllFilesInDest(outputBins);

            Dbg.Write($"importing files from {inputPath} into {outputBins}, generating code into {outputCode}");

            List<string> orphans = new List<string>();
            foreach (string file in GetFilesInDestWithNoCorrespondent(correspondenceList, allFilesInDest))
            {
                Dbg.Write("found orphan "+file);
                orphans.Add(file);
            }
        
            Dictionary<string, string> correspondents = new Dictionary<string, string>();
            foreach (string changedFile in changedFilesFull)
            {
                if (correspondenceList.TryGetValue(changedFile, out string finalFilePath))
                {
                    Dbg.Write("found corresponding pair "+changedFile+"  -  "+finalFilePath);
                    correspondents[changedFile] = finalFilePath;
                }
                else
                {
                    Dbg.Write("FOUND MODIFIED FILE WITH NO CORRESPONDENT!!! "+changedFile);
                }
            }

            Dbg.Write("running high-level importing");
            ImportHighLevel(correspondents, orphans);
        }

        protected virtual void ImportHighLevel(Dictionary<string, string> correspondents, List<string> orphans)
        {}

        protected static void DeleteAllFiles(List<string> files)
        {
            foreach (string file in files)
            {
                Dbg.Write("DELETING ORPHAN: "+file);
                File.Delete(file);
            }
        }
    }

    public class FileCopierImporter : Importer
    {
        public FileCopierImporter(string[] extensions, string exportExtension) : base(extensions, exportExtension)
        {}
    }

    public class ShaderImporter : Importer
    {
        public ShaderImporter(string[] extensions, string exportExtension) : base(extensions, exportExtension)
        {}

        protected override void ImportHighLevel(Dictionary<string, string> correspondents, List<string> orphans)
        {
            foreach (KeyValuePair<string, string> correspondent in correspondents)
            {
                Dbg.Write("COMPILING SHADER: "+correspondent.Key+" -> "+correspondent.Value);
                try
                {
                    Process process = new Process();
                    process.StartInfo.FileName = DirectoryWidget.GetPathOf(DXCPL);
                    process.StartInfo.Arguments = $"/T fx_2_0 5 \"{correspondent.Key}\" /Fo \"{correspondent.Value}\"";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    Dbg.Write(process.StandardOutput.ReadLine()+process.StandardError.ReadLine());
                }
                catch (Exception e)
                {
                    Dbg.Write("ERROR COMPILING SHADERS!: "+e.Message);
                }
            }
            DeleteAllFiles(orphans);
        }
    }
}


[DesignerCategory("")] // we don't want useless tools
public partial class PipelineTool : Form
{
    //text IDS
    public const string ATLAS = "Atlas";
    public const string SOUND = "Sound";
    public const string MUSIC = "Music";
    public const string FONTS = "Fonts";
    public const string SHADR = "Shaders";
    public const string MESHS = "Meshes";
    public const string OTHER = "Other"; //just copy
    //extensions
    public static Dictionary<string,Importer> IMPORTERS = new Dictionary<string,Importer>
    {
        {ATLAS,new FileCopierImporter(new []{"png",}, "png")},
        {SOUND,new FileCopierImporter(new []{"wav",}, "wav")},
        {MUSIC,new FileCopierImporter(new []{"ogg",}, "ogg")},
        {FONTS,new FileCopierImporter(new []{"xnb",}, "xnb")},//TODO
        {SHADR,new ShaderImporter(new []{"hlsl",}, "fxb")},
        {MESHS,new FileCopierImporter(new []{"",},"")},
        {OTHER,new FileCopierImporter(new string[]{},null)},//empty array = catch all
    };

    public const string DXCPL = "DX Compiler";
    public const string XNBCP = "XNB Compiler";
    public const string FFMPG = "FFMpeg";
    public const string BLEND = "Blender";
    public static string[] BINS_IDS = {DXCPL,XNBCP,FFMPG,BLEND,};

    public const string OUTBN = "OutputBinaries";
    public const string OUTCD = "OutputCode";

    //misc
    public const string SAVEF = "ToolConf";

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new PipelineTool());
    }

    public static string Sanitize(string text)
    {
        const string regexPattern = @"[^a-zA-Z0-9]"; //TODO cache
        return Regex.Replace(text, regexPattern, "_");
    }

    //global ui stuff
    public static SaneToggleButton MasterSwitch;//sucks
    public static DirectoryWidget OutBinsDirWid;//sucks
    public static DirectoryWidget OutCodeDirWid;//sucks

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
        public void SetPath(string path) //TODO property
        {
            path_ = path;
            label_.Text = $"{id_}: {path_}";
            PathChanged?.Invoke();
        }
        public string GetPath()
        {
            return path_;
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

            //hacky solution SUCKS
            allDirs[id] = this;
        }
        //we go hacky a liddle bit :trollface:
        static Dictionary<string, DirectoryWidget> allDirs = new Dictionary<string, DirectoryWidget>();
        public static string GetPathOf(string id){return allDirs[id].path_;}


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
                "namespace DYNAMN { public class DYNAMC { public string DYNAMF(string path, string file, string folder) { "+code+"; } } } ";
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
            return 
                Sanitize(
                methodInfo_.Invoke(instance_, new object[]
                {
                    Path.GetDirectoryName(input),
                    Path.GetFileNameWithoutExtension(input),
                    new DirectoryInfo(Path.GetDirectoryName(input)).Name,
                }) as string);
        }
    }

    public class AssetDirectoryMonitorWidget : DirectoryWidget
    {
        //ui
        private SaneToggleButton switch_;
        private SaneTextBox namerBox_;
        private SaneTextBox namerPreviewBox_;

        private FileChangesBuffer monitorBuffer_;
        private Importer importer_;
        private int ticks_;

        public AssetDirectoryMonitorWidget(Control parent, string id, SaneTabs namerTabs, int width = 14, int height = 1) : base(parent, id, width, height)
        {
            label_.SaneCoords.SanePosition(2, 0);
            label_.SaneCoords.SaneScale(10, 1);

            switch_ = new SaneToggleButton(this);
            switch_.SaneClick += b => { StateChanged(); };

            importer_ = IMPORTERS[id];

            //shader editor
            var page = namerTabs.NewPage(id);
            new SaneLabel(page, id+" Naming Processor");
            var minihelp = new SaneLabel(page, "input vars: file, path, folder", 8);
            minihelp.SaneCoords.SanePosition(6, 0);
            minihelp.TextAlign = ContentAlignment.BottomRight;
            minihelp.Font = new Font(FontFamily.GenericMonospace, 8);
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
            var monitorResults = GetMonitorResults();
            if (monitorResults.Length > 0)
                BuildInternal(monitorResults);
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
            if (filesToBuild.Length == 0)
            {
                Dbg.Write("No files to build for "+id_);
                return;
            }
            
            Dbg.Write("building "+id_+" from "+path_);

            NamerProcessor proc = new NamerProcessor(namerBox_.Text);
            importer_.Import(path_, filesToBuild, OutBinsDirWid.GetPath(), OutCodeDirWid.GetPath(), proc.RunProcessor);
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
            string folder
        Output:
            string

### More information:
Think of these as shaders but for filenames and in C#.
You get these input variables:
    'file' which contains the filename without extension
    'path' which contains the full path to the file
    'folder' which is the folder where the file is (last in path)
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
        foreach (string s in IMPORTERS.Keys)
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
//        foreach (string s in OUTS_IDS)
//        new DirectoryWidget(this, s).SaneCoords.SanePosition(0, c++);
        OutBinsDirWid = new DirectoryWidget(this, OUTBN);
        OutBinsDirWid.SaneCoords.SanePosition(0, c++);
        OutCodeDirWid = new DirectoryWidget(this, OUTCD);
        OutCodeDirWid.SaneCoords.SanePosition(0, c++);
        
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
        ticker_.Interval = 500;//todo 1000
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
        catch (Exception e) {}
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
