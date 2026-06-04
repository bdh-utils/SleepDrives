using System;
using System.Collections.Generic;
using SleepDrives;
using Xunit;

namespace SleepDrives.Tests
{
    public class RestartManagerNotifierTests
    {
        [Fact]
        public void NotifyClosing_NoVolumes_ReturnsEmptyWithoutStartingSession()
        {
            var notifier = new RestartManagerNotifier();

            var result = notifier.NotifyClosing(Array.Empty<string>());

            Assert.Empty(result);
        }

        [Fact]
        public void NotifyClosing_OnlyBlankPaths_ReturnsEmpty()
        {
            var notifier = new RestartManagerNotifier();

            var result = notifier.NotifyClosing(new List<string> { "", "   " });

            Assert.Empty(result);
        }
    }
}
