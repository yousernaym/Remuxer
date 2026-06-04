using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Remuxer
{
    public partial class Form1 : Form
    {
        Args _args;
        bool _validFile = false;

        public Form1(Args args) : base()
        {
            InitializeComponent();
            this._args = args;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            processInfo.Text = "";
            processInfo.ScrollBars = RichTextBoxScrollBars.None;
            if (!LibRemuxer.BeginProcessing(ref _args))
            {
                if (!_args.suppressErrors)
                    Program.ShowError($"Couldn't parse input file \"{_args.inputPath}\".");
                Environment.Exit(1);
            }
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
                        progressBar1.Invoke(new Action(
                            delegate
                            {
                                int percent = (int)(progress * 100);
                                if (progress > 0)
                                    progressBar1.Value = percent;
                                Text = percent.ToString() + "%";
                            }));
                        progress = LibRemuxer.Process();
                    }
                });
            }
            Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_validFile)
                LibRemuxer.EndProcessing();
        }

        private void ProcessInfo_TextChanged(object sender, EventArgs e)
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
