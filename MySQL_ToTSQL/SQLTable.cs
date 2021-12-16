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
    public class TableIndex
    {
        Boolean Unique;
//        Boolean FullText;
//        Boolean Spatial;
        String TableName;
        String Attributes;
        String IndexName;
        String Columns;

        public TableIndex( String tab)
        {
            Unique = false;
//            FullText = false;
//            Spatial = false;
            TableName = tab;
            IndexName = "";
            Columns = "";
            Attributes = "";
        }
        public void SetIndexName(String nName)
        {
            IndexName = nName;
        }
        public void AddColumn(String col)
        {
            if (Columns.Length > 0)
                Columns = Columns + ", " + col;
            else
                Columns = col;

        }
        public void AddAttribute(String Attribute)
        {
            switch (Attribute.ToUpper())
            {
                case "UNIQUE":
                    Unique = true;
                    break;
                case "INDEX":
                    break;
                case "FULLTEXT":
//                    FullText = true;
                    break;
                case "SPATIAL":
//                    Spatial = true;
                    break;
                default:
                    Attributes = Attributes + Attribute;
                    break;
            }
        }
        public override String ToString()
        {
            String results;

            results = "INDEX ";
            if (Unique)
                results = "UNIQUE " + results;
            results = results + IndexName + " ON " + TableName + " (" + Columns + ")";
            return results;
        }
    }

    public class ForeignKey
    {
//        String Table;
//        String Column;
    }
    class SQLTable : IOutputFile
    {
        MSSQLFile OutputFile;
        String TableName;
        List<String> ColumnNames;
        String PrimaryKeyName;
        TableIndex CurrentIndex;
        List<TableIndex> Indexes;
//        ForeignKey CurrentForeignKey;
        List<ForeignKey> ForeignKeys;

        public SQLTable(MSSQLFile outputFile)
        {
            OutputFile = outputFile;
            ColumnNames = new List<string>();
            Indexes = new List<TableIndex>();
            ForeignKeys = new List<ForeignKey>();
        }

        public void addIndexAttribute(String Att)
        {
            CurrentIndex.AddAttribute(Att);
        }
        public void addIndexColumn(String Col)
        {
            CurrentIndex.AddColumn(Col);
        }
        public void completeLine(String comment, int pos)
        {
            OutputFile.completeLine(comment, pos);
        }

        public void finishIndex()
        {
            Indexes.Add(CurrentIndex);
        }
        public void insureOutputLineFinished()
        {
            OutputFile.insureOutputLineFinished();
        }

        public void setIndent(int level)
        {
        }

        public void SetPrimaryKey(String primary, string terminator)
        {
            PrimaryKeyName = primary;
            OutputFile.Append("    " + "PRIMARY KEY (" + primary + ")" + terminator);
        }
        public void startForeignKey()
        {
            OutputFile.Append("    " + "FOREIGN");
        }
        public void continueForeignKey(String token)
        {
            OutputFile.Append(" " + token);
        }
        public void finishForiegnKey(String terminator)
        {
            OutputFile.Append(terminator);
        }
        public void StartIndex(String token)
        {
            CurrentIndex = new TableIndex(TableName);
            CurrentIndex.AddAttribute(token);
        }
        public void SetIndexName(String ind)
        {
            CurrentIndex.SetIndexName(ind);
        }
        public void writeDeleteTableIfExists(String LocalTableName)
        {
            OutputFile.write("IF EXISTS (SELECT     *");
            OutputFile.write("           FROM         sys.sysobjects");
            OutputFile.write("           WHERE     (type = 'U') AND (name = '" + LocalTableName + "'))");
            OutputFile.write("	BEGIN");
            OutputFile.write("		DROP  TABLE  " + LocalTableName);
            OutputFile.write("	END");
            OutputFile.write("GO");
        }
        public void writeTableHeader(String LocalTableName)
        {
            TableName = LocalTableName;
            OutputFile.Append("CREATE Table [" + LocalTableName + "]");
        }
        public void writeTableStart()
        {
            OutputFile.Append("(");
        }
        public void writeTableColumn(String ColumnName)
        {
            OutputFile.Append("    [" + ColumnName + "] ");
            ColumnNames.Add(ColumnName);
        }
        public void writeColumnAttribute(String Attribute)
        {
            OutputFile.Append(" " + Attribute);
        }
        public void writeColumnTerminator(String terminator)
        {
            OutputFile.Append(terminator);
        }
    
    
        public void writeTableEnd()
        {
            OutputFile.insureOutputLineFinished();
            OutputFile.write(") ON [PRIMARY]");
            OutputFile.write("GO");
            if (Indexes.Count > 0)
            {
                foreach (TableIndex ti in Indexes)
                {
                    OutputFile.write("Create " + ti.ToString());
                    OutputFile.write("GO");
                }
            }
        }

    }
}
