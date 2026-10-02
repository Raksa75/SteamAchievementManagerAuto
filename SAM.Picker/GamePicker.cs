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
            Common.Theme.SetCueBanner(this._SearchGameTextBox, "Search games");
            Common.Theme.SetCueBanner(this._AddGameTextBox, "App ID");
            this.UpdateApiKeyButton();

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
            this._RefreshGamesButton.Enabled = this._AutoUnlockWorker.IsBusy == false;
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
                $"Showing {this._GameListView.Items.Count} of {this._Games.Count} games. Double-click a game to manage it.";

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this._AutoUnlockWorker.IsBusy == true && e.CloseReason == CloseReason.UserClosing)
            {
                if (MessageBox.Show(
                    this,
                    "Auto-Unlock All is still running. Stop it and close?\n\nThe game currently being processed will finish on its own.",
                    "Auto-Unlock All",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                this._AutoUnlockWorker.CancelAsync();
            }

            base.OnFormClosing(e);
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
                Process.Start(
                    Path.Combine(Application.StartupPath, Common.AutoUnlockProtocol.GameExecutable),
                    info.Id.ToString(CultureInfo.InvariantCulture));
            }
            catch (Win32Exception)
            {
                MessageBox.Show(
                    this,
                    "Couldn't start SAM.Game.exe. Make sure it is next to SAM.Picker.exe.",
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

        private void OnConfigureApiKey(object sender, EventArgs e)
        {
            using ApiKeyForm form = new(Settings.ApiKey, this.GetSteamId());
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                Settings.ApiKey = form.ApiKey;
                this.UpdateApiKeyButton();
            }
        }

        private void UpdateApiKeyButton()
        {
            bool hasKey = string.IsNullOrWhiteSpace(Settings.ApiKey) == false;
            this._ApiKeyButton.Text = hasKey == true ? "API key \u2713" : "Set API key";
            this._ApiKeyButton.ToolTipText = hasKey == true
                ? "Steam Web API key set: Auto-Unlock All skips games that are already 100%."
                : "Set a Steam Web API key so Auto-Unlock All can skip games that are already 100%.";
        }

        private void OnAutoUnlockAll(object sender, EventArgs e)
        {
            if (this._AutoUnlockWorker.IsBusy == true)
            {
                // The button acts as "Stop" while a batch is running.
                this._AutoUnlockWorker.CancelAsync();
                this._AutoUnlockAllButton.Enabled = false;
                this._AutoUnlockAllButton.Text = "Stopping\u2026";
                return;
            }

            var games = this._FilteredGames.ToList();
            if (games.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "There are no games in the list. Adjust the search or filters first.",
                    "Auto-Unlock All",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var apiKey = Settings.ApiKey;
            var steamId = this.GetSteamId();
            bool useApi = string.IsNullOrWhiteSpace(apiKey) == false && steamId != 0;

            var message = _($"Unlock every achievement SAM can change in the {games.Count} game(s) currently listed?\n\n");
            message += useApi == true
                ? "Your Steam profile is checked first, so games that are already 100% or have no achievements are skipped."
                : "Tip: set a Steam Web API key (\"Set API key\") so games that are already 100% are skipped. " +
                  "Without it, every game with achievements is opened briefly.";
            message += "\n\nProtected achievements (shown in red) are never touched. You can stop at any time.";

            if (MessageBox.Show(
                this,
                message,
                "Auto-Unlock All",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            this.SetBatchRunning(true);
            this._AutoUnlockWorker.RunWorkerAsync(new AutoUnlockBatch.Options()
            {
                Games = games,
                ApiKey = apiKey,
                SteamId = steamId,
                GameExecutablePath = Path.Combine(Application.StartupPath, Common.AutoUnlockProtocol.GameExecutable),
            });
        }

        private void SetBatchRunning(bool running)
        {
            this._AutoUnlockAllButton.Enabled = true;
            this._AutoUnlockAllButton.Text = running == true ? "Stop" : "Auto-Unlock All";
            this._AutoUnlockAllButton.ToolTipText = running == true
                ? "Stop after the current game."
                : "Unlock every non-protected achievement in all listed games (red/online ones are skipped).";
            // Don't re-enable Refresh while the game list is still loading.
            this._RefreshGamesButton.Enabled = running == false && this._ListWorker.IsBusy == false;
            this._ApiKeyButton.Enabled = running == false;
            this._BatchProgressBar.Value = 0;
            this._BatchProgressBar.Visible = running;
        }

        private void DoAutoUnlockAll(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            e.Result = AutoUnlockBatch.Run(
                (AutoUnlockBatch.Options)e.Argument,
                (percent, status) => worker.ReportProgress(Math.Max(0, Math.Min(100, percent)), status),
                () => worker.CancellationPending);
        }

        private void OnAutoUnlockAllProgress(object sender, ProgressChangedEventArgs e)
        {
            this._BatchProgressBar.Value = e.ProgressPercentage;
            if (e.UserState is string status)
            {
                this._PickerStatusLabel.Text = status;
            }
        }

        private void OnAutoUnlockAllCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.SetBatchRunning(false);

            if (e.Error != null)
            {
                this._PickerStatusLabel.Text = "Auto-Unlock All failed.";
                MessageBox.Show(this, e.Error.ToString(), "Auto-Unlock All", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (e.Result is not AutoUnlockBatch.Result result)
            {
                return;
            }

            this._PickerStatusLabel.Text = result.Cancelled == true
                ? _($"Auto-Unlock All stopped: {result.AchievementsUnlocked} achievement(s) unlocked.")
                : _($"Auto-Unlock All finished: {result.AchievementsUnlocked} achievement(s) unlocked in {result.GamesUnlocked} game(s).");

            MessageBox.Show(
                this,
                FormatSummary(result),
                result.Cancelled == true ? "Auto-Unlock All stopped" : "Auto-Unlock All finished",
                MessageBoxButtons.OK,
                result.Failures.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private static string FormatSummary(AutoUnlockBatch.Result result)
        {
            var elapsed = result.Elapsed.TotalMinutes >= 1
                ? _($"{(int)result.Elapsed.TotalMinutes} min {result.Elapsed.Seconds} s")
                : _($"{result.Elapsed.Seconds} s");

            var lines = new List<string>()
            {
                _($"{result.AchievementsUnlocked} achievement(s) unlocked in {result.GamesUnlocked} game(s) ({elapsed})."),
                "",
            };

            if (result.SkippedComplete > 0)
            {
                lines.Add(_($"\u2022 {result.SkippedComplete} game(s) already at 100% (skipped)"));
            }
            if (result.GamesNothingToDo > 0)
            {
                lines.Add(_($"\u2022 {result.GamesNothingToDo} game(s) with only protected achievements left"));
            }
            if (result.SkippedNoAchievements > 0)
            {
                lines.Add(_($"\u2022 {result.SkippedNoAchievements} game(s) without achievements (skipped)"));
            }
            if (result.ApiUnusable == true)
            {
                lines.Add("");
                lines.Add("Your Steam Web API key or profile couldn't be used (invalid key, or profile/game " +
                          "details not public), so completed games couldn't be detected.");
            }
            if (result.Failures.Count > 0)
            {
                const int maxShown = 8;
                lines.Add("");
                lines.Add(_($"{result.Failures.Count} game(s) failed:"));
                lines.AddRange(result.Failures.Take(maxShown).Select(failure => $"   {failure.GameName}: {failure.Reason}"));
                if (result.Failures.Count > maxShown)
                {
                    lines.Add(_($"   \u2026and {result.Failures.Count - maxShown} more"));
                }
            }

            return string.Join("\n", lines).TrimEnd();
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
