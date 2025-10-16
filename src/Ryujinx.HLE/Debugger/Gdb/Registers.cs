using ARMeilleure.State;
using Ryujinx.Cpu;
using System;

namespace Ryujinx.HLE.Debugger.Gdb
{
    static class GdbRegisters
    {
        /*
        FPCR = FPSR & ~FpcrMask
        All of FPCR's bits are reserved in FPCR and vice versa,
        see ARM's documentation. 
        */
        private const uint FpcrMask = 0xfc1fffff;
        
        public static string Read64(IExecutionContext state, int gdbRegId)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 31:
                    return Helpers.ToHex(BitConverter.GetBytes(state.GetX(gdbRegId)));
                case 32:
                    return Helpers.ToHex(BitConverter.GetBytes(state.DebugPc));
                case 33:
                    return Helpers.ToHex(BitConverter.GetBytes(state.Pstate));
                case >= 34 and <= 65:
                    return Helpers.ToHex(state.GetV(gdbRegId - 34).ToArray());
                case 66:
                    return Helpers.ToHex(BitConverter.GetBytes((uint)state.Fpsr));
                case 67:
                    return Helpers.ToHex(BitConverter.GetBytes((uint)state.Fpcr));
                default:
                    return null;
            }
        }

        public static bool Write64(IExecutionContext state, int gdbRegId, StringStream ss)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 31:
                    {
                        ulong value = ss.ReadLengthAsLEHex(16);
                        state.SetX(gdbRegId, value);
                        return true;
                    }
                case 32:
                    {
                        ulong value = ss.ReadLengthAsLEHex(16);
                        state.DebugPc = value;
                        return true;
                    }
                case 33:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Pstate = (uint)value;
                        return true;
                    }
                case >= 34 and <= 65:
                    {
                        ulong value0 = ss.ReadLengthAsLEHex(16);
                        ulong value1 = ss.ReadLengthAsLEHex(16);
                        state.SetV(gdbRegId - 34, new V128(value0, value1));
                        return true;
                    }
                case 66:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Fpsr = (uint)value;
                        return true;
                    }
                case 67:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Fpcr = (uint)value;
                        return true;
                    }
                default:
                    return false;
            }
        }

        public static string Read32(IExecutionContext state, int gdbRegId)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 14:
                    return Helpers.ToHex(BitConverter.GetBytes((uint)state.GetX(gdbRegId)));
                case 15:
                    return Helpers.ToHex(BitConverter.GetBytes((uint)state.DebugPc));
                case 16:
                    return Helpers.ToHex(BitConverter.GetBytes((uint)state.Pstate));
                case >= 17 and <= 32:
                    return Helpers.ToHex(state.GetV(gdbRegId - 17).ToArray());
                case >= 33 and <= 64:
                    int reg = (gdbRegId - 33);
                    int n = reg / 2;
                    int shift = reg % 2;
                    ulong value = state.GetV(n).Extract<ulong>(shift);
                    return Helpers.ToHex(BitConverter.GetBytes(value));
                case 65:
                    uint fpscr = (uint)state.Fpsr | (uint)state.Fpcr;
                    return Helpers.ToHex(BitConverter.GetBytes(fpscr));
                default:
                    return null;
            }
        }

        public static bool Write32(IExecutionContext state, int gdbRegId, StringStream ss)
        {
            switch (gdbRegId)
            {
                case >= 0 and <= 14:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.SetX(gdbRegId, value);
                        return true;
                    }
                case 15:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.DebugPc = value;
                        return true;
                    }
                case 16:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Pstate = (uint)value;
                        return true;
                    }
                case >= 17 and <= 32:
                    {
                        ulong value0 = ss.ReadLengthAsLEHex(16);
                        ulong value1 = ss.ReadLengthAsLEHex(16);
                        state.SetV(gdbRegId - 17, new V128(value0, value1));
                        return true;
                    }
                case >= 33 and <= 64:
                    {
                        ulong value = ss.ReadLengthAsLEHex(16);
                        int regId = (gdbRegId - 33);
                        int regNum = regId / 2;
                        int shift = regId % 2;
                        V128 reg = state.GetV(regNum);
                        reg.Insert(shift, value);
                        return true;
                    }
                case 65:
                    {
                        ulong value = ss.ReadLengthAsLEHex(8);
                        state.Fpsr = (uint)value & FpcrMask;
                        state.Fpcr = (uint)value & ~FpcrMask;
                        return true;
                    }
                default:
                    return false;
            }
        }
    }
}
