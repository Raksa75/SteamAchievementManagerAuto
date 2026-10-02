/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using SAM.Common;
using SAM.Game.Stats;

namespace SAM.Game
{
    // Unlocks every achievement SAM is allowed to set: locked ones that are not
    // protected (protected/online achievements, shown in red, are never touched).
    internal static class AutoUnlocker
    {
        private static readonly TimeSpan StatsTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan StoreTimeout = TimeSpan.FromSeconds(5);

        // Locked, non-protected achievements, i.e. what an auto-unlock would set.
        public static List<AchievementDefinition> GetUnlockable(
            API.Client client,
            IEnumerable<AchievementDefinition> definitions,
            out int lockedProtected)
        {
            List<AchievementDefinition> unlockable = new();
            lockedProtected = 0;

            foreach (var definition in definitions)
            {
                if (string.IsNullOrEmpty(definition.Id) == true ||
                    client.SteamUserStats.GetAchievementAndUnlockTime(
                        definition.Id,
                        out bool isAchieved,
                        out var unlockTime) == false ||
                    isAchieved == true)
                {
                    continue;
                }

                if (definition.IsProtected == true)
                {
                    lockedProtected++;
                }
                else
                {
                    unlockable.Add(definition);
                }
            }

            return unlockable;
        }

        // Sets the given achievements (not committed until StoreStats).
        public static int SetUnlocked(API.Client client, IEnumerable<AchievementDefinition> definitions, out int failed)
        {
            int unlocked = 0;
            failed = 0;
            foreach (var definition in definitions)
            {
                if (client.SteamUserStats.SetAchievement(definition.Id, true) == true)
                {
                    unlocked++;
                }
                else
                {
                    failed++;
                }
            }
            return unlocked;
        }

        // Headless run used by "SAM.Game.exe <appId> auto": no window, no icon
        // downloads. Returns the number of newly unlocked achievements, or one of
        // the negative AutoUnlockProtocol codes.
        public static int Run(long appId, API.Client client)
        {
            int? statsResult = null;
            int? storeResult = null;

            var received = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            received.OnRun += param => statsResult = param.Result;
            var stored = client.CreateAndRegisterCallback<API.Callbacks.UserStatsStored>();
            stored.OnRun += param => storeResult = param.Result;

            var steamId = client.SteamUser.GetSteamId();
            if (client.SteamUserStats.RequestUserStats(steamId) == API.CallHandle.Invalid ||
                Pump(client, () => statsResult.HasValue, StatsTimeout) == false ||
                statsResult != 1)
            {
                return AutoUnlockProtocol.StatsUnavailable;
            }

            var schema = GameSchema.Load(appId, client.SteamApps008.GetCurrentGameLanguage());
            if (schema == null || schema.Achievements.Count == 0)
            {
                return AutoUnlockProtocol.NoAchievements;
            }

            var unlockable = GetUnlockable(client, schema.Achievements, out _);
            if (unlockable.Count == 0)
            {
                return 0;
            }

            int unlocked = SetUnlocked(client, unlockable, out _);
            if (unlocked == 0 || client.SteamUserStats.StoreStats() == false)
            {
                return AutoUnlockProtocol.StoreFailed;
            }

            // Close as soon as Steam confirms the commit instead of waiting a
            // fixed delay. A missing confirmation is not treated as a failure:
            // StoreStats already accepted the changes.
            Pump(client, () => storeResult.HasValue, StoreTimeout);
            return storeResult.HasValue == true && storeResult != 1
                ? AutoUnlockProtocol.StoreFailed
                : unlocked;
        }

        private static bool Pump(API.Client client, Func<bool> isDone, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                client.RunCallbacks(false);
                if (isDone() == true)
                {
                    return true;
                }

                if (stopwatch.Elapsed >= timeout)
                {
                    return false;
                }

                Thread.Sleep(20);
            }
        }
    }
}
