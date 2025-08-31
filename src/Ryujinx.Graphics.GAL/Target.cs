namespace Ryujinx.Graphics.GAL
{
    public enum Target
    {
        Texture1D,
        Texture2D,
        Texture3D,
        Texture1DArray,
        Texture2DArray,
        Texture2DMultisample,
        Texture2DMultisampleArray,
        Cubemap,
        CubemapArray,
        TextureBuffer,
    }

    public static class TargetExtensions
    {
        public static bool IsMultisample(this Target target)
        {
            return target is Target.Texture2DMultisample or Target.Texture2DMultisampleArray;
        }

        public static bool HasDepthOrLayers(this Target target)
        {
            return target is Target.Texture3D
                or Target.Texture1DArray
                or Target.Texture2DArray
                or Target.Texture2DMultisampleArray
                or Target.Cubemap
                or Target.CubemapArray;
        }
    }
}
