using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Server.Splitter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    class SplitterDestinationTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0xE0, Unsafe.SizeOf<SplitterDestinationVersion1>());
            ClassicAssert.AreEqual(0x128, Unsafe.SizeOf<SplitterDestinationVersion2>());
        }
    }
}
