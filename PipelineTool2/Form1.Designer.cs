namespace PipelineTool2
{
   partial class Form1
   {
      /// <summary>
      /// Required designer variable.
      /// </summary>
      private System.ComponentModel.IContainer components = null;

      /// <summary>
      /// Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose(bool disposing)
      {
         if (disposing && (components != null))
         {
            components.Dispose();
         }
         base.Dispose(disposing);
      }

      #region Windows Form Designer generated code

      /// <summary>
      /// Required method for Designer support - do not modify
      /// the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent()
      {
         System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
         this.button1 = new System.Windows.Forms.Button();
         this.auto_build_checkbox = new System.Windows.Forms.CheckBox();
         this.button5 = new System.Windows.Forms.Button();
         this.label1 = new System.Windows.Forms.Label();
         this.tabPage6 = new System.Windows.Forms.TabPage();
         this.folderpicker_output = new PipelineTool2.FolderPicker();
         this.folderpicker_input = new PipelineTool2.FolderPicker();
         this.testbox_help = new System.Windows.Forms.RichTextBox();
         this.tabPage4 = new System.Windows.Forms.TabPage();
         this.rtfbox_output = new System.Windows.Forms.RichTextBox();
         this.tabPage3 = new System.Windows.Forms.TabPage();
         this.label3 = new System.Windows.Forms.Label();
         this.tabControl1 = new System.Windows.Forms.TabControl();
         this.templ_namer_page = new System.Windows.Forms.TabPage();
         this.label2 = new System.Windows.Forms.Label();
         this.button3 = new System.Windows.Forms.Button();
         this.textBox1 = new System.Windows.Forms.TextBox();
         this.panel7 = new System.Windows.Forms.Panel();
         this.flowLayoutPanel3 = new System.Windows.Forms.FlowLayoutPanel();
         this.templ_namer_preview_entry = new System.Windows.Forms.Panel();
         this.label6 = new System.Windows.Forms.Label();
         this.label5 = new System.Windows.Forms.Label();
         this.label4 = new System.Windows.Forms.Label();
         this.tabPage1 = new System.Windows.Forms.TabPage();
         this.layout_paths = new System.Windows.Forms.FlowLayoutPanel();
         this.Assets = new System.Windows.Forms.TabControl();
         this.folderpicker_fxc = new PipelineTool2.FolderPicker();
         this.tabPage6.SuspendLayout();
         this.tabPage4.SuspendLayout();
         this.tabPage3.SuspendLayout();
         this.tabControl1.SuspendLayout();
         this.templ_namer_page.SuspendLayout();
         this.panel7.SuspendLayout();
         this.flowLayoutPanel3.SuspendLayout();
         this.templ_namer_preview_entry.SuspendLayout();
         this.tabPage1.SuspendLayout();
         this.Assets.SuspendLayout();
         this.SuspendLayout();
         // 
         // button1
         // 
         this.button1.Location = new System.Drawing.Point(316, 12);
         this.button1.Name = "button1";
         this.button1.Size = new System.Drawing.Size(75, 23);
         this.button1.TabIndex = 1;
         this.button1.Text = "Build New";
         this.button1.UseVisualStyleBackColor = true;
         // 
         // auto_build_checkbox
         // 
         this.auto_build_checkbox.AutoSize = true;
         this.auto_build_checkbox.Location = new System.Drawing.Point(12, 12);
         this.auto_build_checkbox.Name = "auto_build_checkbox";
         this.auto_build_checkbox.Size = new System.Drawing.Size(74, 17);
         this.auto_build_checkbox.TabIndex = 2;
         this.auto_build_checkbox.Text = "Auto Build";
         this.auto_build_checkbox.UseVisualStyleBackColor = true;
         // 
         // button5
         // 
         this.button5.Location = new System.Drawing.Point(397, 12);
         this.button5.Name = "button5";
         this.button5.Size = new System.Drawing.Size(75, 23);
         this.button5.TabIndex = 5;
         this.button5.Text = "Build All";
         this.button5.UseVisualStyleBackColor = true;
         // 
         // label1
         // 
         this.label1.Location = new System.Drawing.Point(84, 12);
         this.label1.Name = "label1";
         this.label1.Size = new System.Drawing.Size(232, 23);
         this.label1.TabIndex = 6;
         this.label1.Text = "# files | # assets | # new";
         this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
         // 
         // tabPage6
         // 
         this.tabPage6.Controls.Add(this.folderpicker_fxc);
         this.tabPage6.Controls.Add(this.folderpicker_output);
         this.tabPage6.Controls.Add(this.folderpicker_input);
         this.tabPage6.Controls.Add(this.testbox_help);
         this.tabPage6.Location = new System.Drawing.Point(4, 22);
         this.tabPage6.Name = "tabPage6";
         this.tabPage6.Padding = new System.Windows.Forms.Padding(3);
         this.tabPage6.Size = new System.Drawing.Size(452, 487);
         this.tabPage6.TabIndex = 4;
         this.tabPage6.Text = "Paths / Help";
         this.tabPage6.UseVisualStyleBackColor = true;
         // 
         // folderpicker_output
         // 
         this.folderpicker_output.Location = new System.Drawing.Point(22, 31);
         this.folderpicker_output.Name = "folderpicker_output";
         this.folderpicker_output.Purpose = "Output";
         this.folderpicker_output.Size = new System.Drawing.Size(410, 21);
         this.folderpicker_output.TabIndex = 3;
         // 
         // folderpicker_input
         // 
         this.folderpicker_input.Location = new System.Drawing.Point(22, 7);
         this.folderpicker_input.Name = "folderpicker_input";
         this.folderpicker_input.Purpose = "Sources";
         this.folderpicker_input.Size = new System.Drawing.Size(410, 21);
         this.folderpicker_input.TabIndex = 2;
         // 
         // testbox_help
         // 
         this.testbox_help.BackColor = System.Drawing.SystemColors.HighlightText;
         this.testbox_help.BorderStyle = System.Windows.Forms.BorderStyle.None;
         this.testbox_help.Location = new System.Drawing.Point(7, 98);
         this.testbox_help.Name = "testbox_help";
         this.testbox_help.ReadOnly = true;
         this.testbox_help.Size = new System.Drawing.Size(445, 383);
         this.testbox_help.TabIndex = 1;
         this.testbox_help.Text = "help rich text goes here";
         // 
         // tabPage4
         // 
         this.tabPage4.Controls.Add(this.rtfbox_output);
         this.tabPage4.Location = new System.Drawing.Point(4, 22);
         this.tabPage4.Name = "tabPage4";
         this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
         this.tabPage4.Size = new System.Drawing.Size(452, 487);
         this.tabPage4.TabIndex = 3;
         this.tabPage4.Text = "Output";
         this.tabPage4.UseVisualStyleBackColor = true;
         // 
         // rtfbox_output
         // 
         this.rtfbox_output.BackColor = System.Drawing.SystemColors.HighlightText;
         this.rtfbox_output.BorderStyle = System.Windows.Forms.BorderStyle.None;
         this.rtfbox_output.Location = new System.Drawing.Point(0, 0);
         this.rtfbox_output.Name = "rtfbox_output";
         this.rtfbox_output.ReadOnly = true;
         this.rtfbox_output.Size = new System.Drawing.Size(452, 487);
         this.rtfbox_output.TabIndex = 0;
         this.rtfbox_output.Text = "OUTPUT\n";
         // 
         // tabPage3
         // 
         this.tabPage3.Controls.Add(this.label3);
         this.tabPage3.Controls.Add(this.tabControl1);
         this.tabPage3.Location = new System.Drawing.Point(4, 22);
         this.tabPage3.Name = "tabPage3";
         this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
         this.tabPage3.Size = new System.Drawing.Size(452, 487);
         this.tabPage3.TabIndex = 2;
         this.tabPage3.Text = "Namers";
         this.tabPage3.UseVisualStyleBackColor = true;
         // 
         // label3
         // 
         this.label3.Location = new System.Drawing.Point(293, 1);
         this.label3.Name = "label3";
         this.label3.Size = new System.Drawing.Size(154, 23);
         this.label3.TabIndex = 8;
         this.label3.Text = "Input vars: file, path, folder";
         this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
         // 
         // tabControl1
         // 
         this.tabControl1.Controls.Add(this.templ_namer_page);
         this.tabControl1.Location = new System.Drawing.Point(7, 7);
         this.tabControl1.Name = "tabControl1";
         this.tabControl1.SelectedIndex = 0;
         this.tabControl1.Size = new System.Drawing.Size(439, 474);
         this.tabControl1.TabIndex = 0;
         // 
         // templ_namer_page
         // 
         this.templ_namer_page.Controls.Add(this.label2);
         this.templ_namer_page.Controls.Add(this.button3);
         this.templ_namer_page.Controls.Add(this.textBox1);
         this.templ_namer_page.Controls.Add(this.panel7);
         this.templ_namer_page.Location = new System.Drawing.Point(4, 22);
         this.templ_namer_page.Name = "templ_namer_page";
         this.templ_namer_page.Padding = new System.Windows.Forms.Padding(3);
         this.templ_namer_page.Size = new System.Drawing.Size(431, 448);
         this.templ_namer_page.TabIndex = 0;
         this.templ_namer_page.Text = "tabPage5";
         this.templ_namer_page.UseVisualStyleBackColor = true;
         // 
         // label2
         // 
         this.label2.Location = new System.Drawing.Point(7, 318);
         this.label2.Name = "label2";
         this.label2.Size = new System.Drawing.Size(337, 23);
         this.label2.TabIndex = 7;
         this.label2.Text = "Edit namer code above and click \"Preview\" to display results below";
         this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
         // 
         // button3
         // 
         this.button3.Location = new System.Drawing.Point(350, 318);
         this.button3.Name = "button3";
         this.button3.Size = new System.Drawing.Size(75, 23);
         this.button3.TabIndex = 2;
         this.button3.Text = "Preview";
         this.button3.UseVisualStyleBackColor = true;
         // 
         // textBox1
         // 
         this.textBox1.Location = new System.Drawing.Point(7, 7);
         this.textBox1.Multiline = true;
         this.textBox1.Name = "textBox1";
         this.textBox1.Size = new System.Drawing.Size(418, 305);
         this.textBox1.TabIndex = 0;
         // 
         // panel7
         // 
         this.panel7.AutoScroll = true;
         this.panel7.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
         this.panel7.Controls.Add(this.flowLayoutPanel3);
         this.panel7.Location = new System.Drawing.Point(7, 347);
         this.panel7.Name = "panel7";
         this.panel7.Size = new System.Drawing.Size(418, 95);
         this.panel7.TabIndex = 9;
         // 
         // flowLayoutPanel3
         // 
         this.flowLayoutPanel3.AutoSize = true;
         this.flowLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
         this.flowLayoutPanel3.Controls.Add(this.templ_namer_preview_entry);
         this.flowLayoutPanel3.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
         this.flowLayoutPanel3.Location = new System.Drawing.Point(0, 0);
         this.flowLayoutPanel3.Name = "flowLayoutPanel3";
         this.flowLayoutPanel3.Size = new System.Drawing.Size(398, 24);
         this.flowLayoutPanel3.TabIndex = 8;
         // 
         // templ_namer_preview_entry
         // 
         this.templ_namer_preview_entry.Controls.Add(this.label6);
         this.templ_namer_preview_entry.Controls.Add(this.label5);
         this.templ_namer_preview_entry.Controls.Add(this.label4);
         this.templ_namer_preview_entry.Location = new System.Drawing.Point(3, 3);
         this.templ_namer_preview_entry.Name = "templ_namer_preview_entry";
         this.templ_namer_preview_entry.Size = new System.Drawing.Size(392, 18);
         this.templ_namer_preview_entry.TabIndex = 0;
         // 
         // label6
         // 
         this.label6.Location = new System.Drawing.Point(213, 0);
         this.label6.Name = "label6";
         this.label6.Size = new System.Drawing.Size(183, 18);
         this.label6.TabIndex = 11;
         this.label6.Text = "output";
         this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
         // 
         // label5
         // 
         this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
         this.label5.Location = new System.Drawing.Point(186, -1);
         this.label5.Name = "label5";
         this.label5.Size = new System.Drawing.Size(21, 18);
         this.label5.TabIndex = 10;
         this.label5.Text = "→";
         this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
         // 
         // label4
         // 
         this.label4.Location = new System.Drawing.Point(0, 0);
         this.label4.Name = "label4";
         this.label4.Size = new System.Drawing.Size(193, 18);
         this.label4.TabIndex = 9;
         this.label4.Text = "input file";
         this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
         // 
         // tabPage1
         // 
         this.tabPage1.Controls.Add(this.layout_paths);
         this.tabPage1.Location = new System.Drawing.Point(4, 22);
         this.tabPage1.Name = "tabPage1";
         this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
         this.tabPage1.Size = new System.Drawing.Size(452, 487);
         this.tabPage1.TabIndex = 0;
         this.tabPage1.Text = "Assets";
         this.tabPage1.UseVisualStyleBackColor = true;
         // 
         // layout_paths
         // 
         this.layout_paths.AutoScroll = true;
         this.layout_paths.Location = new System.Drawing.Point(0, 0);
         this.layout_paths.Name = "layout_paths";
         this.layout_paths.Size = new System.Drawing.Size(452, 487);
         this.layout_paths.TabIndex = 8;
         // 
         // Assets
         // 
         this.Assets.Controls.Add(this.tabPage1);
         this.Assets.Controls.Add(this.tabPage3);
         this.Assets.Controls.Add(this.tabPage4);
         this.Assets.Controls.Add(this.tabPage6);
         this.Assets.Location = new System.Drawing.Point(12, 41);
         this.Assets.Name = "Assets";
         this.Assets.SelectedIndex = 0;
         this.Assets.Size = new System.Drawing.Size(460, 513);
         this.Assets.TabIndex = 0;
         // 
         // folderpicker_fxc
         // 
         this.folderpicker_fxc.Location = new System.Drawing.Point(22, 55);
         this.folderpicker_fxc.Name = "folderpicker_fxc";
         this.folderpicker_fxc.Purpose = "Path to FXC";
         this.folderpicker_fxc.Size = new System.Drawing.Size(410, 21);
         this.folderpicker_fxc.TabIndex = 4;
         // 
         // Form1
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.ClientSize = new System.Drawing.Size(484, 566);
         this.Controls.Add(this.auto_build_checkbox);
         this.Controls.Add(this.label1);
         this.Controls.Add(this.button5);
         this.Controls.Add(this.button1);
         this.Controls.Add(this.Assets);
         this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
         this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
         this.Name = "Form1";
         this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
         this.Text = "ælum Pipeline Tool";
         this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
         this.tabPage6.ResumeLayout(false);
         this.tabPage4.ResumeLayout(false);
         this.tabPage3.ResumeLayout(false);
         this.tabControl1.ResumeLayout(false);
         this.templ_namer_page.ResumeLayout(false);
         this.templ_namer_page.PerformLayout();
         this.panel7.ResumeLayout(false);
         this.panel7.PerformLayout();
         this.flowLayoutPanel3.ResumeLayout(false);
         this.templ_namer_preview_entry.ResumeLayout(false);
         this.tabPage1.ResumeLayout(false);
         this.Assets.ResumeLayout(false);
         this.ResumeLayout(false);
         this.PerformLayout();

      }

      #endregion
      private System.Windows.Forms.Button button1;
      private System.Windows.Forms.CheckBox auto_build_checkbox;
      private System.Windows.Forms.Button button5;
      private System.Windows.Forms.Label label1;
      private System.Windows.Forms.TabPage tabPage6;
      private System.Windows.Forms.RichTextBox testbox_help;
      private System.Windows.Forms.TabPage tabPage4;
      private System.Windows.Forms.RichTextBox rtfbox_output;
      private System.Windows.Forms.TabPage tabPage3;
      private System.Windows.Forms.Label label3;
      private System.Windows.Forms.TabControl tabControl1;
      private System.Windows.Forms.TabPage templ_namer_page;
      private System.Windows.Forms.Label label2;
      private System.Windows.Forms.Button button3;
      private System.Windows.Forms.TextBox textBox1;
      private System.Windows.Forms.Panel panel7;
      private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel3;
      private System.Windows.Forms.Panel templ_namer_preview_entry;
      private System.Windows.Forms.Label label6;
      private System.Windows.Forms.Label label5;
      private System.Windows.Forms.Label label4;
      private System.Windows.Forms.TabPage tabPage1;
      private System.Windows.Forms.FlowLayoutPanel layout_paths;
      private System.Windows.Forms.TabControl Assets;
      private FolderPicker folderpicker_input;
      private FolderPicker folderpicker_output;
      private FolderPicker folderpicker_fxc;
   }
}

