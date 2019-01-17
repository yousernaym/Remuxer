﻿using System;
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
		readonly object _progressLock = new object();
		bool _validFile = false;

		public Form1(Args args) : base()
		{
			InitializeComponent();
			_args = args;			
		}

		private async void Form1_Load(object sender, EventArgs e)
		{
			processInfo.Text = "";
			processInfo.ScrollBars = RichTextBoxScrollBars.None;
			
			if (!LibRemuxer.beginProcessing(ref _args))
				Program.showError($"Unrecognized format of input file \"{_args.inputPath}\".");
			else
			{
				_validFile = true;
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
				processInfo.Text = text;

				await Task.Run(delegate
				{
					float progress = 0;
					while (progress >= 0)
					{
						progressBar1.BeginInvoke(new Action(
							delegate
							{
								lock (_progressLock)
								{
									if (progress > 0)
										progressBar1.Value = (int)(progress * 100);
								}
							}));
						lock (_progressLock)
							progress = LibRemuxer.process();
					}
				});
			}
			Close();
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (_validFile)
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
