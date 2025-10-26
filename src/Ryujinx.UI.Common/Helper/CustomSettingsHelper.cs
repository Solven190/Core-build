using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System.IO;
using CustomSettingsModel = Ryujinx.UI.Common.Models.CustomSettingsModel;
using Path = System.IO.Path;

namespace Ryujinx.UI.Common.Helper
{
    public static class CustomSettingsHelper
    {
        private static readonly CustomSettingsMetadataJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public static CustomSettingsModel LoadCustomSettingsJson(string customSettingsJsonPath)
        {
            var customSettingsModel = new CustomSettingsModel();

            if (!File.Exists(customSettingsJsonPath))
            {
                return customSettingsModel;
            }

            try
            {
                Logger.Info?.Print(LogClass.Configuration, $"Found custom settings data for application at {customSettingsJsonPath}");
                customSettingsModel = JsonHelper.DeserializeFromFile(customSettingsJsonPath, _serializerContext.CustomSettingsModel);
                return customSettingsModel;
            }
            catch
            {
                Logger.Error?.Print(LogClass.Configuration, $"Failed to deserialize custom settings data for application at {customSettingsJsonPath}");
                return customSettingsModel;
            }
        }

        public static void SaveCustomSettingsJson(string customSettingsJsonPath, CustomSettingsModel customSettingsModel)
        {
            if (!File.Exists(customSettingsJsonPath))
            {
                string directoryPath = Path.GetDirectoryName(customSettingsJsonPath);

                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }

            JsonHelper.SerializeToFile(customSettingsJsonPath, customSettingsModel, _serializerContext.CustomSettingsModel);
        }

        public static bool HasCustomSettings(string customSettingsJsonPath)
        {
            return File.Exists(customSettingsJsonPath);
        }

        public static void DeleteCustomSettings(string customSettingsJsonPath)
        {
            FileInfo file = new(customSettingsJsonPath);

            if (file.Exists)
            {
                file.Delete();
            }
        }

        public static void OverrideSettings(CustomSettingsModel customSettingsModel)
        {
            ConfigurationState.Instance.System.EnableDockedMode.Value = customSettingsModel.EnableDockedMode;
            ConfigurationState.Instance.System.Language.Value = (Language)customSettingsModel.SystemLanguage;
            ConfigurationState.Instance.System.Region.Value = (Region)customSettingsModel.SystemRegion;
            ConfigurationState.Instance.Graphics.VSyncMode.Value = (VSyncMode)customSettingsModel.VSyncMode;
            ConfigurationState.Instance.System.DramSize.Value = (MemoryConfiguration)customSettingsModel.DramSize;
            ConfigurationState.Instance.System.EnableFsIntegrityChecks.Value = customSettingsModel.EnableFsIntegrityChecks;
            ConfigurationState.Instance.System.IgnoreMissingServices.Value = customSettingsModel.IgnoreMissingServices;
            ConfigurationState.Instance.System.EnableLowPowerPptc.Value = customSettingsModel.EnableLowPowerPptc;
            ConfigurationState.Instance.System.MemoryManagerMode.Value = (MemoryManagerMode)customSettingsModel.MemoryManagerMode;
            ConfigurationState.Instance.System.UseHypervisor.Value = customSettingsModel.UseHypervisor;
            ConfigurationState.Instance.Graphics.GraphicsBackend.Value = (GraphicsBackend)customSettingsModel.GraphicsBackend;
            ConfigurationState.Instance.Graphics.PreferredGpu.Value = customSettingsModel.PreferredGpu;
            ConfigurationState.Instance.Graphics.EnableShaderCache.Value = customSettingsModel.EnableShaderCache;
            ConfigurationState.Instance.Graphics.EnableTextureRecompression.Value = customSettingsModel.EnableTextureRecompression;
            ConfigurationState.Instance.Graphics.EnableMacroHLE.Value = customSettingsModel.EnableMacroHLE;
            ConfigurationState.Instance.Graphics.EnableColorSpacePassthrough.Value = customSettingsModel.EnableColorSpacePassthrough;
            ConfigurationState.Instance.Graphics.ResScale.Value = customSettingsModel.ResScale;
            ConfigurationState.Instance.Graphics.MaxAnisotropy.Value = customSettingsModel.MaxAnisotropy;
            ConfigurationState.Instance.Graphics.BackendThreading.Value = (BackendThreading)customSettingsModel.BackendThreading;
        }

        public static string PathToGameSettingsJson(ulong applicationIdBase)
        {
            return Path.Combine(AppDataManager.GamesDirPath, applicationIdBase.ToString("x16"), "settings.json");
        }
    }
}
