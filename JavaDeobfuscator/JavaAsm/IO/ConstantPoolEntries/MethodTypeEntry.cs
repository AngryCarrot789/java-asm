﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using BinaryEncoding;

namespace JavaDeobfuscator.JavaAsm.IO.ConstantPoolEntries
{
    public class MethodTypeEntry : Entry
    {
        public Utf8Entry Descriptor { get; private set; }
        private ushort descriptorIndex;

        public MethodTypeEntry(Utf8Entry descriptor)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        public MethodTypeEntry(Stream stream)
        {
            descriptorIndex = Binary.BigEndian.ReadUInt16(stream);
        }

        public override EntryTag Tag => EntryTag.MethodType;

        public override void ProcessFromConstantPool(ConstantPool constantPool)
        {
            Descriptor = constantPool.GetEntry<Utf8Entry>(descriptorIndex);
        }

        public override void Write(Stream stream)
        {
            Binary.BigEndian.Write(stream, descriptorIndex);
        }

        public override void PutToConstantPool(ConstantPool constantPool)
        {
            descriptorIndex = constantPool.Find(Descriptor);
        }

        private bool Equals(MethodTypeEntry other)
        {
            return Equals(Descriptor, other.Descriptor);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((MethodTypeEntry)obj);
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            return Descriptor != null ? Descriptor.GetHashCode() : 0;
        }
    }
}