﻿using System;

//TODO
namespace Phantasma.Blockchain
{
    public class ChainException : Exception
    {
        public ChainException(string msg) : base(msg)
        {

        }
    }

    public class ContractException : Exception
    {
        public ContractException(string msg) : base(msg)
        {

        }
    }
}
