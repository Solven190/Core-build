using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL3;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using Ryujinx.UI.Common.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.ViewModels
{
    public partial class CustomSettingsViewModel : BaseModel
    {
        [ObservableProperty]
        private bool _isVulkanAvailable = true;
        private readonly List<string> _gpuIds = [];

        private int ComputePreferredGpuIndex(string PreferredGpu)
        {
            return _gpuIds.Contains(PreferredGpu) ? _gpuIds.IndexOf(PreferredGpu) : 0;
        }

        private string ComputePreferredGpu(int PreferredGpuIndex)
        {
            return _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
        }

        private int ComputeResScaleIndex(float ResScale)
        {
            if (ResScale <= 0.5f)
            {
                return 0;
            }
            else if (ResScale <= 0.75f)
            {
                return 1;
            }
            else
            {
                return (int)Math.Ceiling(ResScale) + 1;
            }
        }

        private float ComputeResScale(int ResScaleIndex)
        {
            switch (ResScaleIndex)
            {
                case 0:
                    return 0.5f;
                case 1:
                    return 0.75f;
                case 2:
                    return 1.0f;
                case 3:
                    return 2.0f;
                case 4:
                    return 3.0f;
                case 5:
                    return 4.0f;
                default:
                    return 1.0f;
            }
        }

        private int ComputeMaxAnisotropyIndex(float MaxAnisotropy)
        {
            return MaxAnisotropy == -1.0f ? 0 : (int)(MathF.Log2(MaxAnisotropy));
        }

        private float ComputeMaxAnisotropy(int MaxAnisotropyIndex)
        {
            switch (MaxAnisotropyIndex)
            {
                case 0:
                    return -1.0f;
                case 1:
                    return 2.0f;
                case 2:
                    return 4.0f;
                case 3:
                    return 8.0f;
                case 4:
                    return 16.0f;
                default:
                    return 2.0f;
            }
        }

        public bool IsMacOS => OperatingSystem.IsMacOS();
        public bool IsOpenGLAvailable => !OperatingSystem.IsMacOS();
        public bool IsHypervisorAvailable => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        public ulong ApplicationIdBase { get; set; }
        public string ApplicationName { get; set; }
        public Bitmap GameIcon { get; set; }
        public ObservableCollection<ComboBoxItem> AvailableGpus { get; set; }
        public CustomSettingsModel CustomSettingsModel { get; set; }
        public event Action CloseWindow;

        // System settings
        public bool EnableDockedMode { get; set; }
        public int SystemLanguage { get; set; }
        public int SystemRegion { get; set; }
        public int DramSize { get; set; }
        public bool EnableFsIntegrityChecks { get; set; }
        public bool IgnoreMissingServices { get; set; }
        
        // CPU settings
        public bool EnablePptc { get; set; }
        public bool EnableLowPowerPptc { get; set; }
        public int MemoryManagerMode { get; set; }
        public bool UseHypervisor { get; set; }
        public long TurboMultiplier
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;

                    OnPropertyChanged();
                    OnPropertyChanged((nameof(TurboMultiplierPercentageText)));
                }
            }
        }

        public string TurboMultiplierPercentageText => $"{TurboMultiplier}%";

        // Graphics settings
        public int VSyncMode { get; set; }
        public int GraphicsBackend { get; set; }
        public int PreferredGpuIndex { get; set; }
        public bool EnableShaderCache { get; set; }
        public bool EnableTextureRecompression { get; set; }
        public bool EnableMacroHLE { get; set; }
        public bool EnableColorSpacePassthrough { get; set; }
        public bool ColorSpacePassthroughAvailable => IsMacOS;
        public int ResScaleIndex { get; set; }
        public int MaxAnisotropyIndex { get; set; }
        public int BackendThreading { get; set; }

        // Audio settings
        public bool IsOpenAlEnabled { get; set; }
        public bool IsSoundIoEnabled { get; set; }
        public bool IsSDL3Enabled { get; set; }
        public AudioBackend AudioBackend { get; set; }
        
        public float Volume
        {
            get;
            set
            {
                field = value;

                ConfigurationState.Instance.System.AudioVolume.Value = field / 100;

                OnPropertyChanged();
            }
        }
        
        public CustomSettingsViewModel(string titleId, byte[] icon, string titleName)
        {
            AvailableGpus = [];
            ApplicationIdBase = ulong.Parse(titleId, NumberStyles.HexNumber);
            CustomSettingsModel = CustomSettingsHelper.LoadCustomSettingsJson(CustomSettingsHelper.PathToGameSettingsJson(ApplicationIdBase));

            Task.Run(CheckSoundBackends);

            if (icon is { Length: > 0 })
            {
                using MemoryStream ms = new(icon);
                GameIcon = new Bitmap(ms);
            }

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadAvailableGpus);
                LoadCurrentConfiguration();
            }
        }

        public async Task CheckSoundBackends()
        {
            IsOpenAlEnabled = OpenALHardwareDeviceDriver.IsSupported;
            IsSoundIoEnabled = SoundIoHardwareDeviceDriver.IsSupported;
            IsSDL3Enabled = SDL3HardwareDeviceDriver.IsSupported;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(IsOpenAlEnabled));
                OnPropertyChanged(nameof(IsSoundIoEnabled));
                OnPropertyChanged(nameof(IsSDL3Enabled));
            });
        }
        
        private async Task LoadAvailableGpus()
        {
            AvailableGpus.Clear();

            var devices = VulkanRenderer.GetPhysicalDevices();

            if (devices.Length == 0)
            {
                IsVulkanAvailable = false;
                GraphicsBackend = 1;
            }
            else
            {
                foreach (var device in devices)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _gpuIds.Add(device.Id);

                        AvailableGpus.Add(new ComboBoxItem { Content = $"{device.Name} {(device.IsDiscrete ? "(dGPU)" : "")}" });
                    });
                }
            }

            // GPU configuration needs to be loaded during the async method or it will always return 0.
            PreferredGpuIndex = ComputePreferredGpuIndex(ConfigurationState.Instance.Graphics.PreferredGpu.Value);

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(PreferredGpuIndex)));
        }
        public void LoadCurrentConfiguration()
        {
            ConfigurationState config = ConfigurationState.Instance;

            if (!CustomSettingsHelper.HasCustomSettings(CustomSettingsHelper.PathToGameSettingsJson(ApplicationIdBase)))
            {
                // Load all settings from global configuration
                EnableDockedMode = config.System.EnableDockedMode.Value;
                SystemLanguage = (int)config.System.Language.Value;
                SystemRegion = (int)config.System.Region.Value;
                VSyncMode = (int)config.Graphics.VSyncMode.Value;
                DramSize = (int)config.System.DramSize.Value;
                EnableFsIntegrityChecks = config.System.EnableFsIntegrityChecks.Value;
                IgnoreMissingServices = config.System.IgnoreMissingServices.Value;
                EnablePptc = config.System.EnablePptc.Value;
                EnableLowPowerPptc = config.System.EnableLowPowerPptc.Value;
                MemoryManagerMode = (int)config.System.MemoryManagerMode.Value;
                UseHypervisor = config.System.UseHypervisor.Value;
                TurboMultiplier = config.System.TickScalar.Value;
                GraphicsBackend = (int)config.Graphics.GraphicsBackend.Value;
                PreferredGpuIndex = ComputePreferredGpuIndex(config.Graphics.PreferredGpu.Value);
                EnableShaderCache = config.Graphics.EnableShaderCache.Value;
                EnableTextureRecompression = config.Graphics.EnableTextureRecompression.Value;
                EnableMacroHLE = config.Graphics.EnableMacroHLE.Value;
                EnableColorSpacePassthrough = config.Graphics.EnableColorSpacePassthrough.Value;
                ResScaleIndex = ComputeResScaleIndex(config.Graphics.ResScale);
                MaxAnisotropyIndex = ComputeMaxAnisotropyIndex(config.Graphics.MaxAnisotropy);
                BackendThreading = (int)config.Graphics.BackendThreading.Value;
                AudioBackend = config.System.AudioBackend.Value;
                Volume = config.System.AudioVolume * 100;
            }
            else
            {
                // Load all settings from custom configuration
                EnableDockedMode = CustomSettingsModel.EnableDockedMode;
                SystemLanguage = CustomSettingsModel.SystemLanguage;
                SystemRegion = CustomSettingsModel.SystemRegion;
                VSyncMode = CustomSettingsModel.VSyncMode;
                DramSize = CustomSettingsModel.DramSize;
                EnableFsIntegrityChecks = CustomSettingsModel.EnableFsIntegrityChecks;
                IgnoreMissingServices = CustomSettingsModel.IgnoreMissingServices;
                EnablePptc = CustomSettingsModel.EnablePptc;
                EnableLowPowerPptc = CustomSettingsModel.EnableLowPowerPptc;
                MemoryManagerMode = CustomSettingsModel.MemoryManagerMode;
                UseHypervisor = CustomSettingsModel.UseHypervisor;
                TurboMultiplier = CustomSettingsModel.TickScalar;
                GraphicsBackend = CustomSettingsModel.GraphicsBackend;
                PreferredGpuIndex = ComputePreferredGpuIndex(CustomSettingsModel.PreferredGpu);
                EnableShaderCache = CustomSettingsModel.EnableShaderCache;
                EnableTextureRecompression = CustomSettingsModel.EnableTextureRecompression;
                EnableMacroHLE = CustomSettingsModel.EnableMacroHLE;
                EnableColorSpacePassthrough = CustomSettingsModel.EnableColorSpacePassthrough;
                ResScaleIndex = ComputeResScaleIndex(CustomSettingsModel.ResScale);
                MaxAnisotropyIndex = ComputeMaxAnisotropyIndex(CustomSettingsModel.MaxAnisotropy);
                BackendThreading = CustomSettingsModel.BackendThreading;
                AudioBackend = CustomSettingsModel.AudioBackend;
                Volume = CustomSettingsModel.AudioVolume * 100;
            }
        }

        public void DeleteSettings()
        {
            if (CustomSettingsHelper.HasCustomSettings(CustomSettingsHelper.PathToGameSettingsJson(ApplicationIdBase)))
            {
                CustomSettingsHelper.DeleteCustomSettings(CustomSettingsHelper.PathToGameSettingsJson(ApplicationIdBase));
            }
        }

        public void SaveSettings()
        {
            CustomSettingsModel.HasCustomSettings = true;
            CustomSettingsModel.EnableDockedMode = EnableDockedMode;
            CustomSettingsModel.SystemLanguage = SystemLanguage;
            CustomSettingsModel.SystemRegion = SystemRegion;
            CustomSettingsModel.VSyncMode = VSyncMode;
            CustomSettingsModel.DramSize = DramSize;
            CustomSettingsModel.EnableFsIntegrityChecks = EnableFsIntegrityChecks;
            CustomSettingsModel.IgnoreMissingServices = IgnoreMissingServices;
            CustomSettingsModel.EnablePptc = EnablePptc;
            CustomSettingsModel.EnableLowPowerPptc = EnableLowPowerPptc;
            CustomSettingsModel.MemoryManagerMode = MemoryManagerMode;
            CustomSettingsModel.UseHypervisor = UseHypervisor;
            CustomSettingsModel.TickScalar = TurboMultiplier;
            CustomSettingsModel.GraphicsBackend = GraphicsBackend;
            CustomSettingsModel.PreferredGpu = ComputePreferredGpu(PreferredGpuIndex);
            CustomSettingsModel.EnableShaderCache = EnableShaderCache;
            CustomSettingsModel.EnableTextureRecompression = EnableTextureRecompression;
            CustomSettingsModel.EnableMacroHLE = EnableMacroHLE;
            CustomSettingsModel.ResScale = ComputeResScale(ResScaleIndex);
            CustomSettingsModel.MaxAnisotropy = ComputeMaxAnisotropy(MaxAnisotropyIndex);
            CustomSettingsModel.BackendThreading = BackendThreading;
            CustomSettingsModel.AudioBackend = AudioBackend;
            CustomSettingsModel.AudioVolume = Volume / 100;
            CustomSettingsHelper.SaveCustomSettingsJson(CustomSettingsHelper.PathToGameSettingsJson(ApplicationIdBase), CustomSettingsModel);
        }

        public async void DeleteButton()
        {
            UserResult result = await ContentDialogHelper.CreateLocalizedConfirmationDialog(
                LocaleManager.Instance[LocaleKeys.DialogWarning],
                LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogCustomSettingsDeleteMessage, ApplicationName)
            );

            if (result == UserResult.Yes)
            {
                DeleteSettings();
                CloseWindow?.Invoke();
            }
        }

        public void CancelButton()
        {
            CloseWindow?.Invoke();
        }
        
        public void SaveButton()
        {
            SaveSettings();
            CloseWindow?.Invoke();
        }
    }
}
