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
		bool incorrectFlags = false;

		public Form1(Args args) : base()
		{
			InitializeComponent();
			_args = args;			
		}

		
		private async void Form1_Load(object sender, EventArgs e)
		{
			if (incorrectFlags)
				Close();
			await Task.Run( delegate
			{
				LibRemuxer.beginProcessing(_args);
				float progress = 0;
				while (progress >= 0)
				{
					progressBar1.BeginInvoke(new Action(() => progressBar1.Value = (int)(progress * 100)));
					progress = LibRemuxer.process();
				}
			});
			Close();
		}
	}
}
