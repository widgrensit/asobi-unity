using System;

namespace Asobi
{
    [Serializable]
    public class AsobiError
    {
        /// <summary>Human-readable. Log it; never branch on it.</summary>
        public string error;

        /// <summary>
        /// The machine-readable half of asobi's error object, e.g.
        /// <c>player.confirmation_failed</c>. Branch on this. Empty when the
        /// server sent a flat legacy body with no code.
        /// </summary>
        public string code;
    }
}
