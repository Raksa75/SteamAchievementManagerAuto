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
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using static SAM.Picker.InvariantShorthand;

namespace SAM.Picker
{
    // Minimal client for the public Steam Web API, used to read achievement
    // completion so that already-finished games can be skipped during a batch.
    internal static class SteamWebApi
    {
        private const int TimeoutMilliseconds = 15000;

        static SteamWebApi()
        {
            // .NET Framework allows only 2 concurrent connections per host by
            // default, which would serialize the parallel profile scan.
            ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 16);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public struct Completion
        {
            // True if the API gave a definitive answer for this app.
            public bool Queried;
            // True if the app actually has player achievements.
            public bool HasStats;
            public int Total;
            public int Unlocked;

            public bool IsComplete => this.HasStats == true && this.Total > 0 && this.Unlocked >= this.Total;
        }

        public enum KeyStatus
        {
            Valid,
            ProfilePrivate,
            InvalidKey,
            NetworkError,
        }

        private static readonly Regex AchievedPattern =
            new(@"""achieved""\s*:\s*(\d)", RegexOptions.Compiled);

        private static readonly Regex VisibilityPattern =
            new(@"""communityvisibilitystate""\s*:\s*(\d+)", RegexOptions.Compiled);

        public static Completion GetPlayerAchievements(string apiKey, ulong steamId, uint appId)
        {
            var (status, body) = Get(_($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={appId}&key={Uri.EscapeDataString(apiKey)}&steamid={steamId}"));

            switch (status)
            {
                case HttpStatusCode.OK:
                {
                    if (body.IndexOf("\"success\":true", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return new Completion() { Queried = true, HasStats = false };
                    }

                    int total = 0;
                    int unlocked = 0;
                    foreach (Match match in AchievedPattern.Matches(body))
                    {
                        total++;
                        if (match.Groups[1].Value == "1")
                        {
                            unlocked++;
                        }
                    }
                    return new Completion() { Queried = true, HasStats = total > 0, Total = total, Unlocked = unlocked };
                }

                // Bad key, or private profile/game details: the answer is unknown.
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                case null:
                    return new Completion() { Queried = false };

                // 400/500 => this particular app simply has no player stats.
                default:
                    return new Completion() { Queried = true, HasStats = false };
            }
        }

        // Checks the key and whether the profile is public, for the settings dialog.
        public static KeyStatus ValidateKey(string apiKey, ulong steamId)
        {
            var (status, body) = Get(_($"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(apiKey)}&steamids={steamId}"));

            switch (status)
            {
                case HttpStatusCode.OK:
                {
                    var match = VisibilityPattern.Match(body);
                    // 3 = public; anything else hides achievements from the API.
                    return match.Success == true && match.Groups[1].Value == "3"
                        ? KeyStatus.Valid
                        : KeyStatus.ProfilePrivate;
                }

                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    return KeyStatus.InvalidKey;

                default:
                    return KeyStatus.NetworkError;
            }
        }

        // Returns the HTTP status (null on network failure) and the body.
        private static (HttpStatusCode? Status, string Body) Get(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using var response = (HttpWebResponse)request.GetResponse();
                return (response.StatusCode, ReadBody(response));
            }
            catch (WebException e) when (e.Response is HttpWebResponse response)
            {
                using (response)
                {
                    return (response.StatusCode, "");
                }
            }
            catch (Exception)
            {
                return (null, "");
            }
        }

        private static string ReadBody(WebResponse response)
        {
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
