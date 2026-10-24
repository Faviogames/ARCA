namespace WinMemoryCleaner
{
    /// <summary>
    /// IMemoryService
    /// </summary>
    public interface IMemoryService
    {
        /// <summary>
        /// Gets the memory info (RAM)
        /// </summary>
        Memory Memory { get; }

        /// <summary>
        /// Gets the number of memory areas that failed in the last optimization (RamPrototype)
        /// </summary>
        int LastOptimizationErrorCount { get; }

        /// <summary>
        /// Optimize the computer
        /// </summary>
        /// <param name="reason">Optimization reason</param>
        /// <param name="areas">Memory areas</param>
        void Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas);
    }
}
