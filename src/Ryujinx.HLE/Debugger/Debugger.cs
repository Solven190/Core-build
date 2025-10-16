using Ryujinx.Common.Logging;
using Ryujinx.HLE.Debugger.Gdb;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using IExecutionContext = Ryujinx.Cpu.IExecutionContext;
using static Ryujinx.HLE.Debugger.Helpers;

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
        private BlockingCollection<IMessage> Messages = new(1);
        private Thread DebuggerThread;
        private Thread MessageHandlerThread;
        private bool _shuttingDown = false;
        private ManualResetEventSlim _breakHandlerEvent = new(false);

        private GdbCommandProcessor CommandProcessor = null;

        internal ulong? CThread;
        internal ulong? GThread;

        internal BreakpointManager BreakpointManager;

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

        internal KThread[] GetThreads() =>
            DebugProcess.GetThreadUids().Select(x => DebugProcess.GetThread(x)).ToArray();

        internal bool IsProcessAarch32 => GThread != null && DebugProcess.GetThread(GThread.Value).Context.IsAarch32;

        private void MessageHandlerMain()
        {
            while (!_shuttingDown)
            {
                IMessage msg = Messages.Take();
                try
                {
                    switch (msg)
                    {
                        case BreakInMessage:
                            Logger.Notice.Print(LogClass.GdbStub, "Break-in requested");
                            CommandProcessor.Commands.CommandInterrupt();
                            break;

                        case SendNackMessage:
                            WriteStream.WriteByte((byte)'-');
                            break;

                        case CommandMessage { Command: var cmd }:
                            Logger.Debug?.Print(LogClass.GdbStub, $"Received Command: {cmd}");
                            WriteStream.WriteByte((byte)'+');
                            CommandProcessor.Process(cmd);
                            break;

                        case ThreadBreakMessage { Context: var ctx }:
                            DebugProcess.DebugStop();
                            GThread = CThread = ctx.ThreadUid;
                            _breakHandlerEvent.Set();
                            CommandProcessor.Commands.Reply($"T05thread:{ctx.ThreadUid:x};");
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

        public string GetStackTrace()
        {
            if (GThread == null)
                return "No thread selected\n";

            if (Process == null)
                return "No application process found\n";

            return Process.Debugger.GetGuestStackTrace(DebugProcess.GetThread(GThread.Value));
        }

        public string GetRegisters()
        {
            if (GThread == null)
                return "No thread selected\n";

            if (Process == null)
                return "No application process found\n";

            return Process.Debugger.GetCpuRegisterPrintout(DebugProcess.GetThread(GThread.Value));
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
                while ((DebugProcess == null || GetThreads().Length == 0) && retries-- > 0)
                {
                    Thread.Sleep(200);
                }

                if (DebugProcess == null || GetThreads().Length == 0)
                {
                    Logger.Warning?.Print(LogClass.GdbStub,
                        "Application is not running, cannot accept GDB client connection");
                    ClientSocket.Close();
                    continue;
                }

                ClientSocket.NoDelay = true;
                ReadStream = new NetworkStream(ClientSocket, System.IO.FileAccess.Read);
                WriteStream = new NetworkStream(ClientSocket, System.IO.FileAccess.Write);
                CommandProcessor = new GdbCommandProcessor(ListenerSocket, ClientSocket, ReadStream, WriteStream, this);
                Logger.Notice.Print(LogClass.GdbStub, "GDB client connected");

                while (true)
                {
                    try
                    {
                        switch (ReadStream.ReadByte())
                        {
                            case -1:
                                goto EndOfLoop;
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
                                        goto EndOfLoop;
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
                        goto EndOfLoop;
                    }
                }

                EndOfLoop:
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
            // Wait for the process to stop before returning to avoid BreakHandler being called multiple times from the same breakpoint
            _breakHandlerEvent.Wait(5000);
        }

        public void StepHandler(IExecutionContext ctx)
        {
            DebugProcess.DebugInterruptHandler(ctx);
        }
    }
}
