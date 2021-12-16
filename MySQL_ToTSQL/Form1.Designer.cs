
namespace MySQL_ToTSQL
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.convertFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.convertDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MySQLOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.MSSQLSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.FindDirectoryBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.FromLabel = new System.Windows.Forms.Label();
            this.ToLabel = new System.Windows.Forms.Label();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(45, 48);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(576, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Welcome to the MySQL to TSQL Converter including Stored Procedures";
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.convertFileToolStripMenuItem,
            this.convertDirectoryToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(800, 33);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // convertFileToolStripMenuItem
            // 
            this.convertFileToolStripMenuItem.Name = "convertFileToolStripMenuItem";
            this.convertFileToolStripMenuItem.Size = new System.Drawing.Size(121, 29);
            this.convertFileToolStripMenuItem.Text = "Convert File";
            this.convertFileToolStripMenuItem.Click += new System.EventHandler(this.convertFileToolStripMenuItem_Click);
            // 
            // convertDirectoryToolStripMenuItem
            // 
            this.convertDirectoryToolStripMenuItem.Name = "convertDirectoryToolStripMenuItem";
            this.convertDirectoryToolStripMenuItem.Size = new System.Drawing.Size(167, 29);
            this.convertDirectoryToolStripMenuItem.Text = "Convert Directory";
            this.convertDirectoryToolStripMenuItem.Click += new System.EventHandler(this.convertDirectoryToolStripMenuItem_Click);
            // 
            // FromLabel
            // 
            this.FromLabel.AutoSize = true;
            this.FromLabel.Location = new System.Drawing.Point(85, 102);
            this.FromLabel.Name = "FromLabel";
            this.FromLabel.Size = new System.Drawing.Size(58, 25);
            this.FromLabel.TabIndex = 2;
            this.FromLabel.Text = "From:";
            // 
            // ToLabel
            // 
            this.ToLabel.AutoSize = true;
            this.ToLabel.Location = new System.Drawing.Point(85, 143);
            this.ToLabel.Name = "ToLabel";
            this.ToLabel.Size = new System.Drawing.Size(34, 25);
            this.ToLabel.TabIndex = 3;
            this.ToLabel.Text = "To:";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.ToLabel);
            this.Controls.Add(this.FromLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "MySQL to TSQL Converter";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem convertFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem convertDirectoryToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog MySQLOpenFileDialog;
        private System.Windows.Forms.SaveFileDialog MSSQLSaveFileDialog;
        private System.Windows.Forms.FolderBrowserDialog FindDirectoryBrowserDialog;
        private System.Windows.Forms.Label FromLabel;
        private System.Windows.Forms.Label ToLabel;
    }
}

