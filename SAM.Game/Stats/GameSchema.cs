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
using System.Globalization;
using System.IO;
using System.Linq;
using static SAM.Game.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Game.Stats
{
    // The achievement and stat definitions of a game, read from the schema file
    // the Steam client caches in appcache/stats. Shared by the manager window and
    // the headless auto-unlock mode.
    internal sealed class GameSchema
    {
        public List<AchievementDefinition> Achievements { get; } = new();
        public List<StatDefinition> Stats { get; } = new();

        public static string GetSchemaPath(long gameId)
        {
            try
            {
                var path = API.Steam.GetInstallPath();
                return Path.Combine(path, "appcache", "stats", _($"UserGameStatsSchema_{gameId}.bin"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Returns null when the schema is missing or unreadable.
        public static GameSchema Load(long gameId, string language)
        {
            var path = GetSchemaPath(gameId);
            if (path == null || File.Exists(path) == false)
            {
                return null;
            }

            var kv = KeyValue.LoadAsBinary(path);
            if (kv == null)
            {
                return null;
            }

            var stats = kv[gameId.ToString(CultureInfo.InvariantCulture)]["stats"];
            if (stats.Valid == false || stats.Children == null)
            {
                return null;
            }

            GameSchema schema = new();
            foreach (var stat in stats.Children)
            {
                if (stat.Valid == true)
                {
                    schema.ReadStat(stat, language);
                }
            }
            return schema;
        }

        private void ReadStat(KeyValue stat, string language)
        {
            switch (GetStatType(stat))
            {
                case APITypes.UserStatType.Integer:
                {
                    var id = stat["name"].AsString("");
                    this.Stats.Add(new IntegerStatDefinition()
                    {
                        Id = id,
                        DisplayName = GetLocalizedString(stat["display"]["name"], language, id),
                        MinValue = stat["min"].AsInteger(int.MinValue),
                        MaxValue = stat["max"].AsInteger(int.MaxValue),
                        MaxChange = stat["maxchange"].AsInteger(0),
                        IncrementOnly = stat["incrementonly"].AsBoolean(false),
                        SetByTrustedGameServer = stat["bSetByTrustedGS"].AsBoolean(false),
                        DefaultValue = stat["default"].AsInteger(0),
                        Permission = stat["permission"].AsInteger(0),
                    });
                    break;
                }

                case APITypes.UserStatType.Float:
                case APITypes.UserStatType.AverageRate:
                {
                    var id = stat["name"].AsString("");
                    this.Stats.Add(new FloatStatDefinition()
                    {
                        Id = id,
                        DisplayName = GetLocalizedString(stat["display"]["name"], language, id),
                        MinValue = stat["min"].AsFloat(float.MinValue),
                        MaxValue = stat["max"].AsFloat(float.MaxValue),
                        MaxChange = stat["maxchange"].AsFloat(0.0f),
                        IncrementOnly = stat["incrementonly"].AsBoolean(false),
                        DefaultValue = stat["default"].AsFloat(0.0f),
                        Permission = stat["permission"].AsInteger(0),
                    });
                    break;
                }

                case APITypes.UserStatType.Achievements:
                case APITypes.UserStatType.GroupAchievements:
                {
                    if (stat.Children == null)
                    {
                        break;
                    }

                    foreach (var bits in stat.Children.Where(
                        b => string.Compare(b.Name, "bits", StringComparison.InvariantCultureIgnoreCase) == 0))
                    {
                        if (bits.Valid == false || bits.Children == null)
                        {
                            continue;
                        }

                        foreach (var bit in bits.Children)
                        {
                            var id = bit["name"].AsString("");
                            this.Achievements.Add(new()
                            {
                                Id = id,
                                Name = GetLocalizedString(bit["display"]["name"], language, id),
                                Description = GetLocalizedString(bit["display"]["desc"], language, ""),
                                IconNormal = bit["display"]["icon"].AsString(""),
                                IconLocked = bit["display"]["icon_gray"].AsString(""),
                                IsHidden = bit["display"]["hidden"].AsBoolean(false),
                                Permission = bit["permission"].AsInteger(0),
                            });
                        }
                    }
                    break;
                }

                // Unknown/invalid stat types are skipped rather than failing the
                // whole schema, so a new Steam stat type can't break SAM.
            }
        }

        private static APITypes.UserStatType GetStatType(KeyValue stat)
        {
            // schema in the new format?
            var typeNode = stat["type"];
            if (typeNode.Valid == true &&
                typeNode.Type == KeyValueType.String &&
                Enum.TryParse((string)typeNode.Value, true, out APITypes.UserStatType type) == true &&
                type != APITypes.UserStatType.Invalid)
            {
                return type;
            }

            // schema in the old format?
            var typeIntNode = stat["type_int"];
            var rawType = typeIntNode.Valid == true
                ? typeIntNode.AsInteger(0)
                : typeNode.AsInteger(0);
            return (APITypes.UserStatType)rawType;
        }

        private static string GetLocalizedString(KeyValue kv, string language, string defaultValue)
        {
            var name = kv[language].AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            if (language != "english")
            {
                name = kv["english"].AsString("");
                if (string.IsNullOrEmpty(name) == false)
                {
                    return name;
                }
            }

            name = kv.AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            return defaultValue;
        }
    }
}
