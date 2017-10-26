using System;
using System.Windows.Forms;
using System.Drawing;

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

public sealed class SaneLabel : Label, ISaneCoords
{
    public SaneLabel(Control parent, string text, int width = 5)
    {
        SaneCoords = new SaneCoords(this);
        Text = text;
        Parent = parent;
        SaneCoords.SaneScale(width, 1);
        TextAlign = ContentAlignment.MiddleLeft;
        BorderStyle = BorderStyle.Fixed3D;
    }
        
    public SaneCoords SaneCoords { get; }
}

public sealed class SaneButton : Button, ISaneCoords
{
    public readonly object userData;
    public SaneButton(Control parent, string text, int width = 2, object userData = null, Action<SaneButton> callBack = null)
    {
        SaneCoords = new SaneCoords(this);
        Text = text;
        Parent = parent;
        SaneCoords.SaneScale(width, 1);

        this.userData = userData;
        if (callBack != null)
            SetCallback(callBack);
    }

    public void SetCallback(Action<SaneButton> callBack)
    {
        Click += (s, e) =>
        {
            callBack(this);
        };
    }
        
    public SaneCoords SaneCoords { get; }
}

public class SanePanel : Panel, ISaneCoords
{
    public SanePanel(Control parent, int width = 7, int height = 1)
    {
        SaneCoords = new SaneCoords(this);
        Parent = parent;
        BorderStyle = BorderStyle.FixedSingle;
        SaneCoords.SaneScale(width, height);
    }

    public SaneCoords SaneCoords { get; }
}

