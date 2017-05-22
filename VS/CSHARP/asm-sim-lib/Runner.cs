﻿// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmSim.Mnemonics;
using AsmTools;
using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AsmSim
{
    public static class Runner
    {
        public static ExecutionTree Construct_ExecutionTree_Forward(
            CFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLineNumber))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree2_Forward: startLine " + startLineNumber + " does not exist in " + flow);
                return null;
            }
            Stack<string> nextKeys = new Stack<string>();
            int id = 0;

            // Get the head of the current state, this head will be the prevKey of the update, nextKey is fresh. 
            // When state is updated, tail is not changed; head is set to the fresh nextKey.

            #region Create the Root node
            ExecutionTree stateTree;
            {
                string rootKey = "!" + id++; // Tools.CreateKey(tools.Rand);
                nextKeys.Push(rootKey);
                stateTree = new ExecutionTree(rootKey, true, tools);
                stateTree.Add_Vertex(rootKey, startLineNumber, 0);
            }
            #endregion

            while (nextKeys.Count > 0)
            {
                string prevKey = nextKeys.Pop();
                int step = stateTree.Step(prevKey);
                if (step <= maxSteps)
                {
                    int currentLineNumber = stateTree.LineNumber(prevKey);
                    if (flow.HasLine(currentLineNumber))
                    {
                        string nextKey = "!" + id++;// Tools.CreateKey(tools.Rand);
                        string nextKeyBranch = nextKey + "B";

                        var updates = Execute(flow, currentLineNumber, (prevKey, prevKey, nextKey, nextKeyBranch), tools);
                        var nextLineNumber = flow.Get_Next_LineNumber(currentLineNumber);

                        int nextStep = step + 1;

                        HandleBranch_LOCAL(currentLineNumber, nextLineNumber.Branch, nextStep, updates.Branch);
                        HandleRegular_LOCAL(currentLineNumber, nextLineNumber.Regular, nextStep, updates.Regular);
                    }
                }
            }
            void HandleBranch_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a branch yet a branch is computed");
                        throw new Exception();
                    }

                    string prevKey = update.PrevKey;
                    string nextKey = update.NextKey;

                    stateTree.Add_Vertex(nextKey, nextLineNumber, nextStep);
                    stateTree.Add_Edge(true, update);

                    //if (nextState.IsConsistent) 
                    nextKeys.Push(nextKey);

                    if (!tools.Quiet) Console.WriteLine("=====================================");
                    if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + flow.Get_Line_Str(currentLineNumber) + "\" Branches to LINE " + nextLineNumber);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + update);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + stateTree.State_After(nextKey));
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a branch yet no branch is computed");
                    throw new Exception();
                }
            }
            void HandleRegular_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a continueyet a continue is computed");
                        throw new Exception();
                    }

                    string prevKey = update.PrevKey;
                    string nextKey = update.NextKey;

                    stateTree.Add_Vertex(nextKey, nextLineNumber, nextStep);
                    stateTree.Add_Edge(false, update);

                    //if (nextState.IsConsistent) 
                    nextKeys.Push(nextKey);

                    if (!tools.Quiet) Console.WriteLine("=====================================");
                    if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + flow.Get_Line_Str(currentLineNumber) + "\" Continues to LINE " + nextLineNumber);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + update);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + stateTree.State_After(nextKey));
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a regular continue yet no continue is computed");
                    throw new Exception();
                }
            }
            return stateTree;
        }

        public static ExecutionTree Construct_ExecutionTree_Backward(
            CFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            return Construct_ExecutionTree_Backward_OLD(flow, startLineNumber, maxSteps, tools);
            //return Construct_ExecutionTree_Backward_Exp(flow, startLineNumber, maxSteps, tools);
        }

        public static ExecutionTree Construct_ExecutionTree_Backward_OLD(
            CFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            if (!flow.Has_Prev_LineNumber(startLineNumber))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree_Backward: startLine " + startLineNumber + " does not exist in " + flow);
                return null;
            }

            Queue<string> prevKeys = new Queue<string>();
            int id = 0;

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            string rootKey = "!" + id--; // Tools.CreateKey(tools.Rand);

            ExecutionTree stateTree;
            {
                prevKeys.Enqueue(rootKey);
                stateTree = new ExecutionTree(rootKey, false, tools);
                stateTree.Add_Vertex(rootKey, startLineNumber, 0);
            }
            #endregion

            while (prevKeys.Count > 0)
            {
                string nextKey = prevKeys.Dequeue();
                int step = stateTree.Step(nextKey);
                if (step <= maxSteps)
                {
                    int nextStep = step + 1;
                    int currentLineNumber = stateTree.LineNumber(nextKey);

                    foreach (var prev in flow.Get_Prev_LineNumber(currentLineNumber))
                    {
                        int prev_LineNumber = prev.LineNumber;
                        if (flow.HasLine(prev_LineNumber))
                        {
                            string prevKey = "!" + id--;// Tools.CreateKey(tools.Rand);

                            var updates = Runner.Execute(flow, prev.LineNumber, (prevKey, prevKey, nextKey, nextKey), tools);
                            var update = (prev.IsBranch) ? updates.Branch : updates.Regular;

                            stateTree.Add_Vertex(prevKey, prev.LineNumber, nextStep);
                            stateTree.Add_Edge(prev.IsBranch, update);

                            //if (nextState.IsConsistent) 
                            prevKeys.Enqueue(prevKey); // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                            if (!tools.Quiet) Console.WriteLine("=====================================");
                            if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: stepNext " + nextStep + ": LINE " + prev_LineNumber + ": \"" + flow.Get_Line_Str(prev_LineNumber));
                            if (!tools.Quiet && flow.Get_Line(prev_LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + update);
                            //if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + stateTree.State_After(rootKey));
                        }
                    }
                }
            }
            return stateTree;
        }

        public static ExecutionTree Construct_ExecutionTree_Pseudo_Backward_1(
            CFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            int id = 0;
            ExecutionTree tree = new ExecutionTree("!" + id++, true, tools);

            IList<int> startLines = Get_Start_Lines_LOCAL(startLineNumber, maxSteps);

            if (startLines.Count == 0)
            {
                throw new Exception();
            }
            else if (startLines.Count == 1)
            {
                Execute_Forward_LOCAL(startLines[0], startLineNumber);
            } 
            else if (startLines.Count == 2)
            {

            }
            else
            {

            }


            return tree;


            #region Local Functions
            IList<int> Get_Start_Lines_LOCAL(int endLineNumber, int step)
            {
                return null;
            }

            void Execute_Forward_LOCAL(int firstLineNumber, int endLineNumber)
            {

            }


            #endregion
        }

        public static ExecutionTree Construct_ExecutionTree_Pseudo_Backward_2(
            CFlow flow,
            int startLineNumber,
            int maxSteps,
            Tools tools)
        {
            int id = 0;
            ExecutionTree tree = new ExecutionTree("!"+id++, true, tools);
            Update_Pseudo_Backward_LOCAL(startLineNumber);
            return tree;

            string Update_Pseudo_Backward_LOCAL(int lineNumber) {

                if (flow.IsMergePoint(lineNumber))
                {
                    int inFlow = flow.Number_Prev_LineNumbers(lineNumber);

                    if (inFlow < 2) throw new Exception();
                    if (inFlow == 2)
                    {
                        //string prev1Key = Update_Pseudo_Backward_LOCAL(prevs[0].LineNumber);
                        ///string prev2Key = Update_Pseudo_Backward_LOCAL(prevs[1].LineNumber);


                    }
                    else
                    {
                        Console.WriteLine("Not implemented yet");
                    }
                }
                else if (flow.IsBranchPoint(lineNumber))
                {

                } 
                else if (flow.Has_Prev_LineNumber(lineNumber))
                {
                    var prev = flow.Get_Prev_LineNumber(lineNumber).First();
                    string prevKey = Update_Pseudo_Backward_LOCAL(prev.LineNumber);
                    string nextKey = Update_Forward_LOCAL(prev.LineNumber, prevKey);
                    return nextKey;
                }
                return "!INIT";
            }

            string Update_Forward_LOCAL(int lineNumber, string prevKey)
            {
                string nextKey = "!" + id++;// Tools.CreateKey(tools.Rand);
                string nextKeyBranch = nextKey + "B";

                var updates = Execute(flow, lineNumber, (prevKey, prevKey, nextKey, nextKeyBranch), tools);
                var nextLineNumber = flow.Get_Next_LineNumber(lineNumber);
                int nextStep = tree.Step(prevKey) + 1;

//                HandleBranch_LOCAL(lineNumber, nextLineNumber.Branch, nextStep, updates.Branch);
                HandleRegular_LOCAL(lineNumber, nextLineNumber.Regular, nextStep, updates.Regular);

                return nextKey;
            }

            void HandleBranch_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a branch yet a branch is computed");
                        throw new Exception();
                    }

                    string prevKey = update.PrevKey;
                    string nextKey = update.NextKey;

                    tree.Add_Vertex(nextKey, nextLineNumber, nextStep);
                    tree.Add_Edge(true, update);

                    if (!tools.Quiet) Console.WriteLine("=====================================");
                    if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + flow.Get_Line_Str(currentLineNumber) + "\" Branches to LINE " + nextLineNumber);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + update);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + tree.State_After(nextKey));
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a branch yet no branch is computed");
                    throw new Exception();
                }
            }
            void HandleRegular_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a continueyet a continue is computed");
                        throw new Exception();
                    }

                    string prevKey = update.PrevKey;
                    string nextKey = update.NextKey;

                    tree.Add_Vertex(nextKey, nextLineNumber, nextStep);
                    tree.Add_Edge(false, update);

                    if (!tools.Quiet) Console.WriteLine("=====================================");
                    if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + flow.Get_Line_Str(currentLineNumber) + "\" Continues to LINE " + nextLineNumber);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + update);
                    if (!tools.Quiet && flow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + tree.State_After(nextKey));
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a regular continue yet no continue is computed");
                    throw new Exception();
                }
            }
        }


        /// <summary>Perform one step forward and return the regular branch</summary>
        public static State SimpleStep_Forward(string line, State state)
        {
            string nextKey = Tools.CreateKey(state.Tools.Rand);
            string nextKeyBranch = nextKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (state.HeadKey, state.HeadKey, nextKey, nextKeyBranch), state.Tools);
            if (opcodeBase == null) return null;
            if (opcodeBase.IsHalted) return null;

            opcodeBase.Execute();
            State stateOut = new State(state);
            stateOut.Update_Forward(opcodeBase.Updates.Regular);
            if (!state.Tools.Quiet) Console.WriteLine("INFO: Runner:SimpleStep_Forward: after \"" + line + "\" we know:");
            if (!state.Tools.Quiet) Console.WriteLine(stateOut);
            return stateOut;
        }

        /// <summary>Perform onestep forward and return the state of the regular branch</summary>
        public static State SimpleStep_Backward(string line, State state)
        {
            string prevKey = Tools.CreateKey(state.Tools.Rand);
            string prevKeyBranch = prevKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (prevKey, prevKeyBranch, state.TailKey, state.TailKey), state.Tools);
            if (opcodeBase == null) return null;
            if (opcodeBase.IsHalted) return null;

            opcodeBase.Execute();
            State stateOut = new State(state);
            stateOut.Update_Backward(opcodeBase.Updates.Regular);
            if (!state.Tools.Quiet) Console.WriteLine("INFO: Runner:SimpleStep_Backward: after \"" + line + "\" we know:");
            if (!state.Tools.Quiet) Console.WriteLine(stateOut);
            return stateOut;
        }

        /// <summary>Perform one step forward and return states for both branches</summary>
        public static (State Regular, State Branch) Step_Forward(string line, State state)
        {
            string nextKey = Tools.CreateKey(state.Tools.Rand);
            string nextKeyBranch = nextKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (state.HeadKey, state.HeadKey, nextKey, nextKeyBranch), state.Tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);
            if (opcodeBase.IsHalted) return (Regular: null, Branch: null);

            opcodeBase.Execute();
            State stateRegular = null;
            if (opcodeBase.Updates.Regular != null)
            {
                stateRegular = new State(state);
                stateRegular.Update_Forward(opcodeBase.Updates.Regular);
            }
            State stateBranch = null;
            if (opcodeBase.Updates.Branch != null)
            {
                stateBranch = new State(state);
                stateBranch.Update_Forward(opcodeBase.Updates.Branch);
            }
            return (Regular: stateRegular, Branch: stateBranch);
        }

        public static (StateUpdate Regular, StateUpdate Branch) Execute(
            CFlow flow,
            int lineNumber,
            (string prevKey, string prevKeyBranch, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            var content = flow.Get_Line(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, keys, tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);
            if (opcodeBase.IsHalted) return (Regular: null, Branch: null);

            opcodeBase.Execute();
            return opcodeBase.Updates;
        }

        /// <summary>Get the branch condition for the provided lineNumber</summary>
        public static (BoolExpr Regular, BoolExpr Branch) GetBranchCondition(
            CFlow flow,
            int lineNumber,
            (string prevKey, string prevKeyBranch, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            var content = flow.Get_Line(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, keys, tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);

            throw new NotImplementedException();
        }

        public static StateConfig GetUsage_StateConfig(
            CFlow flow,
            int lineNumberBegin,
            int lineNumberEnd,
            Tools tools)
        {
            StateConfig config = new StateConfig();
            config.Set_All_Off();
            var usage = GetUsage(flow, 0, flow.LastLineNumber, tools);
            config.Set_Flags_On(usage.Flags);
            foreach (Rn reg in usage.Regs) config.Set_Reg_On(reg);
            config.mem = usage.Mem;
            return config;
        }
        public static (ISet<Rn> Regs, Flags Flags, bool Mem) GetUsage(
            CFlow flow,
            int lineNumberBegin,
            int lineNumberEnd,
            Tools tools)
        {
            ISet<Rn> regs = new HashSet<Rn>();
            Flags flags = Flags.NONE;
            bool mem = false;
            var dummyKeys = ("", "", "", "");
            for (int lineNumber = lineNumberBegin; lineNumber <= lineNumberEnd; lineNumber++)
            {
                var content = flow.Get_Line(lineNumber);
                var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, tools);
                if (opcodeBase != null)
                {
                    flags |= (opcodeBase.FlagsReadStatic | opcodeBase.FlagsWriteStatic);
                    foreach (Rn r in opcodeBase.RegsReadStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                    foreach (Rn r in opcodeBase.RegsWriteStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                    mem |= opcodeBase.MemReadWriteStatic;
                }
            }
            return (Regs: regs, Flags: flags, Mem: mem);
        }

        public static OpcodeBase InstantiateOpcode(
            Mnemonic mnemonic,
            string[] args,
            (string prevKey, string prevKeyBranch, string nextKey, string nextKeyBranch) keys,
            Tools t)
        {
            switch (mnemonic)
            {
                #region NonSse
                case Mnemonic.NONE:
                case Mnemonic.UNKNOWN: return new Ignore(mnemonic, args, keys, t);
                case Mnemonic.MOV: return new Mov(args, keys, t);
                case Mnemonic.CMOVE:
                case Mnemonic.CMOVZ:
                case Mnemonic.CMOVNE:
                case Mnemonic.CMOVNZ:
                case Mnemonic.CMOVA:
                case Mnemonic.CMOVNBE:
                case Mnemonic.CMOVAE:
                case Mnemonic.CMOVNB:
                case Mnemonic.CMOVB:
                case Mnemonic.CMOVNAE:
                case Mnemonic.CMOVBE:
                case Mnemonic.CMOVNA:
                case Mnemonic.CMOVG:
                case Mnemonic.CMOVNLE:
                case Mnemonic.CMOVGE:
                case Mnemonic.CMOVNL:
                case Mnemonic.CMOVL:
                case Mnemonic.CMOVNGE:
                case Mnemonic.CMOVLE:
                case Mnemonic.CMOVNG:
                case Mnemonic.CMOVC:
                case Mnemonic.CMOVNC:
                case Mnemonic.CMOVO:
                case Mnemonic.CMOVNO:
                case Mnemonic.CMOVS:
                case Mnemonic.CMOVNS:
                case Mnemonic.CMOVP:
                case Mnemonic.CMOVPE:
                case Mnemonic.CMOVNP:
                case Mnemonic.CMOVPO: return new Cmovcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.XCHG: break;
                case Mnemonic.BSWAP: break;
                case Mnemonic.XADD: return new Xadd(args, keys, t);
                case Mnemonic.CMPXCHG: break;
                case Mnemonic.CMPXCHG8B: break;
                case Mnemonic.PUSH: return new Push(args, keys, t);
                case Mnemonic.POP: return new Pop(args, keys, t);
                case Mnemonic.PUSHA: break;
                case Mnemonic.PUSHAD: break;
                case Mnemonic.POPA: break;
                case Mnemonic.POPAD: break;
                case Mnemonic.CWD: return new Cwd(args, keys, t);
                case Mnemonic.CDQ: return new Cdq(args, keys, t);
                case Mnemonic.CBW: return new Cbw(args, keys, t);
                case Mnemonic.CWDE: return new Cwde(args, keys, t);
                case Mnemonic.CQO: return new Cqo(args, keys, t);
                case Mnemonic.MOVSX: return new Movsx(args, keys, t);
                case Mnemonic.MOVSXD: return new Movsxd(args, keys, t);
                case Mnemonic.MOVZX: return new Movzx(args, keys, t);
                //case Mnemonic.MOVZXD: return new Movzxd(args, keys, t);
                case Mnemonic.ADCX: return new Adcx(args, keys, t);
                case Mnemonic.ADOX: return new Adox(args, keys, t);
                case Mnemonic.ADD: return new Add(args, keys, t);
                case Mnemonic.ADC: return new Adc(args, keys, t);
                case Mnemonic.SUB: return new Sub(args, keys, t);
                case Mnemonic.SBB: return new Sbb(args, keys, t);
                case Mnemonic.IMUL: return new Imul(args, keys, t);
                case Mnemonic.MUL: return new Mul(args, keys, t);
                case Mnemonic.IDIV: return new Idiv(args, keys, t);
                case Mnemonic.DIV: return new Div(args, keys, t);
                case Mnemonic.INC: return new Inc(args, keys, t);
                case Mnemonic.DEC: return new Dec(args, keys, t);
                case Mnemonic.NEG: return new Neg(args, keys, t);
                case Mnemonic.CMP: return new Cmp(args, keys, t);

                case Mnemonic.DAA: break;
                case Mnemonic.DAS: break;
                case Mnemonic.AAA: break;
                case Mnemonic.AAS: break;
                case Mnemonic.AAM: break;
                case Mnemonic.AAD: break;

                case Mnemonic.AND: return new And(args, keys, t);
                case Mnemonic.OR: return new Or(args, keys, t);
                case Mnemonic.XOR: return new Xor(args, keys, t);
                case Mnemonic.NOT: return new Not(args, keys, t);
                case Mnemonic.SAR: return new Sar(args, keys, t);
                case Mnemonic.SHR: return new Shr(args, keys, t);
                case Mnemonic.SAL: return new Sal(args, keys, t);
                case Mnemonic.SHL: return new Shl(args, keys, t);
                case Mnemonic.SHRD: return new Shrd(args, keys, t);
                case Mnemonic.SHLD: return new Shld(args, keys, t);
                case Mnemonic.ROR: return new Ror(args, keys, t);
                case Mnemonic.ROL: return new Rol(args, keys, t);
                case Mnemonic.RCR: return new Rcr(args, keys, t);
                case Mnemonic.RCL: return new Rcl(args, keys, t);

                case Mnemonic.BT: return new Bt_Opcode(args, keys, t);
                case Mnemonic.BTS: return new Bts(args, keys, t);
                case Mnemonic.BTR: return new Btr(args, keys, t);
                case Mnemonic.BTC: return new Btc(args, keys, t);
                case Mnemonic.BSF: return new Bsf(args, keys, t);
                case Mnemonic.BSR: return new Bsr(args, keys, t);
                case Mnemonic.TEST: return new Test(args, keys, t);
                case Mnemonic.CRC32: break;//return new Crc32(args, keys, t);

                case Mnemonic.SETE:
                case Mnemonic.SETZ:
                case Mnemonic.SETNE:
                case Mnemonic.SETNZ:
                case Mnemonic.SETA:
                case Mnemonic.SETNBE:
                case Mnemonic.SETAE:
                case Mnemonic.SETNB:
                case Mnemonic.SETNC:
                case Mnemonic.SETB:
                case Mnemonic.SETNAE:
                case Mnemonic.SETC:
                case Mnemonic.SETBE:
                case Mnemonic.SETNA:
                case Mnemonic.SETG:
                case Mnemonic.SETNLE:
                case Mnemonic.SETGE:
                case Mnemonic.SETNL:
                case Mnemonic.SETL:
                case Mnemonic.SETNGE:
                case Mnemonic.SETLE:
                case Mnemonic.SETNG:
                case Mnemonic.SETS:
                case Mnemonic.SETNS:
                case Mnemonic.SETO:
                case Mnemonic.SETNO:
                case Mnemonic.SETPE:
                case Mnemonic.SETP:
                case Mnemonic.SETNP:
                case Mnemonic.SETPO: return new Setcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.JMP: return new Jmp(args, keys, t);

                case Mnemonic.JE:
                case Mnemonic.JZ:
                case Mnemonic.JNE:
                case Mnemonic.JNZ:
                case Mnemonic.JA:
                case Mnemonic.JNBE:
                case Mnemonic.JAE:
                case Mnemonic.JNB:
                case Mnemonic.JB:
                case Mnemonic.JNAE:
                case Mnemonic.JBE:
                case Mnemonic.JNA:
                case Mnemonic.JG:
                case Mnemonic.JNLE:
                case Mnemonic.JGE:
                case Mnemonic.JNL:
                case Mnemonic.JL:
                case Mnemonic.JNGE:
                case Mnemonic.JLE:
                case Mnemonic.JNG:
                case Mnemonic.JC:
                case Mnemonic.JNC:
                case Mnemonic.JO:
                case Mnemonic.JNO:
                case Mnemonic.JS:
                case Mnemonic.JNS:
                case Mnemonic.JPO:
                case Mnemonic.JNP:
                case Mnemonic.JPE:
                case Mnemonic.JP:
                case Mnemonic.JCXZ:
                case Mnemonic.JECXZ:
                case Mnemonic.JRCXZ: return new Jmpcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.LOOP: return new Loop(args, keys, t);
                case Mnemonic.LOOPZ: return new Loopz(args, keys, t);
                case Mnemonic.LOOPE: return new Loope(args, keys, t);
                case Mnemonic.LOOPNZ: return new Loopnz(args, keys, t);
                case Mnemonic.LOOPNE: return new Loopne(args, keys, t);

                case Mnemonic.CALL: break;// return new Call(args, keys, t);
                case Mnemonic.RET: break; // return new Ret(args, keys, t);
                case Mnemonic.IRET: break;
                case Mnemonic.INT: break;
                case Mnemonic.INTO: break;
                case Mnemonic.BOUND: break;
                case Mnemonic.ENTER: break;
                case Mnemonic.LEAVE: break;
                case Mnemonic.MOVS: break;
                case Mnemonic.MOVSB: break;
                case Mnemonic.MOVSW: break;
                case Mnemonic.MOVSD: break;
                case Mnemonic.CMPS: break;
                case Mnemonic.CMPSB: break;
                case Mnemonic.CMPSW: break;
                case Mnemonic.CMPSD: break;
                case Mnemonic.SCAS: break;
                case Mnemonic.SCASB: break;
                case Mnemonic.SCASW: break;
                case Mnemonic.SCASD: break;
                case Mnemonic.LODS: break;
                case Mnemonic.LODSB: break;
                case Mnemonic.LODSW: break;
                case Mnemonic.LODSD: break;
                case Mnemonic.STOS: break;
                case Mnemonic.STOSB: break;
                case Mnemonic.STOSW: break;
                case Mnemonic.STOSD: break;
                case Mnemonic.REP: break;
                case Mnemonic.REPE: break;
                case Mnemonic.REPZ: break;
                case Mnemonic.REPNE: break;
                case Mnemonic.REPNZ: break;
                case Mnemonic.IN: return new In(args, keys, t);
                case Mnemonic.OUT: return new Out(args, keys, t);
                case Mnemonic.INS: break;
                case Mnemonic.INSB: break;
                case Mnemonic.INSW: break;
                case Mnemonic.INSD: break;
                case Mnemonic.OUTS: break;
                case Mnemonic.OUTSB: break;
                case Mnemonic.OUTSW: break;
                case Mnemonic.OUTSD: break;
                case Mnemonic.STC: return new Stc(args, keys, t);
                case Mnemonic.CLC: return new Clc(args, keys, t);
                case Mnemonic.CMC: return new Cmc(args, keys, t);
                case Mnemonic.CLD: break;
                case Mnemonic.STD: break;
                case Mnemonic.LAHF: return new Lahf(args, keys, t);
                case Mnemonic.SAHF: return new Sahf(args, keys, t);
                case Mnemonic.PUSHF: break;
                case Mnemonic.PUSHFD: break;
                case Mnemonic.POPF: break;
                case Mnemonic.POPFD: break;
                case Mnemonic.STI: break;
                case Mnemonic.CLI: break;
                case Mnemonic.LDS: break;
                case Mnemonic.LES: break;
                case Mnemonic.LFS: break;
                case Mnemonic.LGS: break;
                case Mnemonic.LSS: break;
                case Mnemonic.LEA: return new Lea(args, keys, t);
                case Mnemonic.NOP: return new Nop(args, keys, t);
                case Mnemonic.UD2: return new Nop(args, keys, t);
                case Mnemonic.XLAT: break;
                case Mnemonic.XLATB: break;
                case Mnemonic.CPUID: break;
                case Mnemonic.MOVBE: break;
                case Mnemonic.PREFETCHW: return new Nop(args, keys, t);
                case Mnemonic.PREFETCHWT1: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSH: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSHOPT: return new Nop(args, keys, t);
                case Mnemonic.XSAVE: break;
                case Mnemonic.XSAVEC: break;
                case Mnemonic.XSAVEOPT: break;
                case Mnemonic.XRSTOR: break;
                case Mnemonic.XGETBV: break;
                case Mnemonic.RDRAND: break;
                case Mnemonic.RDSEED: break;
                case Mnemonic.ANDN: break;
                case Mnemonic.BEXTR: break;
                case Mnemonic.BLSI: break;
                case Mnemonic.BLSMSK: break;
                case Mnemonic.BLSR: break;
                case Mnemonic.BZHI: break;
                case Mnemonic.LZCNT: break;
                case Mnemonic.MULX: break;
                case Mnemonic.PDEP: break;
                case Mnemonic.PEXT: break;
                case Mnemonic.RORX: return new Rorx(args, keys, t);
                case Mnemonic.SARX: return new Sarx(args, keys, t);
                case Mnemonic.SHLX: return new Shlx(args, keys, t);
                case Mnemonic.SHRX: return new Shrx(args, keys, t);
                case Mnemonic.TZCNT: break;

                #endregion NonSse

                #region SSE
                //case Mnemonic.ADDPD: return new AddPD(args, keys, t); 
                case Mnemonic.POPCNT: return new Popcnt(args, keys, t);

                #endregion SSE

                default: return new NotImplemented(mnemonic, args, keys, t);
            }
            return new NotImplemented(mnemonic, args, keys, t);
        }
    }
}
