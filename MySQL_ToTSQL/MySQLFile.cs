using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

//
// Copyright 2012-2013 Prairie Trail Software, Inc. 
// All rights reserved
//
namespace MySQL_ToTSQL
{
    class MySQLFile
    {
        static String InputPath;
        static String OutputPath;
        static String resultingIncludeFileName;

        StreamReader MySQLInputFile;
        static IOutputFile WritingToOutput;
        static MSSQLFile MSSQLOutputFile;
        static SQLProcedure MSSQLProc;
        static SQLTable MSSQLTable;
        static VariableDeclaration Vars;  // singleton to help with mapping variables
        public static string Delimiter;

        static Signature thisProcedureSignature;
        static int ifLevels;

        static List<parseState> returnStack;
        static List<String> IncludeFiles;
        static List<Signature> ModuleSignatures;
        
        public static parseState currentParseState;



        // Ok, here is the base class for the state transitions
        public abstract class parseState
        {
            public abstract void handleToken(String token);
            protected virtual void ChangeState(parseState s)
            {
                currentParseState = s; 
            }
        }


        public static void pushReturn (parseState s)
        {
            returnStack.Add(s);
        }

        public static parseState popReturn()
        {
            parseState s;

            if (returnStack.Count > 0)
            {
                s = returnStack[returnStack.Count-1];
                returnStack.RemoveAt(returnStack.Count-1);
            }
            else
                s = new parseBody();
            return s;
        }



        // what to do when nothing has been started yet
        public class IdleState : parseState
        {

            public override void handleToken(String token)
            {
                switch (token.ToUpper())
                {
                    case "CREATE":
                        ChangeState(new CreateStatement());
                        break;
                    case "DELIMITER":
                        ChangeState(new DelimiterStatement());
                        break;
                    case "DROP":
                        ChangeState(new DropStatement());
                        break;
                    case "INSERT":
                        MSSQLOutputFile.Append("INSERT");
                        ChangeState(new InsertStatement());
                        break;
                    case "SET":
                        ChangeState(new SetStatement());
                        break;
                    case "USE":
                        ChangeState(new parseUse());
                        break;
                    case "\\.":
                        pushReturn(this);
                        ChangeState (new handleIncludeFile());
                        break;
                    default:
                        break;
                }
            }
        }



        public class handleIncludeFile : parseState
        {
            public override void handleToken(string token)
            {
                MSSQLOutputFile.Append("r: " + token);
                IncludeFiles.Add(token);
                ChangeState(popReturn());
            }

            public void EnsureDirectoryExists(String Dir)
            {
                // make sure the parent directory exists

                if (!Directory.Exists(Path.GetDirectoryName(Dir)))
                {
                    EnsureDirectoryExists(Path.GetDirectoryName(Dir));
                }

                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);
            }
        }

        




        // set the delimiter to the new one
        public class DelimiterStatement : parseState
        {
            String oldDelimiter;

            public DelimiterStatement()
            {
                oldDelimiter = Delimiter;
            }
            public override void  handleToken(string token)
            {
                if (token == oldDelimiter)
                    ChangeState(new IdleState());
                else
                {
                    // there might not be an old delimiter on the line
                    Delimiter = token;
                    ChangeState(new IdleState());
                }
            }
        }


        // not started yet SET statements
        // there can be several SET statements before any create or drop statements
        // In this inplementation, we ignore them.
        public class SetStatement : parseState
        {
            public override void handleToken(string token)
            {
                if (token == Delimiter)
                    ChangeState(new IdleState());
            }
        }




        // CREATE statement
        public class CreateStatement : parseState
        {
            public override void handleToken(string token)
            {
                if (token == Delimiter)
                    ChangeState(new IdleState());
                else
                {
                    switch (token.ToUpper())
                    {
                        case "DATABASE":
                            break;
                        case "TABLE":
                            ChangeState(new CreateTableStatement());
                            break;
                        case "PROCEDURE":
                            ChangeState(new CreateProcedureStatement());
                            break;
                        case "INDEX":
                            break;
                        case "VIEW":
                            break;
                        case "TRIGGER":
                            break;
                        case "USER":
                            break;
                        case "FUNCTION":
                            break;
                    }
                }
            }
        }



        //********************************************************
        // Parsing the Create Table 


        public static Boolean ifIndex(String token)
        {
            switch (token.ToUpper())
            {
                case "UNIQUE":
                case "INDEX":
                case "FULLTEXT":
                case "SPATIAL":
                    return true;
            }
            return false;
        }

        public class CreateTableStatement : parseState
        {
            String TableName;
            public override void handleToken(string token)
            {
                if (token == Delimiter)
                    ChangeState(new IdleState());
                else
                {
                    switch (token)
                    {
                        case "(":
                            MSSQLTable.writeTableStart();
                            pushReturn(this);
                            ChangeState(new CreateTableColumn());
                            break;
                        case ";":
                            ChangeState(new IdleState());
                            break;
                        case ")":
                            MSSQLTable.writeTableEnd();
                            ChangeState(new parsingEngine());
                            break;
                        default:
                            TableName = token;
                            MSSQLTable = new SQLTable(MSSQLOutputFile);
                            MSSQLTable.writeTableHeader(TableName);
                            break;
                    }
                }
            }
        }

        public class CreateTableColumn : parseState
        {
            static String CurrentColumnName;
            static String SQLType;
            static String ParameterSize;

            public CreateTableColumn()
            {
                SQLType = "";
                ParameterSize = "";
            }
            public override void handleToken(string token)
            {
                if (token.ToUpper() == "PRIMARY")
                {
                    MSSQLTable.insureOutputLineFinished();
                    pushReturn(this);
                    ChangeState(new parsePrimaryKey());
                }else
                if (token.ToUpper() == "FOREIGN")
                {
                    MSSQLTable.startForeignKey();
                    pushReturn(this);
                    ChangeState(new parseForeignKey());
                }else
                if (ifIndex(token))
                {
                    MSSQLTable.StartIndex(token);
                    pushReturn(this);
                    ChangeState(new parseTableIndex());
                }
                else
                    if (token == ",")
                    { }
                    else
                    {
                        if (token == ")")
                        {
                            ChangeState(popReturn());
                            tokenPushBack();
                        }
                        else
                        {
                            MSSQLTable.writeTableColumn(token);
                            CurrentColumnName = token;
                            SQLType = "";
                            ParameterSize = "";
                            pushReturn(this);
                            ChangeState(new parsingColumnSQLType());
                        }
                    }
            }

            
            
            public class parsingColumnSQLType : parseState
            {
                String Prefix;

                public parsingColumnSQLType()
                {
                    Prefix = "";
                }
                public override void handleToken(string token)
                {
                    if (token.ToUpper() == "NATURAL")
                        Prefix = "N";
                    else
                        if (token.ToUpper() == "DEFAULT")
                        {
                            MSSQLTable.writeColumnAttribute("DEFAULT");
                            pushReturn(this);
                            ChangeState(new parsingDefault());
                        }
                        else
                        if (ifColumnQualifier(token))
                        {
                            SQLType = SQLType + " " + Vars.mapQualifier(token);
                            MSSQLTable.writeColumnAttribute (Vars.mapQualifier(token));
                        }
                        else
                        {
                            switch (token)
                            {
                                case "(":
                                    pushReturn(this);
                                    ChangeState(new parsingSQLColumnSize());
                                    break;
                                case ")":
                                    tokenPushBack();
                                    ChangeState(popReturn());
                                    break;
                                // if a comma, that means that there is not length field 
                                case ",":
                                    MSSQLTable.writeColumnTerminator(token);
                                    ChangeState(popReturn());
                                    break;

                                default:
                                    if (SQLType.Length > 0)
                                    {
                                        SQLType = SQLType + " " + Vars.mapSqlDataType(token);
                                        MSSQLTable.writeColumnAttribute(Vars.mapSqlDataType(token));
                                    }
                                    else
                                    {
                                        SQLType = Prefix + Vars.mapSqlDataType(token);
                                        MSSQLTable.writeColumnAttribute(Prefix + Vars.mapSqlDataType(token));
                                    }
                                    break;
                            }
                        }
                }
                public Boolean ifColumnQualifier(String token)
                {
                    switch (token.ToUpper())
                    {
                        case "UNSIGNED":
                        case "NOT":
                        case "NULL":
                        case "AUTO_INCREMENT":
                        case "UNIQUE":
                        case "DEFAULT":
                            return true;
                    }
                    return false;
                }
            }

            public class parsingDefault : parseState
            {
                public override void handleToken(string token)
                {
                    MSSQLTable.writeColumnAttribute(token);
                    ChangeState(popReturn());
                }
            }

            public class parsingSQLColumnSize : parseState
            {
                public override void handleToken(string token)
                {
                    if (Char.IsDigit(token[0]))
                    {
                        ParameterSize = ParameterSize + token;
                    }
                    else
                    {
                        if (token == ")")
                        {
                            SQLType = SQLType + "(" + ParameterSize + ")";
                            MSSQLTable.writeColumnAttribute("(" + ParameterSize + ")");
                            ChangeState(popReturn());
                        }
                        else
                            if (token == ",")
                            {
                                if ((SQLType.ToUpper().Contains("DECIMAL")) ||
                                    (SQLType.ToUpper().Contains("NUMERIC")))
                                    ParameterSize = ParameterSize + token;
                                else
                                    MessageBox.Show("Unknown entry in parameter list " + token);
                            }
                            else
                                MessageBox.Show("Unknown entry in column definition " + token);
                    }
                }
            }

            public class parsePrimaryKey : parseState
            {
                String PrimaryKey; 

                public override void handleToken(string token)
                {
                    switch (token.ToUpper())
                    {
                        case "(":
                            break;
                        case ")":
                            token = tokenLookAhead();
                            if (token == ",")
                                MSSQLTable.SetPrimaryKey(PrimaryKey, token);
                            else
                                MSSQLTable.SetPrimaryKey(PrimaryKey, "");
                            ChangeState(popReturn());
                            break;
                        case "KEY":
                            break;
                        default:
                            PrimaryKey = token;
                            break;
                    }
                }
            }

            // this is for an index defined in the middle of the table definition
            public class parseTableIndex : parseState
            {
                public override void handleToken(string token)
                {
                    switch (token)
                    {
                        case ",":
                            MSSQLTable.finishIndex();
                            ChangeState(popReturn());
                            break;
                        case "(":
                            pushReturn(this);
                            ChangeState(new parseIndexColumns());
                            break;
                        case ")":
                            MSSQLTable.finishIndex();
                            tokenPushBack();
                            ChangeState(popReturn());
                            break;
                        default:
                            if (ifIndex(token))
                                MSSQLTable.addIndexAttribute(token);
                            else
                                MSSQLTable.SetIndexName(token);
                            break;
                    }
                }
            }
            public class parseIndexColumns : parseState
            {
                public override void handleToken(string token)
                {
                    switch (token)
                    {
                        case ",":
                            break;
                        case ")":
                            ChangeState(popReturn());
                            break;
                        default:
                            MSSQLTable.addIndexColumn(token);
                            break;
                    }
                }
            }
            public class parseForeignKey : parseState
            {
                Boolean openParen;
                public parseForeignKey()
                {
                    openParen = false;
                }
                public override void handleToken(string token)
                {
                    switch (token)
                    {
                        case ",":
                            MSSQLTable.finishForiegnKey(",");
                            ChangeState(popReturn());
                            break;
                        case "(":
                            MSSQLTable.continueForeignKey(token);
                            openParen = true;
                            break;
                        case ")":
                            if (openParen)
                            {
                                MSSQLTable.continueForeignKey(token);
                                openParen = false;
                            }
                            else
                            {
                                MSSQLTable.finishForiegnKey("");
                                tokenPushBack();
                                ChangeState(popReturn());
                            }
                            break;
                        default:
                            MSSQLTable.continueForeignKey(token);
                            break;
                    }
                }
            }
        }

        public class parsingEngine : parseState
        {
            // we don't need to do anything with the engine statements
            public override void handleToken(string token)
            {
                if (token == ";")
                    ChangeState(new IdleState());
            }
        }












        //***************************************************
        // Parsing Procedure

        public class CreateProcedureStatement : parseState
        {
            String ProcedureName;

            public CreateProcedureStatement()
            {
                ProcedureName = "";
            }
            // at this point, we have handled the "create" and "procedure"
            // so the token is the procedure name and any start of parameters
            public override void handleToken(string token)
            {
                if (ProcedureName.Length == 0)
                {
                    ProcedureName = token;

                    MSSQLProc = new SQLProcedure(MSSQLOutputFile);
                    MSSQLProc.writeProcedureHeader(ProcedureName);
                    WritingToOutput = MSSQLProc;

                    thisProcedureSignature = new Signature();
                    thisProcedureSignature.ModuleName = ProcedureName;
                    
                }
                else
                {
                    switch (token.ToUpper())
                    {
                        case "(":

                            MSSQLProc.writeParameterStart();

                            // parse and write the parameters

                            ChangeState(new parsingProcedureParameters());
                            break;

                        case "AS":
                            MSSQLProc.writeProcedureStartIndicator();
                            ifLevels = 0;
                            ChangeState(new parseBody());
                            break;
                        default:
                            MessageBox.Show("Unknown result in create procedure header " + token);
                            break;
                    }
                }
            }
        }


        public class parsingProcedureParameters : parseState
        {
            static VariableDeclaration nVar;
            static Argument currentArgument;
            static String CurrentParameter;
            static String SQLType;
            static int ParameterSize;
            static int Precision;

            public class parsingParameterSQLType : parseState
            {
                String Prefix;

                public parsingParameterSQLType()
                {
                    Prefix = "";
                    ParameterSize = 0;
                    Precision = -1;
                }
                public override void handleToken(string token)
                {
                    if (token == ")")
                    {
                        tokenPushBack();
                        ChangeState(popReturn());
                        return;
                    }

                    if (token == "NATURAL")
                        Prefix = "N";
                    else
                    {
                        SQLType = Prefix + Vars.mapSqlDataType(token);
                        switch (tokenLookAhead())
                        {
                            case "(":
                                ChangeState(new parsingSQLParameterSize());
                                break;
                            case ",":
                                ChangeState(new parsingSQLParameterSize());
                                break;
                            case ")":
                            case "":
                                FinishParameter();
                                break;
                        }
                    }
                }
            }

            public class parsingSQLParameterSize : parseState
            {

                public override void handleToken(string token)
                {
                    switch (token)
                    {
                        // we ignore the starting ( and wait
                        // for the digits
                        case "(":
                            pushReturn(this);
                            ChangeState(new parsingSQLParameterDigits());
                            break;
                        // and ignore the ending )
                        // but look for an end to the list of parameters
                        // no opening paren => end of parameters
                        case ")":
                            tokenPushBack();
                            ChangeState (popReturn());
                            break;
                        // if a comma, that means that there is not length field 
                        // and are more parameters in this list
                        // tell the converted to end this parameter and start another
                        case ",":
                            FinishParameter();
                            MSSQLProc.addParameterTerminator();
                            ChangeState(popReturn());
                            break;

                        default:
                            MessageBox.Show("Unknown entry in parameter list " + token);
                            break;

                    }
                }
            }
            public class parsingSQLParameterDigits : parseState
            {
                public override void handleToken(string token)
                {
                    if (Char.IsDigit(token[0]))
                    {
                        ParameterSize = Int32.Parse(token);
                    }
                    else
                    {
                        if (token == ")")
                        {
                            SeeIfToFinish();
                            ChangeState(popReturn());
                        }
                        else
                            if (token == ",")
                            {
                                if ((SQLType.ToUpper().Contains("DECIMAL")) ||
                                    (SQLType.ToUpper().Contains("NUMERIC")))
                                {
                                    ChangeState (new parsePrecision());
                                }
                                else
                                    MessageBox.Show("Unknown entry in variable definition " + token);
                            }
                            else
                                MessageBox.Show("Unknown entry in variable definition " + token);
                    }
                }
            }

            public class parsePrecision : parseState
            {
                public override void handleToken(string token)
                {
                    if (Char.IsDigit(token[0]))
                    {
                        Precision = Int32.Parse(token);
                    }
                    else
                    {
                        switch (token)
                        {
                            case ")":
                                SeeIfToFinish();
                                ChangeState(popReturn());
                                break;
                            default:
                                MessageBox.Show("Unknown entry in parameter list " + token);
                                break;
                        }
                    }
                }
            }

            public static void SeeIfToFinish()
            {
                // if nothing else is on this source line, 
                // finish what we have already.

                if (tokenLookAhead().Length == 0)
                    FinishParameter();
            }

            public static void FinishParameter()
            {
                Boolean INPUT;
                Boolean OUTPUT;
                INPUT = false;
                OUTPUT = true;

                
                if (currentArgument.WhichDirection == Direction.INPUT)
                {
                    nVar = new VariableDeclaration(VariableTypes.INPUTVARIABLE, CurrentParameter);
                    nVar.SetSQLType(SQLType);
                    nVar.SetLength(ParameterSize);
                    nVar.SetPrecision(Precision);
                    MSSQLProc.addParameter(nVar, INPUT);
                }
                else
                {
                    nVar = new VariableDeclaration(VariableTypes.OUTPUTVARIABLE, CurrentParameter);
                    nVar.SetSQLType(SQLType);
                    nVar.SetLength(ParameterSize);
                    nVar.SetPrecision(Precision);
                    MSSQLProc.addParameter(nVar, OUTPUT);
                }
                currentArgument.ArgumentType = nVar.GetSQLType();
                thisProcedureSignature.ModuleArguments.Add(currentArgument);
            }




            public parsingProcedureParameters()
            {
                CurrentParameter = "";
                currentArgument = new Argument();
                ParameterSize = 0;
                MSSQLProc.setIndent(2);
            }

            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "IN":
                        currentArgument.WhichDirection = Direction.INPUT;
                        break;
                    case "OUT":
                        currentArgument.WhichDirection = Direction.OUTPUT;
                        break;
                    case ")":
                        MSSQLProc.writeParameterEnd();
                        MSSQLProc.writeProcedureStartIndicator();
                        WritingToOutput.completeLine(CommentPortion, CommentPosition);
                        WritingToOutput = MSSQLProc;
                        ChangeState(new parseBody());
                        break;
                    case "AS":
                        MSSQLProc.writeParameterEnd();
                        MSSQLProc.writeProcedureStartIndicator();
                        WritingToOutput.completeLine(CommentPortion, CommentPosition);
                        ChangeState(new parseBody());
                        break;

                            // assume that the token is the parameter name
                    default:
                        CurrentParameter = token;
                        currentArgument.ArgumentName = token;
                        pushReturn(this);
                        ChangeState(new parsingParameterSQLType());
                        break;
                }
            }
        }




        public class parseUse : parseState
        {
            public override void handleToken(string token)
            {
                MSSQLOutputFile.Append("USE [" + token + "]");
                ChangeState(new IdleState());
            }
        }



        public class parseBody : parseState
        {
            public override void handleToken(string token)
            {
                if (token == Delimiter)
                {
                    ChangeState(new IdleState());
                    return;
                }
                switch (token.ToUpper())
                {
                    case "BEGIN":
                        if (ifLevels > 0)
                        {
                            ifLevels++;
                            MSSQLProc.Append("BEGIN");
                            MSSQLProc.incrementIndent();
                        }
                        else
                            MSSQLProc.addInitialBegin();
                        break;

                    case "CALL":
                        ChangeState (new parseCall());
                        break;
                    case "COMMIT":
                        MSSQLProc.Append(token);
                        break;
                    case "DECLARE":
                        ChangeState(new parseDeclareStatement());
                        break;
                    case "IF":
                        ChangeState(new parseIfStatement());
                        ifLevels++;
                        MSSQLProc.Append("IF ");
                        break;
                    case "ELSE":
                        MSSQLProc.ELSE();
                        break;
                    case "END":
                        MSSQLProc.decrementIndent();
                        ChangeState(new parseEndStatement());
                        break;
                    case "SET":
                        ChangeState(new parseSetStatement());
                        break;
                    case "SELECT":
                        MSSQLProc.StartLine();
                        MSSQLProc.StartSelect();
                        pushReturn(this);
                        ChangeState(new parseSelect());
                        break;
                    case "UPDATE":
                        MSSQLProc.StartUpdate();
                        ChangeState(new parseUpdate());
                        break;
                    case "INSERT":
                        MSSQLProc.StartInsert();
                        ChangeState(new parseInsert());
                        break;
                    case "DELETE":
                        MSSQLProc.StartDelete();
                        ChangeState(new parseDelete());
                        break;
                    case "":
                        break;
                    case "WHILE":
                        ifLevels++;
                        MSSQLProc.Append("WHILE ");
                        pushReturn(this);
                        ChangeState(new parseWhile());
                        break;
                    case "START":
                        ChangeState(new parseStart());
                        break;
                    case ";":
                        MSSQLProc.Append(token);
                        break;
                    default:
                        throw new NotImplementedException("Unhandled statement " + token);
                        //                        results.Add(InputLine);
                }
            }
        }



        // we have a DECLARE statement
        public class parseDeclareStatement : parseState
        {
            static String VariableName;
            static String SQLType;
            static int Length;
            static int Precision;
            static VariableDeclaration nVar;
            

            public override void handleToken(string token)
            {
                VariableName = token;
                Length = 0;
                Precision = 0;
                ChangeState(new parseDeclareType());
            }

            public class parseDeclareType : parseState
            {
                // this whole thing about a prefix is a mixup of where 
                // things should be handled. But, I don't see a good 
                // solution otherwise
                String Prefix;

                public parseDeclareType()
                {
                    Prefix = "";
                }
                public override void handleToken(string token)
                {
                    if (token == "NATURAL")
                        Prefix = "N";
                    else
                    {
                        SQLType = Prefix + Vars.mapSqlDataType(token);
                        ChangeState(new parsingSQLVariableSize());
                    }
                }
            }

            public class parsingSQLVariableSize : parseState
            {
                public override void handleToken(string token)
                {
                    if (Char.IsDigit(token[0]))
                    {
                        Length = Int32.Parse(token);
                    }
                    else
                    {
                        switch (token)
                        {
                            // we ignore the starting ( and wait
                            // for the digits
                            case "(":
                                break;
                            case ")":
                                break;
                            case ",":
                                if ((SQLType.ToUpper().Contains("DECIMAL")) ||
                                    (SQLType.ToUpper().Contains("NUMERIC")))
                                    ChangeState(new parsePrecision());
                                else
                                    MessageBox.Show("Unknown entry in parameter list " + token);
                                break;
                            case ";":
                                nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, VariableName);
                                nVar.SetSQLType(SQLType);
                                nVar.SetLength(Length);
                                MSSQLProc.addLocalVariable(nVar);
                                ChangeState(new parseBody());
                                break;

                            default:
                                MessageBox.Show("Unknown entry in parameter list " + token);
                                break;
                        }
                    }
                }
            }
            public class parsePrecision : parseState
            {
                public override void handleToken(string token)
                {
                    if (Char.IsDigit(token[0]))
                    {
                        Precision = Int32.Parse(token);
                    }
                    else
                    {
                        switch (token)
                        {
                            case ")":
                                break;
                            case ";":
                                nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, VariableName);
                                nVar.SetSQLType(SQLType);
                                nVar.SetLength(Length);
                                nVar.SetPrecision(Precision);
                                MSSQLProc.addLocalVariable(nVar);
                                ChangeState(new parseBody());
                                break;

                            default:
                                MessageBox.Show("Unknown entry in parameter list " + token);
                                break;
                        }
                    }
                }
            }
        }


        public class parseSetStatement : parseState
        {
            String localVariable;

            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "=":
                        MSSQLProc.addAssignment(localVariable);
                        pushReturn(this);
                        ChangeState(new parseReferences());
                        break;
                    case ";":
                        MSSQLProc.finishStatement();
                        ChangeState(new parseBody());
                        break;
                        // ignore the leading @ on variables
                    case "@":
                        break;
                    default:
                        if (token[0] == '@')
                            localVariable = token.Substring(1);
                        else
                            localVariable = token;
                        break;
                }
            }
        }


        // this is called from if statements and
        // other places where variables and functions can be mixed
        public class parseReferences : parseState
        {
            public override void handleToken(string token)
            {
                if (Char.IsDigit(token[0]))
                {
                    MSSQLProc.Append(token);
                }
                else
                {
                    switch (token.ToUpper())
                    {
                        case "+":
                        case "-":
                        case "*":
                        case "/":
                        case "%":
                        case "==":
                        case "!=":
                        case ">":
                        case "<":
                        case ">=":
                        case "<=":
                        case "AND":
                        case "OR":
                        case "NULL":
                            MSSQLProc.Append(" " + token + " ");
                            break;
                        case "NOT":
                            if (tokenLookAhead().ToUpper() == "ISNULL")
                            {
                                pushReturn(this);
                                ChangeState(new parseIsNotNull());
                            }
                            else
                                MSSQLProc.Append(" " + token + " ");
                            break;
                        case ",":
                            MSSQLProc.Append(", ");
                            break;
                        case "@":
                            token = getNextToken ();
                            if (MSSQLProc.isAlreadyDefined(token))
                            {
                                MSSQLProc.AppendVariableReference(token);
                            }
                            else
                            {
                                VariableDeclaration nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, token);
                                nVar.SetSQLType("INT");
                                nVar.SetLength(1);
                                MSSQLProc.addLocalVariable(nVar);
                                MSSQLProc.AppendVariableReference(token);
                            }
                            break;
                            // we are handling isnull function call 
                            // as it has to be totally reformatted in MSSQL
                        case "ISNULL":
                            pushReturn(this);
                            ChangeState(new parseIsNull());
                            break;
                        case "CONCAT":
                            pushReturn(this);
                            ChangeState(new parseConcat());
                            break;
                        case "DO":
                        case "THEN":
                            tokenPushBack();
                            ChangeState(popReturn());
                            break;
                        case ")":
                            MSSQLProc.Append(token);
                            ChangeState(popReturn());
                            break;
                        case "(":
                            MSSQLProc.Append(token);
                            pushReturn(this);
                            break;
                        case ";":
                            tokenPushBack();
                            ChangeState(popReturn());
                            break;
                        default:
                            if (MSSQLProc.isAlreadyDefined(token))
                            {
                                MSSQLProc.AppendVariableReference(token);
                            }
                            else
                            if (isMySQLFunction(token))
                            {
                                string sysVar = MSSQLProc.convertToSystemVar(token);
                                if (sysVar.Length > 0)
                                {
                                    MSSQLProc.Append(sysVar);
                                    // need to eat the paren from a function call
                                    if (tokenLookAhead() == "(")
                                        getNextToken();
                                    if (tokenLookAhead() == ")")
                                        getNextToken();
                                }
                                else
                                {

                                    MSSQLProc.Append(MSSQLProc.convertFunction(token));
                                    pushReturn(this);
                                    ChangeState(new parsingFunctionCall());
                                }
                            }
                            else
                            {
                                if (token.ToUpper() == "AS") token = token + " ";
                                MSSQLProc.Append(token);
                            }
                            break;
                    }
                }
            }
        }


        // an isnull call within an if statement
        // needs to be totally rewritten
        public class parseIsNull : parseState
        {
            Boolean closed = false;
            Boolean equaled = false;
            String VariableName = "";

            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "(":   // don't do anything with openning (
                        break;
                    case ")":
                        closed = true;
                        break;
                    case "=":
                        equaled = true;
                        break;
                    case "THEN":
                        MSSQLProc.AppendVariableReference(VariableName);
                        MSSQLProc.Append(" IS NULL");
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    default:
                        if (closed && equaled)
                        {
                            MSSQLProc.AppendVariableReference(VariableName);
                            if (token == "0")
                                MSSQLProc.Append(" IS NOT NULL");
                            else
                                MSSQLProc.Append(" IS NULL");
                            ChangeState(popReturn());
                        }
                        else
                            VariableName = token;
                        break;
                }
            }
        }


        // a NOT isnull call within an if statement
        // needs to be totally rewritten
        public class parseIsNotNull : parseState
        {
            String VariableName = "";

            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "(":   // don't do anything with openning (
                        break;
                    case ")":
                        MSSQLProc.AppendVariableReference(VariableName);
                        MSSQLProc.Append(" IS NOT NULL");
                        ChangeState(popReturn());
                        break;
                    case "THEN":
                        MSSQLProc.AppendVariableReference(VariableName);
                        MSSQLProc.Append(" IS NOT NULL");
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    default:
                        VariableName = token;
                        break;
                }
            }
        }
        // we have to handle the concat function as a special case
        // as TSQL does not have it as a function
        public class parseConcat : parseState
        {
            String concatFunction = "";

            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "(":
                        break;
                    case ")":
                        MSSQLProc.Append(concatFunction);
                        ChangeState(popReturn());
                        break;
                    case ",":       // ignore the commas
                        break;
                    default:
                        if (MSSQLProc.isAlreadyDefined(token))
                            token = MSSQLProc.ConvertVariableReference(token);
                        if (concatFunction.Length > 0)
                            concatFunction = concatFunction + " + " + token;
                        else
                            concatFunction = token;
                        break;
                }
            }
        }


        public class parsingFunctionCall : parseState
        {
            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "(":
                        MSSQLProc.Append(token);
                        ChangeState(new parseReferences());
                        break;
                    case ")":
                        MSSQLProc.Append(token);
                        ChangeState(popReturn());
                        break;
                    case ";":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;

                        // we are treating some constants as function calls,
                        // thus, with a comma after its token, 
                        // we need to treat that as the end of the "call"
                    case ",":       
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    default:
                        MSSQLProc.Append(token);
                        break;
                }
            }
        }



        public class parseIfStatement : parseState
        {
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "THEN":
                        WritingToOutput.completeLine(CommentPortion, CommentPosition);
                        MSSQLProc.IF();
                        MSSQLProc.incrementIndent();
                        ChangeState(new parseBody());
                        break;
                    case "(":
                        MSSQLProc.Append(token);
                        ChangeState(new parseReferences());
                        pushReturn(this);
                        break;
                    case ";":
                        MSSQLProc.Append(token);
                        ChangeState(new parseBody());
                        break;
                    default:
                        tokenPushBack();
                        ChangeState(new parseReferences());
                        pushReturn(this);
                        break;
                }
            }
        }
        // we can have an END for both an if statement and for the procedure
        public class parseEndStatement : parseState
        {
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "IF":
                        break;      // ignore the IF of the end if clause
                    case "WHILE":
                        break;      // ignore the WHILE of the END WHILE clause
                    case ";":
                        if (ifLevels > 0)
                        {
                            MSSQLProc.ENDIF();
                            ifLevels--;
                            ChangeState(new parseBody());
                        }
                        else
                        {
                            // if ending the procedure
                            // write the end 
                            // and return to idle state

                            MSSQLProc.writeEndStoredProcedure();
                            ChangeState(new IdleState());
                            break;
                        }
                        break;
                
                }
            }
        }



        // called when parsing an insert inside a stored procedure
        public class parseInsert : parseState
        {
            public parseInsert()
            {
                MSSQLProc.incrementIndent();
            }
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case ";":
                        MSSQLProc.Append(token);
                        MSSQLProc.decrementIndent();
                        ChangeState(new parseBody());
                        break;
                    case "VALUES":
                        MSSQLProc.Append(token.ToUpper() + " ");
                        pushReturn(this);
                        ChangeState(new parseInsertValues());
                        break;
                    default:
                        MSSQLProc.Append(" " + token);
                        break;
                }
            }
        }
        public class parseInsertValues : parseState
        {
            static String functionCall;
            public parseInsertValues()
            {
                functionCall = "";
            }

            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "SELECT":
                        MessageBox.Show("Need manual handling of Select in insert statement");
                        MSSQLProc.Append(token);
                        break;
                    case ",":
                        MSSQLProc.Append(", ");
                        break;
                    case "(":
                        MSSQLProc.Append(token);
                        break;
                    case ")":
                        MSSQLProc.Append(token);
                        ChangeState(popReturn());
                        break;
                    default:
                        if ((token[0] == '\'') ||
                            (Char.IsDigit(token[0])) ||
                            (token[0] == '@') ||
                            (token.ToUpper() == "NULL"))
                            MSSQLProc.Append(token);
                        else
                            if (MSSQLProc.isAlreadyDefined(token))
                                MSSQLProc.AppendVariableReference(token);
                            else
                                if (isMySQLFunction(token))
                                {
                                    string sysVar = MSSQLProc.convertToSystemVar(token);
                                    if (sysVar.Length > 0)
                                    {
                                        MSSQLProc.Append(sysVar);
                                        // need to eat the paren from a function call
                                        if (tokenLookAhead() == "(")
                                            getNextToken();
                                        if (tokenLookAhead() == ")")
                                            getNextToken();
                                    }
                                    else
                                    {
                                        functionCall = MSSQLProc.convertFunction(token);
                                        pushReturn(this);
                                        ChangeState(new parsingInsertFunctionCall());
                                    }
                                }
                                else
                                    MSSQLProc.Append(token);
                        break;
                }
            }
            public class parsingInsertFunctionCall : parseState
            {
                public override void handleToken(string token)
                {
                    functionCall = functionCall + token;
                    if (token == ")")
                        ChangeState(popReturn());
                }
            }
        }

        // handling the parsing of a delete statement within the body
        public class parseDelete : parseState
        {
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "FROM":
                        MSSQLProc.Append(" FROM ");
                        break;
                    case "WHERE":
                        MSSQLProc.decrementIndent();
                        MSSQLProc.startWhereClause();
                        pushReturn(this);
                        ChangeState(new parseWhereClause());
                        break;
                    case "ORDER":
                        MessageBox.Show("Can not convert ORDER BY in DELETE statement in " + resultingIncludeFileName);
                        pushReturn(this);
                        ChangeState(new parseOrderClause());
                        break;
                    // possible bug - not checking for variables or function calls
                    case "LIMIT":  // this could be bug if spread over 2 lines
                        String lim = getNextToken();
                        if (lim == "(")
                        {
                            lim = getNextToken();
                            MSSQLProc.Top(lim, "DELETE");
                            lim = getNextToken();
                        }
                        else
                            MSSQLProc.Top(lim, "DELETE");
                        break;

                    case ";":
                        MSSQLProc.finishStatement();
                        MSSQLProc.decrementIndent();
                        ChangeState(new parseBody());
                        break;
                    default:
                        MSSQLProc.setUpdateTable(token);
                        break;
                }
            }
        }



        public class parseUpdate : parseState
        {
            public parseUpdate()
            {
                MSSQLProc.incrementIndent();
            }
            // we have had an UPDATE statement
            // get the table to update
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "SET":
                        MSSQLProc.incrementIndent();
                        MSSQLProc.startSetClause();
                        pushReturn(this);
                        ChangeState(new parseSetClause());
                        break;
                    case "WHERE":
                        MSSQLProc.decrementIndent();
                        MSSQLProc.startWhereClause();
                        pushReturn(this);
                        ChangeState(new parseWhereClause());
                        break;
                    case ";":
                        MSSQLProc.finishStatement();
                        MSSQLProc.decrementIndent();
                        ChangeState(new parseBody());
                        break;
                    default:
                        MSSQLProc.setUpdateTable(token);
                        break;
                }

            }
        }

        public class parseSetClause : parseState
        {
            String columnName;

            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "=":
                        MSSQLProc.setColumn(columnName, token);
                        pushReturn(this);
                        ChangeState(new parseSQLReferences());
                        break;
                    case "WHERE":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    case ";":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    default:
                        columnName = token;
                        break;
                }
            }
        }

        // this is used by several statements to parse
        // the where clause 
        public class parseWhereClause : parseState
        {
            String columnName;
            int parens = 0;

            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "=":
                    case "==":
                    case "!=":
                    case ">":
                    case "<":
                    case ">=":
                    case "<=":
                    case "!<":
                    case "!>":
                    case "<>":
                    case "LIKE":
                    case "IN":
                        MSSQLProc.setColumn(columnName, token);
                        pushReturn(this);
                        ChangeState(new parseSQLReferences());
                        break;
                    case "BETWEEN":
                        MSSQLProc.setColumn(columnName, token);
                        tokenPushBack();
                        pushReturn(this);
                        ChangeState(new parseBetween());
                        break;
                    case "ORDER":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    case "ALL":
                    case "AND":
                    case "ANY":
                    case "EXISTS":
                    case "OR":
                    case "NOT":
                    case "WHERE":
                    case "ASC":
                    case "DESC":

                        MSSQLProc.Append(" " + token + " ");
                        break;
                    case "LIMIT":  // this could be bug if spread over 2 lines
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    case "(":
                        parens++;
                        MSSQLProc.Append(" " + token);
                        break;

                    case ")":
                        if (parens > 0)
                        {
                            MSSQLProc.Append(token);
                            parens--;
                        }
                        else
                        {
                            tokenPushBack();
                            ChangeState(popReturn());
                        }
                        break;
                    case ";":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    default:
                        if (Char.IsDigit(token[0]))
                            MSSQLProc.Append(token);
                        else
                            if (MSSQLProc.isAlreadyDefined(token))
                                columnName = MSSQLProc.ConvertVariableReference(token);
                            else
                                columnName = token;
                        break;
                }
            }
        }


        public class parseBetween : parseState
        {
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "BETWEEN":
                        // we have already put the word BETWEEN in the output
                        pushReturn(this);
                        ChangeState(new parseSQLReferences());
                        break;
                    case "AND":
                        MSSQLProc.Append(" " + token + " ");
                        pushReturn(this);
                        ChangeState(new parseSQLReferences());
                        break;
                    default:
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                }
            }
        }

//MySQL
// SELECT
//     [ALL | DISTINCT | DISTINCTROW ]
//       [HIGH_PRIORITY]
//       [STRAIGHT_JOIN]
//       [SQL_SMALL_RESULT] [SQL_BIG_RESULT] [SQL_BUFFER_RESULT]
//       [SQL_CACHE | SQL_NO_CACHE] [SQL_CALC_FOUND_ROWS]
//     select_expr [, select_expr ...]
//     [FROM table_references
//     [WHERE where_condition]
//     [GROUP BY {col_name | expr | position}
//       [ASC | DESC], ... [WITH ROLLUP]]
//     [HAVING where_condition]
//     [ORDER BY {col_name | expr | position}
//       [ASC | DESC], ...]
//     [LIMIT {[offset,] row_count | row_count OFFSET offset}]
//     [PROCEDURE procedure_name(argument_list)]
//     [INTO OUTFILE 'file_name'
//         [CHARACTER SET charset_name]
//         export_options
//       | INTO DUMPFILE 'file_name'
//       | INTO var_name [, var_name]]
//     [FOR UPDATE | LOCK IN SHARE MODE]]


//MS SQL has a different syntax
//        <SELECT statement> ::=  
//
//    [WITH <common_table_expression> [,...n]]
//    <query_expression> 
//    [ ORDER BY { order_by_expression | column_position [ ASC | DESC ] } 
//  [ ,...n ] ] 
//    [ COMPUTE 
//  { { AVG | COUNT | MAX | MIN | SUM } (expression )} [ ,...n ] 
//  [ BY expression [ ,...n ] ] 
//    ] 
//    [ <FOR Clause>] 
//    [ OPTION ( <query_hint> [ ,...n ] ) ] 
//
//<query_expression> ::= 
//    { <query_specification> | ( <query_expression> ) } 
//    [  { UNION [ ALL ] | EXCEPT | INTERSECT }
//        <query_specification> | ( <query_expression> ) [...n ] ] 
//
//<query_specification> ::= 
//SELECT [ ALL | DISTINCT ] 
//    [TOP (expression) [PERCENT] [ WITH TIES ] ] 
//
//    < select_list > 
//    [ INTO new_table ] 
//    [ FROM { <table_source> } [ ,...n ] ] 
//    [ WHERE <search_condition> ] 
//    [ <GROUP BY> ] 
//    [ HAVING < search_condition > ] 
//
        public class parseSelect : parseState
        {
            // we have had a SELECT statement
            // get the columns to select
            public List<string> columns;
            int parens;

            static String FunctionCallString;
            static int functionLevels = 0;

            public parseSelect()
            {
                columns = new List<string>();
                MSSQLProc.incrementIndent();
                parens = 0;
            }
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "FROM":
                        dumpColumns();
                        MSSQLProc.Append(" FROM ");
                        pushReturn(this);
                        ChangeState(new parseFromClause());
                        break;
                    case "WHERE":
                        MSSQLProc.startWhereClause();
                        pushReturn(this);
                        ChangeState(new parseWhereClause());
                        break;
                    case "INTO":
                        pushReturn(this);
                        ChangeState(new parseIntoClause(this));
                        break;
                    case "ORDER":
                        pushReturn(this);
                        ChangeState(new parseOrderClause());
                        break;
                    case "AS":
                        pushReturn(this);
                        ChangeState(new parseAsColumn(this));
                        break;
                    case "SELECT":
                        MSSQLProc.StartSelect();
//                        pushReturn(this);             // if we have this here, we have double pushes
                        ChangeState(new parseSelect());
                        break;

                        // if we have a ) out of order
                        // that could be the end of an embedded select
                    case ")":
                        MSSQLProc.Append(")");
                        if (parens == 0)
                            ChangeState(popReturn());
                        else
                            parens--;
                        break;
                    case ";":
                        if (columns.Count > 0)
                            dumpColumns();
                        MSSQLProc.finishStatement();
                        MSSQLProc.decrementIndent();
                        ChangeState(popReturn());
                        break;
                    case "ALL":
                    case "DISTINCT":
                    case "DISTINCTROW":
                    case "GROUP":  // <------- need to specify this better
                    case "HAVING":
                    case "(":
                        parens++;
                        pushReturn(this);
                        MSSQLProc.Append(token);
                        break;
                        // possible bug - not checking for variables or function calls
                    case "LIMIT":  // this could be bug if spread over 2 lines
                        String lim = getNextToken();
                        if (lim == "(")
                        {
                            lim = getNextToken();
                            MSSQLProc.Top(lim, "SELECT");
                            lim = getNextToken();
                        }
                        else
                            MSSQLProc.Top(lim, "SELECT");
                        break;

                        // at this point, we are finding the columns to select
                    default:

                        // we could have a select just with constants and local variables

                        if ((token[0] == '\'') || 
                            (Char.IsDigit(token[0])) ||
                            (token[0] == '@'))
                            columns.Add(token);
                        else
                        if (MSSQLProc.isAlreadyDefined(token))
                            columns.Add(MSSQLProc.ConvertVariableReference(token));
                        else
                            if (isMySQLFunction(token))
                            {
                                string sysVar = MSSQLProc.convertToSystemVar(token);
                                if (sysVar.Length > 0)
                                {
                                    MSSQLProc.Append(sysVar);
                                    // need to eat the paren from a function call
                                    if (tokenLookAhead() == "(")
                                        getNextToken();
                                    if (tokenLookAhead() == ")")
                                        getNextToken();
                                }
                                else
                                {
                                    // save the function name and start
                                    // processing its arguments
                                    functionLevels = 0;
                                    FunctionCallString = MSSQLProc.convertFunction(token);
                                    pushReturn(this);
                                    ChangeState(new parsingColumnFunctionCall(this));
                                }
                            }
                            else
                        if (token != ",")
                            columns.Add(token);
                        break;
                }

            }
            public class parseAsColumn : parseState
            {
                parseSelect parent;

                public parseAsColumn(parseSelect nParent)
                {
                    parent = nParent;
                }
                // the last thing was the AS clause in a column statement
                // that means to add this to the last column definition
                public override void handleToken(string token)
                {
                    string AsClause;
                    AsClause = parent.columns[parent.columns.Count-1] + " AS " + token;
                    parent.columns [parent.columns.Count-1] = AsClause;
                    ChangeState(popReturn());
                }
            }
            void dumpColumns()
            {
                int cnt;

                cnt = 0;
                foreach (String col in columns)
                {
                    MSSQLProc.Append(col);
                    if (cnt < columns.Count-1)
                        MSSQLProc.Append(", ");
                    cnt++;
                }
                columns = new List<string>();
            }

            // called from within select statement when selecting a function
            // this version does not allow for variables and embedded function calls
            public class parsingColumnFunctionCall : parseState
            {
                parseSelect parent;

                public parsingColumnFunctionCall(parseSelect nParent)
                {
                    parent = nParent;
                }
                public override void handleToken(string token)
                {
                    switch (token)
                    {
                        case ")":
                            FunctionCallString = FunctionCallString + token;
                            if (functionLevels == 0)
                                parent.columns.Add(FunctionCallString);
                            ChangeState(popReturn());
                            functionLevels--;
                            break;
                        case ";":
                            parent.columns.Add(FunctionCallString);
                            tokenPushBack();
                            ChangeState(popReturn());
                            break;
                        default:
                            if (isMySQLFunction(token))
                            {
                                string sysVar = MSSQLProc.convertToSystemVar(token);
                                if (sysVar.Length > 0)
                                {
                                    FunctionCallString = FunctionCallString + sysVar;
                                    // need to eat the paren from a function call
                                    if (tokenLookAhead() == "(")
                                        getNextToken();
                                    if (tokenLookAhead() == ")")
                                        getNextToken();
                                }
                                else
                                {
                                    functionLevels++;
                                    FunctionCallString = FunctionCallString + MSSQLProc.convertFunction(token);
                                    pushReturn(this);
                                    ChangeState(new parsingColumnFunctionCall(parent));
                                }
                            }
                            else
                                if (token == ",")
                                    FunctionCallString = FunctionCallString + ", ";
                                else
                                    FunctionCallString = FunctionCallString + token;
                            break;
                    }
                }
            }
            public class parseIntoClause : parseState
            {
                Int16 whichCol;
                parseSelect parent;
                public parseIntoClause(parseSelect nParent)
                {
                    whichCol = 0;
                    parent = nParent;
                }
                public override void handleToken(string token)
                {
                    if ((token.ToUpper() == "FROM")||
                        (token == ";"))
                    {
                        parent.columns = new List<string>();
                        tokenPushBack();
                        ChangeState(popReturn());
                        return;
                    }
                    // token is the variable to put the column into

                    if (token == ",")
                        MSSQLProc.Append(", ");
                    else
                    {
                        if (!MSSQLProc.isAlreadyDefined(token))
                        {
                            VariableDeclaration nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, token);
                            nVar.SetSQLType("INT");
                            nVar.SetLength(1);
                            MSSQLProc.addLocalVariable(nVar);
                        }
                        MSSQLProc.AppendVariableReference(token);
                        MSSQLProc.Append("=" + parent.columns[whichCol++]);
                    }
                }
            }
        }
        public class parseOrderClause : parseState
        {
            String OrderByClause = "";

            public override void handleToken(string token)
            {
                String nextToken;

                if (token.ToUpper() != "BY")
                {
                    if ((token == "ASC") || (token == "DESC"))
                        OrderByClause = OrderByClause + " " + token;
                    else
                    if (OrderByClause.Length > 0)
                        OrderByClause = OrderByClause + ", " + token;
                    else
                        OrderByClause = token;
                    nextToken = tokenLookAhead();
                    if ((nextToken != ",") && (nextToken != "DESC") && (nextToken != "ASC"))
                    {
                        MSSQLProc.Append(" ORDER BY " + OrderByClause + " ");
                        ChangeState(popReturn());
                    }
                }
            }
        }

        public class parseFromClause : parseState
        {
            int parens = 0;

            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "WHERE":
                    case "GROUP":
                    case "ORDER":
                    case "LIMIT":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    case ")":
                        if (parens > 0)
                        {
                            MSSQLProc.Append(")");
                            parens--;
                        }
                        else
                        {
                            tokenPushBack();
                            ChangeState(popReturn());
                        }
                        break;
                    case ";":
                        tokenPushBack();
                        ChangeState(popReturn());
                        break;
                    case ",":
                        MSSQLProc.Append(", ");
                        break;
                    case "(":
                        MSSQLProc.Append("(");
                        parens++;
                        break;
                    default:
                        MSSQLProc.setTableName(token);
                        break;
                }
            }
        }






        // this is called when an insert statement exists stand alone
        // and not in any stored procedure

        public class InsertStatement : parseState
        {
            public override void handleToken(string token)
            {
                switch (token)
                {
                    case "(":
                    case ")":
                    case ",":
                        MSSQLOutputFile.Append(token);
                        break;
                    case ";":
                        MSSQLOutputFile.Append(token);
                        ChangeState(new IdleState());
                        break;
                    default:
                        MSSQLOutputFile.Append(" " + token);
                        break;
                }
            }
        }








        // the parsing of references within an SQL statement
        // is different from references within a function call. 
        // The terminators are different for one thing
        public class parseSQLReferences : parseState
        {
            int parens = 0;

            public override void handleToken(string token)
            {
                if (Char.IsDigit(token[0]))
                {
                    MSSQLProc.Append(token);
                }
                else
                {
                    switch (token.ToUpper())
                    {
                        case "+":
                        case "-":
                        case "*":
                        case "/":
                        case "%":
                        case "==":
                        case "!=":
                        case ">":
                        case "<":
                        case ">=":
                        case "<=":
                        case "!<":
                        case "!>":
                        case "<>":
                        case "LIKE":
                            MSSQLProc.Append(" " + token + " ");
                            break;
                        case "ALL":
                        case "AND":
                        case "ANY":
                        case "BETWEEN":
                        case "EXISTS":
                        case "IN":
                        case "OR":
                        case "NOT":
                        case "LIMIT":
                        case "WHERE":
                        case "ORDER":
                            tokenPushBack();
                            ChangeState(popReturn());
                            break;
                            // we may have an embedded select statement in some other clause
                            // specifically, in a WHERE clause
                        case "SELECT":
                            MSSQLProc.StartLine();
                            MSSQLProc.StartSelect();
                            pushReturn(this);
                            ChangeState(new parseSelect());
                            break;

                            // an unexpected ) may be the end of an embedded SELECT
                        case ")":
                            if (parens == 0)
                            {
                                tokenPushBack();
                                ChangeState(popReturn());
                            }
                            else
                                parens--;
                            break;
                        case "(":
                            MSSQLProc.Append("(");
                            parens++;
                            break;
                        case ",":
                            MSSQLProc.Append(", ");
                            ChangeState(popReturn());
                            break;
                        case "@":
                            token = getNextToken();
                            if (MSSQLProc.isAlreadyDefined(token))
                            {
                                MSSQLProc.AppendVariableReference(token);
                            }
                            else
                            {
                                VariableDeclaration nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, token);
                                nVar.SetSQLType("INT");
                                nVar.SetLength(1);
                                MSSQLProc.addLocalVariable(nVar);
                                MSSQLProc.AppendVariableReference(token);
                            }
                            break;
                        case ";":
                            tokenPushBack();
                            ChangeState(popReturn());
                            break;
                        default:
                            if (MSSQLProc.isAlreadyDefined(token))
                            {
                                MSSQLProc.AppendVariableReference(token);
                            }
                            else
                                if (isMySQLFunction(token))
                                {
                                    string sysVar = MSSQLProc.convertToSystemVar(token);
                                    if (sysVar.Length > 0)
                                    {
                                        MSSQLProc.Append(sysVar);
                                        // need to eat the paren from a function call
                                        if (tokenLookAhead() == "(")
                                            getNextToken();
                                        if (tokenLookAhead() == ")")
                                            getNextToken();
                                    }
                                    else
                                    {
                                        MSSQLProc.Append(MSSQLProc.convertFunction(token));
                                        pushReturn(this);
                                        ChangeState(new parsingFunctionCall());
                                    }
                                }
                                else
                                {
                                    MSSQLProc.Append(token);
                                }
                            break;
                    }
                }
            }
        }



        // this handles the subroutine calls that are stand alone statements

        public class parseCall : parseState
        {
            String routineName;
            static Signature ProcedureSignature;

            public override void handleToken(string token)
            {
                routineName = token;
                MSSQLProc.addCall(routineName);
                ProcedureSignature = findProcedureSignature(routineName);
                if (ProcedureSignature == null)
                {
                    MessageBox.Show("Procedure " + routineName + " not found in list");
                }
                ChangeState(new parseCallArguments());
            }

            public class parseCallArguments : parseState
            {
                int ArgumentNumber;
                Argument CurrentArgument;

                public override void handleToken(string token)
                {
                    if (Char.IsDigit(token[0]))
                    {
                        ArgumentNumber++;
                        if (ProcedureSignature != null)
                        {
                            if (ArgumentNumber > ProcedureSignature.ModuleArguments.Count)
                                MessageBox.Show("Too many arguments for " + ProcedureSignature.ModuleName);
                            CurrentArgument = ProcedureSignature.ModuleArguments[ArgumentNumber-1];
                            if (CurrentArgument.WhichDirection != Direction.INPUT)
                                MessageBox.Show("Constant given for output parameter");
                        }
                        MSSQLProc.addInputCallParameter(token);
                    }
                    else
                    {
                        switch (token)
                        {
                            case "(":
                                break;
                            case ")":
                                break;
                            case ";":
                                MSSQLProc.finishStatement();
                                ChangeState(new parseBody());
                                break;
                            case ",":
                                MSSQLProc.addParameterSeparator();
                                break;
                            default:
                                ArgumentNumber++;
                                if (ProcedureSignature != null)
                                {
                                    if (ArgumentNumber > ProcedureSignature.ModuleArguments.Count)
                                        MessageBox.Show("Too many arguments for " + ProcedureSignature.ModuleName);
                                    CurrentArgument = ProcedureSignature.ModuleArguments[ArgumentNumber-1];
                                    if (CurrentArgument.WhichDirection == Direction.INPUT)
                                    {
                                        MSSQLProc.addInputCallParameter(token);
                                    }
                                    else
                                    {
                                        if (token[0] == '@')
                                        {
                                            if (!MSSQLProc.isAlreadyDefined(token.Substring(1)))
                                            {
                                                VariableDeclaration nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, token.Substring(1));
                                                nVar.SetSQLType("INT");
                                                nVar.SetLength(1);
                                                MSSQLProc.addLocalVariable(nVar);
                                            }
                                        }
                                        MSSQLProc.addOutputCallParameter(token.Substring(1));
                                    }
                                }
                                else
                                if (token[0] == '@')
                                {
                                    if (!MSSQLProc.isAlreadyDefined(token.Substring(1)))
                                    {
                                        VariableDeclaration nVar = new VariableDeclaration(VariableTypes.REGULARVARIABLE, token.Substring(1));
                                        nVar.SetSQLType("INT");
                                        nVar.SetLength(1);
                                        MSSQLProc.addLocalVariable(nVar);
                                    }
                                    MSSQLProc.addOutputCallParameter(token.Substring(1));
                                }
                                else
                                    MSSQLProc.addInputCallParameter(token);
                                break;
                        }
                    }
                }
            }

        }
        public class parseWhile : parseState
        {
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "(":
                        pushReturn(this);
                        ChangeState(new parseReferences());
                        break;
                    case "DO":
                        MSSQLProc.DO();
                        ChangeState(popReturn());
                        break;
                    default:
                        tokenPushBack();
                        pushReturn(this);
                        ChangeState(new parseReferences());
                        break;
                }
            }
        }

        public class parseStart : parseState
        {
            public override void handleToken(string token)
            {
                if (token == ";")
                {
                    MSSQLProc.Append(token);
                    ChangeState(new parseBody());
                }
                else
                    MSSQLProc.Append("BEGIN " + token);
            }
        }
       






        // DROP statement

        public class DropStatement : parseState
        {
            public override void handleToken(string token)
            {
                switch (token.ToUpper())
                {
                    case "TABLE":
                        ChangeState(new DropTableStatement());
                        break;
                    case "PROCEDURE":
                        ChangeState(new DropProcedureStatement());
                        break;
                    default:
                        break;
                }
            }
        }


        public class DropTableStatement : parseState
        {
            String TableName;

            public override void  handleToken(string token)
            {
                if (token == Delimiter)
                {
                    MSSQLTable = new SQLTable(MSSQLOutputFile);
                    MSSQLTable.writeDeleteTableIfExists(TableName);
                    ChangeState(new IdleState());
                }
                else
                {
                    switch (token.ToUpper())
                    {
                        case "IF":
                            break;
                        case "EXISTS":
                            break;
                        default:
                            TableName = token;
                            break;
                    }
                }
            }
        }


        public class DropProcedureStatement : parseState
        {
            String ProcedureName;

            public override void handleToken(string token)
            {
                if (token == Delimiter)
                {
                    MSSQLProc = new SQLProcedure(MSSQLOutputFile); 
                    MSSQLProc.writeDeleteProcedureIfExists (ProcedureName);
                    ChangeState(new IdleState());
                }
                else
                {
                    switch (token.ToUpper())
                    {
                        case "IF":
                            break;
                        case "EXISTS":
                            break;
                        default:
                            ProcedureName = token;
                            break;
                    }
                }
            }
        }








        public int TabStopSize = 8;
        static public String InputLine;
        public int pos;
        public bool StartOfLine;
        public int levels;

        public int MaxTypeSize;

        // tokens parsed from the line

        public static List<String> tokens;
        public static int tokenI;
        public static String CommentPortion;
        public static int CommentPosition;





        public static string getNextToken()
        {
            if (tokenI < tokens.Count)
            {
                return (tokens[tokenI++]);
            }
            return "";
        }

        public static string tokenLookAhead()
        {
            if (tokenI < tokens.Count)
            {
                return (tokens[tokenI]);
            }
            return "";
        }
        // allow a sub state to say "up a level, handle this"
        public static void tokenPushBack()
        {
            tokenI--;
        }







        public void ChangeParseState(parseState s)
        {
            currentParseState = s;
        }


 


 















        public bool delimiterMatched()
        {
            int i = 0;
            while (i < Delimiter.Length)
            {
                if (pos + i < InputLine.Length)
                {
                    if (InputLine[pos + i] != Delimiter[i]) return false;
                    i++;
                }
                else return false;
            }
            return true;
        }


        // check lookahead - used to see if quoted string is 
        // really finished as it could be an embedded quote
        // So, a quote is truly finished if it is terminated afterwards
        // with a comment, a comma, a paren.
        
        Boolean checkLookahead(int pos)
        {
            char ch;

            while (pos < InputLine.Length)
            {
                ch = InputLine[pos++];

                switch (ch)
                {
                    case '\'':
                        return true;
                    case ',':
                        return false;
                    case ')':
                        return false;
                    case ';':
                        return false;
                    case '-':  // check for double -
                        if (InputLine[pos] == '-')
                            return false;
                        break;
                    default:
                        break;
                }
            }
            return false;
        }

        // Parse Next - general pull next token from line
        // in order to preserve some formatting of comments
        // CommentPortion and CommentPosition may be modified by this routine

        public String parseNext()
        {
            StringBuilder token;
            bool delimiterReached;

            token = new StringBuilder();
            delimiterReached = false;
            while (!delimiterReached)
            {
                if (pos < InputLine.Length)
                {
                    if (delimiterMatched())
                    {
                        delimiterReached = true;
                        if (token.Length == 0)
                        {
                            token.Append(Delimiter);
                            pos = pos + Delimiter.Length;
                        }
                    }
                    else
                    {
                        char ch = InputLine[pos++];
                        switch (ch)
                        {
                            case ' ':
                                if (token.Length > 0)
                                {
                                    delimiterReached = true;
                                    break;
                                }
                                else
                                {
                                    if (StartOfLine)
                                    {
                                        MSSQLOutputFile.Indent++;
                                        break;
                                    }
                                    else
                                        break;
                                }
                            case '\t':
                                if (token.Length > 0)
                                {
                                    delimiterReached = true;
                                    break;
                                }
                                else
                                {
                                    if (StartOfLine)
                                    {
                                        MSSQLOutputFile.Indent = MSSQLOutputFile.Indent + TabStopSize - (MSSQLOutputFile.Indent % TabStopSize);
                                        break;
                                    }
                                    else
                                        break;
                                }

                                // we have a quoted value
                                // pull to end of quote or end of line

                            case '\'':
                                if (pos < InputLine.Length)
                                {
                                    token.Append(ch);
                                    ch = InputLine[pos++];

                                    while (true)
                                    {
                                        if (ch == '\'')
                                        {
                                            if ( ! checkLookahead(pos))
                                            {
                                                token.Append(ch);
                                                break;
                                            }
                                        }
                                        if (pos >= InputLine.Length) break;
                                        token.Append(ch);
                                        ch = InputLine[pos++];
                                    }
                                    delimiterReached = true;
                                }
                                break;


                            case '\n': delimiterReached = true; break;
                            // if we have already started building a token
                            // then this is the end of that token
                            // else take the delimiter as the token
                            case ',':
                            case '(':
                            case ')':
                            case '+':
                            case '*':
                            case '/':
                            case '%':
                            case ';': delimiterReached = true;
                                if (token.Length > 0)
                                {
                                    pos--; // leave the character on the line
                                    break;
                                }
                                else
                                {
                                    token.Append(ch);
                                    StartOfLine = false;
                                }
                                break;

                                // check for double character tokens
                            case '<':
                            case '>':
                            case '!':
                            case '=':
                                if (token.Length > 0)
                                {
                                    pos--; // leave the character on the line
                                    delimiterReached = true;
                                    break;
                                }
                                else
                                {
                                    token.Append(ch);
                                    StartOfLine = false;
                                    delimiterReached = true;
                                    if (pos >= InputLine.Length)
                                        break;
                                    if ((InputLine[pos] == '=') || (InputLine[pos] == '>'))
                                    {
                                        token.Append(InputLine[pos]);
                                        pos++;
                                    }
                                }
                                break;

                                // check for comments
                            case '-':
                                if (pos >= InputLine.Length)
                                    break;
                                if (InputLine[pos] == '-')
                                {
                                    pos--;
                                    CommentPosition = pos;
                                    CommentPortion = InputLine.Substring(pos + 2);
                                    pos = InputLine.Length;
                                    delimiterReached = true;
                                }
                                else
                                {
                                    token.Append(ch);
                                    StartOfLine = false;
                                }
                                break;

                            default:
                                token.Append(ch);
                                StartOfLine = false;
                                break;
                        }
                    }
                }
                else    // at end of line
                {
                    delimiterReached = true;
                }
            }
            return token.ToString();
        }


        List <String> parseLine ()
        {
            List<String> results;
            String token;

            results = new List<string>();
            while (pos < InputLine.Length)
            {
                token = parseNext();
                if (token == "--")
                {
                    CommentPosition = pos;
                    CommentPortion = InputLine.Substring(pos);
                    break;
                }

                // the following lines are to handle a difference in
                // how SQL insertion protection is handled between the two
                // either that or there is a widespread bug in the MySQL code
                if (token == "'' ''") token = "''''";
                if (token == "'' '' ''") token = "''''''";
                if (token.Length > 0)
                    results.Add(token);
                if (token == "\\.")
                {
                    token = InputLine.Substring(pos);
                    pos = InputLine.Length;
                    results.Add(token);
                }
            }

            return results;
        }













 






        // this simply is a test to see if an identifier is a 
        // valid MySQL function name
        static Boolean isMySQLFunction(String name)
        {
            switch (name.ToUpper())
            {
                case "ASCII":
                case "BIN":
                case "BIT_LENGTH":
                case "CHAR_LENGTH":
                case "CHAR":
                case "CHARACTER_LENGTH":
                case "CONCAT_WS":
                case "CONCAT":
                case "ELT":
                case "EXPORT_SET":
                case "FIELD":
                case "FIND_IN_SET":
                case "FORMAT":
                case "HEX":
                case "INSERT":
                case "INSTR":
                case "LCASE":
                case "LEFT":
                case "LENGTH":
                case "LOAD_FILE":
                case "LOCATE":
                case "LOWER":
                case "LPAD":
                case "LTRIM":
                case "MAKE_SET":
                case "MATCH":
                case "MID":
                case "OCTET_LENGTH":
                case "ORD":
                case "POSITION":
                case "QUOTE":
                case "REGEXP":
                case "REPEAT":
                case "REPLACE":
                case "REVERSE":
                case "RIGHT":
                case "RLIKE":
                case "RPAD":
                case "RTRIM":
                case "SOUNDEX":
                case "SOUNDS":     // has following "like"
                case "SPACE":
                case "STRCMP":
                case "SUBSTR":
                case "SUBSTRING_INDEX":
                case "SUBSTRING":
                case "TRIM":
                case "UCASE":
                case "UNHEX":
                case "UPPER":
                case "ABS":
                case "ACOS":
                case "ASIN":
                case "ATAN2":
                case "ATAN":
                case "CEIL":
                case "CEILING":
                case "CONV":    
                case "COS":
                case "COT":
                case "CRC32":
                case "DEGREES":
                case "DIV":
                case "EXP":
                case "FLOOR":
                case "LN":
                case "LOG10":
                case "LOG2":
                case "LOG":
                case "MOD":
                case "OCT": 
                case "PI":
                case "POW":
                case "POWER":
                case "RADIANS":
                case "RAND":
                case "ROUND":
                case "SIGN":
                case "SIN":
                case "SQRT":
                case "TAN":
                case "TRUNCATE":

                    // date time functions

                case "ADDDATE":
                case "ADDTIME":
                case "CONVERT_TZ":
                case "CURDATE":
                case "CURRENT_DATE":
                case "CURRENT_TIME":
                case "CURRENT_TIMESTAMP":
                case "CURTIME":
                case "DATE_ADD":
                case "DATE_FORMAT":
                case "DATE_SUB":
                case "DATE":
                case "DATEDIFF":
                case "DAY":
                case "DAYNAME":
                case "DAYOFMONTH":
                case "DAYOFWEEK":
                case "DAYOFYEAR":
                case "EXTRACT":
                case "FROM_DAYS":
                case "FROM_UNIXTIME":
                case "GET_FORMAT":
                case "HOUR":
                case "LAST_DAY":
                case "LOCALTIME":
                case "LOCALTIMESTAMP":
                case "MAKEDATE":
                case "MAKETIME":
                case "MICROSECOND":
                case "MINUTE":
                case "MONTH":
                case "MONTHNAME":
                case "NOW":
                case "PERIOD_ADD":
                case "PERIOD_DIFF":
                case "QUARTER":
                case "SEC_TO_TIME":
                case "SECOND":
                case "STR_TO_DATE":
                case "SUBDATE":
                case "SUBTIME":
                case "SYSDATE":
                case "TIME_FORMAT":
                case "TIME_TO_SEC":
                case "TIME":
                case "TIMEDIFF":
                case "TIMESTAMP":
                case "TIMESTAMPADD":
                case "TIMESTAMPDIFF":
                case "TO_DAYS":
                case "TO_SECONDS":
                case "UNIX_TIMESTAMP":
                case "UTC_DATE":
                case "UTC_TIME":
                case "UTC_TIMESTAMP":
                case "WEEK":
                case "WEEKDAY":
                case "WEEKOFYEAR":
                case "YEAR":
                case "YEARWEEK":

                case "BINARY":
                case "CAST":
                case "CONVERT":
                case "EXTRACTVALUE":
                case "UPDATEXML":

                case "AES_DECRYPT":
                case "AES_ENCRYPT":
                case "BENCHMARK":
                case "BIT_COUNT":
                case "CHARSET":
                case "COERCIBILITY":
                case "COLLATION":
                case "COMPRESS":
                case "CONNECTION_ID":
                case "CURRENT_USER":
                case "DATABASE":
                case "DECODE":
                case "DEFAULT":
                case "DES_DECRYPT":
                case "DES_ENCRYPT":
                case "ENCODE":
                case "ENCRYPT":
                case "FOUND_ROWS":
                case "GET_LOCK":
                case "INET_ATON":
                case "INET_NTOA":
                case "IFNULL":
                case "IS_FREE_LOCK":
                case "IS_USED_LOCK":
                case "LAST_INSERT_ID":
                case "MASTER_POS_WAIT":
                case "MD5":
                case "NAME_CONST":
                case "OLD_PASSWORD":
                case "PASSWORD":
                case "RELEASE_LOCK":
                case "ROW_COUNT":
                case "SCHEMA":
                case "SESSION_USER":
                case "SHA1":
                case "SHA2":
                case "SLEEP":
                case "SYSTEM_USER":
                case "UNCOMPRESS":
                case "UNCOMPRESSED_LENGTH":
                case "USER":
                case "UUID_SHORT":
                case "UUID":
                case "VALUES":
                case "VERSION":

                case "AVG":
                case "BIT_AND":
                case "BIT_OR":
                case "BIT_XOR":
                case "COUNT":
                case "GROUP_CONCAT":
                case "MAX":
                case "MIN":
                case "STD":
                case "STDDEV_POP":
                case "STDDEV_SAMP":
                case "STDDEV":
                case "SUM":
                case "VAR_POP":
                case "VAR_SAMP":
                case "VARIANCE":
                    return true;
                case "ISNULL":
                    MessageBox.Show("Need to rewrite ISNULL with IS NULL");
                    return true;
            }
            return false;
        }








        public void parseMySQLFile(String FileName, String OutputFileName)
        {
            MySQLInputFile = new StreamReader(FileName, Encoding.ASCII);
            MSSQLOutputFile = new MSSQLFile();
            WritingToOutput = MSSQLOutputFile;
            Vars = new VariableDeclaration();
            MSSQLOutputFile.StartOutputFile(OutputFileName);
            returnStack = new List<parseState>();

            MaxTypeSize = 0;
            levels = 0;

            Delimiter = ";";
            ChangeParseState(new IdleState());
            try
            {
                while (!MySQLInputFile.EndOfStream)
                {
                    InputLine = MySQLInputFile.ReadLine();
                    parseMySQLLine();
                    WritingToOutput.completeLine(CommentPortion, CommentPosition);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + " in " + FileName);
            }
            finally
            {
                if (MSSQLOutputFile != null)
                    MSSQLOutputFile.finish();
                MySQLInputFile.Close();
            }
        }

        public void parseMySQLLine()
        {
            pos = 0;
            MSSQLOutputFile.setIndent(levels);
            StartOfLine = true;
            CommentPortion = "";
            CommentPosition = -1;

            tokens = parseLine();
            if (tokens.Count > 0)
            {
                tokenI = 0;
                while (tokenI < tokens.Count)
                {
                    String token = getNextToken();
                    currentParseState.handleToken(token);
                }

            }
        }


        public void parseAllfiles(String LeadFile, String resultPath)
        {
            String outfile;

            IncludeFiles = new List<string>();
            ModuleSignatures = new List<Signature>();
            if (resultPath.Length > 0)
                outfile = resultPath + Path.DirectorySeparatorChar + Path.GetFileName(LeadFile);
            else
            {
                MessageBox.Show("Can't overwrite existing files");
                return;
            }
            OutputPath = resultPath;
            InputPath = Path.GetDirectoryName(LeadFile);

            // go through and build the call signatures

            buildSignatures();

            // process the files

            parseMySQLFile(LeadFile, outfile);
            processIncludeFiles();
        }


        public void buildSignatures()
        {
            // this really needs to go through all the files in the directory
            // and pull in just the signature of each procedure.
            // That way, when one procedure calls another, 
            // we can have what it is supposed to be

            // alternatively, simple handle this on a call by call basis
            // where we load the signature and process the subroutine when called.

            /*
            Signature ProcedureSignature;
            Argument CurrentArgument;

            ProcedureSignature = new Signature();
            ProcedureSignature.ModuleName = "gp_IncrementSequenceNumber";
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "MID";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);

            ModuleSignatures.Add(ProcedureSignature);


            ProcedureSignature = new Signature();
            ProcedureSignature.ModuleName = "gp_IncTranCnt";
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "TCount";
            CurrentArgument.WhichDirection = Direction.OUTPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);

            ModuleSignatures.Add(ProcedureSignature);

            ProcedureSignature = new Signature();
            ProcedureSignature.ModuleName = "gp_LogTransaction";
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "MID";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);

            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "Clerk";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "CardNum";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "WhereFrom";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "TransType";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "TransNum";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "TransText";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "Amount";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "NewCard";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "SeqNum";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "WhenHap";
            CurrentArgument.WhichDirection = Direction.OUTPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);

            ModuleSignatures.Add(ProcedureSignature);

            ProcedureSignature = new Signature();
            ProcedureSignature.ModuleName = "gp_ValidateMerchant";
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "MID";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "MerchID";
            CurrentArgument.WhichDirection = Direction.OUTPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "SeqNum";
            CurrentArgument.WhichDirection = Direction.OUTPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "Offset";
            CurrentArgument.WhichDirection = Direction.OUTPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);

            ModuleSignatures.Add(ProcedureSignature);


            ProcedureSignature = new Signature();
            ProcedureSignature.ModuleName = "gp_ComputePoints";
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "Merchant";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "AmountOfSale";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "CurrentBalance";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "CurrentVisits";
            CurrentArgument.WhichDirection = Direction.INPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);
            CurrentArgument = new Argument();
            CurrentArgument.ArgumentName = "Points ";
            CurrentArgument.WhichDirection = Direction.OUTPUT;
            ProcedureSignature.ModuleArguments.Add(CurrentArgument);

            ModuleSignatures.Add(ProcedureSignature);
            */
        }

        static public Signature findProcedureSignature(String ProcedureName)
        {
            foreach (Signature SI in ModuleSignatures)
            {
                if (SI.ModuleName == ProcedureName)
                    return (SI);
            }
            return (null);
        }

        public void processIncludeFiles()
        {

            foreach (String includeFile in IncludeFiles)
            {
                if (Path.IsPathRooted(includeFile))
                    resultingIncludeFileName = includeFile;
                else
                    resultingIncludeFileName = OutputPath + Path.DirectorySeparatorChar + includeFile;
                EnsureDirectoryExists(Path.GetDirectoryName(resultingIncludeFileName));

                MySQLFile msq = new MySQLFile();
                msq.parseMySQLFile(InputPath + Path.DirectorySeparatorChar + includeFile, resultingIncludeFileName);
            }
        }

        public void EnsureDirectoryExists(String Dir)
        {
            // make sure the parent directory exists

            if (!Directory.Exists(Path.GetDirectoryName(Dir)))
            {
                EnsureDirectoryExists(Path.GetDirectoryName(Dir));
            }

            if (!Directory.Exists(Dir))
                Directory.CreateDirectory(Dir);
        }


    }
}
