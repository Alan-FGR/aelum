using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipelineTool2
{
   public partial class Form1 : Form
   {
      private NotifyIcon notifIcon = new NotifyIcon();

      public Form1()
      {
         InitializeComponent();
         notifIcon.Icon = Icon;
         notifIcon.Text = "ælum Pipeline Tool";
         notifIcon.ContextMenu = new ContextMenu(new []{new MenuItem("Quit", OnNotifQuitClick), });
         notifIcon.Visible = true;
         notifIcon.Click+=NotifIconOnClick;





         richTextBox2.Rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil\fcharset0 Calibri;}}
{\*\generator Msftedit 5.41.21.2510;}\viewkind4\uc1\pard\sa200\sl276\slmult1\lang22\b\f0\fs22 test\b0\par
test\par
}";



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

      private void checkBox1_CheckedChanged(object sender, EventArgs e)
      {

      }
   }
}
