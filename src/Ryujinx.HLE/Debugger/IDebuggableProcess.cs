using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Memory;

namespace Ryujinx.HLE.Debugger
{
    internal interface IDebuggableProcess
    {
        void DebugStop();
        void DebugContinue();
        void DebugContinue(KThread thread);
        bool DebugStep(KThread thread);
        KThread GetThread(ulong threadUid);
        bool IsThreadPaused(KThread thread);
        public void DebugInterruptHandler(IExecutionContext ctx);
        IVirtualMemoryManager CpuMemory { get; }
        ulong[] ThreadUids { get; }
        DebugState DebugState { get; }
        void InvalidateCacheRegion(ulong address, ulong size);
    }
}
