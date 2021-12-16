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
    class SQLProcedure : IOutputFile
    {
        MSSQLFile OutputFile;
        public List<VariableDeclaration> Declarations;
        public int MaxNameSize;
        public List<String> Body;
        public List<VariableDeclaration> Parameters;
        StringBuilder outputLine;
        String ProcedureName;

        public enum whereWeAre { notActive, BuildingParmList, BuildingVarList, BuildingBody } ;
        whereWeAre BuildState;

        public int Indent;

        public SQLProcedure(MSSQLFile baseFile)
        {
            OutputFile = baseFile;
            Declarations = new List<VariableDeclaration>();
            Body = new List<string>();
            Parameters = new List<VariableDeclaration>();
            MaxNameSize = 0;
            outputLine = new StringBuilder();
            ProcedureName = "";
            BuildState = whereWeAre.notActive;
        }


        public void writeDeleteProcedureIfExists(String LocalProcedureName)
        {
            OutputFile.write("IF EXISTS (SELECT     *");
            OutputFile.write("           FROM         sys.sysobjects");
            OutputFile.write("           WHERE     (type = 'P') AND (name = '" + LocalProcedureName + "'))");
            OutputFile.write("	BEGIN");
            OutputFile.write("		DROP  Procedure  " + LocalProcedureName);
            OutputFile.write("	END");
            OutputFile.write("");
            OutputFile.write("GO");
            OutputFile.write("");

        }

        
        // actually, we don't do it here
        public void addInitialBegin()
        {
        }

        public void AddLine(String line)
        {
            OutputFile.write(line);
        }

        public Boolean isAlreadyDefined(String Variable)
        {
                // say all numeric constants are already defined

            if ((Variable[0] >= '0') && (Variable[0] <= '9'))
                return true;

            if (Variable[0] == '\'')
                return true;
            if (Variable.ToUpper() == "NULL")
                return true;

                // look for the variable in the current list

            foreach (VariableDeclaration vr in Declarations)
            {
                if (vr.CompareName(Variable))
                    return true;
            }
            return false;
        }

        // we will delay the adding to results till we have all 
        // local variables defined

        public void addLocalVariable(VariableDeclaration lVar)
        {
            Declarations.Add(lVar);
            if (MaxNameSize < lVar.getNameLength()) MaxNameSize = lVar.getNameLength() +2;
            BuildState = whereWeAre.BuildingVarList;

            // to be deleted later
//            string DeclarationLine;
//            DeclarationLine = newIndent() + "DECLARE " + lVar.GetName() + " " + lVar.GetSQLType() + ";";
//            OutputFile.Append(DeclarationLine);
        }
        public void addParameter(VariableDeclaration nVar, Boolean ifOutput)
        {
            Declarations.Add(nVar);
            nVar.setDirection(ifOutput);
            if (MaxNameSize < nVar.getNameLength()) 
                MaxNameSize = nVar.getNameLength() + 2;
            Parameters.Add(nVar);
        }
        public void addParameterTerminator()
        {
        }
        public void writeProcedureHeader(String LocalProcedureName)
        {
            ProcedureName = LocalProcedureName;
            OutputFile.write("CREATE Procedure " + LocalProcedureName);
        }
        public void writeParameterStart()
        {
            Parameters = new List<VariableDeclaration>();
            BuildState = whereWeAre.BuildingParmList;
        }
        public void writeParameterEnd()
        {
            int cnt;
            string parmStr;
            string term;

            if (Parameters.Count > 0)
            {
                OutputFile.write("    (");
                cnt = 1;
                foreach (VariableDeclaration parm in Parameters)
                {
                    parmStr = formatName(parm.GetName(), MaxNameSize) + " " + parm.GetSQLType();
                    if (cnt < Parameters.Count)
                        term = ",";
                    else
                        term = "";

                    if (parm.comment.Length > 0)
                        parmStr = parmStr + term + "      -- " + parm.comment;
                    else
                        parmStr = parmStr + term;
                    parmStr = newIndent() + parmStr;
                    OutputFile.write(parmStr);

                    cnt++;
                }
                OutputFile.write("    )");
            }
            BuildState = whereWeAre.notActive;
        }

        public void writeProcedureStartIndicator()
        {
            OutputFile.write("AS");
        }


        public void Append(String str)
        {
            if (outputLine.Length == 0)
                outputLine.Append(newIndent());
            outputLine.Append(str);
        }
        public void AppendVariableReference(String Variable)
        {
            Append(ConvertVariableReference(Variable));
        }
        public string ConvertVariableReference(String Variable)
        {
            String results;

            if (Variable[0] == '\'')
                results = Variable;
            else
                if (Variable[0] == '@')
                    results = Variable;
                else
                    if (Variable.ToUpper() == "NULL")
                        results = "NULL";
                    else
                        results = "@" + Variable;
            return results;
        }

        public void addAssignment(String Variable)
        {
            String AssignLine;

            // first make sure that the variable is in the list of local declarations

            VariableDeclaration vr = new VariableDeclaration(VariableTypes.REGULARVARIABLE, Variable);
            if (!isAlreadyDefined(Variable))
            {
                // if not, add it and default to integer
                // may want to check the type of assign to see if char is warrented

                vr.SetSQLType("INT");
                vr.SetLength(1);
                Declarations.Add(vr);
            }
            AssignLine = newIndent() + "SET " + vr.GetName() + " = ";
            outputLine.Append(AssignLine);
            BuildState = whereWeAre.BuildingBody;
        }

        public void addCall(String functionName)
        {
            String callLine;

            insureOutputLineFinished();
            callLine = newIndent() + "EXECUTE " + functionName + " ";
            outputLine.Append(callLine);
            BuildState = whereWeAre.BuildingBody;
        }

        public void addParameterSeparator()
        {
            outputLine.Append(", ");
        }
        public void addInputCallParameter(String parameter)
        {
            if (parameter[0] == '\'')
                outputLine.Append(parameter);
            else
                if (Char.IsDigit(parameter[0]))
                    outputLine.Append(parameter);
                else
                    if (parameter[0] == '@')
                        outputLine.Append(parameter);
                    else
                        if (parameter.ToUpper() == "NULL")
                            outputLine.Append(parameter);
                        else
                            outputLine.Append("@" + parameter);
        }
        public void addOutputCallParameter(String parameter)
        {
            if (parameter[0] == '@')
                outputLine.Append(parameter + " OUTPUT");
            else
            outputLine.Append("@" + parameter + " OUTPUT");
        }
        public void finishStatement()
        {
            outputLine.Append(";");
            BuildState = whereWeAre.BuildingBody;
        }

        // we are going to assume that all if's and else's will need
        // to include Begin end clauses
        public void IF()
        {
            String ifLine;

            insureOutputLineFinished();
            ifLine = newIndent() + "BEGIN";
            outputLine.Append(ifLine);
            BuildState = whereWeAre.BuildingBody;
        }
        public void ELSE()
        {
            String elseLine;

            insureOutputLineFinished();
            decrementIndent();
            elseLine = newIndent() + "END";
            Body.Add(elseLine);
            elseLine = newIndent() + "ELSE";
            Body.Add(elseLine);
            elseLine = newIndent() + "BEGIN";
            outputLine.Append(elseLine);
            incrementIndent();
        }
        public void ENDIF()
        {
            String endifLine;
            insureOutputLineFinished();
            endifLine = newIndent() + "END";
            outputLine.Append(endifLine);
        }
        // the end of the WHILE clause
        public void DO()
        {
            String doLine;

            insureOutputLineFinished();
            doLine = newIndent() + "BEGIN";
            outputLine.Append(doLine);
            incrementIndent();
        }


        public void StartLine()
        {
            outputLine.Append(newIndent());
        }

        public void StartInsert()
        {
            insureOutputLineFinished();
            outputLine.Append(newIndent() + "INSERT ");
            BuildState = whereWeAre.BuildingBody;
        }
        // a select can occur within another statement
        // so simply start it.
        public void StartSelect()
        {
            outputLine.Append("SELECT ");
            BuildState = whereWeAre.BuildingBody;
        }
        public void setTableName(String TableName)
        {
            switch (TableName)
            {
                case "INNER":
                case "OUTER":
                case "LEFT":
                case "RIGHT":
                case "JOIN":
                case "ON":
                    outputLine.Append(TableName + " ");
                    break;
                case ".":
                case "=":
                case ",":
                    outputLine.Append(TableName);
                    break;
                default:
                    if (TableName.IndexOf(".") > 0)
                    {
                        string tbl = TableName.Substring(0, TableName.IndexOf("."));
                        string col = TableName.Substring(TableName.IndexOf(".")+1);
                        outputLine.Append("[" + tbl + "]." + col);
                    }
                    else
                        outputLine.Append("[" + TableName + "] ");
                    break;
            }
        }
        // the challenge with the TOP clause is that 
        // TOP has to be right after the SELECT/DELETE in SQL Server while
        // the it doesn't in MySQL
        // So, we have to find the select statement and insert the TOP clause
        public void Top(String expression, String Statement)
        {
            int i;
            int line;

            if (outputLine.ToString().IndexOf(Statement) > 0)
            {
                i = outputLine.ToString().LastIndexOf(Statement);
                outputLine.Insert(i + 6, " TOP(" + expression + ")");
            }
            else
            {
                line = Body.Count - 1;
                while (line > 0)
                {
                    if (Body[line].IndexOf(Statement) > 0)
                    {
                        String s = Body[line];
                        i = s.LastIndexOf(Statement);
                        s = s.Insert(i + 6, " TOP(" + expression + ")");
                        Body[line] = s;
                        break;
                    }
                    else
                        line--;
                }
            }
        }


        public void StartDelete()
        {
            String DeleteLine;

            insureOutputLineFinished();
            DeleteLine = newIndent() + "DELETE";
            outputLine.Append(DeleteLine);
            BuildState = whereWeAre.BuildingBody;
        }

        public void StartUpdate()
        {
            String UpdateLine;

            insureOutputLineFinished();
            UpdateLine = newIndent() + "UPDATE";
            outputLine.Append(UpdateLine);
            BuildState = whereWeAre.BuildingBody;
        }

        public void setUpdateTable(String TableName)
        {
            outputLine.Append(" [" + TableName + "]");
        }
        public void startSetClause()
        {
            if (outputLine.Length < 1)
                outputLine.Append(newIndent() + "SET");
            else
                outputLine.Append(" " + "SET");
        }

        public void StartWhile()
        {
            insureOutputLineFinished();
            outputLine.Append(newIndent() + "WHILE");
            BuildState = whereWeAre.BuildingBody;
        }
        public void setColumn(String columnName, String ColOperator)
        {
            if (outputLine.Length == 0)
                outputLine.Append(newIndent() + columnName + " " + ColOperator + " ");
            else
                outputLine.Append(" " + columnName + " " + ColOperator + " ");
        }

        public void startWhereClause()
        {
            String UpdateLine;

            insureOutputLineFinished();
            UpdateLine = newIndent() + "  WHERE";
            outputLine.Append(UpdateLine);
        }

        public void writeEndStoredProcedure()
        {
            string DeclarationLine;
            string declare;

            Indent = 2;

            OutputFile.write("BEGIN");

            foreach (VariableDeclaration vr in Declarations)
            {
                if (vr.VariableType == VariableTypes.REGULARVARIABLE)
                {
                    declare = formatName(vr.GetName(), MaxNameSize) + " " + vr.GetSQLType();
                    if (vr.comment.Length > 0)
                        declare = declare + ";      -- " + vr.comment;
                    else
                        declare = declare + ";";
                    DeclarationLine = newIndent() + "DECLARE " + declare;
                    OutputFile.write(DeclarationLine);
                }
            }
            foreach (String l in Body)
            {
                OutputFile.write(l);
            }

            OutputFile.write("END");
            OutputFile.write("GO");
            OutputFile.write("");
            OutputFile.write("GRANT EXEC ON " + ProcedureName + " TO PUBLIC");
            OutputFile.write("");
            OutputFile.write("GO");
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
        public void incrementIndent()
        {
            Indent++;
            Indent++;
        }
        public void decrementIndent()
        {
            if (Indent > 0) Indent--;
            if (Indent > 0) Indent--;
        }

        string formatName(String name, int length)
        {
            while (name.Length < length)
                name = name + " ";
            return name;
        }


        public String convertToSystemVar(String functionName)
        {
            switch (functionName.ToUpper())
            {
                // function in MySQL but sys var in SQLSERVER
                case "FOUND_ROWS":
                    return "@@ROWCOUNT";
            }
            return ("");
        }

        // we need a way to convert function names between the two
        public String convertFunction(String functionName)
        {
            switch (functionName.ToUpper())
            {
                case "ASCII":
                    return "ASCII";
                case "BIN":
                    throw new NotImplementedException("Unable to convert BIN function");
                case "BIT_LENGTH":
                    return "LEN";
                case "CHAR_LENGTH":
                    return "LEN";
                case "CHAR":
                    return "CHAR";
                case "CHARACTER_LENGTH":
                    return "LEN";
                case "CONCAT_WS":
                    return "CONCAT";
                case "CONCAT":
                    return "CONCAT";
                case "ELT":
                    throw new NotImplementedException ("Unable to convert ELT function");
                case "EXPORT_SET":
                    throw new NotImplementedException ("Unable to convert Export_set function");
                case "FIELD":
                    throw new NotImplementedException ("Unable to convert Field function");
                case "FIND_IN_SET":
                    throw new NotImplementedException("Unable to convert Find_in_set function");
                case "FORMAT":
                    throw new NotImplementedException("Unable to convert Format function");
                case "HEX":
                    throw new NotImplementedException("Unable to convert Hex function");
                case "INSERT":
                    throw new NotImplementedException("Unable to convert insert function");
                case "INSTR":
                    return "CHARINDEX";
                case "LCASE":
                    return "LOWER";
                case "LEFT":
                    return "LEFT";
                case "LENGTH":
                    return "LEN";
                case "LOAD_FILE":
                    throw new NotImplementedException("Unable to convert Load_File function");
                case "LOCATE":
                    return "CHARINDEX";
                case "LOWER":
                    return "LOWER";
                case "LPAD":
                    throw new NotImplementedException ("Unable to convert LPAD function");
                case "LTRIM":
                    return "LTRIM";
                case "MAKE_SET":
                    throw new NotImplementedException("Unable to convert MAKE_set function");
                case "MATCH":
                    throw new NotImplementedException("Unable to convert Match function");
                case "MID":
                    return "SUBSTRING";
                case "OCTET_LENGTH":
                    throw new NotImplementedException("Unable to convert Octet_Length function");
                case "ORD":
                    throw new NotImplementedException("Unable to convert ORD function");
                case "POSITION":
                    return "CHARINDEX";
                case "QUOTE":
                    throw new NotImplementedException("Unable to convert Quote function");
                case "REGEXP":
                    throw new NotImplementedException("Unable to convert REGEXP function");
                case "REPEAT":
                    return "REPLICATE";
                case "REPLACE":
                    return "REPLACE";
                case "REVERSE":
                    return "REVERSE";
                case "RIGHT":
                    return "RIGHT";
                case "RLIKE":
                    throw new NotImplementedException("Unable to convert RLIKE function");
                case "RPAD":
                    throw new NotImplementedException("Unable to convert RPAD function");
                case "RTRIM":
                    return "RTRIM";
                case "SOUNDEX":
                    return "SOUNDEX";
                case "SOUNDS":     // has following "like"
                    throw new NotImplementedException("Unable to convert Sounds function");
                case "SPACE":
                    return "SPACE";
                case "STRCMP":
                    throw new NotImplementedException("Unable to convert STRCMP function");
                case "SUBSTR":
                    return "SUBSTRING";
                case "SUBSTRING_INDEX":
                    throw new NotImplementedException("Unable to convert Substring_Index function");
                case "SUBSTRING":
                    return "SUBSTRING";
                case "TRIM":
                    return "TRIM";
                case "UCASE":
                    return "UPPER";
                case "UNHEX":
                    throw new NotImplementedException("Unable to convert UnHex function");
                case "UPPER":
                    return "UPPER";

                case "TIMESTAMPADD":
                    return "DATEADD";
                case "TIMESTAMPDIFF":
                    return "DATEDIFF";
                case "ISNULL":
                    return "ISNULL";
                case "IFNULL":
                    return "ISNULL";
                case "NOW":
                    return "GETDATE";
                case "LAST_INSERT_ID":
                    return "SCOPE_IDENTITY";  // better than @@identity when triggers exist
                case "HOUR":
                    return "HOUR";

                case "AVG":
                    return "AVG";
                case "BIT_AND":
                case "BIT_OR":
                case "BIT_XOR":
                    throw new NotImplementedException ("Can't convert " + functionName + " function");
                case "COUNT":
                    return "COUNT";
                case "GROUP_CONCAT":
                    throw new NotImplementedException ("Can't convert " + functionName + " function");
                case "MAX":
                    return "MAX";
                case "MIN":
                    return "MIN";
                case "STD":
                    return "STDEV";
                case "STDDEV_POP":
                    throw new NotImplementedException ("Can't convert " + functionName + " function");
                case "STDDEV_SAMP":
                    throw new NotImplementedException ("Can't convert " + functionName + " function");
                case "STDDEV":
                    return "STDEV";
                case "SUM":
                    return "SUM";
                case "VAR_POP":
                    throw new NotImplementedException ("Can't convert " + functionName + " function");
                case "VAR_SAMP":
                    throw new NotImplementedException ("Can't convert " + functionName + " function");
                case "VARIANCE":
                    return "VAR";


                case "CAST":
                    return "CAST";
                case "RAND":
                    return "RAND";
                case "ROUND":
                    return "ROUND";


            //       MS SQL Server string functions
            //ASCII
            //NCHAR
            //SOUNDEX
            //CHAR 
            //PATINDEX
            //SPACE
            //CHARINDEX
            //QUOTENAME
            //STR
            //DIFFERENCE
            //REPLACE
            //STUFF
            //LEFT
            //REPLICATE
            //SUBSTRING
            //LEN
            //REVERSE
            //UNICODE
            //LOWER
            //RIGHT
            //UPPER
            //LTRIM
            //RTRIM

                    // MS SQL Aggregate Functions
            //AVG
            //MIN
            //CHECKSUM_AGG
            //SUM
            //COUNT
            //STDEV
            //COUNT_BIG
            //STDEVP
            //GROUPING
            //VAR
            //MAX
            //VARP
 

                    // MS configuration variables / functions
            //@@DATEFIRST
            //@@OPTIONS
            //@@DBTS
            //@@REMSERVER
            //@@LANGID
            //@@SERVERNAME
            //@@LANGUAGE
            //@@SERVICENAME
            //@@LOCK_TIMEOUT
            //@@SPID
            //@@MAX_CONNECTIONS
            //@@TEXTSIZE
            //@@MAX_PRECISION
            //@@VERSION
            //@@NESTLEVEL
  
            // MS Cursor functions
            // @@CURSOR_ROWS
            // CURSOR_STATUS
            // @@FETCH_STATUS

            // SYSDATETIME
                //SYSDATETIMEOFFSET
                //SYSUTCDATETIME
                //CURRENT_TIMESTAMP
                //GETDATE
                //GETUTCDATE
                //DATENAME ( datepart , date )
                //DATEPART ( datepart , date )
                //DAY ( date )
                //MONTH ( date )
                //YEAR ( date )
                //DATEDIFF ( datepart , startdate , enddate )
                //DATEADD (datepart , number , date )
                //SWITCHOFFSET (DATETIMEOFFSET , time_zone)
                //TODATETIMEOFFSET (expression , time_zone)
                //@@DATEFIRST
                //SET DATEFIRST { number | @number_var }
                //SET DATEFORMAT { format | @format_var }
                //@@LANGUAGE
                //SET LANGUAGE { [ N ] 'language' | @language_var }
                //sp_helplanguage [ [ @language = ] 'language' ]
                //ISDATE ( expression )
//ABS DEGREES RAND
                    //ACOS EXP ROUND
                    //ASIN FLOOR SIGN
                    //ATAN LOG SIN
                    //ATN2 LOG10 SQRT
                    //CEILING PI SQUARE
                    //COS POWER TAN
                    //COT RADIANS  
 




// MySQL functions                                          MS SQL Function
// String Functions
//ASCII() Return numeric value of left-most character       ASCII
//BIN() Return a string representation of the argument      CHAR
//BIT_LENGTH() Return length of argument in bits            no equivalent
//CHAR_LENGTH() Return number of characters in argument     LEN
//CHAR() Return the character for each integer passed       CHAR
//CHARACTER_LENGTH() A synonym for CHAR_LENGTH()            LEN
//CONCAT_WS() Return concatenate with separator             +
//CONCAT() Return concatenated string                       +
//ELT() Return string at index number                       SUBSTR
//EXPORT_SET() Return a string such that for every bit set in the value bits, you get an on string and for every unset bit, you get an off string 
//                                                          no equivalent
//FIELD() Return the index (position) of the first argument in the subsequent arguments 
//                                                          CHARINDEX
//FIND_IN_SET() Return the index position of the first argument within the second argument 
//                                                          CHARINDEX
//FORMAT() Return a number formatted to specified number of decimal places 
//                                                          ROUND
//HEX() Return a hexadecimal representation of a decimal or string value 
//                                                          CONVERT
//INSERT() Insert a substring at the specified position up to the specified number of characters 
//                                                          STUFF
//INSTR() Return the index of the first occurrence of substring 
//                                                          CHARINDEX
//LCASE() Synonym for LOWER()                               LOWER
//LEFT() Return the leftmost number of characters as specified 
//                                                          LEFT
//LENGTH() Return the length of a string in bytes           LEN
//LIKE Simple pattern matching                              LIKE (only in select)
//LOAD_FILE() Load the named file                           no equivalent
//LOCATE() Return the position of the first occurrence of substring 
//                                                          CHARINDEX
//LOWER() Return the argument in lowercase                  LOWER
//LPAD() Return the string argument, left-padded with the specified string 
//
//LTRIM() Remove leading spaces                             LTRIM
//MAKE_SET() Return a set of comma-separated strings that have the corresponding bit in bits set 
//MATCH Perform full-text search 
//MID() Return a substring starting from the specified position 
//NOT LIKE Negation of simple pattern matching 
//NOT REGEXP Negation of REGEXP 
//OCTET_LENGTH() A synonym for LENGTH() 
//ORD() Return character code for leftmost character of the argument 
//POSITION() A synonym for LOCATE() 
//QUOTE() Escape the argument for use in an SQL statement 
//REGEXP Pattern matching using regular expressions 
//REPEAT() Repeat a string the specified number of times 
//REPLACE() Replace occurrences of a specified string 
//REVERSE() Reverse the characters in a string 
//RIGHT() Return the specified rightmost number of characters 
//RLIKE Synonym for REGEXP 
//RPAD() Append string the specified number of times 
//RTRIM() Remove trailing spaces 
//SOUNDEX() Return a soundex string 
//SOUNDS LIKE(v4.1.0) Compare sounds 
//SPACE() Return a string of the specified number of spaces 
//STRCMP() Compare two strings 
//SUBSTR() Return the substring as specified 
//SUBSTRING_INDEX() Return a substring from a string before the specified number of occurrences of the delimiter 
//SUBSTRING() Return the substring as specified 
//TRIM() Remove leading and trailing spaces 
//UCASE() Synonym for UPPER() 
//UNHEX()(v4.1.2) Convert each pair of hexadecimal digits to a character 
//UPPER() 


                    // numeric functions

//ABS() Return the absolute value 
//ACOS() Return the arc cosine 
//ASIN() Return the arc sine 
//ATAN2(), ATAN() Return the arc tangent of the two arguments 
//ATAN() Return the arc tangent 
//CEIL() Return the smallest integer value not less than the argument 
//CEILING() Return the smallest integer value not less than the argument 
//CONV() Convert numbers between different number bases 
//COS() Return the cosine 
//COT() Return the cotangent 
//CRC32()(v4.1.0) Compute a cyclic redundancy check value 
//DEGREES() Convert radians to degrees 
//DIV(v4.1.0) Integer division 
// / Division operator 
//EXP() Raise to the power of 
//FLOOR() Return the largest integer value not greater than the argument 
//LN() Return the natural logarithm of the argument 
//LOG10() Return the base-10 logarithm of the argument 
//LOG2() Return the base-2 logarithm of the argument 
//LOG() Return the natural logarithm of the first argument  
//- Minus operator 
//MOD() Return the remainder 
//% Modulo operator 
//OCT() Return an octal representation of a decimal number 
//PI() Return the value of pi 
//+ Addition operator 
//POW() Return the argument raised to the specified power 
//POWER() Return the argument raised to the specified power 
//RADIANS() Return argument converted to radians 
//RAND() Return a random floating-point value 
//ROUND() Round the argument 
//SIGN() Return the sign of the argument 
//SIN() Return the sine of the argument 
//SQRT() Return the square root of the argument 
//TAN() Return the tangent of the argument 
//* Times operator 
//TRUNCATE() Truncate to specified number of decimal places 
//- Change the sign of the argument 

                    // date time functions

//ADDDATE()(v4.1.1) Add time values (intervals) to a date value 
//ADDTIME()(v4.1.1) Add time 
//CONVERT_TZ()(v4.1.3) Convert from one timezone to another 
//CURDATE() Return the current date 
//CURRENT_DATE(), CURRENT_DATE Synonyms for CURDATE() 
//CURRENT_TIME(), CURRENT_TIME Synonyms for CURTIME() 
//CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP Synonyms for NOW() 
//CURTIME() Return the current time 
//DATE_ADD() Add time values (intervals) to a date value 
//DATE_FORMAT() Format date as specified 
//DATE_SUB() Subtract a time value (interval) from a date 
//DATE()(v4.1.1) Extract the date part of a date or datetime expression 
//DATEDIFF()(v4.1.1) Subtract two dates 
//DAY()(v4.1.1) Synonym for DAYOFMONTH() 
//DAYNAME()(v4.1.21) Return the name of the weekday 
//DAYOFMONTH() Return the day of the month (0-31) 
//DAYOFWEEK() Return the weekday index of the argument 
//DAYOFYEAR() Return the day of the year (1-366) 
//EXTRACT() Extract part of a date 
//FROM_DAYS() Convert a day number to a date 
//FROM_UNIXTIME() Format UNIX timestamp as a date 
//GET_FORMAT()(v4.1.1) Return a date format string 
//HOUR() Extract the hour 
//LAST_DAY(v4.1.1) Return the last day of the month for the argument 
//LOCALTIME(), LOCALTIME Synonym for NOW() 
//LOCALTIMESTAMP, LOCALTIMESTAMP()(v4.0.6) Synonym for NOW() 
//MAKEDATE()(v4.1.1) Create a date from the year and day of year 
//MAKETIME(v4.1.1) MAKETIME() 
//MICROSECOND()(v4.1.1) Return the microseconds from argument 
//MINUTE() Return the minute from the argument 
//MONTH() Return the month from the date passed 
//MONTHNAME()(v4.1.21) Return the name of the month 
//NOW() Return the current date and time 
//PERIOD_ADD() Add a period to a year-month 
//PERIOD_DIFF() Return the number of months between periods 
//QUARTER() Return the quarter from a date argument 
//SEC_TO_TIME() Converts seconds to 'HH:MM:SS' format 
//SECOND() Return the second (0-59) 
//STR_TO_DATE()(v4.1.1) Convert a string to a date 
//SUBDATE() A synonym for DATE_SUB() when invoked with three arguments 
//SUBTIME()(v4.1.1) Subtract times 
//SYSDATE() Return the time at which the function executes 
//TIME_FORMAT() Format as time 
//TIME_TO_SEC() Return the argument converted to seconds 
//TIME()(v4.1.1) Extract the time portion of the expression passed 
//TIMEDIFF()(v4.1.1) Subtract time 
//TIMESTAMP()(v4.1.1) With a single argument, this function returns the date or datetime expression; with two arguments, the sum of the arguments 
//TIMESTAMPADD()(v5.0.0) Add an interval to a datetime expression 
//TIMESTAMPDIFF()(v5.0.0) Subtract an interval from a datetime expression 
//TO_DAYS() Return the date argument converted to days 
//TO_SECONDS()(v5.0.0) Return the date or datetime argument converted to seconds since Year 0 
//UNIX_TIMESTAMP() Return a UNIX timestamp 
//UTC_DATE()(v4.1.1) Return the current UTC date 
//UTC_TIME()(v4.1.1) Return the current UTC time 
//UTC_TIMESTAMP()(v4.1.1) Return the current UTC date and time 
//WEEK() Return the week number 
//WEEKDAY() Return the weekday index 
//WEEKOFYEAR()(v4.1.1) Return the calendar week of the date (0-53) 
//YEAR() Return the year 
//YEARWEEK() Return the year and week 

//MATCH (col1,col2,...) AGAINST (expr [search_modifier]) 
//BINARY 
//CAST()
//Convert()
//ExtractValue()(v5.1.5) Extracts a value from an XML string using XPath notation 
//UpdateXML()(v5.1.5) Return replaced XML fragment 

/*
 * AES_DECRYPT() Decrypt using AES 
AES_ENCRYPT() Encrypt using AES 
BENCHMARK() Repeatedly execute an expression 
BIT_COUNT() Return the number of bits that are set 
& Bitwise AND 
~ Invert bits 
| Bitwise OR 
^ Bitwise XOR 
CHARSET()(v4.1.0) Return the character set of the argument 
COERCIBILITY()(v4.1.1) Return the collation coercibility value of the string argument 
COLLATION()(v4.1.0) Return the collation of the string argument 
COMPRESS()(v4.1.1) Return result as a binary string 
CONNECTION_ID() Return the connection ID (thread ID) for the connection 
CURRENT_USER(), CURRENT_USER The authenticated user name and host name 
DATABASE() Return the default (current) database name 
DECODE() Decodes a string encrypted using ENCODE() 
DEFAULT() Return the default value for a table column 
DES_DECRYPT() Decrypt a string 
DES_ENCRYPT() Encrypt a string 
ENCODE() Encode a string 
ENCRYPT() Encrypt a string 
FOUND_ROWS() For a SELECT with a LIMIT clause, the number of rows that would be returned were there no LIMIT clause 
GET_LOCK() Get a named lock 
INET_ATON() Return the numeric value of an IP address 
INET_NTOA() Return the IP address from a numeric value 
IS_FREE_LOCK() Checks whether the named lock is free 
IS_USED_LOCK()(v4.1.0) Checks whether the named lock is in use. Return connection identifier if true. 
LAST_INSERT_ID() Value of the AUTOINCREMENT column for the last INSERT 
<< Left shift 
MASTER_POS_WAIT() Block until the slave has read and applied all updates up to the specified position 
MD5() Calculate MD5 checksum 
NAME_CONST()(v5.0.12) Causes the column to have the given name 
OLD_PASSWORD()(v4.1) Return the value of the old (pre-4.1) implementation of PASSWORD 
PASSWORD() Calculate and return a password string 
RAND() Return a random floating-point value 
RELEASE_LOCK() Releases the named lock 
>> Right shift 
ROW_COUNT()(v5.0.1) The number of rows updated 
SCHEMA()(v5.0.2) A synonym for DATABASE() 
SESSION_USER() Synonym for USER() 
SHA1(), SHA() Calculate an SHA-1 160-bit checksum 
SHA2()(v6.0.5) Calculate an SHA-2 checksum 
SLEEP()(v5.0.12) Sleep for a number of seconds 
SYSTEM_USER() Synonym for USER() 
UNCOMPRESS()(v4.1.1) Uncompress a string compressed 
UNCOMPRESSED_LENGTH()(v4.1.1) Return the length of a string before compression 
USER() The user name and host name provided by the client 
UUID_SHORT()(v5.1.20) Return an integer-valued universal identifier 
UUID()(v4.1.2) Return a Universal Unique Identifier (UUID) 
VALUES()(v4.1.1) Defines the values to be used during an INSERT 
VERSION() Returns a string that indicates the MySQL server version 
*/

                    /*
* AVG() Return the average value of the argument 
BIT_AND() Return bitwise and 
BIT_OR() Return bitwise or 
BIT_XOR()(v4.1.1) Return bitwise xor 
COUNT(DISTINCT) Return the count of a number of different values 
COUNT() Return a count of the number of rows returned 
GROUP_CONCAT()(v4.1) Return a concatenated string 
MAX() Return the maximum value 
MIN() Return the minimum value 
STD() Return the population standard deviation 
STDDEV_POP()(v5.0.3) Return the population standard deviation 
STDDEV_SAMP()(v5.0.3) Return the sample standard deviation 
STDDEV() Return the population standard deviation 
SUM() Return the sum 
VAR_POP()(v5.0.3) Return the population standard variance 
VAR_SAMP()(v5.0.3) Return the sample variance 
VARIANCE()(v4.1) Return the population standard variance 
*/


           }

           throw new NotImplementedException ("Can't convert " + functionName + " function");
        }



        public void insureOutputLineFinished()
        {
            if (outputLine.Length > 0)
            {
                Body.Add(outputLine.ToString());
                outputLine = new StringBuilder();
            }
        }
        public  void completeLine(String comment, int pos)
        {
            VariableDeclaration LastVar;
            switch (BuildState)
            {
                case whereWeAre.BuildingParmList:
                    if (Parameters.Count > 0)
                    {
                        LastVar = Declarations[Declarations.Count - 1];
                        if (pos > -1)
                        {
                            LastVar.setComment(comment);
                        }
                    }
                    break;
                case whereWeAre.BuildingVarList:
                    LastVar = Declarations[Declarations.Count - 1];
                    if (pos > -1)
                    {
                        LastVar.setComment(comment);
                    }
                    break;
                case whereWeAre.notActive:
                case whereWeAre.BuildingBody:
                default:
                    if (pos > -1)
                    {
                        while (outputLine.Length < pos)
                            outputLine.Append(' ');
                        outputLine.Append("--" + comment);
                    }
                    Body.Add(outputLine.ToString());
                    outputLine = new StringBuilder();
                    break;
            }
        }

    }
}
