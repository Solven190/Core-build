using Gommon;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.HLE.Debugger
{
    public partial class Debugger
    {
        static Debugger()
        {
            _rcmdDelegates.Add(["help"], 
                _ => _rcmdDelegates.Keys
                    .Where(x => !x[0].Equals("help"))
                    .Select(x => x.JoinToString('\n'))
                    .JoinToString('\n') + '\n'
                );
            _rcmdDelegates.Add(["get info"], dbgr => dbgr.GetProcessInfo());
            _rcmdDelegates.Add(["backtrace", "bt"], dbgr => dbgr.GetStackTrace());
            _rcmdDelegates.Add(["registers", "reg"], dbgr => dbgr.GetRegisters());
            _rcmdDelegates.Add(["minidump"], dbgr => dbgr.GetMinidump());
        }

        private static readonly Dictionary<string[], Func<Debugger, string>> _rcmdDelegates = new();

        public static Func<Debugger, string> FindRcmdDelegate(string command)
        {
            Func<Debugger, string> searchResult = _ => $"Unknown command: {command}\n";

            foreach ((string[] names, Func<Debugger, string> dlg) in _rcmdDelegates)
            {
                if (names.ContainsIgnoreCase(command.Trim()))
                {
                    searchResult = dlg;
                    break;
                }
            }

            return searchResult;
        }

        public string GetStackTrace()
        {
            if (GThreadId == null)
                return "No thread selected\n";

            return Process?.Debugger?.GetGuestStackTrace(DebugProcess.GetThread(GThreadId.Value)) ?? "No application process found\n"; 
        }

        public string GetRegisters()
        {
            if (GThreadId == null)
                return "No thread selected\n";

            return Process?.Debugger?.GetCpuRegisterPrintout(DebugProcess.GetThread(GThreadId.Value)) ?? "No application process found\n";
        }

        public string GetMinidump()
        {
            if (Process is not { } kProcess)
                return "No application process found\n";

            if (kProcess.Debugger is not { } debugger)
                return "Error getting minidump: debugger is null\n";

            string response = debugger.GetMinidump();

            Logger.Info?.Print(LogClass.GdbStub, response);
            return response;
        }

        public string GetProcessInfo()
        {
            try
            {
                if (Process is not { } kProcess)
                    return "No application process found\n";

                return kProcess.Debugger?.GetProcessInfoPrintout() 
                       ?? "Error getting process info: debugger is null\n";
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.GdbStub, $"Error getting process info: {e.Message}");
                return $"Error getting process info: {e.Message}\n";
            }
        }
    }
}
