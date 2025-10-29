using System;

namespace ARMeilleure.Translation
{
    class DelegateInfo
    {
        public nint FuncPtr { get; }

        public DelegateInfo(nint funcPtr)
        {
            FuncPtr = funcPtr;
        }
    }
}
