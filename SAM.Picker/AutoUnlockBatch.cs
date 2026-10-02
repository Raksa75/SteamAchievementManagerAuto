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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAM.Common;
using static SAM.Picker.InvariantShorthand;

namespace SAM.Picker
{
    // Runs "Auto-Unlock All": optionally scans the Steam profile to find the
    // games that still need work, then runs "SAM.Game.exe <id> auto" for each,
    // one at a time. UI-agnostic: progress and cancellation go through delegates.
    internal sealed class AutoUnlockBatch
    {
        private static readonly TimeSpan GameTimeout = TimeSpan.FromSeconds(60);
        private const int ScanParallelism = 8;

        public sealed class Options
        {
            public List<GameInfo> Games;
            public string ApiKey;
            public ulong SteamId;
            public string GameExecutablePath;
        }

        public sealed class Failure
        {
            public string GameName;
            public string Reason;
        }

        public sealed class Result
        {
            public int TotalGames;
            public int AchievementsUnlocked;
            public int GamesUnlocked;
            public int GamesNothingToDo;
            public int SkippedComplete;
            public int SkippedNoAchievements;
            public bool UsedApi;
            public bool ApiUnusable;
            public bool Cancelled;
            public TimeSpan Elapsed;
            public readonly List<Failure> Failures = new();
        }

        private readonly Options _Options;
        private readonly Action<int, string> _Report;
        private readonly Func<bool> _IsCancelled;
        private readonly Result _Result = new();

        private AutoUnlockBatch(Options options, Action<int, string> report, Func<bool> isCancelled)
        {
            this._Options = options;
            this._Report = report;
            this._IsCancelled = isCancelled;
        }

        public static Result Run(Options options, Action<int, string> report, Func<bool> isCancelled)
        {
            return new AutoUnlockBatch(options, report, isCancelled).Run();
        }

        private Result Run()
        {
            var stopwatch = Stopwatch.StartNew();
            this._Result.TotalGames = this._Options.Games.Count;

            bool useApi = string.IsNullOrWhiteSpace(this._Options.ApiKey) == false && this._Options.SteamId != 0;
            List<GameInfo> toProcess = null;
            if (useApi == true)
            {
                this._Result.UsedApi = true;
                toProcess = this.ScanProfile();
            }
            toProcess ??= this.FilterLocal();

            for (int i = 0; i < toProcess.Count && this._IsCancelled() == false; i++)
            {
                var game = toProcess[i];
                this._Report(
                    (int)(i * 100L / toProcess.Count),
                    _($"Unlocking {i + 1} of {toProcess.Count}: {game.Name}"));
                this.UnlockGame(game);
            }

            this._Result.Cancelled = this._IsCancelled();
            this._Result.Elapsed = stopwatch.Elapsed;
            return this._Result;
        }

        // Returns the games that still have locked achievements, or null when the
        // API can't be used (bad key / private profile) so the caller falls back.
        private List<GameInfo> ScanProfile()
        {
            var games = this._Options.Games;
            var incomplete = new ConcurrentBag<GameInfo>();
            int scanned = 0;
            int answered = 0;
            int complete = 0;
            int noAchievements = 0;

            Parallel.ForEach(
                games,
                new ParallelOptions() { MaxDegreeOfParallelism = ScanParallelism },
                (game, state) =>
                {
                    if (this._IsCancelled() == true)
                    {
                        state.Stop();
                        return;
                    }

                    var completion = SteamWebApi.GetPlayerAchievements(this._Options.ApiKey, this._Options.SteamId, game.Id);
                    if (completion.Queried == false)
                    {
                        incomplete.Add(game); // unknown: process it to be safe
                    }
                    else
                    {
                        Interlocked.Increment(ref answered);
                        if (completion.HasStats == false)
                        {
                            Interlocked.Increment(ref noAchievements);
                        }
                        else if (completion.IsComplete == true)
                        {
                            Interlocked.Increment(ref complete);
                        }
                        else
                        {
                            incomplete.Add(game);
                        }
                    }

                    int done = Interlocked.Increment(ref scanned);
                    this._Report(
                        (int)(done * 100L / games.Count),
                        _($"Checking your Steam profile: {done} of {games.Count} games"));
                });

            if (answered == 0 && this._IsCancelled() == false)
            {
                this._Result.ApiUnusable = true;
                return null;
            }

            this._Result.SkippedComplete = complete;
            this._Result.SkippedNoAchievements = noAchievements;

            // Keep the list's alphabetical order for a predictable run.
            var order = games.Select((game, index) => (game, index)).ToDictionary(pair => pair.game, pair => pair.index);
            return incomplete.OrderBy(game => order[game]).ToList();
        }

        // Without the API: skip games that have no achievement schema locally.
        private List<GameInfo> FilterLocal()
        {
            string statsDirectory;
            try
            {
                statsDirectory = Path.Combine(API.Steam.GetInstallPath(), "appcache", "stats");
            }
            catch (Exception)
            {
                statsDirectory = null;
            }

            List<GameInfo> toProcess = new();
            foreach (var game in this._Options.Games)
            {
                if (statsDirectory == null ||
                    File.Exists(Path.Combine(statsDirectory, _($"UserGameStatsSchema_{game.Id}.bin"))) == true)
                {
                    toProcess.Add(game);
                }
                else
                {
                    this._Result.SkippedNoAchievements++;
                }
            }
            return toProcess;
        }

        private void UnlockGame(GameInfo game)
        {
            int exitCode;
            try
            {
                var startInfo = new ProcessStartInfo(
                    this._Options.GameExecutablePath,
                    _($"{game.Id} {AutoUnlockProtocol.Argument}"))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                // Never kill a game mid-commit on cancel: let it finish, but don't
                // wait forever on one that hangs.
                if (process.WaitForExit((int)GameTimeout.TotalMilliseconds) == false)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                    }
                    this.AddFailure(game, "timed out");
                    return;
                }
                exitCode = process.ExitCode;
            }
            catch (Exception)
            {
                this.AddFailure(game, "couldn't start SAM.Game.exe");
                return;
            }

            if (exitCode > 0)
            {
                this._Result.AchievementsUnlocked += exitCode;
                this._Result.GamesUnlocked++;
            }
            else if (exitCode == 0)
            {
                this._Result.GamesNothingToDo++;
            }
            else if (exitCode == AutoUnlockProtocol.NoAchievements)
            {
                this._Result.SkippedNoAchievements++;
            }
            else
            {
                this.AddFailure(game, AutoUnlockProtocol.Describe(exitCode));
            }
        }

        private void AddFailure(GameInfo game, string reason)
        {
            this._Result.Failures.Add(new Failure() { GameName = game.Name, Reason = reason });
        }
    }
}
