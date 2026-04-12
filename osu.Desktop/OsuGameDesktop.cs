// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using osu.Desktop.Performance;
using osu.Desktop.Security;
using osu.Desktop.Updater;
using osu.Framework.Platform;
using osu.Game;
using osu.Framework;
using osu.Framework.Logging;
using osu.Game.Updater;
using osu.Desktop.Windows;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Online.Multiplayer;
using osu.Game.Performance;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Utils;

namespace osu.Desktop
{
    internal partial class OsuGameDesktop : OsuGame
    {
        private OsuSchemeLinkIPCChannel? osuSchemeLinkIPCChannel;
        private ArchiveImportIPCChannel? archiveImportIPCChannel;
        private BmsBeatmapImporter? bmsBeatmapImporter;
        private ManiaBeatmapImporter? maniaBeatmapImporter;
        private ExternalLibraryScanner? externalLibraryScanner;
        private ExternalLibraryConfig? externalLibraryConfig;
        private DependencyContainer desktopDependencies = null!;

        [Cached(typeof(IHighPerformanceSessionManager))]
        private readonly HighPerformanceSessionManager highPerformanceSessionManager = new HighPerformanceSessionManager();

        public bool IsFirstRun { get; init; }

        public OsuGameDesktop(string[]? args = null)
            : base(args)
        {
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            desktopDependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        public override StableStorage? GetStorageForStableInstall()
        {
            try
            {
                if (Host is DesktopGameHost desktopHost)
                {
                    string? stablePath = getStableInstallPath();
                    if (!string.IsNullOrEmpty(stablePath))
                        return new StableStorage(stablePath, desktopHost);
                }
            }
            catch (Exception)
            {
                Logger.Log("Could not find a stable install", LoggingTarget.Runtime, LogLevel.Important);
            }

            return null;
        }

        private string? getStableInstallPath()
        {
            static bool checkExists(string p) => Directory.Exists(Path.Combine(p, "Songs")) || File.Exists(Path.Combine(p, "osu!.cfg"));

            string? stableInstallPath;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stableInstallPath = getStableInstallPathFromRegistry("osustable.File.osz");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;

                    stableInstallPath = getStableInstallPathFromRegistry("osu!");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }
            }

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            return null;
        }

        [SupportedOSPlatform("windows")]
        private string? getStableInstallPathFromRegistry(string progId)
        {
            using (RegistryKey? key = Registry.ClassesRoot.OpenSubKey(progId))
                return key?.OpenSubKey(WindowsAssociationManager.SHELL_OPEN_COMMAND)?.GetValue(string.Empty)?.ToString()?.Split('"')[1].Replace("osu!.exe", "");
        }

        public static bool IsPackageManaged => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_PROVIDER"));

        // OMS 当前首发阶段只支持便携整包发布与手工覆盖更新，暂不开放游戏内在线更新。
        public static bool IsInAppUpdateEnabled => false;

        protected override UpdateManager CreateUpdateManager()
        {
            // If this is the first time we've run the game, ie it is being installed,
            // reset the user's release stream to "lazer".
            //
            // This ensures that if a user is trying to recover from a failed startup on an unstable release stream,
            // the game doesn't immediately try and update them back to the release stream after starting up.
            if (IsFirstRun)
                LocalConfig.SetValue(OsuSetting.ReleaseStream, ReleaseStream.Lazer);

            if (!IsInAppUpdateEnabled)
                return new UpdateManager();

            if (IsPackageManaged)
                return new NoActionUpdateManager();

            return new osu.Desktop.Updater.VelopackUpdateManager();
        }

        public override bool RestartAppWhenExited()
        {
            if (!IsInAppUpdateEnabled)
                return false;

            return base.RestartAppWhenExited();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Cache external library services during BDL so they are available before
            // the Settings overlay async-loads (loadComponentSingleFile uses Schedule,
            // but dependency resolution must find these already registered).
            externalLibraryConfig = new ExternalLibraryConfig(Storage);
            externalLibraryScanner = new ExternalLibraryScanner(externalLibraryConfig);

            desktopDependencies.CacheAs(externalLibraryConfig);
            desktopDependencies.CacheAs(externalLibraryScanner);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bmsBeatmapImporter = new BmsBeatmapImporter(Storage, ClientRealm)
            {
                PostNotification = notification => BeatmapManager.PostNotification?.Invoke(notification)
            };
            RegisterImportHandler(bmsBeatmapImporter);

            maniaBeatmapImporter = new ManiaBeatmapImporter(Storage, ClientRealm)
            {
                PostNotification = notification => BeatmapManager.PostNotification?.Invoke(notification)
            };
            RegisterImportHandler(maniaBeatmapImporter);

            // Wire importer delegates now that importers are created.
            externalLibraryScanner!.BmsDirectoryImporter = (path, ct) => bmsBeatmapImporter.Import(path);
            externalLibraryScanner!.ManiaDirectoryImporter = (path, ct) => maniaBeatmapImporter.Import(path);

            if (OnlineFeaturesEnabled)
                LoadComponentAsync(new DiscordRichPresence(), Add);

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                LoadComponentAsync(new GameplayWinKeyBlocker(), Add);
                Add(new WindowsRawKeyboardSource());
            }

            LoadComponentAsync(new ElevatedPrivilegesChecker(), Add);

            osuSchemeLinkIPCChannel = new OsuSchemeLinkIPCChannel(Host, this);
            archiveImportIPCChannel = new ArchiveImportIPCChannel(Host, this);
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // Apple operating systems use a better icon provided via external assets.
            if (!RuntimeInfo.IsApple)
            {
                var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "lazer.ico");
                if (iconStream != null)
                    host.Window.SetIconFromStream(iconStream);
            }

            host.Window.Title = Name;
        }

        protected override BatteryInfo CreateBatteryInfo() => FrameworkEnvironment.UseSDL3 ? new SDL3BatteryInfo() : new SDL2BatteryInfo();

        protected override IEnumerable<ICustomBeatmapLoader> CreateCustomBeatmapLoaders() => new ICustomBeatmapLoader[]
        {
            new BmsBeatmapLoader()
        };

        /// <summary>
        /// Expose the external library scanner for use by settings UI or console commands.
        /// </summary>
        public ExternalLibraryScanner? ExternalLibraryScanner => externalLibraryScanner;

        /// <summary>
        /// Expose the external library config for use by settings UI.
        /// </summary>
        public ExternalLibraryConfig? ExternalLibraryConfig => externalLibraryConfig;

        protected override void Dispose(bool isDisposing)
        {
            if (maniaBeatmapImporter != null)
                UnregisterImportHandler(maniaBeatmapImporter);

            if (bmsBeatmapImporter != null)
                UnregisterImportHandler(bmsBeatmapImporter);

            base.Dispose(isDisposing);
            osuSchemeLinkIPCChannel?.Dispose();
            archiveImportIPCChannel?.Dispose();
        }
    }
}
