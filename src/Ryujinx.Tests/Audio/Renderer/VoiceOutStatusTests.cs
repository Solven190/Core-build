using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class VoiceOutStatusTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0x10, Unsafe.SizeOf<VoiceOutStatus>());
        }
    }
}
