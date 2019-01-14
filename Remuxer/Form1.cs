using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
			await Task.Run( delegate
			{
				if (!LibRemuxer.beginProcessing(_args))
					MessageBox.Show($"Couldn't load fIle {_args.inputPath}");
				else
				{
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


	}
}
