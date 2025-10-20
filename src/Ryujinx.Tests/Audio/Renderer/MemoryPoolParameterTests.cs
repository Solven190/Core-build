using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class MemoryPoolParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0x20, Unsafe.SizeOf<MemoryPoolInParameter>());
            ClassicAssert.AreEqual(0x10, Unsafe.SizeOf<MemoryPoolOutStatus>());
        }
    }
}
