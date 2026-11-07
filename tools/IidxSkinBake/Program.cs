// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace osu.Tools.IidxSkinBake;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            int index = Array.IndexOf(args, "--runtime");
            if (index < 0 || index + 1 == args.Length)
                throw new ArgumentException("Supply --runtime <current OMS program directory>.");
            string runtime = Path.GetFullPath(args[index + 1]);
            if (!File.Exists(Path.Combine(runtime, "osu.Game.dll")))
                throw new DirectoryNotFoundException("OMS runtime is missing.");
            var expected = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key.StartsWith("BakeRuntime.", StringComparison.Ordinal)).ToArray();
            if (expected.Length != 4)
                throw new InvalidOperationException("Build-time rendering dependency identities are missing.");
            foreach (AssemblyMetadataAttribute identity in expected)
            {
                string name = identity.Key["BakeRuntime.".Length..^".Sha256".Length] + ".dll";
                using var source = File.OpenRead(Path.Combine(runtime, name));
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(source)), identity.Value, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The runtime differs from this tool's build-time rendering dependency: " + name);
            }
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                string candidate = Path.Combine(runtime, name.Name + ".dll");
                return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
            };
            string native = Path.Combine(runtime, "runtimes", "win-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), "native");
            Environment.SetEnvironmentVariable("PATH", native + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
            return Run(args, runtime);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args, string runtime)
    {
        if (args.Contains("--tile-index"))
        {
            TileBaker.Run(args).GetAwaiter().GetResult();
            return 0;
        }
        if (args.Contains("--describe"))
        {
            foreach (var blend in new[] { osu.Framework.Graphics.BlendingParameters.Mixture, osu.Framework.Graphics.BlendingParameters.Additive })
            {
                Console.WriteLine("BLEND " + blend);
                foreach (FieldInfo field in blend.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    Console.WriteLine($"{field.Name}: {field.GetValue(blend)}");
            }
            foreach (Type type in new[]
                     {
                         typeof(osu.Framework.Graphics.Rendering.Renderer),
                         typeof(osu.Framework.Graphics.Rendering.IRenderer),
                         typeof(osu.Framework.Graphics.Containers.BufferedContainer),
                         typeof(osu.Framework.Graphics.DrawNode),
                         typeof(osu.Framework.Game),
                     })
            {
                Console.WriteLine(type.FullName);
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .Where(m => new[] { "FrameBuffer", "Draw", "Shader", "Texture", "Dependencies", "Clear", "Storage", "Config" }.Any(q => m.Name.Contains(q))))
                    Console.WriteLine(member);
            }
            return 0;
        }
        var input = new BakeInput(args);
        Directory.CreateDirectory(input.Output);
        using var game = new BakeGame(input);
        using var host = osu.Framework.Host.GetSuitableDesktopHost("oms-iidx-bake-" + Guid.NewGuid().ToString("N"),
            new osu.Framework.HostOptions { PortableInstallation = true });
        using var timeout = new System.Threading.Timer(_ => host.Exit(), null, TimeSpan.FromSeconds(60 + input.Frames.Length * 8), System.Threading.Timeout.InfiniteTimeSpan);
        host.Run(game);
        if (game.Failure != null)
            ExceptionDispatchInfo.Capture(game.Failure).Throw();
        if (!game.Completed)
            throw new InvalidOperationException("The rendering host exited before baking completed.");
        return 0;
    }
}
