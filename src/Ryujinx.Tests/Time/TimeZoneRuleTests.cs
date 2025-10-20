using NUnit.Framework;
using NUnit.Framework.Legacy;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Time
{
    internal class TimeZoneRuleTests
    {
        class EffectInfoParameterTests
        {
            [Test]
            public void EnsureTypeSize()
            {
                ClassicAssert.AreEqual(0x4000, Unsafe.SizeOf<TimeZoneRule>());
            }
        }
    }
}
