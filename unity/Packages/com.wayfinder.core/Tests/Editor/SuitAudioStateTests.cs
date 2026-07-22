using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    public class SuitAudioStateTests
    {
        [Test]
        public void Surface_With_Atmosphere_Has_Wind_Breathing_Radio_No_Hum()
        {
            var m = SuitAudioState.Resolve(TravelState.OnSurface, worldHasAtmosphere: true);
            Assert.Greater(m.Ambience, 0f, "Mars surface must have wind");
            Assert.Greater(m.Breathing, 0f, "always hear your breath in the suit");
            Assert.Greater(m.Radio, 0f, "faint comms bed on surface");
            Assert.AreEqual(0f, m.BridgeHum, "no bridge hum on a surface");
        }

        [Test]
        public void Airless_Surface_Is_Silent_Of_Wind_But_Still_Has_Breathing()
        {
            // The Moon: vacuum, no airborne ambience — but the suit's own sounds
            // (breathing, boots conducted through the body) remain.
            var m = SuitAudioState.Resolve(TravelState.OnSurface, worldHasAtmosphere: false);
            Assert.AreEqual(0f, m.Ambience, "vacuum has no airborne wind");
            Assert.Greater(m.Breathing, 0f, "you still breathe in vacuum");
            Assert.Greater(m.Boots, 0f, "boots still enabled (motion-gated)");
        }

        [Test]
        public void Bridge_Is_Shirtsleeve_Hum_And_Quiet_Comms_No_Breathing()
        {
            var m = SuitAudioState.Resolve(TravelState.OnBridge, worldHasAtmosphere: true);
            Assert.Greater(m.BridgeHum, 0f, "bridge machinery hum");
            Assert.AreEqual(0f, m.Breathing, "no suit breathing on the pressurized bridge");
            Assert.AreEqual(0f, m.Ambience, "no surface wind on the bridge");
            Assert.Greater(m.Radio, 0f, "quiet comms on the bridge");
        }

        [Test]
        public void Warp_Transitions_Are_Silent()
        {
            foreach (var s in new[] { TravelState.WarpingToSurface, TravelState.WarpingToBridge })
            {
                var m = SuitAudioState.Resolve(s, true);
                Assert.AreEqual(0f, m.Ambience);
                Assert.AreEqual(0f, m.Breathing);
                Assert.AreEqual(0f, m.BridgeHum);
            }
        }

        [Test]
        public void Boot_Gain_Scales_With_Motion_And_Clamps()
        {
            var m = SuitAudioState.Resolve(TravelState.OnSurface, true);
            Assert.AreEqual(0f, SuitAudioState.BootGain(m, 0f), "still = no footsteps");
            Assert.AreEqual(m.Boots, SuitAudioState.BootGain(m, 1f), 1e-5f, "full motion = full boots");
            Assert.AreEqual(m.Boots * 0.5f, SuitAudioState.BootGain(m, 0.5f), 1e-5f);
            Assert.AreEqual(0f, SuitAudioState.BootGain(m, -3f), "clamps below 0");
            Assert.AreEqual(m.Boots, SuitAudioState.BootGain(m, 9f), 1e-5f, "clamps above 1");
        }

        [Test]
        public void Bridge_Boots_Are_Off_Regardless_Of_Motion()
        {
            var m = SuitAudioState.Resolve(TravelState.OnBridge, true);
            Assert.AreEqual(0f, SuitAudioState.BootGain(m, 1f), "no EVA boots on the bridge");
        }
    }
}
