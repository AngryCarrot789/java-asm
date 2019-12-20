﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JavaDeobfuscator.JavaAsm.Instructions.Types
{
    internal class TableSwitchInstruction : Instruction
    {
        public override Opcode Opcode => Opcode.TABLESWITCH;

        public Label Default { get; set; }

        public int LowValue { get; set; }

        public int HighValue => LowValue + Labels.Count;

        public List<Label> Labels { get; set; } = new List<Label>();
    }
}
