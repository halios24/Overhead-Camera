using System;
using System.Collections.Generic;
using HarmonyLib;

namespace OverHeadCamera;

public class OverheadGameEvents
{
    #region Team Changed Event

        /// <summary>
        /// Delegate for team changed events
        /// </summary>
        /// <param name="player">The player whose team changed</param>
        /// <param name="oldTeam">The team the player was on</param>
        /// <param name="newTeam">The team the player is now on</param>
        public delegate void TeamChangedEventHandler(Player player, PlayerTeam oldTeam, PlayerTeam newTeam);

        /// <summary>
        /// Event that fires when a player's team changes
        /// </summary>
        public static event TeamChangedEventHandler OnTeamChanged;

        /// <summary>
        /// Dictionary to track which players have had event handlers attached
        /// </summary>
        private static readonly Dictionary<Player, bool> teamChangedSubscribed = new Dictionary<Player, bool>();

        /// <summary>
        /// Subscribe to team changed events for a specific player
        /// </summary>
        /// <param name="player">The player to monitor</param>
        /// <param name="handler">The callback to invoke when team changes</param>
        public static void SubscribeToTeamChanged(Player player, TeamChangedEventHandler handler)
        {
            if (player == null)
            {
                Plugin.Log("Attempted to subscribe to team changed events for null player");
                return;
            }
            // Add the handler to our event
            OnTeamChanged += handler;

            // Track that we've subscribed to this player
            if (!teamChangedSubscribed.ContainsKey(player))
            {
                teamChangedSubscribed[player] = true;
                Plugin.Log($"Subscribed to team changed events for player {player.GetInstanceID()}");
            }
        }

        /// <summary>
        /// Unsubscribe from team changed events for a specific player
        /// </summary>
        /// <param name="player">The player to stop monitoring</param>
        /// <param name="handler">The callback to remove</param>
        public static void UnsubscribeFromTeamChanged(Player player, TeamChangedEventHandler handler)
        {
            // Remove the handler
            OnTeamChanged -= handler;

            Plugin.Log($"Unsubscribed from team changed events for player {(player != null ? player.GetInstanceID().ToString() : "null")}");
        }

        /// <summary>
        /// Harmony patch to intercept team changed events
        /// </summary>
        [HarmonyPatch(typeof(Player), "OnPlayerTeamChanged")]
        private class PlayerTeamChangedPatch
        {
            [HarmonyPostfix]
            static void Postfix(Player __instance, PlayerTeam oldTeam, PlayerTeam newTeam)
            {
                try
                {
                    // Invoke the event with the player instance and team values
                    OnTeamChanged?.Invoke(__instance, oldTeam, newTeam);

                    Plugin.Log($"Player {__instance.GetInstanceID()} team changed from {oldTeam} to {newTeam}");

                }
                catch (Exception e)
                {
                    Plugin.LogError($"Error in team changed event handler: {e.Message}\n{e.StackTrace}");
                }
            }
        }
        #endregion
}