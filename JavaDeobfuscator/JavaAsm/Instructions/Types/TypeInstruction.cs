﻿using System;
using System.Collections.Generic;
using System.Text;
using JavaDeobfuscator.JavaAsm.Helpers;

namespace JavaDeobfuscator.JavaAsm.Instructions.Types
{
    internal class TypeInstruction : Instruction
    {
        public override Opcode Opcode { get; }

        public ClassName Type { get; set; }

        public TypeInstruction(Opcode opcode)
        {
            opcode.CheckInAndThrow(nameof(opcode), Opcode.NEW, Opcode.ANEWARRAY, Opcode.CHECKCAST, Opcode.INSTANCEOF);
            Opcode = opcode;
        }

        public override string ToString()
        {
            return $"{Opcode} {Type}";
        }
    }
}
