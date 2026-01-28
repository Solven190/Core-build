using Ryujinx.Common.Configuration;
using Ryujinx.HLE;
using Ryujinx.UI.Common.Configuration;

namespace Ryujinx.UI.Common.Models
{
    // NOTE: most consuming code relies on this model being value-comparable
    public record CustomSettingsModel()
    {
        public bool HasCustomSettings { get; set; }
        public bool EnableDockedMode { get; set; }
        public int SystemLanguage { get; set; }
        public int SystemRegion { get; set; }
        public int VSyncMode { get; set; }
        public int DramSize { get; set; }
        public bool EnableFsIntegrityChecks { get; set; }
        public bool IgnoreMissingServices { get; set; }
        public bool EnablePptc { get; set; }
        public bool EnableLowPowerPptc { get; set; }
        public int MemoryManagerMode { get; set; }
        public bool UseHypervisor { get; set; }
        public long TickScalar { get; set; }
        public int GraphicsBackend { get; set; }
        public string PreferredGpu { get; set; }
        public bool EnableShaderCache { get; set; }
        public bool EnableTextureRecompression { get; set; }
        public bool EnableMacroHLE { get; set; }
        public bool EnableColorSpacePassthrough { get; set; }
        public float ResScale { get; set; }
        public float MaxAnisotropy { get; set; }
        public int BackendThreading { get; set; }
        public AudioBackend AudioBackend { get; set; }
        public float AudioVolume { get; set; }
    }
}
