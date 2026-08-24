namespace ARMeilleure.Translation.PTC
{
    public readonly struct PtcCacheInfo
    {
        public const string TitleIdTextDefault = "0000000000000000";
        public const string ApplicationIdTextDefault = "0000000000000000";
        public const string DisplayVersionDefault = "0";

        public ulong ProcessId { get; }
        public string TitleIdText { get; }
        public string ApplicationIdText { get; }
        public byte ProgramIndex { get; }
        public string DisplayVersion { get; }
        public string ProcessKind { get; }
        public string CacheSelector { get; }

        public string CacheKey => $"{DisplayVersion}-{CacheSelector}";

        public PtcCacheInfo(
            ulong processId,
            string titleIdText,
            string applicationIdText,
            byte programIndex,
            string displayVersion,
            string processKind,
            string cacheSelector)
        {
            ProcessId = processId;
            TitleIdText = !string.IsNullOrEmpty(titleIdText) ? titleIdText : TitleIdTextDefault;
            ApplicationIdText = !string.IsNullOrEmpty(applicationIdText) ? applicationIdText : ApplicationIdTextDefault;
            ProgramIndex = programIndex;
            DisplayVersion = !string.IsNullOrEmpty(displayVersion) ? displayVersion : DisplayVersionDefault;
            ProcessKind = processKind ?? string.Empty;
            CacheSelector = string.IsNullOrEmpty(cacheSelector) ? "default" : cacheSelector;
        }
    }
}
