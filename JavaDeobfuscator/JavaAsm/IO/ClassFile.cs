﻿using System;
using System.IO;
using BinaryEncoding;
using JavaDeobfuscator.JavaAsm.IO.ConstantPoolEntries;

namespace JavaDeobfuscator.JavaAsm.IO
{
    internal class ClassFile
    {
        private const uint magic = 0xCAFEBABE;

        public static AttributeNode ParseAttribute(Stream stream, ClassReaderState state, AttributeScope scope)
        {
            var attribute = new AttributeNode
            {
                Name = state.ConstantPool.GetEntry<Utf8Entry>(Binary.BigEndian.ReadUInt16(stream)).String,
                Data = new byte[Binary.BigEndian.ReadUInt32(stream)]
            };
            stream.Read(attribute.Data);
            attribute.Parse(scope, state);
            return attribute;
        }

        private static FieldNode ParseField(Stream stream, ClassReaderState state)
        {
            var fieldNode = new FieldNode
            {
                Owner = state.ClassNode,

                Access = (AccessModifiers) Binary.BigEndian.ReadUInt16(stream),
                Name = state.ConstantPool.GetEntry<Utf8Entry>(Binary.BigEndian.ReadUInt16(stream)).String,
                Descriptor = TypeDescriptor.Parse(state.ConstantPool.GetEntry<Utf8Entry>(Binary.BigEndian.ReadUInt16(stream)).String)
            };
            var attributesCount = Binary.BigEndian.ReadUInt16(stream);
            fieldNode.Attributes.Capacity = attributesCount;
            for (var i = 0; i < attributesCount; i++)
                fieldNode.Attributes.Add(ParseAttribute(stream, state, AttributeScope.Field));
            fieldNode.ParseAttributes(state);
            return fieldNode;
        }

        private static MethodNode ParseMethod(Stream stream, ClassReaderState state)
        {
            var methodNode = new MethodNode
            {
                Owner = state.ClassNode,

                Access = (AccessModifiers)Binary.BigEndian.ReadUInt16(stream),
                Name = state.ConstantPool.GetEntry<Utf8Entry>(Binary.BigEndian.ReadUInt16(stream)).String,
                Descriptor = MethodDescriptor.Parse(state.ConstantPool.GetEntry<Utf8Entry>(Binary.BigEndian.ReadUInt16(stream)).String)
            };
            var attributesCount = Binary.BigEndian.ReadUInt16(stream);
            methodNode.Attributes.Capacity = attributesCount;
            for (var i = 0; i < attributesCount; i++)
                methodNode.Attributes.Add(ParseAttribute(stream, state, AttributeScope.Method));
            methodNode.ParseAttributes(state);
            return methodNode;
        }

        public static ClassNode ParseClass(Stream stream)
        {
            var state = new ClassReaderState();
            var result = new ClassNode();
            state.ClassNode = result;

            if (Binary.BigEndian.ReadUInt32(stream) != magic)
                throw new IOException("Wrong magic in class");
            
            result.MinorVersion = Binary.BigEndian.ReadUInt16(stream);
            result.MajorVersion = (ClassVersion) Binary.BigEndian.ReadUInt16(stream);
            
            var constantPool = new ConstantPool();
            constantPool.Read(stream);
            state.ConstantPool = constantPool;

            result.Access = (AccessModifiers) Binary.BigEndian.ReadUInt16(stream);
            
            result.Name = new ClassName(constantPool.GetEntry<ClassEntry>(Binary.BigEndian.ReadUInt16(stream)).Name.String);
            result.SuperName = new ClassName(constantPool.GetEntry<ClassEntry>(Binary.BigEndian.ReadUInt16(stream)).Name.String);

            var interfacesCount = Binary.BigEndian.ReadUInt16(stream);
            result.Interfaces.Capacity = interfacesCount;
            for (var i = 0; i < interfacesCount; i++)
                result.Interfaces.Add(new ClassName(constantPool.GetEntry<ClassEntry>(Binary.BigEndian.ReadUInt16(stream)).Name.String));

            var fieldsCount = Binary.BigEndian.ReadUInt16(stream);
            result.Fields.Capacity = fieldsCount;
            for (var i = 0; i < fieldsCount; i++)
                result.Fields.Add(ParseField(stream, state));

            var methodsCount = Binary.BigEndian.ReadUInt16(stream);
            result.Methods.Capacity = methodsCount;
            for (var i = 0; i < methodsCount; i++)
                result.Methods.Add(ParseMethod(stream, state));

            var attributesCount = Binary.BigEndian.ReadUInt16(stream);
            result.Attributes.Capacity = attributesCount;
            for (var i = 0; i < attributesCount; i++)
                result.Attributes.Add(ParseAttribute(stream, state, AttributeScope.Class));

            result.ParseAttributes(state);

            return result;
        }

        public static void WriteAttribute(Stream stream, AttributeNode attribute, ClassWriterState state, AttributeScope scope)
        {
            Binary.BigEndian.Write(stream, state.ConstantPool.Find(new Utf8Entry(attribute.Name)));
            attribute.Data = attribute.ParsedAttribute?.Save(state, scope) ?? attribute.Data;
            if (attribute.Data.LongLength > uint.MaxValue)
                throw new ArgumentOutOfRangeException($"Attribute data length too big: {attribute.Data.LongLength} > {uint.MaxValue}");
            Binary.BigEndian.Write(stream, (uint) attribute.Data.LongLength);
            stream.Write(attribute.Data);
        }

        private static void WriteField(Stream stream, FieldNode fieldNode, ClassWriterState state)
        {
            Binary.BigEndian.Write(stream, (ushort) fieldNode.Access);
            Binary.BigEndian.Write(stream, state.ConstantPool.Find(new Utf8Entry(fieldNode.Name)));
            Binary.BigEndian.Write(stream, state.ConstantPool.Find(new Utf8Entry(fieldNode.Descriptor.ToString())));
            if (fieldNode.Attributes.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Too many attributes: {fieldNode.Attributes.Count} > {ushort.MaxValue}");
            Binary.BigEndian.Write(stream, (ushort) fieldNode.Attributes.Count);
            foreach (var attriute in fieldNode.Attributes)
                WriteAttribute(stream, attriute, state, AttributeScope.Field);
        }

        private static void WriteMethod(Stream stream, MethodNode methodNode, ClassWriterState state)
        {
            Binary.BigEndian.Write(stream, (ushort)methodNode.Access);
            Binary.BigEndian.Write(stream, state.ConstantPool.Find(new Utf8Entry(methodNode.Name)));
            Binary.BigEndian.Write(stream, state.ConstantPool.Find(new Utf8Entry(methodNode.Descriptor.ToString())));
            if (methodNode.Attributes.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Too many attributes: {methodNode.Attributes.Count} > {ushort.MaxValue}");
            Binary.BigEndian.Write(stream, (ushort)methodNode.Attributes.Count);
            foreach (var attriute in methodNode.Attributes)
                WriteAttribute(stream, attriute, state, AttributeScope.Method);
        }

        public static void WriteClass(Stream stream, ClassNode classNode)
        {
            Binary.BigEndian.Write(stream, magic);
            Binary.BigEndian.Write(stream, classNode.MinorVersion);
            Binary.BigEndian.Write(stream, (ushort) classNode.MajorVersion);
            var afterConstantPoolDataStream = new MemoryStream();
            var constantPool = new ConstantPool();
            var state = new ClassWriterState
            {
                ConstantPool = constantPool
            };

            Binary.BigEndian.Write(afterConstantPoolDataStream, (ushort) classNode.Access);
            Binary.BigEndian.Write(afterConstantPoolDataStream,
                constantPool.Find(new ClassEntry(new Utf8Entry(classNode.Name.Name))));
            Binary.BigEndian.Write(afterConstantPoolDataStream,
                constantPool.Find(new ClassEntry(new Utf8Entry(classNode.SuperName.Name))));

            if (classNode.Interfaces.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Too many interfaces: {classNode.Interfaces.Count} > {ushort.MaxValue}");
            Binary.BigEndian.Write(afterConstantPoolDataStream, (ushort) classNode.Interfaces.Count);
            foreach (var interfaceClassName in classNode.Interfaces)
                Binary.BigEndian.Write(afterConstantPoolDataStream,
                    constantPool.Find(new ClassEntry(new Utf8Entry(interfaceClassName.Name))));

            if (classNode.Fields.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Too many fields: {classNode.Fields.Count} > {ushort.MaxValue}");
            Binary.BigEndian.Write(afterConstantPoolDataStream, (ushort) classNode.Fields.Count);
            foreach (var field in classNode.Fields)
                WriteField(afterConstantPoolDataStream, field, state);

            if (classNode.Methods.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Too many methods: {classNode.Methods.Count} > {ushort.MaxValue}");
            Binary.BigEndian.Write(afterConstantPoolDataStream, (ushort)classNode.Methods.Count);
            foreach (var method in classNode.Methods)
                WriteMethod(afterConstantPoolDataStream, method, state);

            if (classNode.Attributes.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Too many attributes: {classNode.Attributes.Count} > {ushort.MaxValue}");
            Binary.BigEndian.Write(afterConstantPoolDataStream, (ushort)classNode.Attributes.Count);
            foreach (var attriute in classNode.Attributes)
                WriteAttribute(afterConstantPoolDataStream, attriute, state, AttributeScope.Class);

            constantPool.Write(stream);
            stream.Write(afterConstantPoolDataStream.ToArray());
        }
    }
}
