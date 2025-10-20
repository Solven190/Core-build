using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class EffectInfoParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0xC0, Unsafe.SizeOf<EffectInParameterVersion1>());
            ClassicAssert.AreEqual(0xC0, Unsafe.SizeOf<EffectInParameterVersion2>());
        }
    }
}
