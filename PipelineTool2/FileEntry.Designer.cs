namespace PipelineTool2
{
   partial class FileEntry
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
         this.import_type_selector = new System.Windows.Forms.ComboBox();
         this.build_button = new System.Windows.Forms.Button();
         this.file_checkbox_label = new System.Windows.Forms.CheckBox();
         this.SuspendLayout();
         // 
         // import_type_selector
         // 
         this.import_type_selector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
         this.import_type_selector.FormattingEnabled = true;
         this.import_type_selector.Items.AddRange(new object[] {
            "Copy"});
         this.import_type_selector.Location = new System.Drawing.Point(272, 0);
         this.import_type_selector.Name = "import_type_selector";
         this.import_type_selector.Size = new System.Drawing.Size(79, 21);
         this.import_type_selector.TabIndex = 7;
         // 
         // build_button
         // 
         this.build_button.Location = new System.Drawing.Point(351, -1);
         this.build_button.Margin = new System.Windows.Forms.Padding(0);
         this.build_button.Name = "build_button";
         this.build_button.Size = new System.Drawing.Size(50, 23);
         this.build_button.TabIndex = 2;
         this.build_button.Text = "Build";
         this.build_button.UseVisualStyleBackColor = true;
         // 
         // file_checkbox_label
         // 
         this.file_checkbox_label.Location = new System.Drawing.Point(0, 2);
         this.file_checkbox_label.Name = "file_checkbox_label";
         this.file_checkbox_label.Size = new System.Drawing.Size(351, 19);
         this.file_checkbox_label.TabIndex = 1;
         this.file_checkbox_label.Text = "Some File Name -> Imported File Imported File Imported File";
         // 
         // FileEntry
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.Controls.Add(this.build_button);
         this.Controls.Add(this.import_type_selector);
         this.Controls.Add(this.file_checkbox_label);
         this.Name = "FileEntry";
         this.Size = new System.Drawing.Size(400, 21);
         this.ResumeLayout(false);

      }

      #endregion
      private System.Windows.Forms.ComboBox import_type_selector;
      private System.Windows.Forms.Button build_button;
      private System.Windows.Forms.CheckBox file_checkbox_label;
   }
}
