using System;

namespace Asobi
{
    // Exponential-backoff math only, no UnityEngine/System.Net.WebSockets
    // dependency, so it can be exercised headlessly by the .NET test
    // project. Mirrors asobi-dart's _baseReconnectDelay (1s) /
    // _maxReconnectAttempts (10) shape.
    public static class AsobiReconnectPolicy
    {
        public const int MaxAttempts = 10;
        public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);

        // Delay before reconnect attempt number `attemptIndex` (0-based:
        // the first retry after a drop is attemptIndex 0, giving 1s, 2s,
        // 4s, ... doubling each attempt).
        public static TimeSpan GetDelay(int attemptIndex)
        {
            if (attemptIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(attemptIndex));

            return TimeSpan.FromSeconds(BaseDelay.TotalSeconds * Math.Pow(2, attemptIndex));
        }
    }
}
