﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmSim;
using Microsoft.Z3;
using AsmTools;
using System.Diagnostics;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_MemZ3
    {
        const bool logToDisplay = TestTools.LOG_TO_DISPLAY;

        private Tools CreateTools(int timeOut = TestTools.DEFAULT_TIMEOUT)
        {
            /* The following parameters can be set: 
                    - proof (Boolean) Enable proof generation
                    - debug_ref_count (Boolean) Enable debug support for Z3_ast reference counting
                    - trace (Boolean) Tracing support for VCC 
                    - trace_file_name (String) Trace out file for VCC traces 
                    - timeout (unsigned) default timeout (in milliseconds) used for solvers 
                    - well_sorted_check type checker 
                    - auto_config use heuristics to automatically select solver and configure it 
                    - model model generation for solvers, this parameter can be overwritten when creating a solver 
                    - model_validate validate models produced by solvers 
                    - unsat_core unsat-core generation for solvers, this parameter can be overwritten when creating 
                            a solver Note that in previous versions of Z3, this constructor was also used to set 
                            global and module parameters. For this purpose we should now use 
                            Microsoft.Z3.Global.SetParameter(System.String,System.String)
            */

            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "unsat_core", "false" },    // enable generation of unsat cores
                { "model", "false" },         // enable model generation
                { "proof", "false" },         // enable proof generation
                { "timeout", timeOut.ToString() }
            };
            return new Tools(settings);
        }

        private State CreateState(StateConfig stateConfig)
        {
            Tools tools = CreateTools();
            tools.StateConfig = stateConfig;
            return CreateState(tools);
        }

        private State CreateState(Tools tools)
        {
            string tailKey = "!0";// Tools.CreateKey(tools.Rand);
            string headKey = tailKey;
            return new State(tools, tailKey, headKey);
        }

        [TestMethod]
        public void Test_MemZ3_Forward_SetGet0()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax]", state.HeadKey, tools, ctx);
            BitVecExpr value1 = state.Create(Rn.RBX);

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value1);
                state.Update_Forward(updateState);
            }
            BitVecExpr value2 = state.Create_Mem(address1, 8);

            if (logToDisplay)
            {
                Console.WriteLine("value1 = " + value1);
                Console.WriteLine("value2 = " + value2);
                Console.WriteLine(state);
            }
            Assert.AreEqual(Tv.ONE, state.EqualValues(value1, value2));
        }

        [TestMethod]
        public void Test_MemZ3_Forward_SetGet1()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.R8 = true;
            stateConfig.R9 = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax]", state.HeadKey, tools, ctx);
            BitVecExpr value1a = state.Create(Rn.R8);
            BitVecExpr value2a = state.Create(Rn.R9);

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value1a);
                state.Update_Forward(updateState);
            }
            BitVecExpr value1b = state.Create_Mem(address1, 8);
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value2a);
                state.Update_Forward(updateState);
            }
            BitVecExpr value2b = state.Create_Mem(address1, 8);

            if (logToDisplay)
            {
                Console.WriteLine("value1a = " + value1a);
                Console.WriteLine("value1b = " + value1b);
                Console.WriteLine("value2a = " + value2a);
                Console.WriteLine("value2b = " + value2b);
                Console.WriteLine(state);
            }
            Assert.AreEqual(Tv.ONE, state.EqualValues(value1a, value1b));
            Assert.AreEqual(Tv.ONE, state.EqualValues(value2a, value2b));
        }

        [TestMethod]
        public void Test_MemZ3_Forward_Eq1()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.RCX = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                //updateState.Set(Rn.RAX, 20);
                updateState.Set(Rn.RBX, 10);
                updateState.Set(Rn.RCX, 5);
                state.Update_Forward(updateState);
            }
            //TestTools.AreEqual(Rn.RAX, 20, state);
            TestTools.AreEqual(Rn.RBX, 10, state);
            TestTools.AreEqual(Rn.RCX, 5, state);

            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax + 2 * rbx + 10]", state.HeadKey, tools, ctx);
            BitVecExpr address2 = Tools.Calc_Effective_Address("qword ptr[rax + 4 * rcx + 10]", state.HeadKey, tools, ctx);

            Tv equalAddresses = state.EqualValues(address1, address2);
            if (logToDisplay)
            {
                Console.WriteLine("equalAddresses=" + equalAddresses);
                Console.WriteLine("address1 = " + address1);
                Console.WriteLine("address2 = " + address2);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ONE, equalAddresses);

            BitVecExpr value1 = state.Create(Rn.R8);

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value1);
                state.Update_Forward(updateState);
            }
            BitVecExpr value2 = state.Create_Mem(address2, 8);

            if (logToDisplay)
            {
                Console.WriteLine("value1 = " + value1);
                Console.WriteLine("value2 = " + value2);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ONE, state.EqualValues(value1, value2));
        }

        [TestMethod]
        public void Test_MemZ3_Forward_Eq2()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.RCX = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set(Rn.RBX, 10);
                updateState.Set(Rn.RCX, 5 + 1);
                state.Update_Forward(updateState);
            }

            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax + 2 * rbx + 10]", state.HeadKey, tools, ctx);
            BitVecExpr address2 = Tools.Calc_Effective_Address("qword ptr[rax + 4 * rcx + 10]", state.HeadKey, tools, ctx);
            Tv equalAddresses = state.EqualValues(address1, address2);

            if (logToDisplay)
            {
                Console.WriteLine("equalAddresses=" + equalAddresses + "; expected = ZERO");
                Console.WriteLine("address1 = " + address1);
                Console.WriteLine("address2 = " + address2);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ZERO, equalAddresses);

            BitVecExpr value1 = state.Create(Rn.R8);
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value1);
                state.Update_Forward(updateState);
            }

            BitVecExpr value2 = state.Create_Mem(address2, 8);

            Tv equalValues = state.EqualValues(value1, value2);
            if (logToDisplay)
            {
                Console.WriteLine("equalValues=" + equalValues);
                Console.WriteLine("value1 = " + value1);
                Console.WriteLine("value2 = " + value2);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.UNKNOWN, equalValues);
        }

        [TestMethod]
        public void Test_MemZ3_Forward_Eq3()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.RCX = true;
            stateConfig.R8 = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set(Rn.RBX, 10);
                updateState.Set(Rn.RCX, 5);
                state.Update_Forward(updateState);
            }
            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax + 2 * rbx + 10]", state.HeadKey, tools, ctx);
            {
                StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools);
                updateState.Set(Rn.RAX, state.Ctx.MkBVAdd(state.Create(Rn.RAX), state.Ctx.MkBV(0, 64)));
                state.Update_Forward(updateState);
            }            
            BitVecExpr address2 = Tools.Calc_Effective_Address("qword ptr[rax + 4 * rcx + 10]", state.HeadKey, tools, ctx);

            BitVecExpr value1 = state.Create(Rn.R8B);
            int nBytes = (int)value1.SortSize >> 3;
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value1);
                state.Update_Forward(updateState);
            }
            BitVecExpr value2 = state.Create_Mem(address2, nBytes);

            if (logToDisplay)
            {
                Console.WriteLine("value1 = " + value1);
                Console.WriteLine("value2 = " + value2);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ONE, state.EqualValues(value1, value2));
        }

        [TestMethod]
        public void Test_MemZ3_Forward_Eq4()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.RCX = true;
            stateConfig.RDX = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set(Rn.RAX, state.Create(Rn.RBX));
                state.Update_Forward(updateState);
            }
            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax]", state.HeadKey, tools, ctx);
            BitVecExpr address2 = Tools.Calc_Effective_Address("qword ptr[rbx]", state.HeadKey, tools, ctx);
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, state.Create(Rn.RCX));
                state.Update_Forward(updateState);
            }
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address2, state.Create(Rn.RDX));
                state.Update_Forward(updateState);
            }
            BitVecExpr value1 = state.Create_Mem(address1, 1);
            BitVecExpr value2 = state.Create_Mem(address2, 1);

            if (logToDisplay)
            {
                Console.WriteLine("value1 = " + value1);
                Console.WriteLine("value2 = " + value2);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ONE, state.EqualValues(value1, value2));
        }

        [TestMethod]
        public void Test_MemZ3_Forward_Eq5()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.RCX = true;
            stateConfig.RDX = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            {
                StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools);
                updateState.Set(Rn.RAX, state.Create(Rn.RBX));
                state.Update_Forward(updateState);
            }
            BitVecExpr address1 = Tools.Calc_Effective_Address("byte ptr[rax]", state.HeadKey, tools, ctx);
            BitVecExpr address2 = Tools.Calc_Effective_Address("byte ptr[rbx]", state.HeadKey, tools, ctx);
            BitVecExpr value1a = state.Create(Rn.CL);
            BitVecExpr value2a = state.Create(Rn.DL);

            Debug.Assert(value1a.SortSize == value2a.SortSize);
            int nBytes = (int)value1a.SortSize >> 3;
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address1, value1a);
                state.Update_Forward(updateState);
            }
            BitVecExpr value1b = state.Create_Mem(address1, nBytes);
            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set_Mem(address2, value2a);
                state.Update_Forward(updateState);
            }
            BitVecExpr value2b = state.Create_Mem(address2, nBytes);

            if (logToDisplay)
            {
                Console.WriteLine("value1a = " + value1a);
                Console.WriteLine("value2a = " + value2a);
                Console.WriteLine("value1b = " + value1b);
                Console.WriteLine("value2b = " + value2b);
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ONE, state.EqualValues(value1a, value1b));
            TestTools.AreEqual(Tv.ONE, state.EqualValues(value2a, value2b));
        }

        [TestMethod]
        public void Test_MemZ3_Forward_Eq6()
        {
            StateConfig stateConfig = new StateConfig();
            stateConfig.Set_All_Off();
            stateConfig.RAX = true;
            stateConfig.RBX = true;
            stateConfig.RCX = true;
            stateConfig.RDX = true;
            stateConfig.mem = true;

            State state = CreateState(stateConfig);
            Context ctx = state.Ctx;
            Tools tools = state.Tools;

            Rn reg1 = Rn.CL;
            Rn reg2 = Rn.DL;
            int nBytes = RegisterTools.NBits(reg1) >> 3;
            Debug.Assert(RegisterTools.NBits(reg1) == RegisterTools.NBits(reg2));

            BitVecExpr address1 = Tools.Calc_Effective_Address("qword ptr[rax]", state.HeadKey, tools, ctx);
            BitVecExpr address2 = Tools.Calc_Effective_Address("qword ptr[rbx]", state.HeadKey, tools, ctx);

            BitVecExpr value1 = state.Create_Mem(address1, nBytes);
            BitVecExpr value2 = state.Create_Mem(address2, nBytes);
            Assert.AreNotEqual(value1, value2); //value1 is not equal to value2 simply because rax and rbx are not related yet

            state.Add(new BranchInfo(state.Ctx.MkEq(state.Create(Rn.RAX), state.Create(Rn.RBX)), true));
            // value1 and value2 are now (intuitively) equal; however, the retrieved memory values have not been updated yet to reflect this.

            using (StateUpdate updateState = new StateUpdate(state.HeadKey, Tools.CreateKey(tools.Rand), tools))
            {
                updateState.Set(reg1, value1);
                updateState.Set(reg2, value2);
                state.Update_Forward(updateState);
            }
            if (logToDisplay)
            {
                Console.WriteLine("value1 = " + value1);
                Console.WriteLine("value2 = " + value2);
                Console.WriteLine(reg1 + " = " + state.Create(reg1));
                Console.WriteLine(reg2 + " = " + state.Create(reg2));
                Console.WriteLine(state);
            }
            TestTools.AreEqual(Tv.ONE, state.EqualValues(reg1, reg2));
        }
    }
}
