using VulkanLibrary.Unmanaged;

namespace VulkanLibrary
{
    /// <summary>
    /// Quad float color
    /// </summary>
    public struct VkColor
    {
        public static readonly VkColor Zero = new VkColor(0, 0, 0, 0);
        public static readonly VkColor Black = new VkColor(0, 0, 0, 1);
        public static readonly VkColor White = new VkColor(1, 1, 1, 1);

        public float R;
        public float G;
        public float B;
        public float A;

        public VkColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static implicit operator VkClearColorValue(VkColor color)
        {
            var res = new VkClearColorValue();
            unsafe
            {
                res.Float32[0] = color.R;
                res.Float32[1] = color.G;
                res.Float32[2] = color.B;
                res.Float32[3] = color.A;
            }

            return res;
        }
    }
}