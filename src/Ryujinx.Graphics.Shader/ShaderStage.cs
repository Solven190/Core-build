namespace Ryujinx.Graphics.Shader
{
    public enum ShaderStage : byte
    {
        Compute,
        Vertex,
        TessellationControl,
        TessellationEvaluation,
        Geometry,
        Fragment,

        Count,
    }

    public static class ShaderStageExtensions
    {
        /// <summary>
        /// Checks if the shader stage supports render scale.
        /// </summary>
        /// <param name="stage">Shader stage</param>
        /// <returns>True if the shader stage supports render scale, false otherwise</returns>
        public static bool SupportsRenderScale(this ShaderStage stage)
        {
            return stage is ShaderStage.Vertex or ShaderStage.Fragment or ShaderStage.Compute;
        }

        /// <summary>
        /// Checks if the shader stage is vertex, tessellation or geometry.
        /// </summary>
        /// <param name="stage">Shader stage</param>
        /// <returns>True if the shader stage is vertex, tessellation or geometry, false otherwise</returns>
        public static bool IsVtg(this ShaderStage stage)
        {
            return stage is ShaderStage.Vertex
                or ShaderStage.TessellationControl
                or ShaderStage.TessellationEvaluation
                or ShaderStage.Geometry;
        }
    }
}
