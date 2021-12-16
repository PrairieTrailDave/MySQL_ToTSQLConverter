using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//
// Copyright 2012-2013 Prairie Trail Software, Inc. 
// All rights reserved
//
namespace MySQL_ToTSQL
{

    class Signature
    {
        public String ModuleName;
        public List<Argument> ModuleArguments;

        public Signature()
        {
            ModuleArguments = new List<Argument>();
        }
    }
}
