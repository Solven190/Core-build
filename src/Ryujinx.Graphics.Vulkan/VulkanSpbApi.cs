using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using SPB.Graphics.Vulkan;

namespace Ryujinx.Graphics.Vulkan
{
    public static class VulkanSpbApi
    {
        public static Vk GetApiFromSpb()
        {
            LamdaNativeContext baseCtx = new(VulkanHelper.GetProcAddress);
            MultiNativeContext ctx = new(baseCtx, null);
            Vk ret = new(ctx);

            ctx.Contexts[1] = new LamdaNativeContext(x =>
            {
                if (x.EndsWith("ProcAddr"))
                {
                    return default;
                }

                nint ptr = ret.GetDeviceProcAddr(ret.CurrentDevice.GetValueOrDefault(), x);
                if (ptr != default)
                {
                    return ptr;
                }

                return ret.GetInstanceProcAddr(ret.CurrentInstance.GetValueOrDefault(), x);
            });

            return ret;
        }
    }
}
