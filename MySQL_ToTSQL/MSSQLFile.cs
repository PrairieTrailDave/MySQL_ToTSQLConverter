using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

//
// Copyright 2012-2013 Prairie Trail Software, Inc. 
// All rights reserved
//
namespace MySQL_ToTSQL
{
    public interface IOutputFile
    {
        void completeLine(String comment, int pos);
        void setIndent(int level);
    }

    class MSSQLFile :IOutputFile
    {
        public String OutputFileName;
        public StreamWriter OutputFile;

        public int Indent;
        protected StringBuilder outputLine;

        // we start with a list of strings and add to it till the table/
        // procedure is done. Then, we print it all out at once.

        public MSSQLFile()
        {
            Indent = 0;     // start with no indent
            outputLine = new StringBuilder();
        }
        public void CreateNewFileFromOldName(String OldFileName)
        {
            OutputFileName = OldFileName;
        }
        public void StartOutputFile(String FileName)
        {
            OutputFile = new StreamWriter(FileName, false);
        }
        public void Add(String toAdd)
        {
            write(toAdd);
        }

        public String newIndent()
        {
            return (
        "                                                                      ".Substring(0, Indent));
        }
        public void setIndent(int level)
        {
            Indent = level * 2;
        }

        public void writeComment(String Comment)
        {
            write(newIndent() + "-- " + Comment);
        }
        public void writeIncludeFile(String filename)
        {
            write("r: " + filename);
        }

        public void completeLine(String comment, int pos)
        {
            if (pos > -1)
            {
                while (outputLine.Length < pos)
                    outputLine.Append(' ');
                outputLine.Append("--" + comment);
            }
            write(outputLine.ToString());
            outputLine = new StringBuilder();
        }

        public void insureOutputLineFinished()
        {
            if (outputLine.Length > 0)
            {
                write(outputLine.ToString());
                outputLine = new StringBuilder();
            }
        }
       
        public void Append(String str)
        {
            outputLine.Append(str);
        }

        public void write(String str)
        {
            OutputFile.WriteLine(str);
        }
        public void finish()
        {
            if (outputLine.Length > 0)
                OutputFile.Write(outputLine.ToString());
            OutputFile.Close();
        }
    }
}
