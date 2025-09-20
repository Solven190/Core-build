using ARMeilleure.State;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using IExecutionContext = Ryujinx.Cpu.IExecutionContext;

namespace Ryujinx.HLE.Debugger
{
    public class Debugger : IDisposable
    {
        internal Switch Device { get; private set; }

        public ushort GdbStubPort { get; private set; }

        private TcpListener ListenerSocket;
        private Socket ClientSocket = null;
        private NetworkStream ReadStream = null;
        private NetworkStream WriteStream = null;
        private BlockingCollection<IMessage> Messages = new BlockingCollection<IMessage>(1);
        private Thread DebuggerThread;
        private Thread MessageHandlerThread;
        private bool _shuttingDown = false;
        private ManualResetEventSlim _breakHandlerEvent = new ManualResetEventSlim(false);

        private ulong? cThread;
        private ulong? gThread;

        private BreakpointManager BreakpointManager;

        private string previousThreadListXml = "";

        public Debugger(Switch device, ushort port)
        {
            Device = device;
            GdbStubPort = port;

            ARMeilleure.Optimizations.EnableDebugging = true;

            DebuggerThread = new Thread(DebuggerThreadMain);
            DebuggerThread.Start();
            MessageHandlerThread = new Thread(MessageHandlerMain);
            MessageHandlerThread.Start();
            BreakpointManager = new BreakpointManager(this);
        }

        internal KProcess Process => Device.System?.DebugGetApplicationProcess();
        internal IDebuggableProcess DebugProcess => Device.System?.DebugGetApplicationProcessDebugInterface();
        private KThread[] GetThreads() => DebugProcess.GetThreadUids().Select(x => DebugProcess.GetThread(x)).ToArray();
        internal bool IsProcessAarch32 => DebugProcess.GetThread(gThread.Value).Context.IsAarch32;
        private KernelContext KernelContext => Device.System.KernelContext;

        const int GdbRegisterCount64 = 68;
        const int GdbRegisterCount32 = 66;
        /* FPCR = FPSR & ~FpcrMask
        All of FPCR's bits are reserved in FPCR and vice versa,
        see ARM's documentation. */
        private const uint FpcrMask = 0xfc1fffff;

        private string GdbReadRegister64(IExecutionContext state, int gdbRegId)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 31:
                    return ToHex(BitConverter.GetBytes(state.GetX(gdbRegId)));
                case 32:
                    return ToHex(BitConverter.GetBytes(state.DebugPc));
                case 33:
                    return ToHex(BitConverter.GetBytes(state.Pstate));
                case >= 34 and <= 65:
                    return ToHex(state.GetV(gdbRegId - 34).ToArray());
                case 66:
                    return ToHex(BitConverter.GetBytes((uint)state.Fpsr));
                case 67:
                    return ToHex(BitConverter.GetBytes((uint)state.Fpcr));
                default:
                    return null;
            }
        }

        private bool GdbWriteRegister64(IExecutionContext state, int gdbRegId, StringStream ss)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 31:
                    {
                        ulong value = ss.ReadLengthAsLEHex(16);
                        state.SetX(gdbRegId, value);
                        return true;
                    }
                case 32:
                    {
                        ulong value = ss.ReadLengthAsLEHex(16);
                        state.DebugPc = value;
                        return true;
                    }
                case 33:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Pstate = (uint)value;
                        return true;
                    }
                case >= 34 and <= 65:
                    {
                        ulong value0 = ss.ReadLengthAsLEHex(16);
                        ulong value1 = ss.ReadLengthAsLEHex(16);
                        state.SetV(gdbRegId - 34, new V128(value0, value1));
                        return true;
                    }
                case 66:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Fpsr = (uint)value;
                        return true;
                    }
                case 67:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Fpcr = (uint)value;
                        return true;
                    }
                default:
                    return false;
            }
        }

        private string GdbReadRegister32(IExecutionContext state, int gdbRegId)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 14:
                    return ToHex(BitConverter.GetBytes((uint)state.GetX(gdbRegId)));
                case 15:
                    return ToHex(BitConverter.GetBytes((uint)state.DebugPc));
                case 16:
                    return ToHex(BitConverter.GetBytes((uint)state.Pstate));
                case >= 17 and <= 32:
                    return ToHex(state.GetV(gdbRegId - 17).ToArray());
                case >= 33 and <= 64:
                    int reg = (gdbRegId - 33);
                    int n = reg / 2;
                    int shift = reg % 2;
                    ulong value = state.GetV(n).Extract<ulong>(shift);
                    return ToHex(BitConverter.GetBytes(value));
                case 65:
                    uint fpscr = (uint)state.Fpsr | (uint)state.Fpcr;
                    return ToHex(BitConverter.GetBytes(fpscr));
                default:
                    return null;
            }
        }

        private bool GdbWriteRegister32(IExecutionContext state, int gdbRegId, StringStream ss)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 14:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.SetX(gdbRegId, value);
                        return true;
                    }
                case 15:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.DebugPc = value;
                        return true;
                    }
                case 16:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Pstate = (uint)value;
                        return true;
                    }
                case >= 17 and <= 32:
                    {
                        ulong value0 = ss.ReadLengthAsLEHex(16);
                        ulong value1 = ss.ReadLengthAsLEHex(16);
                        state.SetV(gdbRegId - 17, new V128(value0, value1));
                        return true;
                    }
                case >= 33 and <= 64:
                    {
                        ulong value = ss.ReadLengthAsLEHex(16);
                        int regId = (gdbRegId - 33);
                        int regNum = regId / 2;
                        int shift = regId % 2;
                        V128 reg = state.GetV(regNum);
                        reg.Insert(shift, value);
                        return true;
                    }
                case 65:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Fpsr = (uint)value & FpcrMask;
                        state.Fpcr = (uint)value & ~FpcrMask;
                        return true;
                    }
                default:
                    return false;
            }
        }

        private void MessageHandlerMain()
        {
            while (!_shuttingDown)
            {
                IMessage msg = Messages.Take();
                try {
                    switch (msg)
                    {
                        case BreakInMessage:
                            Logger.Notice.Print(LogClass.GdbStub, "Break-in requested");
                            CommandInterrupt();
                            break;

                        case SendNackMessage:
                            WriteStream.WriteByte((byte)'-');
                            break;

                        case CommandMessage { Command: var cmd }:
                            Logger.Debug?.Print(LogClass.GdbStub, $"Received Command: {cmd}");
                            WriteStream.WriteByte((byte)'+');
                            ProcessCommand(cmd);
                            break;

                        case ThreadBreakMessage { Context: var ctx }:
                            DebugProcess.DebugStop();
                            gThread = cThread = ctx.ThreadUid;
                            _breakHandlerEvent.Set();
                            Reply($"T05thread:{ctx.ThreadUid:x};");
                            break;

                        case KillMessage:
                            return;
                    }
                }
                catch (IOException e)
                {
                    Logger.Error?.Print(LogClass.GdbStub, "Error while processing GDB messages", e);
                }
                catch (NullReferenceException e)
                {
                    Logger.Error?.Print(LogClass.GdbStub, "Error while processing GDB messages", e);
                }
            }
        }

        private void ProcessCommand(string cmd)
        {
            StringStream ss = new StringStream(cmd);

            switch (ss.ReadChar())
            {
                case '!':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    // Enable extended mode
                    ReplyOK();
                    break;
                case '?':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    CommandQuery();
                    break;
                case 'c':
                    CommandContinue(ss.IsEmpty() ? null : ss.ReadRemainingAsHex());
                    break;
                case 'D':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    CommandDetach();
                    break;
                case 'g':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    CommandReadRegisters();
                    break;
                case 'G':
                    CommandWriteRegisters(ss);
                    break;
                case 'H':
                    {
                        char op = ss.ReadChar();
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        CommandSetThread(op, threadId);
                        break;
                    }
                case 'k':
                    Logger.Notice.Print(LogClass.GdbStub, "Kill request received, detach instead");
                    Reply("");
                    CommandDetach();
                    break;
                case 'm':
                    {
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadRemainingAsHex();
                        CommandReadMemory(addr, len);
                        break;
                    }
                case 'M':
                    {
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadUntilAsHex(':');
                        CommandWriteMemory(addr, len, ss);
                        break;
                    }
                case 'p':
                    {
                        ulong gdbRegId = ss.ReadRemainingAsHex();
                        CommandReadRegister((int)gdbRegId);
                        break;
                    }
                case 'P':
                    {
                        ulong gdbRegId = ss.ReadUntilAsHex('=');
                        CommandWriteRegister((int)gdbRegId, ss);
                        break;
                    }
                case 'q':
                    if (ss.ConsumeRemaining("GDBServerVersion"))
                    {
                        Reply($"name:Ryujinx;version:{ReleaseInformation.Version};");
                        break;
                    }

                    if (ss.ConsumeRemaining("HostInfo"))
                    {
                        if (IsProcessAarch32)
                        {
                            Reply(
                                $"triple:{ToHex("arm-unknown-linux-android")};endian:little;ptrsize:4;hostname:{ToHex("Ryujinx")};");
                        }
                        else
                        {
                            Reply(
                                $"triple:{ToHex("aarch64-unknown-linux-android")};endian:little;ptrsize:8;hostname:{ToHex("Ryujinx")};");
                        }
                        break;
                    }

                    if (ss.ConsumeRemaining("ProcessInfo"))
                    {
                        if (IsProcessAarch32)
                        {
                            Reply(
                                $"pid:1;cputype:12;cpusubtype:0;triple:{ToHex("arm-unknown-linux-android")};ostype:unknown;vendor:none;endian:little;ptrsize:4;");
                        }
                        else
                        {
                            Reply(
                                $"pid:1;cputype:100000c;cpusubtype:0;triple:{ToHex("aarch64-unknown-linux-android")};ostype:unknown;vendor:none;endian:little;ptrsize:8;");
                        }
                        break;
                    }

                    if (ss.ConsumePrefix("Supported:") || ss.ConsumeRemaining("Supported"))
                    {
                        Reply("PacketSize=10000;qXfer:features:read+;qXfer:threads:read+;vContSupported+");
                        break;
                    }

                    if (ss.ConsumePrefix("Rcmd,"))
                    {
                        string hexCommand = ss.ReadRemaining();
                        HandleQRcmdCommand(hexCommand);
                        break;
                    }

                    if (ss.ConsumeRemaining("fThreadInfo"))
                    {
                        Reply($"m{string.Join(",", DebugProcess.GetThreadUids().Select(x => $"{x:x}"))}");
                        break;
                    }

                    if (ss.ConsumeRemaining("sThreadInfo"))
                    {
                        Reply("l");
                        break;
                    }

                    if (ss.ConsumePrefix("ThreadExtraInfo,"))
                    {
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        if (threadId == null)
                        {
                            ReplyError();
                            break;
                        }

                        if (DebugProcess.IsThreadPaused(DebugProcess.GetThread(threadId.Value)))
                        {
                            Reply(ToHex("Paused"));
                        }
                        else
                        {
                            Reply(ToHex("Running"));
                        }
                        break;
                    }

                    if (ss.ConsumePrefix("Xfer:threads:read:"))
                    {
                        ss.ReadUntil(':');
                        ulong offset = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadRemainingAsHex();

                        var data = "";
                        if (offset > 0)
                        {
                            data = previousThreadListXml;
                        } else
                        {
                            previousThreadListXml = data = GetThreadListXml();
                        }

                        if (offset >= (ulong)data.Length)
                        {
                            Reply("l");
                            break;
                        }

                        if (len >= (ulong)data.Length - offset)
                        {
                            Reply("l" + ToBinaryFormat(data.Substring((int)offset)));
                            break;
                        }
                        else
                        {
                            Reply("m" + ToBinaryFormat(data.Substring((int)offset, (int)len)));
                            break;
                        }
                    }

                    if (ss.ConsumePrefix("Xfer:features:read:"))
                    {
                        string feature = ss.ReadUntil(':');
                        ulong offset = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadRemainingAsHex();

                        if (feature == "target.xml")
                        {
                            feature = IsProcessAarch32 ? "target32.xml" : "target64.xml";
                        }

                        string data;
                        if (RegisterInformation.Features.TryGetValue(feature, out data))
                        {
                            if (offset >= (ulong)data.Length)
                            {
                                Reply("l");
                                break;
                            }

                            if (len >= (ulong)data.Length - offset)
                            {
                                Reply("l" + ToBinaryFormat(data.Substring((int)offset)));
                                break;
                            }
                            else
                            {
                                Reply("m" + ToBinaryFormat(data.Substring((int)offset, (int)len)));
                                break;
                            }
                        }
                        else
                        {
                            Reply("E00"); // Invalid annex
                            break;
                        }
                    }

                    goto unknownCommand;
                case 'Q':
                    goto unknownCommand;
                case 's':
                    CommandStep(ss.IsEmpty() ? null : ss.ReadRemainingAsHex());
                    break;
                case 'T':
                    {
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        CommandIsAlive(threadId);
                        break;
                    }
                case 'v':
                    if (ss.ConsumePrefix("Cont"))
                    {
                        if (ss.ConsumeRemaining("?"))
                        {
                            Reply("vCont;c;C;s;S");
                            break;
                        }

                        if (ss.ConsumePrefix(";"))
                        {
                            HandleVContCommand(ss);
                            break;
                        }
                        
                        goto unknownCommand;
                    }
                    if (ss.ConsumeRemaining("MustReplyEmpty"))
                    {
                        Reply("");
                        break;
                    }
                    goto unknownCommand;
                case 'Z':
                    {
                        string type = ss.ReadUntil(',');
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadLengthAsHex(1);
                        string extra = ss.ReadRemaining();

                        if (extra.Length > 0)
                        {
                            Logger.Notice.Print(LogClass.GdbStub, $"Unsupported Z command extra data: {extra}");
                            ReplyError();
                            return;
                        }

                        switch (type)
                        {
                            case "0": // Software breakpoint
                                if (!BreakpointManager.SetBreakPoint(addr, len, false))
                                {
                                    ReplyError();
                                    return;
                                }
                                ReplyOK();
                                return;
                            case "1": // Hardware breakpoint
                            case "2": // Write watchpoint
                            case "3": // Read watchpoint
                            case "4": // Access watchpoint
                                ReplyError();
                                return;
                            default:
                                ReplyError();
                                return;
                        }
                    }
                case 'z':
                    {
                        string type = ss.ReadUntil(',');
                        ss.ConsumePrefix(",");
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadLengthAsHex(1);
                        string extra = ss.ReadRemaining();

                        if (extra.Length > 0)
                        {
                            Logger.Notice.Print(LogClass.GdbStub, $"Unsupported z command extra data: {extra}");
                            ReplyError();
                            return;
                        }

                        switch (type)
                        {
                            case "0": // Software breakpoint
                                if (!BreakpointManager.ClearBreakPoint(addr, len))
                                {
                                    ReplyError();
                                    return;
                                }
                                ReplyOK();
                                return;
                            case "1": // Hardware breakpoint
                            case "2": // Write watchpoint
                            case "3": // Read watchpoint
                            case "4": // Access watchpoint
                                ReplyError();
                                return;
                            default:
                                ReplyError();
                                return;
                        }
                    }
                default:
                unknownCommand:
                    Logger.Notice.Print(LogClass.GdbStub, $"Unknown command: {cmd}");
                    Reply("");
                    break;
            }
        }

        enum VContAction
        {
            None,
            Continue,
            Stop,
            Step
        }

        record VContPendingAction(VContAction Action, ushort? Signal = null);

        private void HandleVContCommand(StringStream ss)
        {
            string[] rawActions = ss.ReadRemaining().Split(';', StringSplitOptions.RemoveEmptyEntries);

            var threadActionMap = new Dictionary<ulong, VContPendingAction>();
            foreach (var thread in GetThreads())
            {
                threadActionMap[thread.ThreadUid] = new VContPendingAction(VContAction.None);
            }

            VContAction defaultAction = VContAction.None;

            // For each inferior thread, the *leftmost* action with a matching thread-id is applied.
            for (int i = rawActions.Length - 1; i >= 0; i--)
            {
                var rawAction = rawActions[i];
                var stream = new StringStream(rawAction);

                char cmd = stream.ReadChar();
                VContAction action = cmd switch
                {
                    'c' => VContAction.Continue,
                    'C' => VContAction.Continue,
                    's' => VContAction.Step,
                    'S' => VContAction.Step,
                    't' => VContAction.Stop,
                    _ => VContAction.None
                };

                // Note: We don't support signals yet.
                ushort? signal = null;
                if (cmd == 'C' || cmd == 'S')
                {
                    signal = (ushort)stream.ReadLengthAsHex(2);
                }

                ulong? threadId = null;
                if (stream.ConsumePrefix(":"))
                {
                    threadId = stream.ReadRemainingAsThreadUid();
                }

                if (threadId.HasValue)
                {
                    if (threadActionMap.ContainsKey(threadId.Value)) {
                        threadActionMap[threadId.Value] = new VContPendingAction(action, signal);
                    }
                }
                else
                {
                    foreach (var row in threadActionMap.ToList())
                    {
                        threadActionMap[row.Key] = new VContPendingAction(action, signal);
                    }

                    if (action == VContAction.Continue) {
                        defaultAction = action;
                    } else {
                        Logger.Warning?.Print(LogClass.GdbStub, $"Received vCont command with unsupported default action: {rawAction}");
                    }
                }
            }

            bool hasError = false;

            foreach (var (threadUid, action) in threadActionMap)
            {
                if (action.Action == VContAction.Step)
                {
                    var thread = DebugProcess.GetThread(threadUid);
                    if (!DebugProcess.DebugStep(thread)) {
                        hasError = true;
                    }
                }
            }

            // If we receive "vCont;c", just continue the process.
            // If we receive something like "vCont;c:2e;c:2f" (IDA Pro will send commands like this), continue these threads.
            // For "vCont;s:2f;c", `DebugProcess.DebugStep()` will continue and suspend other threads if needed, so we don't do anything here.
            if (threadActionMap.Values.All(a => a.Action == VContAction.Continue))
            {
                DebugProcess.DebugContinue();
            } else if (defaultAction == VContAction.None) {
                foreach (var (threadUid, action) in threadActionMap)
                {
                    if (action.Action == VContAction.Continue)
                    {
                        DebugProcess.DebugContinue(DebugProcess.GetThread(threadUid));
                    }
                }
            }

            if (hasError)
            {
                ReplyError();
            }
            else
            {
                ReplyOK();
            }

            foreach (var (threadUid, action) in threadActionMap)
            {
                if (action.Action == VContAction.Step)
                {
                    gThread = cThread = threadUid;
                    Reply($"T05thread:{threadUid:x};");
                }
            }
        }

        private string GetThreadListXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?><threads>\n");

            foreach (var thread in GetThreads())
            {
                string threadName = System.Security.SecurityElement.Escape(thread.GetThreadName());
                sb.Append($"<thread id=\"{thread.ThreadUid:x}\" name=\"{threadName}\">{(DebugProcess.IsThreadPaused(thread) ? "Paused" : "Running")}</thread>\n");
            }

            sb.Append("</threads>");
            return sb.ToString();
        }

        void CommandQuery()
        {
            // GDB is performing initial contact. Stop everything.
            DebugProcess.DebugStop();
            gThread = cThread = DebugProcess.GetThreadUids().First();
            Reply($"T05thread:{cThread:x};");
        }

        void CommandInterrupt()
        {
            // GDB is requesting an interrupt. Stop everything.
            DebugProcess.DebugStop();
            if (gThread == null || !GetThreads().Any(x => x.ThreadUid == gThread.Value))
            {
                gThread = cThread = DebugProcess.GetThreadUids().First();
            }

            Reply($"T02thread:{gThread:x};");
        }

        void CommandContinue(ulong? newPc)
        {
            if (newPc.HasValue)
            {
                if (cThread == null)
                {
                    ReplyError();
                    return;
                }

                DebugProcess.GetThread(cThread.Value).Context.DebugPc = newPc.Value;
            }

            DebugProcess.DebugContinue();
        }

        void CommandDetach()
        {
            BreakpointManager.ClearAll();
            CommandContinue(null);
        }

        void CommandReadRegisters()
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            string registers = "";
            if (IsProcessAarch32)
            {
                for (int i = 0; i < GdbRegisterCount32; i++)
                {
                    registers += GdbReadRegister32(ctx, i);
                }
            }
            else
            {
                for (int i = 0; i < GdbRegisterCount64; i++)
                {
                    registers += GdbReadRegister64(ctx, i);
                }
            }

            Reply(registers);
        }

        void CommandWriteRegisters(StringStream ss)
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            if (IsProcessAarch32)
            {
                for (int i = 0; i < GdbRegisterCount32; i++)
                {
                    if (!GdbWriteRegister32(ctx, i, ss))
                    {
                        ReplyError();
                        return;
                    }
                }
            }
            else
            {
                for (int i = 0; i < GdbRegisterCount64; i++)
                {
                    if (!GdbWriteRegister64(ctx, i, ss))
                    {
                        ReplyError();
                        return;
                    }
                }
            }

            if (ss.IsEmpty())
            {
                ReplyOK();
            }
            else
            {
                ReplyError();
            }
        }

        void CommandSetThread(char op, ulong? threadId)
        {
            if (threadId == 0 || threadId == null)
            {
                threadId = GetThreads().First().ThreadUid;
            }

            if (DebugProcess.GetThread(threadId.Value) == null)
            {
                ReplyError();
                return;
            }

            switch (op)
            {
                case 'c':
                    cThread = threadId;
                    ReplyOK();
                    return;
                case 'g':
                    gThread = threadId;
                    ReplyOK();
                    return;
                default:
                    ReplyError();
                    return;
            }
        }

        void CommandReadMemory(ulong addr, ulong len)
        {
            try
            {
                var data = new byte[len];
                DebugProcess.CpuMemory.Read(addr, data);
                Reply(ToHex(data));
            }
            catch (InvalidMemoryRegionException)
            {
                // InvalidAccessHandler will show an error message, we log it again to tell user the error is from GDB (which can be ignored)
                // TODO: Do not let InvalidAccessHandler show the error message
                Logger.Notice.Print(LogClass.GdbStub, $"GDB failed to read memory at 0x{addr:X16}");
                ReplyError();
            }
        }

        void CommandWriteMemory(ulong addr, ulong len, StringStream ss)
        {
            try
            {
                var data = new byte[len];
                for (ulong i = 0; i < len; i++)
                {
                    data[i] = (byte)ss.ReadLengthAsHex(2);
                }

                DebugProcess.CpuMemory.Write(addr, data);
                DebugProcess.InvalidateCacheRegion(addr, len);
                ReplyOK();
            }
            catch (InvalidMemoryRegionException)
            {
                ReplyError();
            }
        }

        void CommandReadRegister(int gdbRegId)
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            string result;
            if (IsProcessAarch32)
            {
                result = GdbReadRegister32(ctx, gdbRegId);
                if (result != null)
                {
                    Reply(result);
                }
                else
                {
                    ReplyError();
                }
            }
            else
            {
                result = GdbReadRegister64(ctx, gdbRegId);
                if (result != null)
                {
                    Reply(result);
                }
                else
                {
                    ReplyError();
                }
            }
        }

        void CommandWriteRegister(int gdbRegId, StringStream ss)
        {
            if (gThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = DebugProcess.GetThread(gThread.Value).Context;
            if (IsProcessAarch32)
            {
                if (GdbWriteRegister32(ctx, gdbRegId, ss) && ss.IsEmpty())
                {
                    ReplyOK();
                }
                else
                {
                    ReplyError();
                }
            }
            else
            {
                if (GdbWriteRegister64(ctx, gdbRegId, ss) && ss.IsEmpty())
                {
                    ReplyOK();
                }
                else
                {
                    ReplyError();
                }
            }
        }

        private void CommandStep(ulong? newPc)
        {
            if (cThread == null)
            {
                ReplyError();
                return;
            }

            var thread = DebugProcess.GetThread(cThread.Value);

            if (newPc.HasValue)
            {
                thread.Context.DebugPc = newPc.Value;
            }

            if (!DebugProcess.DebugStep(thread))
            {
                ReplyError();
            }
            else
            {
                gThread = cThread = thread.ThreadUid;
                Reply($"T05thread:{thread.ThreadUid:x};");
            }
        }

        private void CommandIsAlive(ulong? threadId)
        {
            if (GetThreads().Any(x => x.ThreadUid == threadId))
            {
                ReplyOK();
            }
            else
            {
                Reply("E00");
            }
        }

        private void HandleQRcmdCommand(string hexCommand)
        {
            try
            {
                string command = FromHex(hexCommand);
                Logger.Debug?.Print(LogClass.GdbStub, $"Received Rcmd: {command}");

                string response = command.Trim().ToLowerInvariant() switch
                {
                    "help" => "backtrace\nbt\nregisters\nreg\nget info\n",
                    "get info" => GetProcessInfo(),
                    "backtrace" => GetStackTrace(),
                    "bt" => GetStackTrace(),
                    "registers" => GetRegisters(),
                    "reg" => GetRegisters(),
                    _ => $"Unknown command: {command}\n"
                };

                Reply(ToHex(response));
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.GdbStub, $"Error processing Rcmd: {e.Message}");
                ReplyError();
            }
        }

        private string GetStackTrace()
        {
            if (gThread == null)
                return "No thread selected\n";

            if (Process == null)
                return "No application process found\n";

            return Process.Debugger.GetGuestStackTrace(DebugProcess.GetThread(gThread.Value));
        }

        private string GetRegisters()
        {
            if (gThread == null)
                return "No thread selected\n";

            if (Process == null)
                return "No application process found\n";

            return Process.Debugger.GetCpuRegisterPrintout(DebugProcess.GetThread(gThread.Value));
        }

        private string GetProcessInfo()
        {
            try
            {
                if (Process == null)
                    return "No application process found\n";

                KProcess kProcess = Process;

                var sb = new StringBuilder();
                
                sb.AppendLine($"Program Id:  0x{kProcess.TitleId:x16}");
                sb.AppendLine($"Application: {(kProcess.IsApplication ? 1 : 0)}");
                sb.AppendLine("Layout:");
                sb.AppendLine($"  Alias: 0x{kProcess.MemoryManager.AliasRegionStart:x10} - 0x{kProcess.MemoryManager.AliasRegionEnd - 1:x10}");
                sb.AppendLine($"  Heap:  0x{kProcess.MemoryManager.HeapRegionStart:x10} - 0x{kProcess.MemoryManager.HeapRegionEnd - 1:x10}");
                sb.AppendLine($"  Aslr:  0x{kProcess.MemoryManager.AslrRegionStart:x10} - 0x{kProcess.MemoryManager.AslrRegionEnd - 1:x10}");
                sb.AppendLine($"  Stack: 0x{kProcess.MemoryManager.StackRegionStart:x10} - 0x{kProcess.MemoryManager.StackRegionEnd - 1:x10}");
                
                sb.AppendLine("Modules:");
                var debugger = kProcess.Debugger;
                if (debugger != null)
                {
                    var images = debugger.GetLoadedImages();
                    for (int i = 0; i < images.Count; i++)
                    {
                        var image = images[i];
                        ulong endAddress = image.BaseAddress + image.Size - 1;
                        string name = image.Name;
                        sb.AppendLine($"  0x{image.BaseAddress:x10} - 0x{endAddress:x10} {name}");
                    }
                }
                
                return sb.ToString();
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.GdbStub, $"Error getting process info: {e.Message}");
                return $"Error getting process info: {e.Message}\n";
            }
        }

        private void Reply(string cmd)
        {
            Logger.Debug?.Print(LogClass.GdbStub, $"Reply: {cmd}");
            WriteStream.Write(Encoding.ASCII.GetBytes($"${cmd}#{CalculateChecksum(cmd):x2}"));
        }

        private void ReplyOK()
        {
            Reply("OK");
        }

        private void ReplyError()
        {
            Reply("E01");
        }

        private void DebuggerThreadMain()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, GdbStubPort);
            ListenerSocket = new TcpListener(endpoint);
            ListenerSocket.Start();
            Logger.Notice.Print(LogClass.GdbStub, $"Currently waiting on {endpoint} for GDB client");

            while (!_shuttingDown)
            {
                try
                {
                    ClientSocket = ListenerSocket.AcceptSocket();
                }
                catch (SocketException)
                {
                    return;
                }

                // If the user connects before the application is running, wait for the application to start.
                int retries = 10;
                while (DebugProcess == null && retries-- > 0)
                {
                    Thread.Sleep(200);
                }
                if (DebugProcess == null)
                {
                    Logger.Warning?.Print(LogClass.GdbStub, "Application is not running, cannot accept GDB client connection");
                    ClientSocket.Close();
                    continue;
                }

                ClientSocket.NoDelay = true;
                ReadStream = new NetworkStream(ClientSocket, System.IO.FileAccess.Read);
                WriteStream = new NetworkStream(ClientSocket, System.IO.FileAccess.Write);
                Logger.Notice.Print(LogClass.GdbStub, "GDB client connected");

                while (true)
                {
                    try
                    {
                        switch (ReadStream.ReadByte())
                        {
                            case -1:
                                goto eof;
                            case '+':
                                continue;
                            case '-':
                                Logger.Notice.Print(LogClass.GdbStub, "NACK received!");
                                continue;
                            case '\x03':
                                Messages.Add(new BreakInMessage());
                                break;
                            case '$':
                                string cmd = "";
                                while (true)
                                {
                                    int x = ReadStream.ReadByte();
                                    if (x == -1)
                                        goto eof;
                                    if (x == '#')
                                        break;
                                    cmd += (char)x;
                                }

                                string checksum = $"{(char)ReadStream.ReadByte()}{(char)ReadStream.ReadByte()}";
                                if (checksum == $"{CalculateChecksum(cmd):x2}")
                                {
                                    Messages.Add(new CommandMessage(cmd));
                                }
                                else
                                {
                                    Messages.Add(new SendNackMessage());
                                }

                                break;
                        }
                    }
                    catch (IOException)
                    {
                        goto eof;
                    }
                }

            eof:
                Logger.Notice.Print(LogClass.GdbStub, "GDB client lost connection");
                ReadStream.Close();
                ReadStream = null;
                WriteStream.Close();
                WriteStream = null;
                ClientSocket.Close();
                ClientSocket = null;

                BreakpointManager.ClearAll();
            }
        }

        private byte CalculateChecksum(string cmd)
        {
            byte checksum = 0;
            foreach (char x in cmd)
            {
                unchecked
                {
                    checksum += (byte)x;
                }
            }

            return checksum;
        }

        private string FromHex(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
                return string.Empty;

            byte[] bytes = Convert.FromHexString(hexString);
            return Encoding.ASCII.GetString(bytes);
        }

        private string ToHex(byte[] bytes)
        {
            return string.Join("", bytes.Select(x => $"{x:x2}"));
        }

        private string ToHex(string str)
        {
            return ToHex(Encoding.ASCII.GetBytes(str));
        }

        private string ToBinaryFormat(byte[] bytes)
        {
            return string.Join("", bytes.Select(x =>
                x switch
                {
                    (byte)'#' => "}\x03",
                    (byte)'$' => "}\x04",
                    (byte)'*' => "}\x0a",
                    (byte)'}' => "}\x5d",
                    _ => Convert.ToChar(x).ToString(),
                }
            ));
        }

        private string ToBinaryFormat(string str)
        {
            return ToBinaryFormat(Encoding.ASCII.GetBytes(str));
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shuttingDown = true;

                ListenerSocket.Stop();
                ClientSocket?.Shutdown(SocketShutdown.Both);
                ClientSocket?.Close();
                ReadStream?.Close();
                WriteStream?.Close();
                DebuggerThread.Join();
                Messages.Add(new KillMessage());
                MessageHandlerThread.Join();
                Messages.Dispose();
                _breakHandlerEvent.Dispose();
            }
        }

        public void BreakHandler(IExecutionContext ctx, ulong address, int imm)
        {
            DebugProcess.DebugInterruptHandler(ctx);

            _breakHandlerEvent.Reset();
            Messages.Add(new ThreadBreakMessage(ctx, address, imm));
            // Messages.Add can block, so we log it after adding the message to make sure user can see the log at the same time GDB receives the break message
            Logger.Notice.Print(LogClass.GdbStub, $"Break hit on thread {ctx.ThreadUid} at pc {address:x016}");
            // Wait for the process to stop before returning to avoid BreakHander being called multiple times from the same breakpoint
            _breakHandlerEvent.Wait(5000);
        }

        public void StepHandler(IExecutionContext ctx)
        {
            DebugProcess.DebugInterruptHandler(ctx);
        }
    }
}
