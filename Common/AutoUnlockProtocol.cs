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

namespace SAM.Common
{
    // Contract between SAM.Picker and SAM.Game for headless runs:
    // "SAM.Game.exe <appId> auto" exits with the number of newly unlocked
    // achievements (>= 0), or one of the negative codes below.
    internal static class AutoUnlockProtocol
    {
        public const string Argument = "auto";
        public const string GameExecutable = "SAM.Game.exe";

        public const int SteamUnavailable = -1;
        public const int StatsUnavailable = -2;
        public const int NoAchievements = -3;
        public const int StoreFailed = -4;
        public const int UnexpectedError = -5;

        public static string Describe(int exitCode) => exitCode switch
        {
            SteamUnavailable => "Steam unavailable (not running, or game locked by Family Sharing)",
            StatsUnavailable => "couldn't read your stats (game not owned?)",
            NoAchievements => "no achievements",
            StoreFailed => "Steam rejected the changes",
            UnexpectedError => "unexpected error",
            _ => exitCode < 0 ? "unknown error" : "ok",
        };
    }
}
