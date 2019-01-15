using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Remuxer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>

		[STAThread]
		static void Main(string[] cmdLineArgs)
		{
			Args args = new Args();
			if (cmdLineArgs.Length == 0)
			{
				showErrorMsg("No arguments specified.");
				return;
			}

			bool audioFlag = false, midiFlag = false;
			for (int i = 0; i < cmdLineArgs.Length; i++)
			{
				string arg = cmdLineArgs[i];
				if (arg.Length >= 2 && arg[0] == '-')
				{
					char flag = arg[1];
					string flagArg = null;

					//Was an argument relating to this flag specified
					if (i + 1 < cmdLineArgs.Length && cmdLineArgs[i + 1][0] != '-') 
						flagArg = cmdLineArgs[++i];
					if (flag == 'm')
					{
						midiFlag = true;
						args.midiPath = flagArg;
					}
					else if (flag == 'a')
					{
						audioFlag = true;
						args.audioPath = flagArg;
					}
					else if (flag == 's')
					{
						if (!int.TryParse(flagArg, out args.subSong))
						{
							MessageBox.Show($"Invalid sub song number: {flagArg}");
							return;
						}
					}
					else if (flag == 'l')
					{
						if (!float.TryParse(flagArg, out args.songLengthS))
						{
							MessageBox.Show($"Invalid sub song number: {flagArg}");
							return;
						}
					}
					else if (flag == 'i')
					{
						args.modInsTrack = true;
					}
					else
					{
						showErrorMsg($"Invalid flag -{flag}");
						return;
					}
				}
				else
				{
					args.inputPath = cmdLineArgs[i];
					if (string.IsNullOrWhiteSpace(args.inputPath))
					{
						MessageBox.Show("No input file specified.");
						return;
					}
				}
			}

			//Check if input file exests
			if (!File.Exists(args.inputPath))
			{
				MessageBox.Show($"Couldn't find or access input file {args.inputPath}");
				return;
			}

			//Derive output paths from input path if output path is not specified or if no output flags are specified
			bool noOutputFlags = !midiFlag && !audioFlag;
			if (noOutputFlags || midiFlag && args.midiPath == null)
				args.midiPath = Path.ChangeExtension(args.inputPath, "mid");
			if (noOutputFlags || audioFlag && args.audioPath == null)
				args.audioPath = Path.ChangeExtension(args.inputPath, "wav");
			
			//Check validity of output paths
			try
			{
				if (midiFlag)
					checkPath(args.midiPath, "-m");
				if (audioFlag)
					checkPath(args.audioPath, "-a");
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
			}

			LibRemuxer.initLib();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1(args));
			LibRemuxer.closeLib();
		}

		static void showErrorMsg(string msg)
		{
			MessageBox.Show(msg);
		}

	static void checkPath(string path, string flag)
	{
		try
		{
			var file = File.Create(path);
			file.Close();
			File.Delete(path);
		}
		catch (Exception)
		{
			throw new Exception($"Invalid {flag} path: \"{path}\"");
		}
	}
}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct Args
	{
		public string inputPath;
		public string audioPath;
		public string midiPath;
		public bool modInsTrack;
		public float songLengthS;
		public int subSong;
	}


}
