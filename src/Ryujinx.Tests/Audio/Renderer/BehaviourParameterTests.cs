using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class BehaviourParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            ClassicAssert.AreEqual(0x10, Unsafe.SizeOf<BehaviourParameter>());
            ClassicAssert.AreEqual(0x10, Unsafe.SizeOf<BehaviourParameter.ErrorInfo>());
        }
    }
}
