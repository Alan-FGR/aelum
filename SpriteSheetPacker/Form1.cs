using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CSharp;


namespace PipelineToolNS
{
    public partial class Form1 : Form
    {
//        [STAThread]
//        public static void Main()
//        {
//            try //TROLOLOLOL 
//            {
//                Application.EnableVisualStyles();
//                Application.Run(new Form1());
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e);
//            }
//        }
        
        public static TextBox debugBox;
        public static void AddDebugText(string text)
        {
            try
            {
                debugBox.AppendText(" \r\n"+text);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        

        public static ProgressBar MainProgressBar;

        class DynamicStringProcessor
        {
            private readonly TextBox previewBox_;

            public DynamicStringProcessor(string code, TextBox previewBox = null, string testPath = null)
            {
                return;
                previewBox_ = previewBox;
                string source = 
                    "namespace DYNAMN { public class DYNAMC { public string DYNAMF(string path, string file) { "+code+"; } } } ";
                Dictionary<string, string> providerOptions = new Dictionary<string, string>{{"CompilerVersion", "v3.5"}};
                CSharpCodeProvider provider = new CSharpCodeProvider(providerOptions);
                CompilerParameters compilerParams = new CompilerParameters {GenerateInMemory = true, GenerateExecutable = false};
                CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, source);
                if (results.Errors.Count > 0)
                {
                    if (previewBox != null) previewBox.Text = "ERROR";
                    return;
                }
                instance_ = results.CompiledAssembly.CreateInstance("DYNAMN.DYNAMC");
                methodInfo_ = instance_.GetType().GetMethod("DYNAMF");

                if (previewBox != null)
                {
                    foreach (string file in Directory.EnumerateFiles(testPath))
                    {
                        
                    }
                }

            }

            public void PrintPreview(string file)
            {
                previewBox_.AppendText(RunProcessor(file));
            }

            private MethodInfo methodInfo_;
            private object instance_;

            public string RunProcessor(string input)
            {
                return methodInfo_.Invoke(instance_, new object[]{Path.GetFullPath(input),Path.GetFileNameWithoutExtension(input)}) as string;
            }

        }

        [Serializable]
        public class PathConfigData
        {
            public string buttonTextId;
            public bool Ticked;
            public string path;

            public PathConfigData(string buttonTextId, bool ticked, string path)
            {
                this.buttonTextId = buttonTextId;
                Ticked = ticked;
                this.path = path;
            }
        }

        [Serializable]
        public class PipelineConfig
        {
            public bool AutoRefresh = false;
            public Dictionary<PathType, string> textShaders = new Dictionary<PathType, string>();
            public List<PathConfigData> configs = new List<PathConfigData>();
        }


        private static List<PathConfigData> savedConfigDatas = new List<PathConfigData>();
        
        public class LoggingFsw : FileSystemWatcher
        {
            static int watcherID;
            private int id;
            public LoggingFsw(string path) : base(path)
            {
                id = watcherID++;
                AddDebugText($"watcher {id} started monitoring path: {path}");
            }

            ~LoggingFsw()
            {
                AddDebugText($"watcher {id} GCd");
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                AddDebugText($"watcher {id} disposed");
            }
        }

        private static ConcurrentQueue<string> messagesFromOtherThreads = new ConcurrentQueue<string>();
        public static bool isProcessing = false;

        public class PipelineCommand
        {
            public string path;
            public PathType type;
        }
        private static ConcurrentQueue<PipelineCommand> commandBuffer = new ConcurrentQueue<PipelineCommand>();

        public class PathConfig
        {
            private string path_;

            private string originalText_;

            public readonly Button button;
            public readonly CheckBox tickBox;
            private readonly PathType type_;
            private readonly TextBox pathShader_;

            private FileSystemWatcher fsw_;
            
            public string Path
            {
                get => path_;
                set
                {
                    path_ = value;
                    UpdateButton();
                }
            }

            DateTime lastChange = DateTime.Now.AddDays(42);
            List<string> changedList_ = new List<string>();

            private void ProcessChange(object sender, FileSystemEventArgs e)
            {
                lastChange = DateTime.Now;
                changedList_.Add(e.FullPath);
                messagesFromOtherThreads.Enqueue("change detected "+e.FullPath);
            }

            void UpdateButton()
            {
                if (originalText_ == null)
                    originalText_ = button.Text;

                button.Text = originalText_+": "+path_;
                UpdateWatcher(tickBox);
            }

            public void UpdateWatcher(CheckBox tick)
            {
                if(tick != tickBox) return;
                fsw_?.Dispose();
                fsw_ = null;
                
                if(!MasterTickBox.Checked) return;
                if(tickBox == null) return;
                if(string.IsNullOrEmpty(path_)) return;
                if(!tickBox.Checked) return;

                fsw_ = new LoggingFsw(path_);
                fsw_.IncludeSubdirectories = true;
                fsw_.EnableRaisingEvents = true;

                fsw_.NotifyFilter = NotifyFilters.Attributes|NotifyFilters.CreationTime|
                    NotifyFilters.DirectoryName|NotifyFilters.FileName|NotifyFilters.LastAccess|
                    NotifyFilters.LastWrite|NotifyFilters.Security|NotifyFilters.Size;

                fsw_.Changed += ProcessChange;
                fsw_.Created += ProcessChange;
                fsw_.Deleted += ProcessChange;
            }

            public bool updtick;
            public void UpdateStatus()
            {
                if(tickBox == null) return;
                
                updtick = !updtick;
                if (fsw_ == null)
                {
                    tickBox.BackColor = Color.OrangeRed;
                }
                else
                {
                    tickBox.BackColor = updtick?Color.MediumSeaGreen:Color.DarkSeaGreen;
                }

            }

            public void TryProcessPath(bool force)
            {
                if(lastChange.AddSeconds(1) < DateTime.Now || force)
                    ProcessPath();
            }

            void ProcessPath()
            {
                if(string.IsNullOrEmpty(path_)) return;
                messagesFromOtherThreads.Enqueue($"queuing {type_.ToString()} path {path_}");
                lastChange = lastChange.AddDays(42);
                commandBuffer.Enqueue(new PipelineCommand(){path = path_+"", type = type_});
            }

            public PathConfig(Button button, CheckBox tickBox, PathType type = PathType.None, TextBox pathShader = null)
            {
                this.button = button;
                this.tickBox = tickBox;
                type_ = type;
                pathShader_ = pathShader;

                foreach (PathConfigData data in savedConfigDatas)
                {
                    if (data.buttonTextId == button.Text)
                    {
                        if (tickBox != null) this.tickBox.Checked = data.Ticked;
                        this.Path = data.path;
                    }
                }

            }

            public PathConfigData GetPrData()
            {
                return new PathConfigData(originalText_, tickBox?.Checked ?? false, path_);
            }

        }
        public static PathConfig[] pathConfigs;

        public enum PathType
        {
            None,
            Atlas
        }

        public void SetPathForButton(object s)
        {
            Button clickedButton = s as Button;
            PathConfig curpb = GetPathButton(clickedButton);

            //FBD totally sucks!!!! worst UX EVEEEEEEEEERRRRRRRRRRRRRRRR :(((((((((((
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Directory.GetCurrentDirectory();
            fbd.Description = "Sorry for this terrible UI. Blame Microsoft :P";

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                string path = fbd.SelectedPath;
                curpb.Path = path;
            }
        }

        public void SetBinPathForButton(object s)
        {
            Button clickedButton = s as Button;
            PathConfig curpb = GetPathButton(clickedButton);

            OpenFileDialog ofd = new OpenFileDialog();

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                string bin = ofd.FileName;
                curpb.Path = bin;
            }

        }

        public void UpdateWatchers(object tickbox, bool force = false)
        {
            foreach (PathConfig config in pathConfigs)
            {
                if (force)
                {
                    config.UpdateWatcher(config.tickBox);
                }
                config.UpdateWatcher(tickbox as CheckBox);
            }
        }

        public void SaveAllConfigs()
        {
            AddDebugText("collecting data to save");
            List<PathConfigData> datas = new List<PathConfigData>();
            foreach (PathConfig config in pathConfigs)
            {
                AddDebugText("collecting from "+config.button.Text);
                datas.Add(config.GetPrData());
            }

            var sdi = new Dictionary<PathType, string>();
            foreach (KeyValuePair<PathType, string> pair in shaders)
            {
                sdi.Add(pair.Key,pair.Value);
            }

            PipelineConfig confs = new PipelineConfig()
            {
                AutoRefresh = MasterTickBox.Checked,
                configs = datas,
                textShaders = sdi
            };

            AddDebugText("saving data");
            IFormatter formatter = new BinaryFormatter();  
            Stream stream = new FileStream("PipelineToolData", FileMode.Create, FileAccess.Write, FileShare.None);  
            formatter.Serialize(stream, datas);  
            stream.Close();  
            AddDebugText("data saved");
        }

        static ConcurrentDictionary<PathType, string> shaders = new ConcurrentDictionary<PathType, string>();

        public void LoadAllConfigs()
        {
            try
            {
                IFormatter formatter = new BinaryFormatter();  
                Stream stream = new FileStream("PipelineToolData", FileMode.Open, FileAccess.Read, FileShare.Read);  
                var saveds = (PipelineConfig) formatter.Deserialize(stream);
                savedConfigDatas = saveds.configs;
                foreach (PathConfigData data in savedConfigDatas)
                {
                    AddDebugText("loading data: "+data.path);
                }

                foreach (KeyValuePair<PathType, string> pair in saveds.textShaders)
                {
                    shaders.TryAdd(pair.Key, pair.Value);
                }

                MasterTickBox.Checked = saveds.AutoRefresh;

                AddDebugText("loaded data from PipelineToolData file");
                stream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void TryProcessAll(bool force)
        {

            //update shaders
            shaders.Clear();
            shaders[PathType.Atlas] = textBox1.Text;


            foreach (PathConfig config in pathConfigs)
            {
                isProcessing = true;
                config.TryProcessPath(force);
            }
            
            ProcessCommands();
        }
        
        public void ProcessCommands()
        {
            while (!commandBuffer.IsEmpty)
                if(commandBuffer.TryDequeue(out PipelineCommand c))
                {

                    DynamicStringProcessor ds = null;

                    //compile shader if any
                    if (shaders.TryGetValue(c.type, out string shader))
                    {
                        ds = new DynamicStringProcessor(shader);
                    }

                    if (c.type == PathType.Atlas)
                    {
                        Packer.Pack(c.path, outBin.Path, outCode.Path);
                    }
                }
            isProcessing = false;
        }

        private static PathConfig GetPathButton(Button clickedButton)
        {
            PathConfig curpb = null;

            foreach (PathConfig pathButton in pathConfigs)
            {
                if (pathButton.button == clickedButton)
                {
                    curpb = pathButton;
                    break;
                }
            }

            if (curpb == null)
            {
                throw new Exception("button not registered in pathbuttons");
            }
            return curpb;
        }

        public static CheckBox MasterTickBox;
        public static PathConfig outBin;
        public static PathConfig outCode;
        public Form1()
        {
            InitializeComponent();

            MasterTickBox = checkBox1;
            MainProgressBar = progressBar1;

            debugBox = textBox4;

            LoadAllConfigs();

            outBin = new PathConfig(button6, null);//output bin
            outCode = new PathConfig(button14, null);//output code

            pathConfigs = new[] {
                new PathConfig(button1,checkBox2,PathType.Atlas,textBox1),//atlas
                new PathConfig(button3,checkBox3),//sounds
                new PathConfig(button4,checkBox4),//musics
                new PathConfig(button5,checkBox5),//fonts
                new PathConfig(button7,checkBox6),//shaders
                new PathConfig(button13,checkBox8),//meshes
                new PathConfig(button8,checkBox7),//other

                new PathConfig(button9,null),//dxcompiler
                new PathConfig(button10,null),//xnacompiler
                new PathConfig(button11,null),//ffmpeg
                new PathConfig(button12,null),//blender

                outBin,outCode
            };

            backgroundWorker1.DoWork += backgroundWorker1_DoWork;
            backgroundWorker1.ProgressChanged += backgroundWorker1_ProgressChanged;

            backgroundWorker1.RunWorkerAsync();

        }

        #region cbs

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender, true);
        }

        private void button11_Click(object sender, EventArgs e)
        {
            SetBinPathForButton(sender);
        }

        private void button13_Click(object sender, EventArgs e)
        {
            var testPath = GetPathButton(button1); // button of the shader path
            DynamicStringProcessor dp = new DynamicStringProcessor(textBox1.Text, textBox2, testPath.Path);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            SetBinPathForButton(sender);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button13_Click_1(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            SetBinPathForButton(sender);
        }

        private void button12_Click(object sender, EventArgs e)
        {
            SetBinPathForButton(sender);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void button14_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWatchers(sender);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isquitting = true;
            SaveAllConfigs();
            Refresh();
            Thread.Sleep(1000);
        }

        #endregion

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            int c = 0;
            while (!isquitting)
            {
                Thread.Sleep(400);
                backgroundWorker1.ReportProgress(c++);
            }
        }

        public static bool isquitting = false;
        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            MainProgressBar.Value = (MainProgressBar.Value+20)%100;

            if (isProcessing)
            {
            }
            else
            {
                foreach (PathConfig config in pathConfigs)
                {
                    config.UpdateStatus();
                }
                while (!messagesFromOtherThreads.IsEmpty)
                    if(messagesFromOtherThreads.TryDequeue(out string s)){
                        AddDebugText(s);
                    }

                if(MasterTickBox.Checked)
                    TryProcessAll(false);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            TryProcessAll(true);
        }
    }
}
