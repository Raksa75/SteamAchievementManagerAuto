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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using static SAM.Game.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Game
{
    internal partial class Manager : Form
    {
        private readonly long _GameId;
        private readonly API.Client _SteamClient;

        private readonly WebClient _IconDownloader = new();

        private readonly List<Stats.AchievementInfo> _IconQueue = new();
        private readonly List<Stats.StatDefinition> _StatDefinitions = new();

        private readonly List<Stats.AchievementDefinition> _AchievementDefinitions = new();

        private readonly BindingList<Stats.StatInfo> _Statistics = new();

        private readonly API.Callbacks.UserStatsReceived _UserStatsReceivedCallback;

        //private API.Callback<APITypes.UserStatsStored> UserStatsStoredCallback;

        public Manager(long gameId, API.Client client)
        {
            this.InitializeComponent();

            this._MainTabControl.SelectedTab = this._AchievementsTabPage;
            //this.statisticsList.Enabled = this.checkBox1.Checked;

            this._AchievementImageList.Images.Add("Blank", new Bitmap(64, 64));

            this._StatisticsDataGridView.AutoGenerateColumns = false;

            this._StatisticsDataGridView.Columns.Add("name", "Name");
            this._StatisticsDataGridView.Columns[0].ReadOnly = true;
            this._StatisticsDataGridView.Columns[0].Width = 200;
            this._StatisticsDataGridView.Columns[0].DataPropertyName = "DisplayName";

            this._StatisticsDataGridView.Columns.Add("value", "Value");
            this._StatisticsDataGridView.Columns[1].ReadOnly = this._EnableStatsEditingCheckBox.Checked == false;
            this._StatisticsDataGridView.Columns[1].Width = 90;
            this._StatisticsDataGridView.Columns[1].DataPropertyName = "Value";

            this._StatisticsDataGridView.Columns.Add("extra", "Extra");
            this._StatisticsDataGridView.Columns[2].ReadOnly = true;
            this._StatisticsDataGridView.Columns[2].Width = 200;
            this._StatisticsDataGridView.Columns[2].DataPropertyName = "Extra";
            this._StatisticsDataGridView.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            this._StatisticsDataGridView.DataSource = new BindingSource()
            {
                DataSource = this._Statistics,
            };

            this._GameId = gameId;
            this._SteamClient = client;

            this._IconDownloader.DownloadDataCompleted += this.OnIconDownload;

            string name = this._SteamClient.SteamApps001.GetAppData((uint)this._GameId, "name");
            if (string.IsNullOrEmpty(name) == true)
            {
                name = this._GameId.ToString(CultureInfo.InvariantCulture);
            }
            this.Text = $"{name} — Steam Achievement Manager";

            this._UserStatsReceivedCallback = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            this._UserStatsReceivedCallback.OnRun += this.OnUserStatsReceived;

            this._AchievementListView.ShowItemToolTips = true;
            this._AchievementListView.ItemChecked += this.OnAchievementChecked;
            this._AchievementListView.ClientSizeChanged += (sender, e) => this.FitAchievementColumns();

            Common.Theme.Apply(this);
            Common.Theme.StyleDetailsHeaders(this._AchievementListView);
            Common.Theme.SetCueBanner(this._MatchingStringTextBox, "Search achievements");

            this.RefreshStats();
        }

        private void AddAchievementIcon(Stats.AchievementInfo info, Image icon)
        {
            if (icon == null)
            {
                info.ImageIndex = 0;
            }
            else
            {
                info.ImageIndex = this._AchievementImageList.Images.Count;
                this._AchievementImageList.Images.Add(info.IsAchieved == true ? info.IconNormal : info.IconLocked, icon);
            }
        }

        private void OnIconDownload(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null && e.Cancelled == false)
            {
                var info = (Stats.AchievementInfo)e.UserState;

                Bitmap bitmap;
                try
                {
                    using (MemoryStream stream = new())
                    {
                        stream.Write(e.Result, 0, e.Result.Length);
                        bitmap = new(stream);
                    }
                }
                catch (Exception)
                {
                    bitmap = null;
                }

                this.AddAchievementIcon(info, bitmap);
                this._AchievementListView.Update();
            }

            this.DownloadNextIcon();
        }

        private void DownloadNextIcon()
        {
            if (this._IconQueue.Count == 0)
            {
                this._DownloadStatusLabel.Visible = false;
                return;
            }

            if (this._IconDownloader.IsBusy == true)
            {
                return;
            }

            this._DownloadStatusLabel.Text = $"Downloading {this._IconQueue.Count} icons...";
            this._DownloadStatusLabel.Visible = true;

            var info = this._IconQueue[0];
            this._IconQueue.RemoveAt(0);


            this._IconDownloader.DownloadDataAsync(
                new Uri(_($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{this._GameId}/{(info.IsAchieved == true ? info.IconNormal : info.IconLocked)}")),
                info);
        }

        private static string TranslateError(int id) => id switch
        {
            2 => "generic error -- this usually means you don't own the game",
            _ => _($"{id}"),
        };

        private bool LoadUserGameStatsSchema()
        {
            var schema = Stats.GameSchema.Load(
                this._GameId,
                this._SteamClient.SteamApps008.GetCurrentGameLanguage());

            this._AchievementDefinitions.Clear();
            this._StatDefinitions.Clear();
            if (schema == null)
            {
                return false;
            }

            this._AchievementDefinitions.AddRange(schema.Achievements);
            this._StatDefinitions.AddRange(schema.Stats);
            return true;
        }

        private void OnUserStatsReceived(APITypes.UserStatsReceived param)
        {
            if (param.Result != 1)
            {
                this._GameStatusLabel.Text = $"Error while retrieving stats: {TranslateError(param.Result)}";
                this.EnableInput();
                return;
            }

            if (this.LoadUserGameStatsSchema() == false)
            {
                this._GameStatusLabel.Text = "This game has no achievements or stats (no schema found).";
                this.EnableInput();
                return;
            }

            try
            {
                this.GetAchievements();
                this.GetStatistics();
            }
            catch (Exception e)
            {
                this._GameStatusLabel.Text = "Error while reading achievements and stats.";
                this.EnableInput();
                MessageBox.Show(
                    this,
                    "Error while reading achievements and stats:\n" + e,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            this.EnableInput();
            this.RecountAchievements();
            this.UpdateStatus();
        }

        // Achievement totals, computed once per refresh (not per checkbox click).
        private int _TotalAchievements;
        private int _UnlockedAchievements;
        private int _LockedProtectedAchievements;

        // A short confirmation ("Saved 3 achievements") shown before the summary.
        private string _Notice;

        private void RecountAchievements()
        {
            this._TotalAchievements = 0;
            this._UnlockedAchievements = 0;
            this._LockedProtectedAchievements = 0;

            foreach (var definition in this._AchievementDefinitions)
            {
                if (string.IsNullOrEmpty(definition.Id) == true ||
                    this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(
                        definition.Id,
                        out bool isAchieved,
                        out var unlockTime) == false)
                {
                    continue;
                }

                this._TotalAchievements++;
                if (isAchieved == true)
                {
                    this._UnlockedAchievements++;
                }
                else if (definition.IsProtected == true)
                {
                    this._LockedProtectedAchievements++;
                }
            }
        }

        private int CountPendingChanges()
        {
            int pending = 0;
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo info && info.IsAchieved != item.Checked)
                {
                    pending++;
                }
            }
            return pending + this._Statistics.Count(stat => stat.IsModified == true);
        }

        private void UpdateStatus()
        {
            string summary;
            if (this._TotalAchievements == 0)
            {
                summary = $"No achievements · {this._Statistics.Count} statistics";
            }
            else
            {
                // Floor, so a game is never shown as 100% while one is missing.
                int percent = this._UnlockedAchievements * 100 / this._TotalAchievements;
                summary = $"{this._UnlockedAchievements} of {this._TotalAchievements} achievements unlocked ({percent}%)";
                if (this._LockedProtectedAchievements > 0)
                {
                    summary += $" · {this._LockedProtectedAchievements} protected";
                }
            }

            int pending = this.CountPendingChanges();
            if (pending > 0)
            {
                summary = $"{pending} unsaved change(s), Ctrl+S to save · {summary}";
            }

            this._GameStatusLabel.Text = string.IsNullOrEmpty(this._Notice) == true
                ? summary
                : $"{this._Notice} · {summary}";
        }

        // The description column takes whatever width is left.
        private void FitAchievementColumns()
        {
            Common.Theme.FillColumn(this._AchievementListView, this._AchievementDescriptionColumnHeader.Index);
        }

        private void OnAchievementChecked(object sender, ItemCheckedEventArgs e)
        {
            if (this._IsUpdatingAchievementList == false)
            {
                this.UpdateStatus();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5 when this._ReloadButton.Enabled == true:
                    this.OnRefresh(this, EventArgs.Empty);
                    return true;

                case Keys.Control | Keys.S when this._StoreButton.Enabled == true:
                    this.OnStore(this, EventArgs.Empty);
                    return true;

                case Keys.Control | Keys.F:
                    this._MainTabControl.SelectedTab = this._AchievementsTabPage;
                    this._MatchingStringTextBox.Focus();
                    this._MatchingStringTextBox.SelectAll();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshStats()
        {
            this._AchievementListView.Items.Clear();
            this._StatisticsDataGridView.Rows.Clear();

            var steamId = this._SteamClient.SteamUser.GetSteamId();

            // This still triggers the UserStatsReceived callback, in addition to the callresult.
            // No need to implement callresults for the time being.
            var callHandle = this._SteamClient.SteamUserStats.RequestUserStats(steamId);
            if (callHandle == API.CallHandle.Invalid)
            {
                MessageBox.Show(this, "Failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this._GameStatusLabel.Text = "Retrieving stat information...";
            this.DisableInput();
        }

        private bool _IsUpdatingAchievementList;

        private void GetAchievements()
        {
            var textSearch = this._MatchingStringTextBox.Text.Length > 0
                ? this._MatchingStringTextBox.Text
                : null;

            this._IsUpdatingAchievementList = true;

            this._AchievementListView.Items.Clear();
            this._AchievementListView.BeginUpdate();
            //this.Achievements.Clear();

            bool wantLocked = this._DisplayLockedOnlyButton.Checked == true;
            bool wantUnlocked = this._DisplayUnlockedOnlyButton.Checked == true;

            foreach (var def in this._AchievementDefinitions)
            {
                if (string.IsNullOrEmpty(def.Id) == true)
                {
                    continue;
                }

                if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(
                    def.Id,
                    out bool isAchieved,
                    out var unlockTime) == false)
                {
                    continue;
                }

                bool wanted = (wantLocked == false && wantUnlocked == false) || isAchieved switch
                {
                    true => wantUnlocked,
                    false => wantLocked,
                };
                if (wanted == false)
                {
                    continue;
                }

                if (textSearch != null)
                {
                    if (def.Name.IndexOf(textSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                        def.Description.IndexOf(textSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                Stats.AchievementInfo info = new()
                {
                    Id = def.Id,
                    IsAchieved = isAchieved,
                    UnlockTime = isAchieved == true && unlockTime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                        : null,
                    IconNormal = string.IsNullOrEmpty(def.IconNormal) ? null : def.IconNormal,
                    IconLocked = string.IsNullOrEmpty(def.IconLocked) ? def.IconNormal : def.IconLocked,
                    Permission = def.Permission,
                    Name = def.Name,
                    Description = def.Description,
                };

                ListViewItem item = new()
                {
                    Checked = isAchieved,
                    Tag = info,
                    Text = info.Name,
                    BackColor = def.IsProtected == true ? Common.Theme.DangerSurface : Common.Theme.Surface,
                    ForeColor = Common.Theme.TextPrimary,
                    ToolTipText = def.IsProtected == true
                        ? "Protected: only the game itself (online/server-side) can unlock this achievement."
                        : info.Description,
                };

                info.Item = item;

                if (item.Text.StartsWith("#", StringComparison.InvariantCulture) == true)
                {
                    item.Text = info.Id;
                    item.SubItems.Add("");
                }
                else
                {
                    item.SubItems.Add(info.Description);
                }

                item.SubItems.Add(info.UnlockTime.HasValue == true
                    ? info.UnlockTime.Value.ToString()
                    : "");

                info.ImageIndex = 0;

                this.AddAchievementToIconQueue(info, false);
                this._AchievementListView.Items.Add(item);
            }

            this._AchievementListView.EndUpdate();
            this._IsUpdatingAchievementList = false;
            this.FitAchievementColumns();

            this.DownloadNextIcon();
        }

        private void GetStatistics()
        {
            this._Statistics.Clear();
            foreach (var stat in this._StatDefinitions)
            {
                if (string.IsNullOrEmpty(stat.Id) == true)
                {
                    continue;
                }

                if (stat is Stats.IntegerStatDefinition intStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(intStat.Id, out int value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.IntStatInfo()
                    {
                        Id = intStat.Id,
                        DisplayName = intStat.DisplayName,
                        IntValue = value,
                        OriginalValue = value,
                        IsIncrementOnly = intStat.IncrementOnly,
                        Permission = intStat.Permission,
                    });
                }
                else if (stat is Stats.FloatStatDefinition floatStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(floatStat.Id, out float value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.FloatStatInfo()
                    {
                        Id = floatStat.Id,
                        DisplayName = floatStat.DisplayName,
                        FloatValue = value,
                        OriginalValue = value,
                        IsIncrementOnly = floatStat.IncrementOnly,
                        Permission = floatStat.Permission,
                    });
                }
            }
        }

        private void AddAchievementToIconQueue(Stats.AchievementInfo info, bool startDownload)
        {
            int imageIndex = this._AchievementImageList.Images.IndexOfKey(
                info.IsAchieved == true ? info.IconNormal : info.IconLocked);

            if (imageIndex >= 0)
            {
                info.ImageIndex = imageIndex;
            }
            else
            {
                this._IconQueue.Add(info);

                if (startDownload == true)
                {
                    this.DownloadNextIcon();
                }
            }
        }

        private int StoreAchievements()
        {
            if (this._AchievementListView.Items.Count == 0)
            {
                return 0;
            }

            List<Stats.AchievementInfo> achievements = new();
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is not Stats.AchievementInfo achievementInfo ||
                    achievementInfo.IsAchieved == item.Checked)
                {
                    continue;
                }

                achievementInfo.IsAchieved = item.Checked;
                achievements.Add(achievementInfo);
            }

            if (achievements.Count == 0)
            {
                return 0;
            }

            foreach (var info in achievements)
            {
                if (this._SteamClient.SteamUserStats.SetAchievement(info.Id, info.IsAchieved) == false)
                {
                    MessageBox.Show(
                        this,
                        $"An error occurred while setting the state for {info.Id}, aborting store.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return -1;
                }
            }

            return achievements.Count;
        }

        private int StoreStatistics()
        {
            if (this._Statistics.Count == 0)
            {
                return 0;
            }

            var statistics = this._Statistics.Where(stat => stat.IsModified == true).ToList();
            if (statistics.Count == 0)
            {
                return 0;
            }

            foreach (var stat in statistics)
            {
                if (stat is Stats.IntStatInfo intStat)
                {
                    if (this._SteamClient.SteamUserStats.SetStatValue(
                        intStat.Id,
                        intStat.IntValue) == false)
                    {
                        MessageBox.Show(
                            this,
                            $"An error occurred while setting the value for {stat.Id}, aborting store.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return -1;
                    }
                }
                else if (stat is Stats.FloatStatInfo floatStat)
                {
                    if (this._SteamClient.SteamUserStats.SetStatValue(
                        floatStat.Id,
                        floatStat.FloatValue) == false)
                    {
                        MessageBox.Show(
                            this,
                            $"An error occurred while setting the value for {stat.Id}, aborting store.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return -1;
                    }
                }
                else
                {
                    throw new InvalidOperationException("unsupported stat type");
                }
            }

            return statistics.Count;
        }

        private void DisableInput()
        {
            this._ReloadButton.Enabled = false;
            this._StoreButton.Enabled = false;
            this._AutoUnlockButton.Enabled = false;
        }

        private void EnableInput()
        {
            this._ReloadButton.Enabled = true;
            this._StoreButton.Enabled = true;
            this._AutoUnlockButton.Enabled = true;
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            this._Notice = null;
            this.RefreshStats();
        }

        // Bulk check/uncheck of the visible list. Protected achievements are left
        // alone (instead of popping one error dialog per protected achievement).
        private void SetAllChecked(Func<bool, bool> newState)
        {
            this._IsUpdatingAchievementList = true;
            this._AchievementListView.BeginUpdate();
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo info && info.IsProtected == false)
                {
                    item.Checked = newState(item.Checked);
                }
            }
            this._AchievementListView.EndUpdate();
            this._IsUpdatingAchievementList = false;
            this.UpdateStatus();
        }

        private void OnLockAll(object sender, EventArgs e) => this.SetAllChecked(_ => false);

        private void OnInvertAll(object sender, EventArgs e) => this.SetAllChecked(isChecked => !isChecked);

        private void OnUnlockAll(object sender, EventArgs e) => this.SetAllChecked(_ => true);

        private void OnAutoUnlock(object sender, EventArgs e)
        {
            var unlockable = AutoUnlocker.GetUnlockable(
                this._SteamClient,
                this._AchievementDefinitions,
                out int lockedProtected);

            if (unlockable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    lockedProtected == 0
                        ? "Nothing to unlock: this game is already at 100%."
                        : "Nothing to unlock: every achievement SAM can change is already unlocked.\n\n" +
                          $"{lockedProtected} protected achievement(s) can only be earned by playing.",
                    "Auto-Unlock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var question = $"Unlock {unlockable.Count} achievement(s) and save them to Steam?";
            if (lockedProtected > 0)
            {
                question += $"\n\n{lockedProtected} protected achievement(s) (shown in red) will stay locked.";
            }

            int pending = this.CountPendingChanges();
            if (pending > 0)
            {
                question += $"\n\nYour {pending} unsaved change(s) will be discarded.";
            }

            if (MessageBox.Show(
                this,
                question,
                "Auto-Unlock",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            int unlocked = AutoUnlocker.SetUnlocked(this._SteamClient, unlockable, out int failed);
            if (unlocked == 0)
            {
                MessageBox.Show(
                    this,
                    "Steam refused to unlock these achievements.",
                    "Auto-Unlock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            else if (this.Store() == true)
            {
                this._Notice = failed == 0
                    ? $"✓ Unlocked {unlocked} achievement(s)"
                    : $"✓ Unlocked {unlocked} achievement(s), {failed} failed";
            }

            this.RefreshStats();
        }

        private bool Store()
        {
            if (this._SteamClient.SteamUserStats.StoreStats() == false)
            {
                MessageBox.Show(
                    this,
                    "An error occurred while storing, aborting.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void OnStore(object sender, EventArgs e)
        {
            // Commit a stat value that is still being edited (e.g. Ctrl+S in a cell).
            this._StatisticsDataGridView.EndEdit();

            if (this.CountPendingChanges() == 0)
            {
                this._Notice = "Nothing to save";
                this.UpdateStatus();
                return;
            }

            int achievements = this.StoreAchievements();
            if (achievements < 0)
            {
                this.RefreshStats();
                return;
            }

            int stats = this.StoreStatistics();
            if (stats < 0)
            {
                this.RefreshStats();
                return;
            }

            if (this.Store() == false)
            {
                this.RefreshStats();
                return;
            }

            this._Notice = $"✓ Saved {achievements} achievement(s) and {stats} statistic(s)";
            this.RefreshStats();
        }

        private void OnStatDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.Context != DataGridViewDataErrorContexts.Commit)
            {
                return;
            }

            var view = (DataGridView)sender;
            if (e.Exception is Stats.StatIsProtectedException)
            {
                e.ThrowException = false;
                e.Cancel = true;
                view.Rows[e.RowIndex].ErrorText = "Stat is protected! -- you can't modify it";
            }
            else
            {
                e.ThrowException = false;
                e.Cancel = true;
                view.Rows[e.RowIndex].ErrorText = "Invalid value";
            }
        }

        private void OnStatAgreementChecked(object sender, EventArgs e)
        {
            this._StatisticsDataGridView.Columns[1].ReadOnly = this._EnableStatsEditingCheckBox.Checked == false;
        }

        private void OnStatCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var view = (DataGridView)sender;
            view.Rows[e.RowIndex].ErrorText = "";
            this.UpdateStatus();
        }

        private void OnResetAllStats(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "Are you absolutely sure you want to reset stats?",
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }

            bool achievementsToo = DialogResult.Yes == MessageBox.Show(
                "Do you want to reset achievements too?",
                "Question",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (MessageBox.Show(
                "Really really sure?",
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error) == DialogResult.No)
            {
                return;
            }

            if (this._SteamClient.SteamUserStats.ResetAllStats(achievementsToo) == false)
            {
                MessageBox.Show(this, "Failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.RefreshStats();
        }

        private void OnCheckAchievement(object sender, ItemCheckEventArgs e)
        {
            if (sender != this._AchievementListView)
            {
                return;
            }

            if (this._IsUpdatingAchievementList == true)
            {
                return;
            }

            if (this._AchievementListView.Items[e.Index].Tag is not Stats.AchievementInfo info)
            {
                return;
            }

            if (info.IsProtected == true)
            {
                MessageBox.Show(
                    this,
                    "This achievement is protected: only the game itself (online/server-side) can unlock it, " +
                    "so Steam Achievement Manager can't change it.",
                    "Protected achievement",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                e.NewValue = e.CurrentValue;
            }
        }

        private void OnDisplayUncheckedOnly(object sender, EventArgs e)
        {
            if ((sender as ToolStripButton).Checked == true)
            {
                this._DisplayLockedOnlyButton.Checked = false;
            }

            this.GetAchievements();
        }

        private void OnDisplayCheckedOnly(object sender, EventArgs e)
        {
            if ((sender as ToolStripButton).Checked == true)
            {
                this._DisplayUnlockedOnlyButton.Checked = false;
            }

            this.GetAchievements();
        }

        private void OnFilterUpdate(object sender, KeyEventArgs e)
        {
            this.GetAchievements();
        }
    }
}
