using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MySQL_ToTSQL
{
    public partial class Form1 : Form
    {
        // a base point for stored procedure signatures
        // Here is a half finished idea. 
        // the concept is to build a list of stored procedure signatures 
        // so that when they are called from within another procedure, the conversion will work right.
        // Right now, I build a list of my subroutines statically instead of dynamically

        List<Signature> StoredProcedureSignatures;

        public Form1()
        {
            InitializeComponent();
        }

        private void convertFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // wanting to convert a single file

            StoredProcedureSignatures = new System.Collections.Generic.List<Signature>();
            if (MySQLOpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                string FileToConvert = MySQLOpenFileDialog.FileName;
                FromLabel.Text = "From: " + FileToConvert;
                if (MSSQLSaveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string FileToSave = MSSQLSaveFileDialog.FileName;
                    ToLabel.Text = "To: " + FileToSave;
                    Application.DoEvents();

                    MySQLFile msq = new MySQLFile();
                    msq.parseMySQLFile(FileToConvert, FileToSave);
                }
            }

        }

        private void convertDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // wanting to convert a whole directory of files
            // actually, the way this is written, 
            // we should look for a batch file that was used to build all the stored procedures
            // MySQL was able to take in a batch file and execute all the create procedure
            // calls in that batch file. 

            StoredProcedureSignatures = new System.Collections.Generic.List<Signature>();
            if (MySQLOpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                string FileToConvert = MySQLOpenFileDialog.FileName;
                FromLabel.Text = "From: " + FileToConvert;

                if (FindDirectoryBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string ToDirectory = FindDirectoryBrowserDialog.SelectedPath;
                    MySQLFile msq = new MySQLFile();
                    msq.parseAllfiles(FileToConvert, ToDirectory);
                }
            }
        }
    }
}
