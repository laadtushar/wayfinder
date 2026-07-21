using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    /// Abort semantics: a warp that fails mid-transition (scene load failure)
    /// must return the machine to the stable state it left, so travel keeps
    /// working after the error is surfaced.
    public class TravelAbortTests
    {
        [Test]
        public void AbortWarp_WhileWarpingToSurface_ReturnsToBridge_AndClearsDestination()
        {
            var m = new TravelStateMachine();
            m.TryBeginWarp("mars-olympus");

            m.AbortWarp();

            Assert.AreEqual(TravelState.OnBridge, m.State);
            Assert.IsNull(m.DestinationWorldId);
        }

        [Test]
        public void AbortWarp_WhileWarpingToBridge_StaysOnSurface_AndKeepsDestination()
        {
            var m = new TravelStateMachine();
            m.TryBeginWarp("mars-olympus");
            m.CompleteWarp();
            m.TryBeginReturn();

            m.AbortWarp();

            Assert.AreEqual(TravelState.OnSurface, m.State);
            Assert.AreEqual("mars-olympus", m.DestinationWorldId);
        }

        [Test]
        public void AbortWarp_FromStableStates_Throws()
        {
            var bridge = new TravelStateMachine();
            Assert.Throws<System.InvalidOperationException>(() => bridge.AbortWarp());

            var surface = new TravelStateMachine();
            surface.TryBeginWarp("w");
            surface.CompleteWarp();
            Assert.Throws<System.InvalidOperationException>(() => surface.AbortWarp());
        }

        [Test]
        public void Warp_Works_Again_After_An_Aborted_Warp()
        {
            var m = new TravelStateMachine();
            m.TryBeginWarp("w-a");
            m.AbortWarp();

            Assert.IsTrue(m.TryBeginWarp("w-b"));
            Assert.AreEqual("w-b", m.DestinationWorldId);
        }
    }
}

namespace Wayfinder.Core.Tests
{
    public class TravelStateMachineTests
    {
        [Test]
        public void StartsOnBridge_WithNoDestination()
        {
            var sm = new TravelStateMachine();
            Assert.AreEqual(TravelState.OnBridge, sm.State);
            Assert.IsNull(sm.DestinationWorldId);
        }

        [Test]
        public void Warp_FromBridge_EntersWarping_ThenSurface()
        {
            var sm = new TravelStateMachine();
            Assert.IsTrue(sm.TryBeginWarp("mars-olympus"));
            Assert.AreEqual(TravelState.WarpingToSurface, sm.State);
            Assert.AreEqual("mars-olympus", sm.DestinationWorldId);

            sm.CompleteWarp();
            Assert.AreEqual(TravelState.OnSurface, sm.State);
            Assert.AreEqual("mars-olympus", sm.DestinationWorldId);
        }

        [Test]
        public void Warp_WhileWarping_IsRejected()
        {
            var sm = new TravelStateMachine();
            sm.TryBeginWarp("mars-olympus");
            Assert.IsFalse(sm.TryBeginWarp("moon-shackleton"));
            Assert.AreEqual("mars-olympus", sm.DestinationWorldId);
        }

        [Test]
        public void Warp_WhileOnSurface_IsRejected()
        {
            var sm = new TravelStateMachine();
            sm.TryBeginWarp("mars-olympus");
            sm.CompleteWarp();
            Assert.IsFalse(sm.TryBeginWarp("moon-shackleton"));
        }

        [Test]
        public void Warp_WithNullOrEmptyId_IsRejected()
        {
            var sm = new TravelStateMachine();
            Assert.IsFalse(sm.TryBeginWarp(null));
            Assert.IsFalse(sm.TryBeginWarp(""));
            Assert.AreEqual(TravelState.OnBridge, sm.State);
        }

        [Test]
        public void Return_FromSurface_EntersWarping_ThenBridge_AndClearsDestination()
        {
            var sm = new TravelStateMachine();
            sm.TryBeginWarp("mars-olympus");
            sm.CompleteWarp();

            Assert.IsTrue(sm.TryBeginReturn());
            Assert.AreEqual(TravelState.WarpingToBridge, sm.State);

            sm.CompleteReturn();
            Assert.AreEqual(TravelState.OnBridge, sm.State);
            Assert.IsNull(sm.DestinationWorldId);
        }

        [Test]
        public void Return_FromBridge_IsRejected()
        {
            var sm = new TravelStateMachine();
            Assert.IsFalse(sm.TryBeginReturn());
            Assert.AreEqual(TravelState.OnBridge, sm.State);
        }

        [Test]
        public void CompleteWarp_WhenNotWarping_Throws()
        {
            var sm = new TravelStateMachine();
            Assert.Throws<System.InvalidOperationException>(() => sm.CompleteWarp());
        }

        [Test]
        public void CompleteReturn_WhenNotReturning_Throws()
        {
            var sm = new TravelStateMachine();
            Assert.Throws<System.InvalidOperationException>(() => sm.CompleteReturn());
        }

        [Test]
        public void FullLoop_CanRunTwice()
        {
            var sm = new TravelStateMachine();
            foreach (var world in new[] { "mars-olympus", "moon-shackleton" })
            {
                Assert.IsTrue(sm.TryBeginWarp(world));
                sm.CompleteWarp();
                Assert.IsTrue(sm.TryBeginReturn());
                sm.CompleteReturn();
            }
            Assert.AreEqual(TravelState.OnBridge, sm.State);
        }
    }
}
