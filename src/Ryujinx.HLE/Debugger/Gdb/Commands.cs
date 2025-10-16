using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Ryujinx.HLE.Debugger.Gdb
{
    class GdbCommands
    {
        const int GdbRegisterCount64 = 68;
        const int GdbRegisterCount32 = 66;

        public readonly Debugger Debugger;

        private readonly TcpListener _listenerSocket;
        private readonly Socket _clientSocket;
        private readonly NetworkStream _readStream;
        private readonly NetworkStream _writeStream;
        

        public GdbCommands(TcpListener listenerSocket, Socket clientSocket, NetworkStream readStream,
            NetworkStream writeStream, Debugger debugger)
        {
            _listenerSocket = listenerSocket;
            _clientSocket = clientSocket;
            _readStream = readStream;
            _writeStream = writeStream;
            Debugger = debugger;
        }

        public void Reply(string cmd)
        {
            Logger.Debug?.Print(LogClass.GdbStub, $"Reply: {cmd}");
            _writeStream.Write(Encoding.ASCII.GetBytes($"${cmd}#{Helpers.CalculateChecksum(cmd):x2}"));
        }

        public void ReplyOK() => Reply("OK");

        public void ReplyError() => Reply("E01");

        internal void CommandQuery()
        {
            // GDB is performing initial contact. Stop everything.
            Debugger.DebugProcess.DebugStop();
            Debugger.GThread = Debugger.CThread = Debugger.DebugProcess.GetThreadUids().First();
            Reply($"T05thread:{Debugger.CThread:x};");
        }

        internal void CommandInterrupt()
        {
            // GDB is requesting an interrupt. Stop everything.
            Debugger.DebugProcess.DebugStop();
            if (Debugger.GThread == null || Debugger.GetThreads().All(x => x.ThreadUid != Debugger.GThread.Value))
            {
                Debugger.GThread = Debugger.CThread = Debugger.DebugProcess.GetThreadUids().First();
            }

            Reply($"T02thread:{Debugger.GThread:x};");
        }

        internal void CommandContinue(ulong? newPc)
        {
            if (newPc.HasValue)
            {
                if (Debugger.CThread == null)
                {
                    ReplyError();
                    return;
                }

                Debugger.DebugProcess.GetThread(Debugger.CThread.Value).Context.DebugPc = newPc.Value;
            }

            Debugger.DebugProcess.DebugContinue();
        }

        internal void CommandDetach()
        {
            Debugger.BreakpointManager.ClearAll();
            CommandContinue(null);
        }

        internal void CommandReadRegisters()
        {
            if (Debugger.GThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = Debugger.DebugProcess.GetThread(Debugger.GThread.Value).Context;
            string registers = "";
            if (Debugger.IsProcessAarch32)
            {
                for (int i = 0; i < GdbRegisterCount32; i++)
                {
                    registers += GdbRegisters.Read32(ctx, i);
                }
            }
            else
            {
                for (int i = 0; i < GdbRegisterCount64; i++)
                {
                    registers += GdbRegisters.Read64(ctx, i);
                }
            }

            Reply(registers);
        }

        internal void CommandWriteRegisters(StringStream ss)
        {
            if (Debugger.GThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = Debugger.DebugProcess.GetThread(Debugger.GThread.Value).Context;
            if (Debugger.IsProcessAarch32)
            {
                for (int i = 0; i < GdbRegisterCount32; i++)
                {
                    if (!GdbRegisters.Write32(ctx, i, ss))
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
                    if (!GdbRegisters.Write64(ctx, i, ss))
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

        internal void CommandSetThread(char op, ulong? threadId)
        {
            if (threadId is 0 or null)
            {
                var threads = Debugger.GetThreads();
                if (threads.Length == 0)
                {
                    ReplyError();
                    return;
                }

                threadId = threads.First().ThreadUid;
            }

            if (Debugger.DebugProcess.GetThread(threadId.Value) == null)
            {
                ReplyError();
                return;
            }

            switch (op)
            {
                case 'c':
                    Debugger.CThread = threadId;
                    ReplyOK();
                    return;
                case 'g':
                    Debugger.GThread = threadId;
                    ReplyOK();
                    return;
                default:
                    ReplyError();
                    return;
            }
        }

        internal void CommandReadMemory(ulong addr, ulong len)
        {
            try
            {
                var data = new byte[len];
                Debugger.DebugProcess.CpuMemory.Read(addr, data);
                Reply(Helpers.ToHex(data));
            }
            catch (InvalidMemoryRegionException)
            {
                // InvalidAccessHandler will show an error message, we log it again to tell user the error is from GDB (which can be ignored)
                // TODO: Do not let InvalidAccessHandler show the error message
                Logger.Notice.Print(LogClass.GdbStub, $"GDB failed to read memory at 0x{addr:X16}");
                ReplyError();
            }
        }

        internal void CommandWriteMemory(ulong addr, ulong len, StringStream ss)
        {
            try
            {
                var data = new byte[len];
                for (ulong i = 0; i < len; i++)
                {
                    data[i] = (byte)ss.ReadLengthAsHex(2);
                }

                Debugger.DebugProcess.CpuMemory.Write(addr, data);
                Debugger.DebugProcess.InvalidateCacheRegion(addr, len);
                ReplyOK();
            }
            catch (InvalidMemoryRegionException)
            {
                ReplyError();
            }
        }

        internal void CommandReadRegister(int gdbRegId)
        {
            if (Debugger.GThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = Debugger.DebugProcess.GetThread(Debugger.GThread.Value).Context;
            string result;
            if (Debugger.IsProcessAarch32)
            {
                result = GdbRegisters.Read32(ctx, gdbRegId);
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
                result = GdbRegisters.Read64(ctx, gdbRegId);
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

        internal void CommandWriteRegister(int gdbRegId, StringStream ss)
        {
            if (Debugger.GThread == null)
            {
                ReplyError();
                return;
            }

            var ctx = Debugger.DebugProcess.GetThread(Debugger.GThread.Value).Context;
            if (Debugger.IsProcessAarch32)
            {
                if (GdbRegisters.Write32(ctx, gdbRegId, ss) && ss.IsEmpty())
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
                if (GdbRegisters.Write64(ctx, gdbRegId, ss) && ss.IsEmpty())
                {
                    ReplyOK();
                }
                else
                {
                    ReplyError();
                }
            }
        }

        internal void CommandStep(ulong? newPc)
        {
            if (Debugger.CThread == null)
            {
                ReplyError();
                return;
            }

            var thread = Debugger.DebugProcess.GetThread(Debugger.CThread.Value);

            if (newPc.HasValue)
            {
                thread.Context.DebugPc = newPc.Value;
            }

            if (!Debugger.DebugProcess.DebugStep(thread))
            {
                ReplyError();
            }
            else
            {
                Debugger.GThread = Debugger.CThread = thread.ThreadUid;
                Reply($"T05thread:{thread.ThreadUid:x};");
            }
        }

        internal void CommandIsAlive(ulong? threadId)
        {
            if (Debugger.GetThreads().Any(x => x.ThreadUid == threadId))
            {
                ReplyOK();
            }
            else
            {
                Reply("E00");
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

        internal void HandleVContCommand(StringStream ss)
        {
            string[] rawActions = ss.ReadRemaining().Split(';', StringSplitOptions.RemoveEmptyEntries);

            var threadActionMap = new Dictionary<ulong, VContPendingAction>();
            foreach (var thread in Debugger.GetThreads())
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
                    'c' or 'C' => VContAction.Continue,
                    's' or 'S' => VContAction.Step,
                    't' => VContAction.Stop,
                    _ => VContAction.None
                };

                // Note: We don't support signals yet.
                ushort? signal = null;
                if (cmd is 'C' or 'S')
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
                    if (threadActionMap.ContainsKey(threadId.Value))
                    {
                        threadActionMap[threadId.Value] = new VContPendingAction(action, signal);
                    }
                }
                else
                {
                    foreach (var row in threadActionMap.ToList())
                    {
                        threadActionMap[row.Key] = new VContPendingAction(action, signal);
                    }

                    if (action == VContAction.Continue)
                    {
                        defaultAction = action;
                    }
                    else
                    {
                        Logger.Warning?.Print(LogClass.GdbStub,
                            $"Received vCont command with unsupported default action: {rawAction}");
                    }
                }
            }

            bool hasError = false;

            foreach (var (threadUid, action) in threadActionMap)
            {
                if (action.Action == VContAction.Step)
                {
                    var thread = Debugger.DebugProcess.GetThread(threadUid);
                    if (!Debugger.DebugProcess.DebugStep(thread))
                    {
                        hasError = true;
                    }
                }
            }

            // If we receive "vCont;c", just continue the process.
            // If we receive something like "vCont;c:2e;c:2f" (IDA Pro will send commands like this), continue these threads.
            // For "vCont;s:2f;c", `DebugProcess.DebugStep()` will continue and suspend other threads if needed, so we don't do anything here.
            if (threadActionMap.Values.All(a => a.Action == VContAction.Continue))
            {
                Debugger.DebugProcess.DebugContinue();
            }
            else if (defaultAction == VContAction.None)
            {
                foreach (var (threadUid, action) in threadActionMap)
                {
                    if (action.Action == VContAction.Continue)
                    {
                        Debugger.DebugProcess.DebugContinue(Debugger.DebugProcess.GetThread(threadUid));
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
                    Debugger.GThread = Debugger.CThread = threadUid;
                    Reply($"T05thread:{threadUid:x};");
                }
            }
        }
        
        internal void HandleQRcmdCommand(string hexCommand)
        {
            try
            {
                string command = Helpers.FromHex(hexCommand);
                Logger.Debug?.Print(LogClass.GdbStub, $"Received Rcmd: {command}");

                string response = command.Trim().ToLowerInvariant() switch
                {
                    "help" => "backtrace\nbt\nregisters\nreg\nget info\nminidump\n",
                    "get info" => Debugger.GetProcessInfo(),
                    "backtrace" or "bt" => Debugger.GetStackTrace(),
                    "registers" or "reg" => Debugger.GetRegisters(),
                    "minidump" => Debugger.GetMinidump(),
                    _ => $"Unknown command: {command}\n"
                };

                Reply(Helpers.ToHex(response));
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.GdbStub, $"Error processing Rcmd: {e.Message}");
                ReplyError();
            }
        }
    }
}
