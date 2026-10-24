namespace WinMemoryCleaner
{
    /// <summary>
    /// RamPrototype: outcome of one optimization, measured before/after (approximate)
    /// </summary>
    public class OptimizationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizationResult" /> class.
        /// </summary>
        /// <param name="succeeded">Whether every memory area was optimized without errors.</param>
        /// <param name="physicalFreeBefore">Free physical memory before the optimization (bytes).</param>
        /// <param name="physicalFreeAfter">Free physical memory after the optimization (bytes).</param>
        public OptimizationResult(bool succeeded, long physicalFreeBefore, long physicalFreeAfter)
        {
            Succeeded = succeeded;
            PhysicalFreeBefore = physicalFreeBefore;
            PhysicalFreeAfter = physicalFreeAfter;
        }

        /// <summary>
        /// Gets the change of free physical memory (bytes). It can be zero or negative due to concurrent activity.
        /// </summary>
        public long PhysicalFreeDelta
        {
            get { return PhysicalFreeAfter - PhysicalFreeBefore; }
        }

        /// <summary>
        /// Gets the free physical memory after the optimization (bytes).
        /// </summary>
        public long PhysicalFreeAfter { get; private set; }

        /// <summary>
        /// Gets the free physical memory before the optimization (bytes).
        /// </summary>
        public long PhysicalFreeBefore { get; private set; }

        /// <summary>
        /// Gets a value indicating whether every memory area was optimized without errors.
        /// </summary>
        public bool Succeeded { get; private set; }
    }
}
