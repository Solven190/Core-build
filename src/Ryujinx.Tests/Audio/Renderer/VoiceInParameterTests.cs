using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class VoiceInParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0x170, Unsafe.SizeOf<VoiceInParameter1>());
            ClassicAssert.AreEqual(0x188, Unsafe.SizeOf<VoiceInParameter2>());
        }
    }
}
