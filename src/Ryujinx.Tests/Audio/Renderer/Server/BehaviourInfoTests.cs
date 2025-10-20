using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Server;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class BehaviourInfoTests
    {
        [Test]
        public void TestCheckFeature()
        {
            int latestRevision = BehaviourInfo.BaseRevisionMagic + BehaviourInfo.LastRevision;
            int previousRevision = BehaviourInfo.BaseRevisionMagic + (BehaviourInfo.LastRevision - 1);
            int invalidRevision = BehaviourInfo.BaseRevisionMagic + (BehaviourInfo.LastRevision + 1);

            ClassicAssert.IsTrue(BehaviourInfo.CheckFeatureSupported(latestRevision, latestRevision));
            ClassicAssert.IsFalse(BehaviourInfo.CheckFeatureSupported(previousRevision, latestRevision));
            ClassicAssert.IsTrue(BehaviourInfo.CheckFeatureSupported(latestRevision, previousRevision));
            // In case we get an invalid revision, this is supposed to auto default to REV1 internally.. idk what the hell Nintendo was thinking here..
            ClassicAssert.IsTrue(BehaviourInfo.CheckFeatureSupported(invalidRevision, latestRevision));
        }

        [Test]
        public void TestsMemoryPoolForceMappingEnabled()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision1);

            ClassicAssert.IsFalse(behaviourInfo.IsMemoryPoolForceMappingEnabled());

            behaviourInfo.UpdateFlags(0x1);

            ClassicAssert.IsTrue(behaviourInfo.IsMemoryPoolForceMappingEnabled());
        }

        [Test]
        public void TestRevision1()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision1);

            ClassicAssert.IsFalse(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsFalse(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.70f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(1, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(1, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision2()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision2);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsFalse(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.70f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(1, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(1, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision3()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision3);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsFalse(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.70f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(1, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(1, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision4()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision4);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.75f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(1, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(1, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision5()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision5);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(2, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision6()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision6);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsFalse(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(2, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision7()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision7);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(2, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision8()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision8);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(3, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision9()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision9);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsFalse(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(3, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision10()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision10);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsFalse(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(4, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision11()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision11);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsTrue(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsFalse(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(5, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision12()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision12);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsTrue(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsFalse(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(5, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }

        [Test]
        public void TestRevision13()
        {
            BehaviourInfo behaviourInfo = new();

            behaviourInfo.SetUserRevision(BehaviourInfo.BaseRevisionMagic + BehaviourInfo.Revision13);

            ClassicAssert.IsTrue(behaviourInfo.IsAdpcmLoopContextBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsLongSizePreDelaySupported());
            ClassicAssert.IsTrue(behaviourInfo.IsAudioUsbDeviceOutputSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsFlushVoiceWaveBuffersSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsElapsedFrameCountSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsDecodingBehaviourFlagSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterEffectStateClearBugFixed());
            ClassicAssert.IsTrue(behaviourInfo.IsMixInParameterDirtyOnlyUpdateSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsWaveBufferVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.IsEffectInfoVersion2Supported());
            ClassicAssert.IsTrue(behaviourInfo.UseMultiTapBiquadFilterProcessing());
            ClassicAssert.IsTrue(behaviourInfo.IsNewEffectChannelMappingSupported());
            ClassicAssert.IsTrue(behaviourInfo.IsBiquadFilterParameterForSplitterEnabled());
            ClassicAssert.IsTrue(behaviourInfo.IsSplitterPrevVolumeResetSupported());

            ClassicAssert.AreEqual(0.80f, behaviourInfo.GetAudioRendererProcessingTimeLimit());
            ClassicAssert.AreEqual(5, behaviourInfo.GetCommandProcessingTimeEstimatorVersion());
            ClassicAssert.AreEqual(2, behaviourInfo.GetPerformanceMetricsDataFormat());
        }
    }
}
