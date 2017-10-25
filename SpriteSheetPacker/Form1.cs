using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CSharp;

namespace PipelineTool
{
    public partial class Form1 : Form
    {
        [STAThread]
        public static void Main(){Application.EnableVisualStyles();Application.Run(new Form1());}

        
        class DynamicStringProcessor
        {
            private readonly TextBox previewBox_;

            public DynamicStringProcessor(string code, TextBox previewBox)
            {
                previewBox_ = previewBox;
                string source = 
                    "namespace DYNAMN { public class DYNAMC { public string DYNAMF(string path, string file) { "+code+"; } } } ";
                Dictionary<string, string> providerOptions = new Dictionary<string, string>{{"CompilerVersion", "v3.5"}};
                CSharpCodeProvider provider = new CSharpCodeProvider(providerOptions);
                CompilerParameters compilerParams = new CompilerParameters {GenerateInMemory = true, GenerateExecutable = false};
                CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, source);
                if (results.Errors.Count > 0)
                {
                    previewBox.Text = "ERROR";
                    return;
                }
                instance_ = results.CompiledAssembly.CreateInstance("DYNAMN.DYNAMC");
                methodInfo_ = instance_.GetType().GetMethod("DYNAMF");
                PrintPreview();
            }

            public void PrintPreview()
            {
                previewBox_.Text = RunProcessor("C:\\PATH\\FILE.EXT");
            }

            private MethodInfo methodInfo_;
            private object instance_;

            public string RunProcessor(string input)
            {
                return methodInfo_.Invoke(instance_, new object[]{Path.GetFullPath(input),Path.GetFileNameWithoutExtension(input)}) as string;
            }

        }

        public static TextBox debugBox;
        public static void AddDebugText(string text)
        {
            debugBox.Text = string.Concat(debugBox.Text, " \r\n"+text);
        }

        public class PathButton
        {
            private string path_;
            public Button button;
            public CheckBox tickBox;
            public string descr { get; private set; }= null;

            private FileSystemWatcher fsw_;
            
            public string Path
            {
                get { return path_; }
                set
                {
                    path_ = value;
                    if(tickBox == null) return;
                    if(string.IsNullOrEmpty(value)) return;
                    fsw_ = new FileSystemWatcher(value);
                    fsw_.IncludeSubdirectories = true;
                    fsw_.EnableRaisingEvents = true;
                    fsw_.Changed += ProcessPath;
                }
            }

            private void ProcessPath(object sender, FileSystemEventArgs e)
            {
                
            }

            public void SetDescr(string d)
            {
                if (descr == null)
                    descr = d;
            }
            
            public PathButton(string path, Button button, CheckBox tickBox)
            {
                this.Path = path;
                this.button = button;
                this.tickBox = tickBox;
            }
        }
        public static PathButton[] pathButtons;

        public void SetPathForButton(object s)
        {
            Button clickedButton = s as Button;
            PathButton curpb = GetPathButton(clickedButton);

            //FBD totally sucks!!!! worst UX EVEEEEEEEEERRRRRRRRRRRRRRRR :(((((((((((
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Directory.GetCurrentDirectory();
            fbd.Description = "Sorry for this terrible UI. Blame Microsoft :P";

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                string path = fbd.SelectedPath;
                curpb.SetDescr(clickedButton.Text);
                curpb.Path = path;
                clickedButton.Text = curpb.descr+": "+curpb.Path;
                AddDebugText(clickedButton.Text);
            }
        }

        public void SetBinPathForButton(object s)
        {
            Button clickedButton = s as Button;
            PathButton curpb = GetPathButton(clickedButton);

            OpenFileDialog ofd = new OpenFileDialog();

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                string bin = ofd.FileName;
                curpb.SetDescr(clickedButton.Text);
                curpb.Path = bin;
                clickedButton.Text = curpb.descr+": "+curpb.Path;
                AddDebugText(clickedButton.Text);
            }

        }


        private static PathButton GetPathButton(Button clickedButton)
        {
            PathButton curpb = null;

            foreach (PathButton pathButton in pathButtons)
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


        public Form1()
        {
            InitializeComponent();
            debugBox = textBox4;
            pathButtons = new[] {
                new PathButton("",button1,checkBox2),//atlas
                new PathButton("",button3,checkBox3),//sounds
                new PathButton("",button4,checkBox4),//musics
                new PathButton("",button5,checkBox5),//fonts
                new PathButton("",button7,checkBox6),//shaders
                new PathButton("",button13,checkBox8),//meshes
                new PathButton("",button8,checkBox7),//other

                new PathButton("",button9,null),//dxcompiler
                new PathButton("",button10,null),//xnacompiler
                new PathButton("",button11,null),//ffmpeg
                new PathButton("",button12,null),//blender

                new PathButton("",button6,null),//output

            };
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void button11_Click(object sender, EventArgs e)
        {
            SetBinPathForButton(sender);
        }




        private void button13_Click(object sender, EventArgs e)
        {


            DynamicStringProcessor dp = new DynamicStringProcessor(textBox1.Text, textBox2);

//            textBox2.Text = textBox1.Text;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            SetPathForButton(sender);
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

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
    }
}
