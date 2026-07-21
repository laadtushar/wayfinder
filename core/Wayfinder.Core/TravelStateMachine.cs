using System;

namespace Wayfinder.Core
{
    public enum TravelState
    {
        OnBridge,
        WarpingToSurface,
        OnSurface,
        WarpingToBridge
    }

    /// Guards the bridge -> warp -> surface -> return loop so the warp lever
    /// and airlock can never double-fire or fire from the wrong place.
    public class TravelStateMachine
    {
        public TravelState State { get; private set; } = TravelState.OnBridge;

        /// The world being visited (or warped to); null while on the bridge.
        public string DestinationWorldId { get; private set; }

        public bool TryBeginWarp(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return false;
            if (State != TravelState.OnBridge) return false;
            DestinationWorldId = worldId;
            State = TravelState.WarpingToSurface;
            return true;
        }

        public void CompleteWarp()
        {
            if (State != TravelState.WarpingToSurface)
                throw new InvalidOperationException(
                    $"CompleteWarp called in state {State}; only valid while WarpingToSurface.");
            State = TravelState.OnSurface;
        }

        public bool TryBeginReturn()
        {
            if (State != TravelState.OnSurface) return false;
            State = TravelState.WarpingToBridge;
            return true;
        }

        public void CompleteReturn()
        {
            if (State != TravelState.WarpingToBridge)
                throw new InvalidOperationException(
                    $"CompleteReturn called in state {State}; only valid while WarpingToBridge.");
            DestinationWorldId = null;
            State = TravelState.OnBridge;
        }
    }
}
