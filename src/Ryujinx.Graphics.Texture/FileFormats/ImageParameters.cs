namespace Ryujinx.Graphics.Texture.FileFormats
{
    public readonly struct ImageParameters(
        int width,
        int height,
        int depthOrLayers,
        int levels,
        ImageFormat format,
        ImageDimensions dimensions)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int DepthOrLayers { get; } = depthOrLayers;
        public int Levels { get; } = levels;
        public ImageFormat Format { get; } = format;
        public ImageDimensions Dimensions { get; } = dimensions;
    }
}
