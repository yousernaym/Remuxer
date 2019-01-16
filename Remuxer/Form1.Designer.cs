namespace Remuxer
{
	partial class Form1
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.progressBar1 = new System.Windows.Forms.ProgressBar();
			this.processInfo = new System.Windows.Forms.RichTextBox();
			this.SuspendLayout();
			// 
			// progressBar1
			// 
			this.progressBar1.Location = new System.Drawing.Point(12, 12);
			this.progressBar1.Name = "progressBar1";
			this.progressBar1.Size = new System.Drawing.Size(259, 23);
			this.progressBar1.TabIndex = 2;
			// 
			// processInfo
			// 
			this.processInfo.Location = new System.Drawing.Point(12, 41);
			this.processInfo.Name = "processInfo";
			this.processInfo.ReadOnly = true;
			this.processInfo.Size = new System.Drawing.Size(259, 18);
			this.processInfo.TabIndex = 4;
			this.processInfo.Text = "";
			this.processInfo.TextChanged += new System.EventHandler(this.processInfo_TextChanged);
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoSize = true;
			this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.ClientSize = new System.Drawing.Size(283, 87);
			this.Controls.Add(this.processInfo);
			this.Controls.Add(this.progressBar1);
			this.Name = "Form1";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
			this.Load += new System.EventHandler(this.Form1_Load);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.ProgressBar progressBar1;
		private System.Windows.Forms.RichTextBox processInfo;
	}
}

