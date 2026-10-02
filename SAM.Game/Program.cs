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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using SAM.Common;

namespace SAM.Game
{
    internal static class Program
    {
        // Usage:
        //   SAM.Game.exe <appId>        open the manager for a game
        //   SAM.Game.exe <appId> auto   headless: unlock everything allowed, exit
        //                               with the unlocked count (see AutoUnlockProtocol)
        [STAThread]
        public static int Main(string[] args)
        {
            bool autoMode = args.Length > 1 &&
                string.Equals(args[1], AutoUnlockProtocol.Argument, StringComparison.OrdinalIgnoreCase);

            if (autoMode == false)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }

            if (args.Length == 0)
            {
                StartPicker();
                return 0;
            }

            if (long.TryParse(args[0], out long appId) == false)
            {
                return Fail(autoMode, AutoUnlockProtocol.UnexpectedError,
                    "Could not parse application ID from command line argument.");
            }

            if (API.Steam.GetInstallPath() == Application.StartupPath)
            {
                return Fail(autoMode, AutoUnlockProtocol.UnexpectedError,
                    "This tool declines to being run from the Steam directory.");
            }

            using API.Client client = new();
            try
            {
                client.Initialize(appId);
            }
            catch (API.ClientInitializeException e)
            {
                var message = "Steam is not running. Please start Steam then run this tool again.";
                if (e.Failure == API.ClientInitializeFailure.ConnectToGlobalUser)
                {
                    message +=
                        "\n\nIf you have the game through Family Share, the game may be locked " +
                        "because the Family Share account is playing a game.";
                }
                if (string.IsNullOrEmpty(e.Message) == false)
                {
                    message += "\n\n(" + e.Message + ")";
                }
                return Fail(autoMode, AutoUnlockProtocol.SteamUnavailable, message);
            }
            catch (DllNotFoundException)
            {
                return Fail(autoMode, AutoUnlockProtocol.SteamUnavailable,
                    "Couldn't load the Steam client library. Is Steam installed?");
            }

            if (autoMode == true)
            {
                try
                {
                    return AutoUnlocker.Run(appId, client);
                }
                catch (Exception)
                {
                    return AutoUnlockProtocol.UnexpectedError;
                }
            }

            ToolStripManager.Renderer = new DarkToolStripRenderer();
            Application.Run(new Manager(appId, client));
            return 0;
        }

        private static void StartPicker()
        {
            try
            {
                Process.Start(Path.Combine(Application.StartupPath, "SAM.Picker.exe"));
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Couldn't start SAM.Picker.exe. Make sure it is next to SAM.Game.exe.",
                    "Steam Achievement Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Headless runs must never block on a dialog: they only report an exit code.
        private static int Fail(bool silent, int exitCode, string message)
        {
            if (silent == false)
            {
                MessageBox.Show(message, "Steam Achievement Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return exitCode;
        }
    }
}
