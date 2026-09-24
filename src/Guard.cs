using System;
using System.Collections.Generic;

namespace Daywheel
{
    /// <summary>
    /// Every entry point runs through here, so a fault is caught, logged
    /// once and skipped instead of reaching the game.
    /// </summary>
    internal static class Guard
    {
        private static readonly HashSet<string> _said = new HashSet<string>();

        /// <summary>
        /// Runs an action and catches anything it throws. Logs once per
        /// name, because a fault in a tick repeats every frame and would
        /// fill the log.
        /// </summary>
        internal static void Run(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (_said.Contains(name)) return;
                _said.Add(name);
                if (Plugin.Log != null)
                    Plugin.Log.Warn("Daywheel: " + name + " failed and was "
                        + "skipped. " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
