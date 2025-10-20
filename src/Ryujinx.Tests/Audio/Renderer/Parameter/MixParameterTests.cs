using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    class MixParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0x930, Unsafe.SizeOf<MixParameter>());
        }
    }
}
