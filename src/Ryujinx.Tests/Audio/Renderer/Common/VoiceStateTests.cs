using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer.Common
{
    class VoiceStateTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.LessOrEqual(Unsafe.SizeOf<VoiceState>(), 0x100);
        }
    }
}
