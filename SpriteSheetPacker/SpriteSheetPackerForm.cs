#region MIT License

/*
 * Copyright (c) 2009 Nick Gravelyn (nick@gravelyn.com), Markus Ewald (cygon@nuclex.org)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a 
 * copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation 
 * the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the Software 
 * is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
 * 
 */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SpriteSheetPacker
{
	internal partial class SpriteSheetPackerForm : Form
	{
		private static readonly Stopwatch stopWatch = new Stopwatch();

		private sspack.IImageExporter currentImageExporter;
		private sspack.IMapExporter currentMapExporter;

		// a list of the files we'll build into our sprite sheet
		private readonly List<string> files = new List<string>();

		public SpriteSheetPackerForm()
		{
			InitializeComponent();

			// find all of the available exporters
			sspack.Exporters.Load();

			// generate the save dialogs based on available exporters
			GenerateImageSaveDialog();
			GenerateMapSaveDialog();

			// set our open file dialog filter based on the valid extensions
			imageOpenFileDialog.Filter = "Image Files|";
			foreach (var ext in sspack.MiscHelper.AllowedImageExtensions) 
				imageOpenFileDialog.Filter += string.Format("*.{0};", ext);

			// set the UI values to our saved settings
            maxWidthTxtBox.Text = SpriteSheetPacker.Settings.Default.MaxWidth.ToString();
            maxHeightTxtBox.Text = SpriteSheetPacker.Settings.Default.MaxHeight.ToString();
            paddingTxtBox.Text = SpriteSheetPacker.Settings.Default.Padding.ToString();
            powOf2CheckBox.Checked = SpriteSheetPacker.Settings.Default.PowOf2;
            squareCheckBox.Checked = SpriteSheetPacker.Settings.Default.Square;
			generateMapCheckBox.Checked = SpriteSheetPacker.Settings.Default.GenerateMap;
		}

		// configures our image save dialog to take into account all loaded image exporters
		public void GenerateImageSaveDialog()
		{
			string filter = "";
            int filterIndex = 0;
            int i = 0;

            foreach (var exporter in sspack.Exporters.ImageExporters)
            {
                i++;
                filter += string.Format("{0} Files|*.{1}|", exporter.ImageExtension.ToUpper(), exporter.ImageExtension);
                if (exporter.ImageExtension.ToLower() == SpriteSheetPacker.Settings.Default.ImageExt)
                {
                    filterIndex = i;
                }
            }
			filter = filter.Remove(filter.Length - 1);

			imageSaveFileDialog.Filter = filter;
            imageSaveFileDialog.FilterIndex = filterIndex;
		}

		// configures our map save dialog to take into account all loaded map exporters
		public void GenerateMapSaveDialog()
		{
			string filter = "";
            int filterIndex = 0;
            int i = 0;

            foreach (var exporter in sspack.Exporters.MapExporters)
            {
                i++;
                filter += string.Format("{0} Files|*.{1}|", exporter.MapExtension.ToUpper(), exporter.MapExtension);
                if (exporter.MapExtension.ToLower() == SpriteSheetPacker.Settings.Default.MapExt)
                {
                    filterIndex = i;
                }
            }
			filter = filter.Remove(filter.Length - 1);

			mapSaveFileDialog.Filter = filter;
            mapSaveFileDialog.FilterIndex = filterIndex;
		}

        private void AddFiles(IEnumerable<string> paths)
		{
			foreach (var path in paths)
			{
				// if the path is a directory, add all files inside the directory
				if (Directory.Exists(path))
				{
					AddFiles(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
					continue;
				}

				// make sure the file is an image
				if (!sspack.MiscHelper.IsImageFile(path))
					continue;

				// prevent duplicates
				if (files.Contains(path)) 
					continue;

				// add to both our internal list and the list box
				files.Add(path);
				listBox1.Items.Add(path);
			}
		}

		// determines if a directory contains an image we can accept
		public static bool DirectoryContainsImages(string directory)
		{
			foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
			{
				if (sspack.MiscHelper.IsImageFile(file))
				{
					return true;
				}
			}

			return false;
		}

		private void listBox1_DragEnter(object sender, DragEventArgs e)
		{
			// if this drag is not for a file drop, ignore it
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) 
				return;

			// figure out if any the files being dropped are images
			bool imageFound = false;
			foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
			{
				// if the path is a directory and it contains images...
				if (Directory.Exists(f) && DirectoryContainsImages(f))
				{
					imageFound = true;
					break;
				}

				// or if the path itself is an image
				if (sspack.MiscHelper.IsImageFile(f))
				{
					imageFound = true;
					break;
				}
			}

			// if an image is being added, we're going to copy them. otherwise, we don't accept them.
			e.Effect = imageFound ? DragDropEffects.Copy : DragDropEffects.None;
		}

		private void listBox1_DragDrop(object sender, DragEventArgs e)
		{
			// add all the files dropped onto the list box
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));
		}

		private void addImageBtn_Click(object sender, EventArgs e)
		{
			// show our file dialog and add all the resulting files
			if (imageOpenFileDialog.ShowDialog() == DialogResult.OK)
				AddFiles(imageOpenFileDialog.FileNames);
		}

		private void removeImageBtn_Click(object sender, EventArgs e)
		{
			// build a list of files to be removed
			List<string> filesToRemove = new List<string>();
			foreach (var i in listBox1.SelectedItems)
				filesToRemove.Add((string)i);

			// remove the files from our internal list
			files.RemoveAll(filesToRemove.Contains);

			// remove the files from our list box
			filesToRemove.ForEach(f => listBox1.Items.Remove(f));
		}

		private void clearBtn_Click(object sender, EventArgs e)
		{
			// clear both lists
			files.Clear();
			listBox1.Items.Clear();
		}

		private void browseImageBtn_Click(object sender, EventArgs e)
		{
			// show the file dialog
			imageSaveFileDialog.FileName = imageFileTxtBox.Text;
			if (imageSaveFileDialog.ShowDialog() == DialogResult.OK)
			{
				// store the image path
				imageFileTxtBox.Text = imageSaveFileDialog.FileName;

				// figure out which image exporter to use based on the extension
                string imageExtension = Path.GetExtension(imageSaveFileDialog.FileName).Substring(1);
				foreach (var exporter in sspack.Exporters.ImageExporters)
				{
					if (exporter.ImageExtension.Equals(imageExtension, StringComparison.InvariantCultureIgnoreCase))
					{
						currentImageExporter = exporter;    
						break;
					}
				}
                SpriteSheetPacker.Settings.Default.ImageExt = imageExtension;
				
				// if there is no selected map exporter, default to the last used
                if (currentMapExporter == null)
				{
                    string mapExtension = SpriteSheetPacker.Settings.Default.MapExt;
                    foreach (var exporter in sspack.Exporters.MapExporters)
                    {
                        if (exporter.MapExtension.Equals(mapExtension, StringComparison.InvariantCultureIgnoreCase))
                        {
                            currentMapExporter = exporter;
                            break;
                        }
                    }

                    // if the last used map exporter didn't exist, default to the first one
                    if (currentMapExporter == null)
                    {
                        currentMapExporter = sspack.Exporters.MapExporters[0];
                    }
				}
				
				mapFileTxtBox.Text = imageSaveFileDialog.FileName.Remove(imageSaveFileDialog.FileName.Length - 3) + currentMapExporter.MapExtension.ToLower();
			}
		}

		private void browseMapBtn_Click(object sender, EventArgs e)
		{
			// show the file dialog
			mapSaveFileDialog.FileName = mapFileTxtBox.Text;
			if (mapSaveFileDialog.ShowDialog() == DialogResult.OK)
			{
				// store the file path
				mapFileTxtBox.Text = mapSaveFileDialog.FileName;

				// figure out which map exporter to use based on the extension
                string mapExtension = Path.GetExtension(mapSaveFileDialog.FileName).Substring(1);
				foreach (var exporter in sspack.Exporters.MapExporters)
				{
                    if (exporter.MapExtension.Equals(mapExtension, StringComparison.InvariantCultureIgnoreCase))
					{
						currentMapExporter = exporter;
						break;
					}
				}
                SpriteSheetPacker.Settings.Default.MapExt = mapExtension;
			}
		}

		private void buildBtn_Click(object sender, EventArgs e)
		{
			// check our parameters
			if (files.Count == 0)
			{
				ShowBuildError("No images to pack into sheet");
				return;
			}
			if (string.IsNullOrEmpty(imageFileTxtBox.Text))
			{
				ShowBuildError("No image filename given.");
				return;
			}
			if (string.IsNullOrEmpty(mapFileTxtBox.Text))
			{
				ShowBuildError("No text filename given.");
				return;
			}
			int outputWidth, outputHeight, padding;
			if (!int.TryParse(maxWidthTxtBox.Text, out outputWidth) || outputWidth < 1)
			{
				ShowBuildError("Maximum width is not a valid integer value greater than 0.");
				return;
			}
			if (!int.TryParse(maxHeightTxtBox.Text, out outputHeight) || outputHeight < 1)
			{
				ShowBuildError("Maximum height is not a valid integer value greater than 0.");
				return;
			}
			if (!int.TryParse(paddingTxtBox.Text, out padding) || padding < 0)
			{
				ShowBuildError("Image padding is not a valid non-negative integer");
				return;
			}

            // disable all the controls while the build happens
            foreach (Control control in Controls)
                control.Enabled = false;

			int mw = int.Parse(maxWidthTxtBox.Text);
			int mh = int.Parse(maxHeightTxtBox.Text);
			int pad = int.Parse(paddingTxtBox.Text);
			bool pow2 = powOf2CheckBox.Checked;
			bool square = squareCheckBox.Checked;
			bool generateMap = generateMapCheckBox.Checked;
			string image = imageFileTxtBox.Text;
			string map = mapFileTxtBox.Text;

			string fileList = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpriteSheetPacker");
			if (!Directory.Exists(fileList))
			{
				Directory.CreateDirectory(fileList);
			}
			fileList = Path.Combine(fileList, "FileList.txt");

			using (StreamWriter writer = new StreamWriter(fileList))
			{
				foreach (var file in files)
				{
					writer.WriteLine(file);
				}
			}

			// construct the arguments in a list so we can pass the array cleanly to sspack.Program.Launch()
			List<string> args = new List<string>();
			args.Add("/image:" + image);
			if (generateMap)
				args.Add("/map:" + map);
			args.Add("/mw:" + mw);
			args.Add("/mh:" + mh);
			args.Add("/pad:" + pad);
			if (pow2)
				args.Add("/pow2");
			if (square)
				args.Add("/sqr");
			args.Add("/il:" + fileList);
			
			Thread buildThread = new Thread(delegate(object obj)
			{
				int sspackResult = sspack.Program.Launch(args.ToArray());
				(obj as Control).Invoke(new Action<int>(BuildThreadComplete), new[] { (object)sspackResult });
			})
			{
				IsBackground = true,
				Name = "Sprite Sheet Packer Worker Thread",
			};

			stopWatch.Reset();
			stopWatch.Start();
			buildThread.Start(this);
		}

		private void BuildThreadComplete(int result)
		{
			stopWatch.Stop();

			if (result == 0)
			{
#if DEBUG
				MessageBox.Show("Build completed in " + stopWatch.Elapsed.TotalSeconds + " seconds.");
#else
				MessageBox.Show("Build Complete", "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
#endif
			}
			else
			{
				ShowBuildError("Error packing images: " + SpaceErrorCode((sspack.FailCode)result));
			}

			// re-enable all our controls
			foreach (Control control in Controls)
				control.Enabled = true;
		}

		protected override void OnClosed(EventArgs e)
		{
			// get our UI values if they are valid
			int outputWidth, outputHeight, padding;

			if (int.TryParse(maxWidthTxtBox.Text, out outputWidth) && outputWidth > 0)
				SpriteSheetPacker.Settings.Default.MaxWidth = outputWidth;

			if (int.TryParse(maxHeightTxtBox.Text, out outputHeight) && outputHeight > 0)
				SpriteSheetPacker.Settings.Default.MaxHeight = outputHeight;

			if (int.TryParse(paddingTxtBox.Text, out padding) && padding >= 0)
				SpriteSheetPacker.Settings.Default.Padding = padding;

			SpriteSheetPacker.Settings.Default.PowOf2 = powOf2CheckBox.Checked;
			SpriteSheetPacker.Settings.Default.Square = squareCheckBox.Checked;
			SpriteSheetPacker.Settings.Default.GenerateMap = generateMapCheckBox.Checked;

			// save the settings
			SpriteSheetPacker.Settings.Default.Save();
		}

		private static string SpaceErrorCode(sspack.FailCode failCode)
		{
			string error = failCode.ToString();

			string result = error[0].ToString();

			for (int i = 1; i < error.Length; i++)
			{
				char c = error[i];
				if (char.IsUpper(c))
					result += " ";
				result += c;
			}

			return result;
		}

		private static void ShowBuildError(string error)
		{
			MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
	}
}
