using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

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

    public static Dictionary<string,object> GlobalData = new Dictionary<string, object>();
    public static Action GlobalSave;
    public static Action GlobalLoad;

    public class DirectoryWidget : SanePanel
    {
        //UI members
        private SaneLabel label_;
        private SaneButton button_;
        
        //data members
        private string id_;
        private string path_;
        public void SetPath(string path)
        {
            path_ = path;
            label_.Text = $"{id_}: {path_}";
        }

        public DirectoryWidget(Control parent, string id, int width = 14, int height = 1) : base(parent, width, height)
        {
            id_ = id;
            label_ = new SaneLabel(this, id);
            label_.SaneCoords.SaneScale(12, 1);
            button_ = new SaneButton(this, "Browse...", callBack:Browse);
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

    public class AssetDirectoryMonitorWidget
    {
        
    }

    public PipelineTool()
    {
        Text = "Pipeline Tool";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;

        LoadGlobalData();

        int c = 1;
        new SaneLabel(this, "Source Paths").SaneCoords.SanePosition(1,c++);
        foreach (string s in PTHS_IDS)
            new DirectoryWidget(this, s).SaneCoords.SanePosition(0, c++);

        c++;
        new SaneLabel(this, "Binaries").SaneCoords.SanePosition(1,c++);
        foreach (string s in BINS_IDS)
            new FileBrowserWidget(this, s).SaneCoords.SanePosition(0, c++);

        c++;
        new SaneLabel(this, "Output Directories").SaneCoords.SanePosition(1,c++);
        foreach (string s in OUTS_IDS)
            new DirectoryWidget(this, s).SaneCoords.SanePosition(0, c++);
        
        SaneCoords SaneCoords = new SaneCoords(this);
        SaneCoords.SaneScale(15, 21);

        CenterToScreen();

        GlobalLoad?.Invoke();

        FormClosing += (sender, args) => { 
            SaveGlobalData();
            };
    }

    private static void LoadGlobalData()
    {
        try
        {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(SAVEF, FileMode.Open, FileAccess.Read, FileShare.Read);
            try{GlobalData = formatter.Deserialize(stream) as Dictionary<string, object>;}catch{}
            stream.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void SaveGlobalData()
    {
        Debug.WriteLine("preparing to save");
        GlobalSave?.Invoke();
        IFormatter formatter = new BinaryFormatter();
        Debug.WriteLine("opening stream");
        Stream stream = new FileStream(SAVEF, FileMode.Create, FileAccess.Write, FileShare.None);
        formatter.Serialize(stream, GlobalData);
        Debug.WriteLine("finished, closing stream...");
        stream.Close();
        Debug.WriteLine("stream closed");
    }
}
