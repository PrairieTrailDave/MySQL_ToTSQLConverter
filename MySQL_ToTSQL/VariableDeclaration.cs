using System;
using System.Collections.Generic;
using System.Text;

//
// Copyright 2012-2013 Prairie Trail Software, Inc. 
// All rights reserved
//
namespace MySQL_ToTSQL
{
    public enum VariableTypes
    {
        REGULARVARIABLE,
        INPUTVARIABLE,
        OUTPUTVARIABLE
    }

    public class VariableDeclaration
    {
        public VariableTypes VariableType;
        String Name;
        public String SQLType;
        int Length;
        int Precision;
        public String comment;

        public VariableDeclaration()
        {
            Length = 0;
            Precision = 0;
            comment = "";
        }

        public VariableDeclaration(VariableTypes Type, String nName)
        {
            VariableType = Type;
            if (nName[0] == '@')
                Name = nName.Substring(1);
            else
                Name = nName;
            Length = 0;
            Precision = -1;
            comment = "";
        }
        public void SetSQLType (String nType)
        {
            SQLType = nType;
        }
        public String GetSQLType()
        {
            if ((Length == 1) || (Length == 0))
                return SQLType;
            if (Precision > -1)
                return SQLType + " (" + Length.ToString() + "," + Precision.ToString() + ")";
            return SQLType + " (" + Length.ToString() + ")";
        }
        public void SetLength(int nLen)
        {
            Length = nLen;
        }
        public void SetPrecision(int prec)
        {
            Precision = prec;
        }
        public String GetName()
        {
            return "@" + Name;
        }
        public Boolean CompareName(String nm)
        {
            if (nm[0] == '@')
                nm = nm.Substring(1);
            if (Name.ToUpper() == nm.ToUpper())
                return true;
            return false;
        }
        public int getNameLength()
        {
            return Name.Length;
        }
        public void setComment(String nComment)
        {
            comment = nComment;
        }
        public void setDirection(Boolean ifOutput)
        {
            if (ifOutput)
                VariableType = VariableTypes.OUTPUTVARIABLE;
            else
                VariableType = VariableTypes.INPUTVARIABLE;
        }
        public String getDirection()
        {
            switch (VariableType)
            {
                case VariableTypes.OUTPUTVARIABLE:
                    return " OUTPUT";
                case VariableTypes.INPUTVARIABLE:
                default:
                    return "";
            }
        }
        public String mapSqlDataType(String sqlType)
        {
            // I pulled some example code for SQL conversion and they didn't have a full
            // set of the SQL data types nor did they convert them all. 
            // Here are my suggestions

            // SQL Server 2008 data types
            // bit, 
            // tinyint, smallint, int, bigint,
            // numeric, decimal,
            // smallmoney, money, 
            // float, real
            // date, datetimeoffset, datetime2, smalldatetime, datetime, time,
            // char, varchar, text, nchar, nvarchar, ntext, 
            // binary, varbinary, image, cursor, 
            // timestamp, hierarchyid, uniqueidentifier, sql_variant, xml, table

            // MySQL Data Types version 5.5        Mapped to
            // Bit (M)                                  BIT
            // TinyInt (M)                              TINYINT
            // Bool                                     BIT
            // Boolean                                  BIT
            // SmallInt                                 SMALLINT
            // MediumInt                                INT
            // Int                                      INT
            // Integer                                  INT
            // BigInt                                   BIGINT
            // Serial - BigInt Unsigned not null auto-increment unique  BIGINT
            // (why not uniqueidentifier? Because of references.)
            // Float (M, D)                             FLOAT
            // Real                                     REAL
            // Double                                   FLOAT
            // Double Precision                         FLOAT
            // Decimal (M, D) UNSIGNED ZEROFILL         DECIMAL
            // Dec (M, D)                               DECIMAL
            // Numeric (M, D)                           NUMERIC
            // Fixed (M, D)                             NUMERIC
            // Date                                     DATE
            // DATETIME                                 DATETIME
            // TIMESTAMP                                DATETIME
            // Time                                     TIME
            // Year(2|4)                                DATE
            // [NATIONAL] CHAR(m) CHARACTER SET n COLLATE nm        CHAR
            // CHAR BYTE - binary                                   BINARY
            // [NATIONAL] VARCHAR (M) CHARACTER SET n COLLATE nm    VARCHAR
            // BINARY (m)                                           BINARY
            // VARBINARY (m)                                        VARBINARY
            // TINYBLOB                                             VARBINARY
            // BLOB                                                 VARBINARY
            // MEDIUMBLOB                                           VARBINARY
            // LONGBLOB                                             VARBINARY
            // TINYTEXT CHARACTER SET n COLLATE nm                  TEXT
            // TEXT(m) CHARACTER SET n COLLATE nm                   TEXT
            // LONGTEXT(m) CHARACTER SET n COLLATE nm               TEXT
            // ENUM (...) CHARACTER SET n COLLATE nm                TEXT
            // SET (...) CHARACTER SET n COLLATE nm                 TEXT


            string results;

            switch (sqlType.ToLower())
            {
                case "bit":              results = "BIT"; break;
                case "tinyint":          results = "TINYINT"; break;
                case "bool":             results = "BIT"; break;
                case "boolean":          results = "BIT"; break;
                case "smallint":         results = "SMALLINT"; break;
                case "mediumint":        results = "INT"; break;
                case "int":              results = "INT"; break;
                case "integer":          results = "INT"; break;
                case "bigint":           results = "BIGINT"; break;
                case "serial":           results = "BIGINT"; break;
                case "float":            results = "FLOAT"; break;
                case "real":             results = "REAL"; break;
                case "double":           results = "FLOAT"; break;
                case "double precision": results = "FLOAT"; break;
                case "decimal":          results = "DECIMAL"; break;
                case "dec":              results = "DECIMAL"; break;
                case "numeric":          results = "NUMERIC"; break;
                case "fixed":            results = "NUMERIC"; break;
                case "date":             results = "DATE"; break;
                case "datetime":         results = "DATETIME"; break;
                case "timestamp":        results = "DATETIME"; break;
                case "time":             results = "TIME"; break;
                case "year":             results = "DATE"; break;
                case "char":             results = "CHAR"; break;
                case "char byte":        results = "BINARY"; break;
                case "varchar":          results = "VARCHAR"; break;
                case "binary":           results = "BINARY"; break;
                case "varbinary":        results = "VARBINARY"; break;
                case "tinyblob":         results = "VARBINARY"; break;
                case "blob":             results = "VARBINARY"; break;
                case "mediumblob":       results = "VARBINARY"; break;
                case "longblob":         results = "VARBINARY"; break;
                case "tinytext":         results = "TEXT"; break;
                case "text":             results = "TEXT"; break;
                case "longtext":         results = "TEXT"; break;
                case "enum":             results = "TEXT"; break;
                case "set":              results = "TEXT"; break;
                case "natural":          results = "NATURAL"; break;
                case "nvarchar":         results = "NVARCHAR"; break;
                default:
                    results = "Object";
                    throw new Exception("Unknown SQL Type: " + sqlType);
            }
            return results;
        }




        public String mapQualifier(String qualifier)
        {
            String results;

            switch (qualifier.ToLower())
            {
                case "not": results = "NOT"; break;
                case "null": results = "NULL"; break;
                case "auto_increment": results = "IDENTITY (1,1)"; break;
                case "default": results = "DEFAULT"; break;
                default:
                    // ignore UNSIGNED as that is used for identity
                    results = "";
                    break;
            }
            return results;
        }
        // one thing that we have to watch for are the MySQL global variables
        // MySQL had @var as global to the connection and didn't need to be
        // declared. SQL Server requires all variables to be declared
        public String addIdentifier(String iIdentifier)
        {
            if (iIdentifier[0] == '@')
                return (iIdentifier);
            else
                return ("@" + iIdentifier);
        }



    }
}
