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
				showUsage();
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

					//Was an argument relating to this flag specified?
					if (arg.Length > 2)
						flagArg = arg.Substring(2);
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
						if (flagArg != null)
						{
							if (!int.TryParse(flagArg, out args.subSong))
							{
								showUsage($"Invalid -s argument \"{flagArg}\".");
								return;
							}
						}
					}
					else if (flag == 'l')
					{
						if (flagArg != null)
						{
							if (!float.TryParse(flagArg, out args.songLengthS))
							{
								showUsage($"Invalid -l argument \"{flagArg}\".");
								return;
							}
						}
					}
					else if (flag == 'i')
					{
						args.modInsTrack = true;
					}
					else
					{
						showUsage($"Invalid flag -{flag}.");
						return;
					}
				}
				else
				{
					args.inputPath = cmdLineArgs[i];
					if (string.IsNullOrWhiteSpace(args.inputPath))
					{
						showUsage("No input file specified.");
						return;
					}
				}
			}

			//Check if input file exests
			if (!File.Exists(args.inputPath))
			{
				showError($"Couldn't find input file \"{args.inputPath}\".");
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
				showError(e.Message);
				return;
			}

			LibRemuxer.initLib();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1(args));
			LibRemuxer.closeLib();
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
				throw new Exception($"Invalid {flag} path \"{path}\".");
			}
		}
	
		static void showUsage(string errorMsg = null)
		{
			MessageBoxIcon mbIcon = MessageBoxIcon.Information;
			string usage = errorMsg;
			if (usage != null)
			{
				usage = "Error: " + errorMsg + "\n\n";
				mbIcon = MessageBoxIcon.Error;
			}

			usage += "Syntax: remuxer <input file> [-<flag>[argument]]\n\n";
			usage += "Flags:\n";
			usage += "-a[wav output file]      default = <input file>.wav\n";
			usage += "-m[midi output file]      default = <input file>.mid\n";
			usage += "-i One track per instrument instead of one per channel.\n";
			usage += "\n";
			usage += "Sid-specific:\n";
			usage += "-s<subsong number>\n";
			usage += "-l<length of song>\n";
			usage += "\n";
			usage += "If both -a and -m are ommitted, both are set implicitly.";
		
			MessageBox.Show(usage, "", MessageBoxButtons.OK, mbIcon);
		}

		public static void showError(string errorMsg)
		{
			MessageBox.Show("Error: " + errorMsg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
		public int numSubSongs; //out parameter
	}


}
