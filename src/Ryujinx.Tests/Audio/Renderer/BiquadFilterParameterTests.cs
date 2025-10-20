using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class BiquadFilterParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0xC, Unsafe.SizeOf<BiquadFilterParameter1>());
            ClassicAssert.AreEqual(0x18, Unsafe.SizeOf<BiquadFilterParameter2>());
        }
    }
}
