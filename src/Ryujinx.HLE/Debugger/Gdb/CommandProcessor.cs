using Ryujinx.Common;
using Ryujinx.Common.Logging;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Ryujinx.HLE.Debugger.Gdb
{
    class GdbCommandProcessor
    {
        public readonly GdbCommands Commands;

        public GdbCommandProcessor(TcpListener listenerSocket, Socket clientSocket, NetworkStream readStream, NetworkStream writeStream, Debugger debugger)
        {
            Commands = new GdbCommands(listenerSocket, clientSocket, readStream, writeStream, debugger);
        }
        
        private string previousThreadListXml = "";
        
        public void Process(string cmd)
        {
            StringStream ss = new(cmd);

            switch (ss.ReadChar())
            {
                case '!':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    // Enable extended mode
                    Commands.ReplyOK();
                    break;
                case '?':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    Commands.CommandQuery();
                    break;
                case 'c':
                    Commands.CommandContinue(ss.IsEmpty() ? null : ss.ReadRemainingAsHex());
                    break;
                case 'D':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    Commands.CommandDetach();
                    break;
                case 'g':
                    if (!ss.IsEmpty())
                    {
                        goto unknownCommand;
                    }

                    Commands.CommandReadRegisters();
                    break;
                case 'G':
                    Commands.CommandWriteRegisters(ss);
                    break;
                case 'H':
                    {
                        char op = ss.ReadChar();
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        Commands.CommandSetThread(op, threadId);
                        break;
                    }
                case 'k':
                    Logger.Notice.Print(LogClass.GdbStub, "Kill request received, detach instead");
                    Commands.Reply("");
                    Commands.CommandDetach();
                    break;
                case 'm':
                    {
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadRemainingAsHex();
                        Commands.CommandReadMemory(addr, len);
                        break;
                    }
                case 'M':
                    {
                        ulong addr = ss.ReadUntilAsHex(',');
                        ulong len = ss.ReadUntilAsHex(':');
                        Commands.CommandWriteMemory(addr, len, ss);
                        break;
                    }
                case 'p':
                    {
                        ulong gdbRegId = ss.ReadRemainingAsHex();
                        Commands.CommandReadRegister((int)gdbRegId);
                        break;
                    }
                case 'P':
                    {
                        ulong gdbRegId = ss.ReadUntilAsHex('=');
                        Commands.CommandWriteRegister((int)gdbRegId, ss);
                        break;
                    }
                case 'q':
                    if (ss.ConsumeRemaining("GDBServerVersion"))
                    {
                        Commands.Reply($"name:Ryujinx;version:{ReleaseInformation.Version};");
                        break;
                    }

                    if (ss.ConsumeRemaining("HostInfo"))
                    {
                        if (Commands.Debugger.IsProcessAarch32)
                        {
                            Commands.Reply(
                                $"triple:{Helpers.ToHex("arm-unknown-linux-android")};endian:little;ptrsize:4;hostname:{Helpers.ToHex("Ryujinx")};");
                        }
                        else
                        {
                            Commands.Reply(
                                $"triple:{Helpers.ToHex("aarch64-unknown-linux-android")};endian:little;ptrsize:8;hostname:{Helpers.ToHex("Ryujinx")};");
                        }

                        break;
                    }

                    if (ss.ConsumeRemaining("ProcessInfo"))
                    {
                        if (Commands.Debugger.IsProcessAarch32)
                        {
                            Commands.Reply(
                                $"pid:1;cputype:12;cpusubtype:0;triple:{Helpers.ToHex("arm-unknown-linux-android")};ostype:unknown;vendor:none;endian:little;ptrsize:4;");
                        }
                        else
                        {
                            Commands.Reply(
                                $"pid:1;cputype:100000c;cpusubtype:0;triple:{Helpers.ToHex("aarch64-unknown-linux-android")};ostype:unknown;vendor:none;endian:little;ptrsize:8;");
                        }

                        break;
                    }

                    if (ss.ConsumePrefix("Supported:") || ss.ConsumeRemaining("Supported"))
                    {
                        Commands.Reply("PacketSize=10000;qXfer:features:read+;qXfer:threads:read+;vContSupported+");
                        break;
                    }

                    if (ss.ConsumePrefix("Rcmd,"))
                    {
                        string hexCommand = ss.ReadRemaining();
                        Commands.HandleQRcmdCommand(hexCommand);
                        break;
                    }

                    if (ss.ConsumeRemaining("fThreadInfo"))
                    {
                        Commands. Reply($"m{string.Join(",", Commands.Debugger.DebugProcess.GetThreadUids().Select(x => $"{x:x}"))}");
                        break;
                    }

                    if (ss.ConsumeRemaining("sThreadInfo"))
                    {
                        Commands.Reply("l");
                        break;
                    }

                    if (ss.ConsumePrefix("ThreadExtraInfo,"))
                    {
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        if (threadId == null)
                        {
                            Commands.ReplyError();
                            break;
                        }

                        Commands.Reply(Helpers.ToHex(
                            Commands.Debugger.DebugProcess.IsThreadPaused(
                                Commands.Debugger.DebugProcess.GetThread(threadId.Value))
                                ? "Paused"
                                : "Running"
                            )
                        );

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
                        }
                        else
                        {
                            previousThreadListXml = data = GetThreadListXml();
                        }

                        if (offset >= (ulong)data.Length)
                        {
                            Commands.Reply("l");
                            break;
                        }

                        if (len >= (ulong)data.Length - offset)
                        {
                            Commands.Reply("l" + Helpers.ToBinaryFormat(data.Substring((int)offset)));
                            break;
                        }
                        else
                        {
                            Commands.Reply("m" + Helpers.ToBinaryFormat(data.Substring((int)offset, (int)len)));
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
                            feature = Commands.Debugger.IsProcessAarch32 ? "target32.xml" : "target64.xml";
                        }

                        string data;
                        if (RegisterInformation.Features.TryGetValue(feature, out data))
                        {
                            if (offset >= (ulong)data.Length)
                            {
                                Commands.Reply("l");
                                break;
                            }

                            if (len >= (ulong)data.Length - offset)
                            {
                                Commands.Reply("l" + Helpers.ToBinaryFormat(data.Substring((int)offset)));
                                break;
                            }
                            else
                            {
                                Commands.Reply("m" + Helpers.ToBinaryFormat(data.Substring((int)offset, (int)len)));
                                break;
                            }
                        }
                        else
                        {
                            Commands.Reply("E00"); // Invalid annex
                            break;
                        }
                    }

                    goto unknownCommand;
                case 'Q':
                    goto unknownCommand;
                case 's':
                    Commands.CommandStep(ss.IsEmpty() ? null : ss.ReadRemainingAsHex());
                    break;
                case 'T':
                    {
                        ulong? threadId = ss.ReadRemainingAsThreadUid();
                        Commands.CommandIsAlive(threadId);
                        break;
                    }
                case 'v':
                    if (ss.ConsumePrefix("Cont"))
                    {
                        if (ss.ConsumeRemaining("?"))
                        {
                            Commands.Reply("vCont;c;C;s;S");
                            break;
                        }

                        if (ss.ConsumePrefix(";"))
                        {
                            Commands.HandleVContCommand(ss);
                            break;
                        }

                        goto unknownCommand;
                    }

                    if (ss.ConsumeRemaining("MustReplyEmpty"))
                    {
                        Commands.Reply("");
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
                            Commands.ReplyError();
                            return;
                        }

                        switch (type)
                        {
                            case "0": // Software breakpoint
                                if (!Commands.Debugger.BreakpointManager.SetBreakPoint(addr, len))
                                {
                                    Commands.ReplyError();
                                    return;
                                }

                                Commands.ReplyOK();
                                return;
                            case "1": // Hardware breakpoint
                            case "2": // Write watchpoint
                            case "3": // Read watchpoint
                            case "4": // Access watchpoint
                                Commands.ReplyError();
                                return;
                            default:
                                Commands.ReplyError();
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
                            Commands.ReplyError();
                            return;
                        }

                        switch (type)
                        {
                            case "0": // Software breakpoint
                                if (!Commands.Debugger.BreakpointManager.ClearBreakPoint(addr, len))
                                {
                                    Commands.ReplyError();
                                    return;
                                }

                                Commands.ReplyOK();
                                return;
                            case "1": // Hardware breakpoint
                            case "2": // Write watchpoint
                            case "3": // Read watchpoint
                            case "4": // Access watchpoint
                                Commands.ReplyError();
                                return;
                            default:
                                Commands.ReplyError();
                                return;
                        }
                    }
                default:
                    unknownCommand:
                    Logger.Notice.Print(LogClass.GdbStub, $"Unknown command: {cmd}");
                    Commands.Reply("");
                    break;
            }
        }

        private string GetThreadListXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?><threads>\n");

            foreach (var thread in Commands.Debugger.GetThreads())
            {
                string threadName = System.Security.SecurityElement.Escape(thread.GetThreadName());
                sb.Append(
                    $"<thread id=\"{thread.ThreadUid:x}\" name=\"{threadName}\">{(Commands.Debugger.DebugProcess.IsThreadPaused(thread) ? "Paused" : "Running")}</thread>\n");
            }

            sb.Append("</threads>");
            return sb.ToString();
        }
    }
}
