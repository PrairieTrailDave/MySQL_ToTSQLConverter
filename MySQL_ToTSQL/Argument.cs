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
    public enum Direction
    {
        INPUT,
        OUTPUT
    };

    class Argument
    {
        public String ArgumentName;
        public String ArgumentType;
        public Direction WhichDirection; 
    }
}
