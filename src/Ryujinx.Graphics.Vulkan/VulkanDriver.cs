using System;
using System.IO;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Vulkan
{
    [SupportedOSPlatform("macos")]
    public static class VulkanDriver
    {
        private static string GetIcdPath(string fileName)
        {
            string contentsDir = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd('/'))!;
            string icdPath = Path.Combine(contentsDir, "Resources", "vulkan", "icd.d", fileName);

            return !File.Exists(icdPath) ? throw new FileNotFoundException($"{fileName} not found", icdPath) : icdPath;
        }

        public static void MoltenVK()
        {
            Environment.SetEnvironmentVariable("MVK_CONFIG_USE_METAL_ARGUMENT_BUFFERS", "1");
            Environment.SetEnvironmentVariable("MVK_CONFIG_VK_SEMAPHORE_SUPPORT_STYLE", "0");
            Environment.SetEnvironmentVariable("MVK_CONFIG_SYNCHRONOUS_QUEUE_SUBMITS", "0");
            Environment.SetEnvironmentVariable("MVK_CONFIG_RESUME_LOST_DEVICE", "1");

            Environment.SetEnvironmentVariable("VK_DRIVER_FILES", GetIcdPath("MoltenVK_icd.json"));

            Console.WriteLine($"[MoltenVK] VK_DRIVER_FILES just set to: {Environment.GetEnvironmentVariable("VK_DRIVER_FILES")}");
        }

        public static void KosmicKrisp()
        {
            Environment.SetEnvironmentVariable("VK_DRIVER_FILES", GetIcdPath("libkosmickrisp_icd.json"));

            Console.WriteLine($"[KosmicKrisp] VK_DRIVER_FILES just set to: {Environment.GetEnvironmentVariable("VK_DRIVER_FILES")}");
        }
    }
}
