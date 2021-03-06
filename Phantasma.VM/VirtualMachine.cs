﻿using System.Collections.Generic;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Cryptography;
#if DEBUG
using System;
using System.Linq;
using System.IO;
#endif

namespace Phantasma.VM
{
#if DEBUG
    public class VMDebugException : Exception
    {
        public VirtualMachine vm;

        private string Header(string s)
        {
            return $"*********{s}*********";
        }

        public VMDebugException(VirtualMachine vm, string msg) : base(msg)
        {
            this.vm = vm;

            var temp = new Disassembler(vm.entryScript);

            var lines = new List<string>();

            if (vm.CurrentContext is ScriptContext sc)
            {
                lines.Add(Header("CURRENT OFFSET"));
                lines.Add(sc.InstructionPointer.ToString());
                lines.Add("");
            }

            lines.Add(Header("STACK"));
            var stack = vm.Stack.ToArray();
            for (int i = 0; i < stack.Length; i++)
            {
                lines.Add(stack[i].ToString());
            }
            lines.Add("");

            lines.Add(Header("FRAMES"));
            int ct = 0;
            var frames = vm.frames.ToArray();
            foreach (var frame in frames)
            {
                if (ct > 0)
                {
                    lines.Add("");
                }

                lines.Add("Active = " + (frame == vm.CurrentFrame).ToString());
                lines.Add("Entry Offset = " + frame.Offset.ToString());
                lines.Add("Registers:");
                int ri = 0;
                foreach (var reg in frame.Registers)
                {
                    if (reg.Type != VMType.None)
                    {
                        lines.Add($"\tR{ri} = {reg}");
                    }

                    ri++;
                }
                ct++;
            }
            lines.Add("");

            var disasm = temp.Instructions.Select(inst => inst.ToString());
            lines.Add(Header("DISASM"));
            lines.AddRange(disasm);
            lines.Add("");

            var path = Directory.GetCurrentDirectory() + "\\" + "vm_dump.txt";
            System.Diagnostics.Debug.WriteLine("Dumped VM data: " + path);
            File.WriteAllLines(path, lines.ToArray());
        }
    }
#endif

    public abstract class VirtualMachine
    {
        public const int DefaultRegisterCount = 32; // TODO temp hack, this should be 4
        public const int MaxRegisterCount = 32;

        public bool ThrowOnFault = false;

        public readonly Stack<VMObject> Stack = new Stack<VMObject>();

        public readonly byte[] entryScript;
        public Address EntryAddress { get; private set; }

        public readonly ExecutionContext entryContext;
        public ExecutionContext CurrentContext { get; private set; }

        private Dictionary<string, ExecutionContext> _contextList = new Dictionary<string, ExecutionContext>();

        public readonly Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public ExecutionFrame CurrentFrame { get; private set; }

        public VirtualMachine(byte[] script)
        {
            Throw.IfNull(script, nameof(script));

            this.EntryAddress = Address.FromScript(script);
            this.entryContext = new ScriptContext(script);
            RegisterContext("entry", this.entryContext); // TODO this should be a constant

            this.entryScript = script;
        }

        internal void RegisterContext(string contextName, ExecutionContext context)
        {
            _contextList[contextName] = context;
        }

        public abstract ExecutionState ExecuteInterop(string method);
        public abstract ExecutionContext LoadContext(string contextName);

        public virtual ExecutionState Execute()
        {
            return SwitchContext(entryContext, 0);
        }

        #region FRAMES

        // instructionPointer is the location to jump after the frame is popped!
        internal void PushFrame(ExecutionContext context, uint instructionPointer,  int registerCount)
        {
            var frame = new ExecutionFrame(this, instructionPointer, context, registerCount);
            frames.Push(frame);
            this.CurrentFrame = frame;
        }

        internal uint PopFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            frames.Pop();
            var instructionPointer = CurrentFrame.Offset;

            this.CurrentFrame = frames.Peek();
            this.CurrentContext = CurrentFrame.Context;

            return instructionPointer;
        }

        internal ExecutionFrame PeekFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            // TODO do this without pop/push
            var temp = frames.Pop();
            var result = frames.Peek();
            frames.Push(temp);

            return result;
        }

        internal ExecutionContext FindContext(string contextName)
        {
            if (_contextList.ContainsKey(contextName))
            {
                return _contextList[contextName];
            }

            var result = LoadContext(contextName);
            if (result == null)
            {
                return null;
            }

            _contextList[contextName] = result;

            return result;
        }

        public virtual ExecutionState ValidateOpcode(Opcode opcode)
        {
            return ExecutionState.Running;
        }

        internal ExecutionState SwitchContext(ExecutionContext context, uint instructionPointer)
        {
            this.CurrentContext = context;
            PushFrame(context, instructionPointer, DefaultRegisterCount);
            return context.Execute(this.CurrentFrame, this.Stack);
        }
        #endregion

#if DEBUG
        public virtual ExecutionState HandleException(VMDebugException ex)
        {
            throw ex;
        }
#endif
    }
}
