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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.XPath;
using static SAM.Picker.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Picker
{
    internal partial class GamePicker : Form
    {
        private readonly API.Client _SteamClient;

        private readonly Dictionary<uint, GameInfo> _Games;
        private readonly List<GameInfo> _FilteredGames;

        private readonly object _LogoLock;
        private readonly HashSet<string> _LogosAttempting;
        private readonly HashSet<string> _LogosAttempted;
        private readonly ConcurrentQueue<GameInfo> _LogoQueue;

        private readonly API.Callbacks.AppDataChanged _AppDataChangedCallback;

        public GamePicker(API.Client client)
        {
            this._Games = new();
            this._FilteredGames = new();
            this._LogoLock = new();
            this._LogosAttempting = new();
            this._LogosAttempted = new();
            this._LogoQueue = new();

            this.InitializeComponent();

            Bitmap blank = new(this._LogoImageList.ImageSize.Width, this._LogoImageList.ImageSize.Height);
            using (var g = Graphics.FromImage(blank))
            {
                g.Clear(Color.DimGray);
            }

            this._LogoImageList.Images.Add("Blank", blank);

            this._SteamClient = client;

            this._AppDataChangedCallback = client.CreateAndRegisterCallback<API.Callbacks.AppDataChanged>();
            this._AppDataChangedCallback.OnRun += this.OnAppDataChanged;

            Common.Theme.Apply(this);

            this.AddGames();
        }

        private ulong GetSteamId()
        {
            try
            {
                return this._SteamClient.SteamUser.GetSteamId();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool HasLocalAchievements(uint id)
        {
            try
            {
                var path = API.Steam.GetInstallPath();
                path = Path.Combine(path, "appcache", "stats", _($"UserGameStatsSchema_{id}.bin"));
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnAppDataChanged(APITypes.AppDataChanged param)
        {
            if (param.Result == false)
            {
                return;
            }

            if (this._Games.TryGetValue(param.Id, out var game) == false)
            {
                return;
            }

            game.Name = this._SteamClient.SteamApps001.GetAppData(game.Id, "name");

            this.AddGameToLogoQueue(game);
            this.DownloadNextLogo();
        }

        private void DoDownloadList(object sender, DoWorkEventArgs e)
        {
            this._PickerStatusLabel.Text = "Downloading game list...";

            byte[] bytes;
            using (WebClient downloader = new())
            {
                bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml"));
            }

            List<KeyValuePair<uint, string>> pairs = new();
            using (MemoryStream stream = new(bytes, false))
            {
                XPathDocument document = new(stream);
                var navigator = document.CreateNavigator();
                var nodes = navigator.Select("/games/game");
                while (nodes.MoveNext() == true)
                {
                    string type = nodes.Current.GetAttribute("type", "");
                    if (string.IsNullOrEmpty(type) == true)
                    {
                        type = "normal";
                    }
                    pairs.Add(new((uint)nodes.Current.ValueAsLong, type));
                }
            }

            this._PickerStatusLabel.Text = "Checking game ownership...";
            foreach (var kv in pairs)
            {
                this.AddGame(kv.Key, kv.Value);
            }
        }

        private void OnDownloadList(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled == true)
            {
                this.AddDefaultGames();
                MessageBox.Show(e.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            this.RefreshGames();
            this._RefreshGamesButton.Enabled = true;
            this.DownloadNextLogo();
        }

        private void RefreshGames()
        {
            var nameSearch = this._SearchGameTextBox.Text.Length > 0
                ? this._SearchGameTextBox.Text
                : null;

            var wantNormals = this._FilterGamesMenuItem.Checked == true;
            var wantDemos = this._FilterDemosMenuItem.Checked == true;
            var wantMods = this._FilterModsMenuItem.Checked == true;
            var wantJunk = this._FilterJunkMenuItem.Checked == true;

            this._FilteredGames.Clear();
            foreach (var info in this._Games.Values.OrderBy(gi => gi.Name))
            {
                if (nameSearch != null &&
                    info.Name.IndexOf(nameSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                bool wanted = info.Type switch
                {
                    "normal" => wantNormals,
                    "demo" => wantDemos,
                    "mod" => wantMods,
                    "junk" => wantJunk,
                    _ => true,
                };
                if (wanted == false)
                {
                    continue;
                }

                this._FilteredGames.Add(info);
            }

            this._GameListView.VirtualListSize = this._FilteredGames.Count;
            this._PickerStatusLabel.Text =
                $"Displaying {this._GameListView.Items.Count} games. Total {this._Games.Count} games.";

            if (this._GameListView.Items.Count > 0)
            {
                this._GameListView.Items[0].Selected = true;
                this._GameListView.Select();
            }
        }

        private void OnGameListViewRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var info = this._FilteredGames[e.ItemIndex];
            e.Item = info.Item = new()
            {
                Text = info.Name,
                ImageIndex = info.ImageIndex,
            };
        }

        private void OnGameListViewSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            if (e.Direction != SearchDirectionHint.Down || e.IsTextSearch == false)
            {
                return;
            }

            var count = this._FilteredGames.Count;
            if (count < 2)
            {
                return;
            }

            var text = e.Text;
            int startIndex = e.StartIndex;

            Predicate<GameInfo> predicate;
            /*if (e.IsPrefixSearch == true)*/
            {
                predicate = gi => gi.Name != null && gi.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase);
            }
            /*else
            {
                predicate = gi => gi.Name != null && string.Compare(gi.Name, text, StringComparison.CurrentCultureIgnoreCase) == 0;
            }*/

            int index;
            if (e.StartIndex >= count)
            {
                // starting from the last item in the list
                index = this._FilteredGames.FindIndex(0, startIndex - 1, predicate);
            }
            else if (startIndex <= 0)
            {
                // starting from the first item in the list
                index = this._FilteredGames.FindIndex(0, count, predicate);
            }
            else
            {
                index = this._FilteredGames.FindIndex(startIndex, count - startIndex, predicate);
                if (index < 0)
                {
                    index = this._FilteredGames.FindIndex(0, startIndex - 1, predicate);
                }
            }

            e.Index = index < 0 ? -1 : index;
        }

        private void DoDownloadLogo(object sender, DoWorkEventArgs e)
        {
            var info = (GameInfo)e.Argument;

            this._LogosAttempted.Add(info.ImageUrl);

            using (WebClient downloader = new())
            {
                try
                {
                    var data = downloader.DownloadData(new Uri(info.ImageUrl));
                    using (MemoryStream stream = new(data, false))
                    {
                        Bitmap bitmap = new(stream);
                        e.Result = new LogoInfo(info.Id, bitmap);
                    }
                }
                catch (Exception)
                {
                    e.Result = new LogoInfo(info.Id, null);
                }
            }
        }

        private void OnDownloadLogo(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled == true)
            {
                return;
            }

            if (e.Result is LogoInfo logoInfo &&
                logoInfo.Bitmap != null &&
                this._Games.TryGetValue(logoInfo.Id, out var gameInfo) == true)
            {
                this._GameListView.BeginUpdate();
                var imageIndex = this._LogoImageList.Images.Count;
                this._LogoImageList.Images.Add(gameInfo.ImageUrl, logoInfo.Bitmap);
                gameInfo.ImageIndex = imageIndex;
                this._GameListView.EndUpdate();
            }

            this.DownloadNextLogo();
        }

        private void DownloadNextLogo()
        {
            lock (this._LogoLock)
            {

                if (this._LogoWorker.IsBusy == true)
                {
                    return;
                }

                GameInfo info;
                while (true)
                {
                    if (this._LogoQueue.TryDequeue(out info) == false)
                    {
                        this._DownloadStatusLabel.Visible = false;
                        return;
                    }

                    if (info.Item == null)
                    {
                        continue;
                    }

                    if (this._FilteredGames.Contains(info) == false ||
                        info.Item.Bounds.IntersectsWith(this._GameListView.ClientRectangle) == false)
                    {
                        this._LogosAttempting.Remove(info.ImageUrl);
                        continue;
                    }

                    break;
                }

                this._DownloadStatusLabel.Text = $"Downloading {1 + this._LogoQueue.Count} game icons...";
                this._DownloadStatusLabel.Visible = true;

                this._LogoWorker.RunWorkerAsync(info);
            }
        }

        private string GetGameImageUrl(uint id)
        {
            string candidate;

            var currentLanguage = this._SteamClient.SteamApps008.GetCurrentGameLanguage();

            candidate = this._SteamClient.SteamApps001.GetAppData(id, _($"small_capsule/{currentLanguage}"));
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
            }

            if (currentLanguage != "english")
            {
                candidate = this._SteamClient.SteamApps001.GetAppData(id, "small_capsule/english");
                if (string.IsNullOrEmpty(candidate) == false)
                {
                    return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
                }
            }

            candidate = this._SteamClient.SteamApps001.GetAppData(id, "logo");
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{id}/{candidate}.jpg");
            }

            return null;
        }

        private void AddGameToLogoQueue(GameInfo info)
        {
            if (info.ImageIndex > 0)
            {
                return;
            }

            var imageUrl = GetGameImageUrl(info.Id);
            if (string.IsNullOrEmpty(imageUrl) == true)
            {
                return;
            }

            info.ImageUrl = imageUrl;

            int imageIndex = this._LogoImageList.Images.IndexOfKey(imageUrl);
            if (imageIndex >= 0)
            {
                info.ImageIndex = imageIndex;
            }
            else if (
                this._LogosAttempting.Contains(imageUrl) == false &&
                this._LogosAttempted.Contains(imageUrl) == false)
            {
                this._LogosAttempting.Add(imageUrl);
                this._LogoQueue.Enqueue(info);
            }
        }

        private bool OwnsGame(uint id)
        {
            return this._SteamClient.SteamApps008.IsSubscribedApp(id);
        }

        private void AddGame(uint id, string type)
        {
            if (this._Games.ContainsKey(id) == true)
            {
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                return;
            }

            GameInfo info = new(id, type);
            info.Name = this._SteamClient.SteamApps001.GetAppData(info.Id, "name");
            this._Games.Add(id, info);
        }

        private void AddGames()
        {
            this._Games.Clear();
            this._RefreshGamesButton.Enabled = false;
            this._ListWorker.RunWorkerAsync();
        }

        private void AddDefaultGames()
        {
            this.AddGame(480, "normal"); // Spacewar
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnActivateGame(object sender, EventArgs e)
        {
            var focusedItem = (sender as MyListView)?.FocusedItem;
            var index = focusedItem != null ? focusedItem.Index : -1;
            if (index < 0 || index >= this._FilteredGames.Count)
            {
                return;
            }

            var info = this._FilteredGames[index];
            if (info == null)
            {
                return;
            }

            try
            {
                Process.Start("SAM.Game.exe", info.Id.ToString(CultureInfo.InvariantCulture));
            }
            catch (Win32Exception)
            {
                MessageBox.Show(
                    this,
                    "Failed to start SAM.Game.exe.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            this._AddGameTextBox.Text = "";
            this.AddGames();
        }

        private sealed class AutoUnlockArgs
        {
            public List<GameInfo> Games;
            public string ApiKey;
            public ulong SteamId;
        }

        private sealed class AutoUnlockResult
        {
            public int Total;
            public int Processed;
            public int SkippedComplete;
            public int SkippedNoAchievements;
            public bool UsedApi;
            public bool ApiFellBack;
        }

        private void OnConfigureApiKey(object sender, EventArgs e)
        {
            using var form = new ApiKeyForm(Settings.ApiKey);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                Settings.ApiKey = form.ApiKey;
            }
        }

        private void OnAutoUnlockAll(object sender, EventArgs e)
        {
            if (this._AutoUnlockWorker.IsBusy == true)
            {
                return;
            }

            var games = this._FilteredGames.ToList();
            if (games.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "There are no games in the current list to process.",
                    "Auto-Unlock All",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var apiKey = Settings.ApiKey;
            var steamId = this.GetSteamId();
            bool useApi = string.IsNullOrWhiteSpace(apiKey) == false && steamId != 0;

            var detection = useApi == true
                ? "Your Steam profile will be checked first so games that are already 100% (or have no achievements) are skipped."
                : "No Steam Web API key is set, so games without achievements are skipped but already-completed games can't be detected without opening them.\n\nTip: set an API key (the key button) for a much faster, smarter run.";

            if (MessageBox.Show(
                this,
                _($"This will unlock every non-protected achievement for the {games.Count} game(s) in the list.\n\n") +
                detection + "\n\n" +
                "Protected/online achievements (shown in red) are always skipped.\n\n" +
                "Continue?",
                "Auto-Unlock All",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            this._AutoUnlockAllButton.Enabled = false;
            this._RefreshGamesButton.Enabled = false;
            this._AutoUnlockWorker.RunWorkerAsync(new AutoUnlockArgs()
            {
                Games = games,
                ApiKey = apiKey,
                SteamId = steamId,
            });
        }

        private void DoAutoUnlockAll(object sender, DoWorkEventArgs e)
        {
            var args = (AutoUnlockArgs)e.Argument;
            var result = new AutoUnlockResult() { Total = args.Games.Count };

            bool useApi = string.IsNullOrWhiteSpace(args.ApiKey) == false && args.SteamId != 0;
            List<GameInfo> toProcess;

            if (useApi == true)
            {
                result.UsedApi = true;
                toProcess = this.ScanWithApi(args, result, out bool fellBack);
                result.ApiFellBack = fellBack;
            }
            else
            {
                toProcess = FilterLocal(args.Games, result);
            }

            int total = toProcess.Count;
            int index = 0;
            foreach (var game in toProcess)
            {
                if (this._AutoUnlockWorker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }

                index++;
                this._AutoUnlockWorker.ReportProgress(
                    total == 0 ? 100 : (int)(index * 100L / total),
                    _($"Unlocking {index}/{total}: {game.Name} ({game.Id})..."));

                if (LaunchAuto(game.Id) == true)
                {
                    result.Processed++;
                }
            }

            e.Result = result;
        }

        // Uses the Steam Web API to build the list of games that actually need
        // unlocking, skipping completed and achievement-less games. Falls back to
        // local detection if the API turns out to be unusable (bad key / private).
        private List<GameInfo> ScanWithApi(AutoUnlockArgs args, AutoUnlockResult result, out bool fellBack)
        {
            var incomplete = new ConcurrentBag<GameInfo>();
            int scanned = 0;
            int queriedOk = 0;
            int skippedComplete = 0;
            int skippedNoAchievements = 0;
            int total = args.Games.Count;

            var options = new ParallelOptions() { MaxDegreeOfParallelism = 6 };
            try
            {
                Parallel.ForEach(args.Games, options, (game, state) =>
                {
                    if (this._AutoUnlockWorker.CancellationPending == true)
                    {
                        state.Stop();
                        return;
                    }

                    var completion = SteamWebApi.GetPlayerAchievements(args.ApiKey, args.SteamId, game.Id);
                    if (completion.Queried == true)
                    {
                        Interlocked.Increment(ref queriedOk);
                        if (completion.HasStats == false)
                        {
                            Interlocked.Increment(ref skippedNoAchievements);
                        }
                        else if (completion.IsComplete == true)
                        {
                            Interlocked.Increment(ref skippedComplete);
                        }
                        else
                        {
                            incomplete.Add(game);
                        }
                    }
                    else
                    {
                        // Couldn't determine this one; include it to be safe.
                        incomplete.Add(game);
                    }

                    int done = Interlocked.Increment(ref scanned);
                    this._AutoUnlockWorker.ReportProgress(
                        (int)(done * 100L / total),
                        _($"Scanning profile {done}/{total}..."));
                });
            }
            catch (OperationCanceledException)
            {
            }

            // If nothing came back cleanly, the key/profile is unusable: fall back.
            if (queriedOk == 0)
            {
                fellBack = true;
                return FilterLocal(args.Games, result);
            }

            fellBack = false;
            result.SkippedComplete = skippedComplete;
            result.SkippedNoAchievements = skippedNoAchievements;
            return incomplete.ToList();
        }

        // Without an API key, skip games that have no local achievement schema.
        private static List<GameInfo> FilterLocal(List<GameInfo> games, AutoUnlockResult result)
        {
            var toProcess = new List<GameInfo>();
            foreach (var game in games)
            {
                if (HasLocalAchievements(game.Id) == true)
                {
                    toProcess.Add(game);
                }
                else
                {
                    result.SkippedNoAchievements++;
                }
            }
            return toProcess;
        }

        private static bool LaunchAuto(uint id)
        {
            try
            {
                var startInfo = new ProcessStartInfo("SAM.Game.exe", _($"{id} auto"))
                {
                    UseShellExecute = false,
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    // Give each game up to a minute; kill it if it hangs so the
                    // batch keeps moving.
                    if (process.WaitForExit(60000) == false)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnAutoUnlockAllProgress(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is string status)
            {
                this._PickerStatusLabel.Text = status;
            }
        }

        private void OnAutoUnlockAllCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this._AutoUnlockAllButton.Enabled = true;
            this._RefreshGamesButton.Enabled = true;

            if (e.Cancelled == true)
            {
                this._PickerStatusLabel.Text = "Auto-unlock all cancelled.";
                return;
            }

            var result = e.Result as AutoUnlockResult;
            if (result == null)
            {
                this._PickerStatusLabel.Text = "Auto-unlock all finished.";
                return;
            }

            this._PickerStatusLabel.Text =
                _($"Auto-unlock all finished. Processed {result.Processed} of {result.Total} game(s).");

            var summary = _($"Processed {result.Processed} game(s).\n");
            if (result.SkippedComplete > 0)
            {
                summary += _($"Skipped {result.SkippedComplete} already at 100%.\n");
            }
            if (result.SkippedNoAchievements > 0)
            {
                summary += _($"Skipped {result.SkippedNoAchievements} with no achievements.\n");
            }
            if (result.UsedApi == true && result.ApiFellBack == true)
            {
                summary += "\nNote: the Steam Web API key/profile looked unusable (private profile or bad key), so local detection was used instead.";
            }

            MessageBox.Show(
                this,
                summary,
                "Auto-Unlock All",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnAddGame(object sender, EventArgs e)
        {
            uint id;

            if (uint.TryParse(this._AddGameTextBox.Text, out id) == false)
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid game ID.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                MessageBox.Show(this, "You don't own that game.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            while (this._LogoQueue.TryDequeue(out var logo) == true)
            {
                // clear the download queue because we will be showing only one app
                this._LogosAttempted.Remove(logo.ImageUrl);
            }

            this._AddGameTextBox.Text = "";
            this._Games.Clear();
            this.AddGame(id, "normal");
            this._FilterGamesMenuItem.Checked = true;
            this.RefreshGames();
            this.DownloadNextLogo();
        }

        private void OnFilterUpdate(object sender, EventArgs e)
        {
            this.RefreshGames();

            // Compatibility with _GameListView SearchForVirtualItemEventHandler (otherwise _SearchGameTextBox loose focus on KeyUp)
            this._SearchGameTextBox.Focus();
        }

        private void OnGameListViewDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;

            if (e.Item.Bounds.IntersectsWith(this._GameListView.ClientRectangle) == false)
            {
                return;
            }

            var info = this._FilteredGames[e.ItemIndex];
            if (info.ImageIndex <= 0)
            {
                this.AddGameToLogoQueue(info);
                this.DownloadNextLogo();
            }
        }
    }
}
