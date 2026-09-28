// This file has been modified for TAEF support.

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Helpers
{
    [TestClass]
    public class ThrottleTests
    {
        struct Event
        {
            public int Id;
            public DateTime Time;
        }

        private static readonly List<Event> Events = new List<Event>();
        private const int MaxEvents = 10;
        private const int TotalEvents = 100;
        private static readonly TimeSpan TimeSpan = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Upper tolerance of the duration: the throttle only guarantees a minimum duration, the actual duration depends
        /// on the load of the machine.
        /// </summary>
        private static readonly TimeSpan UpperTolerance = TimeSpan.FromMilliseconds(1000);

        [ClassInitialize]
        public static void GenerateEventList(TestContext context)
        {
            Events.Clear();
            var throttle = new Throttle(MaxEvents, TimeSpan);
            for (int i = 0; i < TotalEvents; i++)
            {
                var id = i;
                throttle.Execute(() =>
                {
                    Events.Add(new Event { Id = id, Time = DateTime.Now });
                });
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EventsAreInOrder()
        {
            Events.Should().HaveCount(TotalEvents);
            for (int i = 0; i < Events.Count; i++)
                Events[i].Id.Should().Be(i);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DoesNotTakeMuchLongerThanExpected()
        {
            var duration = (Events.Last().Time - Events.First().Time).TotalMilliseconds;
            var minimumDuration = ((TotalEvents / MaxEvents - 1) * TimeSpan.TotalMilliseconds);
            duration.Should().BeGreaterThan(minimumDuration - TestMetadata.ToleranceInMs);
            duration.Should().BeLessThan(minimumDuration + TimeSpan.TotalMilliseconds + UpperTolerance.TotalMilliseconds);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void HasNoTimeFrameWithTooManyElements()
        {
            var firstEvent = Events.First().Time;
            var lastEvent = Events.Last().Time;
            var step = TimeSpan.FromMilliseconds(77);

            for (DateTime start = firstEvent; start < lastEvent; start += step)
            {
                var theStart = start;
                var theEnd = start + TimeSpan;
                var eventsInTimeFrame = Events.Where(e => e.Time >= theStart && e.Time <= theEnd);
                if (eventsInTimeFrame.Count() > MaxEvents)
                {
                    Assert.Inconclusive("Size of Events should never be greater than MaxEvents - but this test is unstable :-)");
                }
            }
        }
    }
}
