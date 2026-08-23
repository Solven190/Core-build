using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Horizon.Common;
using System;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy
{
    class ILockAccessor : IpcService
    {
        private readonly ServiceCtx _context;

        private readonly Apm.ManagerServer _apmManagerServer;
        private readonly Apm.SystemManagerServer _apmSystemManagerServer;
        
        private readonly KEvent _lockEvent;
        private int _lockEventHandle;

        public ILockAccessor(ServiceCtx context)
        {
            
            _context = context;
            
            _apmManagerServer = new Apm.ManagerServer(context);
            _apmSystemManagerServer = new Apm.SystemManagerServer(context);

            _lockEvent = new KEvent(context.Device.System.KernelContext);
        }

        [CommandCmif(1)]
        // TryLock(unknown u8 bool) -> unknown u8 bool
        public ResultCode TryLock(ServiceCtx context)
        {
            // Official sw only uses inflag=false.
            // Official sw just closes the output handle.
            // The input flag controls whether this returns the output handle. 
            bool unknown = context.RequestData.ReadBoolean();
            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { unknown });
            
            context.ResponseData.Write(false);
            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_lockEventHandle);
            
            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // Unlock()
        public ResultCode Unlock(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceAm);
            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // GetEvent() -> EventHandle w/ autoclear = false
        public ResultCode GetEvent(ServiceCtx context)
        {
            if (_lockEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(_lockEvent.ReadableEvent, out _lockEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }
            
            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_lockEventHandle);
            Logger.Stub?.PrintStub(LogClass.ServiceAm);
            return ResultCode.Success;
        }

        [CommandCmif(4)] // 10.0.0+
        // IsLocked() -> unknown u8 bool
        public ResultCode IsLocked(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceAm);
            context.ResponseData.Write(false);
            return ResultCode.Success;
        }
    }
}
