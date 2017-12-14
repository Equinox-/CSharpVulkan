using NLog;

namespace VulkanLibrary
{
    public static class Logging
    {
        public static ILogger Allocations = LogManager.GetLogger("Allocations");
    }
}