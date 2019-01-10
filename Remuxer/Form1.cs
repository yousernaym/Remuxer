using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Remux
{
	public partial class Form1 : Form
	{
		string inputPath = "";
		string audioPath = "";
		string midiPath = "";
		bool incorrectFlags = false;
		public Form1(string []args) : base()
		{
			InitializeComponent();
			bool midiFlag = false, audioFlag = false;

			if (args == null || args.Length == 0)
				showErrorMsg("No arguments specified.");

			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				if (arg[0] == '-')
				{
					if (arg == "-m")
						midiFlag = true;
					else if (arg == "-a")
						audioFlag = true;
					else
						showErrorMsg($"Invalid flag{arg}");
				}
				else
				{
					if (midiFlag)
						midiPath = arg;
					else if (audioFlag)
						audioPath = arg;
					else
						inputPath = arg;

				}
			}
		}

		private void showErrorMsg(string msg)
		{
			MessageBox.Show(msg);
			incorrectFlags = true;
		}
		private async void Form1_Load(object sender, EventArgs e)
		{
			if (incorrectFlags)
				Close();
			await Task.Run( delegate
			{
				for (int i = 0; i < 100; i++)
				{
					progressBar1.BeginInvoke(new Action(()=> progressBar1.Value += 1));
					Thread.Sleep(200);
				}
			});
			Close();
		}
	}
}
