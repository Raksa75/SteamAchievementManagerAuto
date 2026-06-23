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
using System.IO;
using System.Linq;

namespace SAM.Picker
{
    // A tiny key=value settings store kept in the user's AppData folder. Used to
    // persist the (optional) Steam Web API key between runs.
    internal static class Settings
    {
        private static string FilePath
        {
            get
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SteamAchievementManager");
                return Path.Combine(directory, "settings.ini");
            }
        }

        public static string ApiKey
        {
            get => Read("ApiKey");
            set => Write("ApiKey", value);
        }

        private static string Read(string key)
        {
            try
            {
                var path = FilePath;
                if (File.Exists(path) == false)
                {
                    return "";
                }

                foreach (var line in File.ReadAllLines(path))
                {
                    var index = line.IndexOf('=');
                    if (index > 0 && line.Substring(0, index).Trim() == key)
                    {
                        return line.Substring(index + 1).Trim();
                    }
                }
            }
            catch (Exception)
            {
            }

            return "";
        }

        private static void Write(string key, string value)
        {
            try
            {
                var path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var values = new Dictionary<string, string>();
                if (File.Exists(path) == true)
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var index = line.IndexOf('=');
                        if (index > 0)
                        {
                            values[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
                        }
                    }
                }

                values[key] = value ?? "";
                File.WriteAllLines(path, values.Select(pair => pair.Key + "=" + pair.Value));
            }
            catch (Exception)
            {
            }
        }
    }
}
