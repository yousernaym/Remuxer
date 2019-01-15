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
		Args _args;		
		readonly object progressLock = new object();

		public Form1(Args args) : base()
		{
			InitializeComponent();
			_args = args;			
		}

		private async void Form1_Load(object sender, EventArgs e)
		{
			processInfo.Text = "";
			await Task.Run( delegate
			{
				if (!LibRemuxer.beginProcessing(ref _args))
					MessageBox.Show($"Couldn't load fIle {_args.inputPath}");
				else
				{
					string text = "Extracting";
					if (_args.midiPath != null)
					{
						text += " notes";
						if (_args.audioPath != null)
							text += " and audio";
					}
					else
						text += " audio";
					text += $" from {Path.GetFileName(_args.inputPath)}";

					if (_args.numSubSongs > 1)
						text += $" ({_args.subSong}/{_args.numSubSongs}).";
					processInfo.BeginInvoke(new Action(
						delegate
						{
							processInfo.Text = text;
						}));

					float progress = 0;
					while (progress >= 0)
					{
						progressBar1.BeginInvoke(new Action(
							delegate
							{
								lock (progressLock)
								{
									if (progress > 0)
										progressBar1.Value = (int)(progress * 100);
								}
							}));
						lock(progressLock)
							progress = LibRemuxer.process();
					}
				}
			});
			Close();
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			LibRemuxer.finish();
		}

		private void processInfo_TextChanged(object sender, EventArgs e)
		{
			Size s = TextRenderer.MeasureText(this.processInfo.Text, this.processInfo.Font);
			int lines = s.Width / processInfo.Width + 1;
			s.Height *= lines;
			s.Height += 8;
			this.processInfo.Height = s.Height;
		}
	}
}
