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
using System.Net;
using System.Text.RegularExpressions;
using static SAM.Picker.InvariantShorthand;

namespace SAM.Picker
{
    // Minimal client for the public Steam Web API, used only to read achievement
    // completion so that already-finished games can be skipped during a batch.
    internal static class SteamWebApi
    {
        public struct Completion
        {
            // True if the API gave us a definitive answer for this app.
            public bool Queried;
            // True if the app actually has player achievements.
            public bool HasStats;
            public int Total;
            public int Unlocked;

            public bool IsComplete => this.HasStats == true && this.Total > 0 && this.Unlocked >= this.Total;
        }

        private static readonly Regex AchievedPattern =
            new(@"""achieved""\s*:\s*(\d)", RegexOptions.Compiled);

        public static Completion GetPlayerAchievements(string apiKey, ulong steamId, uint appId)
        {
            var completion = new Completion();

            try
            {
                var url = _($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={apiKey}&steamid={steamId}");

                string json;
                using (var client = new WebClient())
                {
                    json = client.DownloadString(new Uri(url));
                }

                completion.Queried = true;

                if (json.IndexOf("\"success\":true", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // Profile reachable but the app reports no usable stats.
                    completion.HasStats = false;
                    return completion;
                }

                int total = 0;
                int unlocked = 0;
                foreach (Match match in AchievedPattern.Matches(json))
                {
                    total++;
                    if (match.Groups[1].Value == "1")
                    {
                        unlocked++;
                    }
                }

                completion.HasStats = total > 0;
                completion.Total = total;
                completion.Unlocked = unlocked;
                return completion;
            }
            catch (WebException webException)
            {
                if (webException.Response is HttpWebResponse response)
                {
                    // 401/403 => bad key or private profile: the API is unusable,
                    // so report "not queried" to trigger the local fallback.
                    if (response.StatusCode == HttpStatusCode.Unauthorized ||
                        response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        completion.Queried = false;
                        return completion;
                    }

                    // 400/500 => this particular app simply has no player stats.
                    completion.Queried = true;
                    completion.HasStats = false;
                    return completion;
                }

                completion.Queried = false;
                return completion;
            }
            catch (Exception)
            {
                completion.Queried = false;
                return completion;
            }
        }
    }
}
