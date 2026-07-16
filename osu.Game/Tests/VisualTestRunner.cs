// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Platform;

#if OSU_EXECUTABLE_TEST_PROJECT
using System.IO;
using System.Reflection;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.IO;
#endif

namespace osu.Game.Tests
{
    public static class VisualTestRunner
    {
        [STAThread]
        public static int Main(string[] args)
        {
#if OSU_EXECUTABLE_TEST_PROJECT
            return runExecutableTestProject(args);
#else
            return runLegacyTestBrowser();
#endif
        }

        private static int runLegacyTestBrowser()
        {
            using (DesktopGameHost host = Host.GetSuitableDesktopHost(OsuGameBase.STORAGE_NAME))
            {
                host.Run(new OsuTestBrowser());
                return 0;
            }
        }

#if OSU_EXECUTABLE_TEST_PROJECT
        internal const string EXACT_TEST_ARGUMENT = "--exact-test";
        internal const string EXACT_HOST_STORAGE_PREFIX = "oms-exact-visual-test-host-";

        internal const int EXIT_SUCCESS = 0;
        internal const int EXIT_FAILURE = 1;
        internal const int EXIT_ABORTED = 3;

        private static int runExecutableTestProject(string[] args)
        {
            if (!TryParseExactTestArguments(args, out string? initialTestName, out string? argumentError))
            {
                Console.Error.WriteLine(argumentError);
                return EXIT_FAILURE;
            }

            // Keep the pre-existing TestBrowser launch behaviour for IDE-supplied or otherwise unknown arguments.
            // Strict argument handling is intentionally limited to the dedicated exact-test mode.
            if (initialTestName == null)
                return runLegacyTestBrowser();

            string startupFrameworkConfig = Path.Combine(RuntimeInfo.StartupDirectory, "framework.ini");

            if (File.Exists(startupFrameworkConfig))
            {
                Console.Error.WriteLine($"Exact visual tests refuse startup-directory framework storage: {startupFrameworkConfig}");
                return EXIT_FAILURE;
            }

            Type? initialTest = Assembly.GetEntryAssembly()?.GetType(initialTestName, throwOnError: false, ignoreCase: false);

            if (!isRunnableExactTest(initialTest))
            {
                Console.Error.WriteLine($"Runnable visual test scene was not found in the entry assembly: {initialTestName}");
                return EXIT_FAILURE;
            }

            // Neither storage name is caller-controlled. This prevents exact mode from ever resolving to a product
            // AppData alias even if its launcher is invoked with hostile or stale arguments.
            string hostStorageName = $"{EXACT_HOST_STORAGE_PREFIX}{Guid.NewGuid():N}";
            string applicationDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);

            if (!TryResolveExactHostStoragePath(applicationDataRoot, hostStorageName, out string? hostStoragePath))
            {
                Console.Error.WriteLine("Exact visual test host storage did not resolve to a unique direct ApplicationData child.");
                return EXIT_FAILURE;
            }

            int result = EXIT_FAILURE;
            bool cleanupSucceeded = true;

            try
            {
                try
                {
                    using DesktopGameHost host = Host.GetSuitableDesktopHost(hostStorageName);
                    var exactGame = new ExactVisualTestGame(initialTest!);
                    host.Run(exactGame);
                    result = exactGame.ExitCode;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Exact visual test failed before a controlled exit: {exception}");
                }
            }
            finally
            {
                try
                {
                    DeleteExactHostStorage(hostStoragePath!);
                }
                catch (Exception exception)
                {
                    cleanupSucceeded = false;
                    Console.Error.WriteLine($"Exact visual test host storage cleanup failed: {exception}");
                }
            }

            return cleanupSucceeded ? result : EXIT_FAILURE;
        }

        internal static bool TryParseExactTestArguments(string[] args, out string? testName, out string? error)
        {
            testName = null;
            error = null;

            bool exactModeRequested = Array.Exists(args, argument => string.Equals(argument, EXACT_TEST_ARGUMENT, StringComparison.Ordinal));

            if (!exactModeRequested)
                return true;

            if (args.Length != 2
                || !string.Equals(args[0], EXACT_TEST_ARGUMENT, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(args[1]))
            {
                error = $"Exact visual-test usage: {EXACT_TEST_ARGUMENT} <fully-qualified-test-scene-type>";
                return false;
            }

            testName = args[1];
            return true;
        }

        internal static bool TryResolveExactHostStoragePath(string applicationDataRoot, string hostStorageName, out string? hostStoragePath)
        {
            hostStoragePath = null;

            if (string.IsNullOrWhiteSpace(applicationDataRoot)
                || string.IsNullOrEmpty(hostStorageName)
                || !Path.IsPathFullyQualified(applicationDataRoot)
                || !hostStorageName.StartsWith(EXACT_HOST_STORAGE_PREFIX, StringComparison.Ordinal))
                return false;

            string guidSuffix = hostStorageName[EXACT_HOST_STORAGE_PREFIX.Length..];

            if (!Guid.TryParseExact(guidSuffix, "N", out Guid storageId)
                || !string.Equals(guidSuffix, storageId.ToString("N"), StringComparison.Ordinal))
                return false;

            try
            {
                string suppliedRoot = Path.TrimEndingDirectorySeparator(applicationDataRoot);
                string resolvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDataRoot));

                // Reject lexical aliases (for example a root containing '..') even if they canonicalise to AppData.
                if (!string.Equals(suppliedRoot, resolvedRoot, StringComparison.OrdinalIgnoreCase))
                    return false;

                string resolvedHostStorage = Path.GetFullPath(Path.Combine(resolvedRoot, hostStorageName));

                if (!string.Equals(Path.GetDirectoryName(resolvedHostStorage), resolvedRoot, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetFileName(resolvedHostStorage), hostStorageName, StringComparison.Ordinal))
                    return false;

                hostStoragePath = resolvedHostStorage;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool isRunnableExactTest(Type? testType)
        {
            return testType != null
                   && typeof(TestScene).IsAssignableFrom(testType)
                   && testType.IsPublic
                   && !testType.IsAbstract
                   && testType.GetConstructor(Type.EmptyTypes) != null
                   && testType.GetCustomAttribute<HeadlessTestAttribute>(inherit: true) == null
                   && testType.IsSupportedOnCurrentOSPlatform();
        }

        /// <summary>
        /// Deletes a host storage path already validated by <see cref="TryResolveExactHostStoragePath"/> without
        /// following reparse points. Kept internal for headless safety tests.
        /// </summary>
        internal static void DeleteExactHostStorage(string hostStoragePath)
        {
            FileAttributes rootAttributes;

            try
            {
                rootAttributes = File.GetAttributes(hostStoragePath);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if (!rootAttributes.HasFlag(FileAttributes.Directory))
                throw new IOException($"Exact visual test host storage is not a directory: {hostStoragePath}");

            deleteDirectoryWithoutFollowingReparsePoints(hostStoragePath, rootAttributes);
        }

        private static void deleteDirectoryWithoutFollowingReparsePoints(string directoryPath, FileAttributes directoryAttributes)
        {
            if (directoryAttributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException($"Refusing to traverse a reparse point during exact visual test cleanup: {directoryPath}");

            foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath, "*", SearchOption.TopDirectoryOnly))
            {
                FileAttributes entryAttributes = File.GetAttributes(entryPath);

                if (entryAttributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Refusing to traverse a reparse point during exact visual test cleanup: {entryPath}");

                if (entryAttributes.HasFlag(FileAttributes.Directory))
                    deleteDirectoryWithoutFollowingReparsePoints(entryPath, entryAttributes);
                else
                {
                    File.SetAttributes(entryPath, FileAttributes.Normal);
                    File.Delete(entryPath);
                }
            }

            File.SetAttributes(directoryPath, FileAttributes.Normal);
            Directory.Delete(directoryPath, recursive: false);
        }
#endif
    }

#if OSU_EXECUTABLE_TEST_PROJECT
    /// <summary>
    /// Marker cached only by <see cref="ExactVisualTestGame"/> so destructive visual gates can reject an ordinary TestBrowser host.
    /// </summary>
    internal sealed class ExactVisualTestIsolation
    {
    }

    /// <summary>
    /// Test-runner-only game which opens one exact visual test without relying on mutable visualtests.cfg state.
    /// </summary>
    internal partial class ExactVisualTestGame : OsuGameBase
    {
        private const double success_dwell_time = 3000;
        private const int exit_success = 0;
        private const int exit_failure = 1;
        private const int exit_aborted = 3;
        private static readonly TimeSpan watchdog_timeout = TimeSpan.FromMinutes(2);

        private readonly Type initialTest;
        private readonly string isolatedStorageName = $"oms-exact-visual-test-data-{Guid.NewGuid():N}";

        [Cached]
        private readonly ExactVisualTestIsolation exactIsolation = new ExactVisualTestIsolation();

        private TemporaryNativeStorage? isolatedStorage;
        private Timer? watchdog;
        private int finishSignalled;
        private int exitCode = exit_aborted;

        public int ExitCode => Volatile.Read(ref exitCode);

        public ExactVisualTestGame(Type initialTest)
        {
            this.initialTest = initialTest;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            watchdog = new Timer(
                _ => reportFailure(new TimeoutException($"Exact visual test exceeded the {watchdog_timeout.TotalSeconds:0}-second watchdog."), "Exact visual test watchdog expired"),
                null,
                watchdog_timeout,
                Timeout.InfiniteTimeSpan);

            try
            {
                Add(new ExactAutomatedTestBrowser(initialTest, () => signalFinished(exit_success), reportFailure));
            }
            catch (Exception exception)
            {
                reportFailure(exception, "Exact visual test browser failed to load");
            }
        }

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage)
        {
            isolatedStorage = new TemporaryNativeStorage(isolatedStorageName, host);
            return new OsuStorage(host, isolatedStorage);
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            host.Window.CursorState |= CursorState.Hidden;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.CompareExchange(ref finishSignalled, 1, 0);
                watchdog?.Dispose();
                watchdog = null;
            }

            base.Dispose(isDisposing);

            if (isDisposing)
            {
                isolatedStorage?.Dispose();
                isolatedStorage = null;
            }
        }

        private void reportFailure(Exception exception, string context)
        {
            Logger.Error(exception, context);
            signalFinished(exit_failure);
        }

        private void signalFinished(int requestedExitCode)
        {
            if (Interlocked.CompareExchange(ref finishSignalled, 1, 0) != 0)
                return;

            Volatile.Write(ref exitCode, requestedExitCode);
            watchdog?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            if (requestedExitCode == exit_success)
                Scheduler.AddDelayed(Host.Exit, success_dwell_time);
            else
                Host.Exit();
        }
    }

    internal partial class ExactAutomatedTestBrowser : TestBrowser
    {
        private readonly Type initialTest;
        private readonly Action onSuccess;
        private readonly Action<Exception, string> onFailure;

        public ExactAutomatedTestBrowser(Type initialTest, Action onSuccess, Action<Exception, string> onFailure)
        {
            this.initialTest = initialTest;
            this.onSuccess = onSuccess;
            this.onFailure = onFailure;
        }

        protected override void LoadComplete()
        {
            try
            {
                // Load before TestBrowser's default config handling so mutable visualtests.cfg state cannot
                // select another scene. Register before base.LoadComplete() to avoid missing a fast load.
                LoadTest(initialTest);
                TestScene test = CurrentTest ?? throw new InvalidOperationException("TestBrowser did not retain the requested exact scene.");
                test.OnLoadComplete += _ => Schedule(() => Schedule(() => runAllSteps(test)));
                base.LoadComplete();

                if (!ReferenceEquals(CurrentTest, test))
                    onFailure(createMismatchException(test), "Exact visual test changed during browser initialisation");
            }
            catch (Exception exception)
            {
                onFailure(exception, "Exact visual test scene failed to load");
            }
        }

        private void runAllSteps(TestScene expectedTest)
        {
            try
            {
                if (!ReferenceEquals(CurrentTest, expectedTest))
                {
                    onFailure(createMismatchException(expectedTest), "Exact visual test changed before step execution");
                    return;
                }

                expectedTest.RunAllSteps(
                    onCompletion: onSuccess,
                    onError: (step, exception) => onFailure(exception, $"Exact visual test step {step} triggered an error"));
            }
            catch (Exception exception)
            {
                onFailure(exception, "Exact visual test steps failed to start");
            }
        }

        private InvalidOperationException createMismatchException(TestScene expectedTest)
        {
            return new InvalidOperationException($"Expected exact scene '{expectedTest.GetType().FullName}', but TestBrowser selected '{CurrentTest?.GetType().FullName ?? "<none>"}'.");
        }
    }
#endif
}
