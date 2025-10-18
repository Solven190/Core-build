using Ryujinx.Common.Logging;
using Ryujinx.HLE.Debugger.Gdb;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using IExecutionContext = Ryujinx.Cpu.IExecutionContext;


namespace Ryujinx.HLE.Debugger
{
    public partial class Debugger : IDisposable
    {
        internal Switch Device { get; private set; }

        public ushort GdbStubPort { get; private set; }

        private readonly BlockingCollection<IMessage> _messages = new(1);
        private readonly Thread _debuggerThread;
        private readonly Thread _messageHandlerThread;

        private TcpListener _listenerSocket;
        private Socket _clientSocket;
        private NetworkStream _readStream;
        private NetworkStream _writeStream;

        private GdbCommandProcessor _commandProcessor;
        private GdbCommands _commands;

        private bool _shuttingDown;
        private readonly ManualResetEventSlim _breakHandlerEvent = new(false);

        internal ulong? CThread;
        internal ulong? GThread;

        public readonly BreakpointManager BreakpointManager;

        public Debugger(Switch device, ushort port)
        {
            Device = device;
            GdbStubPort = port;

            ARMeilleure.Optimizations.EnableDebugging = true;

            _debuggerThread = new Thread(DebuggerThreadMain);
            _debuggerThread.Start();
            _messageHandlerThread = new Thread(MessageHandlerMain);
            _messageHandlerThread.Start();
            BreakpointManager = new BreakpointManager(this);
        }

        internal KProcess Process => Device.System?.DebugGetApplicationProcess();
        internal IDebuggableProcess DebugProcess => Device.System?.DebugGetApplicationProcessDebugInterface();

        internal KThread[] GetThreads() =>
            DebugProcess.GetThreadUids().Select(x => DebugProcess.GetThread(x)).ToArray();

        internal bool IsProcess32Bit => GThread != null && DebugProcess.GetThread(GThread.Value).Context.IsAarch32;

        private void MessageHandlerMain()
        {
            while (!_shuttingDown)
            {
                IMessage msg = _messages.Take();
                try
                {
                    switch (msg)
                    {
                        case BreakInMessage:
                            Logger.Notice.Print(LogClass.GdbStub, "Break-in requested");
                            _commandProcessor.Commands.Interrupt();
                            break;

                        case SendNackMessage:
                            _writeStream.WriteByte((byte)'-');
                            break;

                        case CommandMessage { Command: var cmd }:
                            Logger.Debug?.Print(LogClass.GdbStub, $"Received Command: {cmd}");
                            _writeStream.WriteByte((byte)'+');
                            _commandProcessor.Process(cmd);
                            break;

                        case ThreadBreakMessage { Context: var ctx }:
                            DebugProcess.DebugStop();
                            GThread = CThread = ctx.ThreadUid;
                            _breakHandlerEvent.Set();
                            _commandProcessor.Reply($"T05thread:{ctx.ThreadUid:x};");
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
                catch (ObjectDisposedException e)
                {
                    Logger.Error?.Print(LogClass.GdbStub, "Error while processing GDB messages", e);
                }
            }
        }

        internal bool WriteRegister(IExecutionContext ctx, int gdbRegId, StringStream ss) =>
            IsProcess32Bit
                ? ctx.WriteRegister32(gdbRegId, ss)
                : ctx.WriteRegister64(gdbRegId, ss);

        internal string ReadRegister(IExecutionContext ctx, int gdbRegId) =>
            IsProcess32Bit
                ? ctx.ReadRegister32(gdbRegId)
                : ctx.ReadRegister64(gdbRegId);

        public string GetStackTrace()
        {
            if (GThread == null)
                return "No thread selected\n";

            return Process?.Debugger?.GetGuestStackTrace(DebugProcess.GetThread(GThread.Value)) ?? "No application process found\n"; 
        }

        public string GetRegisters()
        {
            if (GThread == null)
                return "No thread selected\n";

            return Process?.Debugger?.GetCpuRegisterPrintout(DebugProcess.GetThread(GThread.Value)) ?? "No application process found\n";
        }

        public string GetMinidump()
        {
            var response = new StringBuilder();
            response.AppendLine("=== Begin Minidump ===\n");
            response.AppendLine(GetProcessInfo());

            foreach (var thread in GetThreads())
            {
                response.AppendLine($"=== Thread {thread.ThreadUid} ===");
                try
                {
                    string stackTrace = Process.Debugger.GetGuestStackTrace(thread);
                    response.AppendLine(stackTrace);
                }
                catch (Exception e)
                {
                    response.AppendLine($"[Error getting stack trace: {e.Message}]");
                }

                try
                {
                    string registers = Process.Debugger.GetCpuRegisterPrintout(thread);
                    response.AppendLine(registers);
                }
                catch (Exception e)
                {
                    response.AppendLine($"[Error getting registers: {e.Message}]");
                }
            }

            response.AppendLine("=== End Minidump ===");

            Logger.Info?.Print(LogClass.GdbStub, response.ToString());
            return response.ToString();
        }

        public string GetProcessInfo()
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
                sb.AppendLine(
                    $"  Alias: 0x{kProcess.MemoryManager.AliasRegionStart:x10} - 0x{kProcess.MemoryManager.AliasRegionEnd - 1:x10}");
                sb.AppendLine(
                    $"  Heap:  0x{kProcess.MemoryManager.HeapRegionStart:x10} - 0x{kProcess.MemoryManager.HeapRegionEnd - 1:x10}");
                sb.AppendLine(
                    $"  Aslr:  0x{kProcess.MemoryManager.AslrRegionStart:x10} - 0x{kProcess.MemoryManager.AslrRegionEnd - 1:x10}");
                sb.AppendLine(
                    $"  Stack: 0x{kProcess.MemoryManager.StackRegionStart:x10} - 0x{kProcess.MemoryManager.StackRegionEnd - 1:x10}");

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

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shuttingDown = true;

                _listenerSocket.Stop();
                _clientSocket?.Shutdown(SocketShutdown.Both);
                _clientSocket?.Close();
                _readStream?.Close();
                _writeStream?.Close();
                _debuggerThread.Join();
                _messages.Add(new KillMessage());
                _messageHandlerThread.Join();
                _messages.Dispose();
                _breakHandlerEvent.Dispose();
            }
        }

        public void BreakHandler(IExecutionContext ctx, ulong address, int imm)
        {
            DebugProcess.DebugInterruptHandler(ctx);

            _breakHandlerEvent.Reset();
            _messages.Add(new ThreadBreakMessage(ctx, address, imm));
            // Messages.Add can block, so we log it after adding the message to make sure user can see the log at the same time GDB receives the break message
            Logger.Notice.Print(LogClass.GdbStub, $"Break hit on thread {ctx.ThreadUid} at pc {address:x016}");
            // Wait for the process to stop before returning to avoid BreakHandler being called multiple times from the same breakpoint
            _breakHandlerEvent.Wait(5000);
        }

        public void StepHandler(IExecutionContext ctx)
        {
            DebugProcess.DebugInterruptHandler(ctx);
        }
    }
}
