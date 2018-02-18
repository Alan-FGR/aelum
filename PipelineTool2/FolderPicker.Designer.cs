namespace PipelineTool2
{
   partial class FolderPicker
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

      #region Component Designer generated code

      /// <summary> 
      /// Required method for Designer support - do not modify 
      /// the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent()
      {
         this.browse_button = new System.Windows.Forms.Button();
         this.path_label = new System.Windows.Forms.Label();
         this.SuspendLayout();
         // 
         // browse_button
         // 
         this.browse_button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
         this.browse_button.AutoSize = true;
         this.browse_button.Location = new System.Drawing.Point(338, -1);
         this.browse_button.Name = "browse_button";
         this.browse_button.Size = new System.Drawing.Size(73, 23);
         this.browse_button.TabIndex = 2;
         this.browse_button.Text = "Browse...";
         this.browse_button.UseVisualStyleBackColor = true;
         this.browse_button.Click += new System.EventHandler(this.browse_button_Click);
         // 
         // path_label
         // 
         this.path_label.AutoSize = true;
         this.path_label.Location = new System.Drawing.Point(4, 5);
         this.path_label.Name = "path_label";
         this.path_label.Size = new System.Drawing.Size(29, 13);
         this.path_label.TabIndex = 3;
         this.path_label.Text = "label";
         // 
         // FolderPicker
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.Controls.Add(this.browse_button);
         this.Controls.Add(this.path_label);
         this.Name = "FolderPicker";
         this.Size = new System.Drawing.Size(410, 21);
         this.ResumeLayout(false);
         this.PerformLayout();

      }

      #endregion
      private System.Windows.Forms.Button browse_button;
      private System.Windows.Forms.Label path_label;
   }
}
