using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Remuxer
{
	public partial class Form1 : Form
	{
		Args args;	
		bool validFile = false;

		public Form1(Args args) : base()
		{
			InitializeComponent();
			this.args = args;			
		}

		private async void Form1_Load(object sender, EventArgs e)
		{
			processInfo.Text = "";
			processInfo.ScrollBars = RichTextBoxScrollBars.None;
			if (!LibRemuxer.beginProcessing(ref args))
			{
				if (!args.suppressErrors)
					Program.showError($"Couldn't parse input file \"{args.inputPath}\".");
				Environment.Exit(1);
			}
			else
			{
				validFile = true;
				string text = "Extracting";
				if (args.midiPath != null)
				{
					text += " notes";
					if (args.audioPath != null)
						text += " and audio";
				}
				else
					text += " audio";
				text += $" from {Path.GetFileName(args.inputPath)}";

				if (args.numSubSongs > 1)
					text += $" ({args.subSong}/{args.numSubSongs}).";
				processInfo.Text = text;

				await Task.Run(delegate
				{
					float progress = 0;
					while (progress >= 0)
					{
						progressBar1.Invoke(new Action(
							delegate
							{
								if (progress > 0)
									progressBar1.Value = (int)(progress * 100);
							}));
						progress = LibRemuxer.process();
					}
				});
			}
			Close();
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (validFile)
				LibRemuxer.finish();
		}

		private void processInfo_TextChanged(object sender, EventArgs e)
		{
			Size s = TextRenderer.MeasureText(this.processInfo.Text, this.processInfo.Font);
			int lines = (s.Width - 2) / processInfo.Width + 1;
			s.Height *= lines;
			s.Height += 8;
			this.processInfo.Height = s.Height;
		}

		private void Form1_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
				Close();
		}
	}
}
