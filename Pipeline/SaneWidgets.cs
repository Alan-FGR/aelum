using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

public struct SaneCoords
{
    public const int STD_SIZE = 30;
    private readonly Control control_;
        
    public SaneCoords(Control control)
    {
        control_ = control;
    }

    public Control SanePosition(int x, int y)
    {
        control_.Left = x * STD_SIZE;
        control_.Top = y * STD_SIZE;
        return control_;
    }

    public Control SaneScale(int w, int h)
    {
        control_.Width = STD_SIZE * w;
        control_.Height = STD_SIZE * h;
        return control_;
    }

    public int Width => control_.Width / STD_SIZE;
    public int Height => control_.Height / STD_SIZE;

    public int Left => control_.Left / STD_SIZE;
    public int Top => control_.Top / STD_SIZE;
        
    public Point Scale => new Point(Width,Height);
    public Point Position => new Point(Left,Top);
}


public interface ISaneCoords
{
    SaneCoords SaneCoords { get; }
}

[DesignerCategory("")] // we don't want useless tools
public sealed class SaneLabel : Label, ISaneCoords
{
    public SaneLabel(Control parent, string text, int width = 5)
    {
        SaneCoords = new SaneCoords(this);
        Text = text;
        Parent = parent;
        SaneCoords.SaneScale(width, 1);
        TextAlign = ContentAlignment.MiddleLeft;
        BackColor = Color.Transparent;
//        BorderStyle = BorderStyle.Fixed3D;
    }
        
    public SaneCoords SaneCoords { get; }
}

[DesignerCategory("")] // we don't want useless tools
public class SaneButton : Button, ISaneCoords
{
    public readonly object userData;
    public Action<SaneButton> SaneClick;
    public SaneButton(Control parent, string text, int width = 2, object userData = null)
    {
        SaneCoords = new SaneCoords(this);
        SetText(text);
        Parent = parent;
        SaneCoords.SaneScale(width, 1);
        
        this.userData = userData;
        Click += (s, e) => { Clicked(); };
    }

    public void SetText(string text) // Text property is virtual
    {
        Text = text;
    }

    public virtual void Clicked()
    {
        SaneClick?.Invoke(this);
    }
    
    public SaneCoords SaneCoords { get; }
}

[DesignerCategory("")] // we don't want useless tools
public class SaneToggleButton : SaneButton
{
    private bool state_;
    public bool Toggled
    {
        get => state_;
        set
        {
            state_ = value;
            BackColor = state_ ? Color.LightGreen : Color.LightGray;
        }
    }

    public SaneToggleButton(Control parent, int width = 2, object userData = null) : base(parent, "Toggle", width, userData)
    {
        Toggled = false;
    }

    public override void Clicked()
    {
        Toggled = !state_;
        base.Clicked();
    }
}

[DesignerCategory("")] // we don't want useless tools
public class SanePanel : Panel, ISaneCoords
{
    public SanePanel(Control parent, int width = 7, int height = 1)
    {
        SaneCoords = new SaneCoords(this);
        Parent = parent;
        SaneCoords.SaneScale(width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = ClientRectangle;
        ControlPaint.DrawBorder(e.Graphics, r,
            Color.Black, 1, ButtonBorderStyle.None,
            Color.Black, 1, ButtonBorderStyle.None,
            Color.Black, 1, ButtonBorderStyle.None,
            Color.Black, 1, ButtonBorderStyle.Inset);
    }

    public SaneCoords SaneCoords { get; }
}

[DesignerCategory("")] // we don't want useless tools
public class SaneTextBox : TextBox, ISaneCoords
{
    public SaneTextBox(Control parent, int width = 7, int height = 3)
    {
        SaneCoords = new SaneCoords(this);
        Parent = parent;
        ScrollBars = ScrollBars.Both;
        WordWrap = false;
        Multiline = true;
        SaneCoords.SaneScale(width, height);
    }

    public SaneCoords SaneCoords { get; }
}

[DesignerCategory("")] // we don't want useless tools
public class SaneTabs : TabControl, ISaneCoords
{
    public SaneTabs(Control parent, int width = 7, int height = 7)
    {
        SaneCoords = new SaneCoords(this);
        Parent = parent;
        SaneCoords.SaneScale(width, height);
    }

    public TabPage NewPage(string label)
    {
        TabPage newPage = new TabPage(label);
        Controls.Add(newPage);
        return newPage;
    }

    public SaneCoords SaneCoords { get; }
}

//[DesignerCategory("")] // we don't want useless tools
//[DesignerCategory("")] // we don't want useless tools
//[DesignerCategory("")] // we don't want useless tools
//[DesignerCategory("")] // we don't want useless tools
//[DesignerCategory("")] // we don't want useless tools
//[DesignerCategory("")] // we don't want useless tools
