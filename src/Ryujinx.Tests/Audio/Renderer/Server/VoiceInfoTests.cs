using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Server.Voice;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    class VoiceInfoTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.LessOrEqual(Unsafe.SizeOf<VoiceInfo>(), 0x238);
        }
    }
}
