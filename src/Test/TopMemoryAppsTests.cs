using System;
using NUnit.Framework;

#pragma warning disable CS1591
namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class TopMemoryAppsTests
    {
        [Test]
        public void RankingAggregatesBrowserProcessesAndReturnsThreeLargest()
        {
            var result = TopMemoryApps.Rank(new[] {
                new AppMemorySample { Name = "chrome", Bytes = 400 },
                new AppMemorySample { Name = "CHROME", Bytes = 500 },
                new AppMemorySample { Name = "game", Bytes = 800 },
                new AppMemorySample { Name = "editor", Bytes = 100 },
                new AppMemorySample { Name = "small", Bytes = 10 } });
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("chrome", result[0].Name);
            Assert.AreEqual(900, result[0].Bytes);
            Assert.AreEqual("game", result[1].Name);
        }

        [Test]
        public void EmptyAndDepartedSamplesDoNotInventApplications()
        {
            Assert.AreEqual(0, TopMemoryApps.Rank(new AppMemorySample[] { null,
                new AppMemorySample { Name = "", Bytes = 12 },
                new AppMemorySample { Name = "ended", Bytes = 0 } }).Count);
        }

        [Test]
        public void TotalsUse64BitValuesAndTiesHaveStableOrder()
        {
            var result = TopMemoryApps.Rank(new[] {
                new AppMemorySample { Name = "z", Bytes = 3000000000L },
                new AppMemorySample { Name = "a", Bytes = 3000000000L },
                new AppMemorySample { Name = "a", Bytes = 3000000000L } });
            Assert.AreEqual(6000000000L, result[0].Bytes);
            Assert.AreEqual("a", result[0].Name);
        }

        [Test]
        public void ClearingRankRemovesPreviousLabelAndDescription()
        {
            var chip = new RamChip();
            chip.Update(new AppMemorySample { Name = "chrome", Bytes = 1073741824L }, 1);
            Assert.AreEqual("Chrome", chip.Name);
            StringAssert.Contains("chrome", chip.Description);
            chip.Update(null, 1);
            Assert.AreEqual(string.Empty, chip.Name);
            Assert.AreEqual(string.Empty, chip.Usage);
            Assert.AreEqual(string.Empty, chip.Rank);
        }
    }
}
#pragma warning restore CS1591
