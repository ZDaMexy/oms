// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Tools.IidxSkinBake;

internal sealed partial class BakeGame : osu.Framework.Game
{
    private readonly BakeInput input;
    private BufferedContainer canvas = null!;
    private Container baked = null!;
    private Box background = null!;
    private ProductionAdapter production = null!;
    private ReadbackMarker readback = null!;
    private IRenderer renderer = null!;
    private IReadOnlyList<BakedRun>? currentBaked;
    private readonly List<Texture> bakedTextures = new();
    public Exception? Failure { get; private set; }
    public bool Completed { get; private set; }

    public BakeGame(BakeInput input) => this.input = input;

    protected override Storage CreateStorage(GameHost host, Storage defaultStorage)
        => new NativeStorage(Path.Combine(input.Output, "host-storage"));

    protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() => new Dictionary<FrameworkSetting, object>
    {
        [FrameworkSetting.WindowedSize] = new System.Drawing.Size((int)input.Animation.Width, (int)input.Animation.Height),
        [FrameworkSetting.WindowMode] = WindowMode.Windowed,
    };

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer)
    {
        this.renderer = renderer;
        Resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
        production = new ProductionAdapter(input, renderer);
        Add(canvas = new BufferedContainer
        {
            Size = new Vector2((float)input.Animation.Width, (float)input.Animation.Height),
            BackgroundColour = new Color4(0f, 0f, 0f, 0f),
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both, Depth = 1000, Colour = new Color4(0f, 0f, 0f, 0f) },
                production.Drawable,
                baked = new Container { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                readback = new ReadbackMarker { RelativeSizeAxes = Axes.Both, Depth = -1_000_000 },
            },
        });
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        _ = Task.Run(async () =>
        {
            try
            {
                await new BakeWriter(input, this).Run();
                input.VerifyUnchanged();
                Completed = true;
            }
            catch (Exception exception)
            {
                Failure = exception;
            }
            finally
            {
                Schedule(Exit);
            }
        });
    }

    public BlendRun[] Runs(int frame) => production.GetRuns(frame);
    public string PreparedFrameIdentity(int frame) => production.PreparedFrameIdentity(frame);

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        if (!isDisposing)
            return;
        foreach (Texture texture in bakedTextures)
            texture.Dispose();
        production?.Dispose();
    }

    public Task<Image<Rgba32>> CaptureOriginal(int frame, BlendRun? run, Color4 background)
        => Capture(() =>
        {
            baked.Alpha = 0;
            production.Drawable.Alpha = 1;
            production.Show(frame, run);
            this.background.Colour = background;
        });

    public Task<Image<Rgba32>> CaptureBaked(IReadOnlyList<BakedRun> runs, Color4 background)
        => Capture(() =>
        {
            production.Drawable.Alpha = 0;
            baked.Alpha = 1;
            this.background.Colour = background;
            if (ReferenceEquals(currentBaked, runs))
                return;
            baked.Clear();
            foreach (Texture texture in bakedTextures)
                texture.Dispose();
            bakedTextures.Clear();
            currentBaked = runs;
            for (int i = 0; i < runs.Count; i++)
            {
                BakedRun run = runs[i];
                Texture texture = renderer.CreateTexture(run.Pixels.Width, run.Pixels.Height, manualMipmaps: true, filteringMode: TextureFilteringMode.Nearest);
                bakedTextures.Add(texture);
                texture.SetData(new TextureUpload(run.Pixels.Clone()));
                baked.Add(new Sprite
                {
                    Texture = texture,
                    Position = new Vector2(run.X, run.Y),
                    Size = new Vector2(run.Pixels.Width, run.Pixels.Height),
                    Depth = -i,
                    Blending = run.Additive ? BlendingParameters.Additive : BlendingParameters.Mixture,
                });
            }
        });

    private async Task<Image<Rgba32>> Capture(Action prepare)
    {
        // Texture upload completion alone precedes the first drawable-lifetime draw-node publication.
        // Discard one acknowledged GPU draw, then read the next draw of the same fixed source state.
        // This is a render barrier, not an animation-time advance or a retry accepting different pixels.
        using Image<Rgba32> warmup = await CaptureDraw(prepare, true);
        return await CaptureDraw(() => { }, false);
    }

    private async Task<Image<Rgba32>> CaptureDraw(Action prepare, bool waitForUpdate)
    {
        var request = new CaptureRequest();
        Schedule(() =>
        {
            try
            {
                prepare();
                // Visibility changes participate in the next child lifetime/update pass. Arm only after
                // a complete update interval, while the explicitly sampled source frame remains fixed.
                if (waitForUpdate)
                {
                    Scheduler.AddDelayed(() => ArmWhenUploaded(request), 35);
                }
                else
                    ArmWhenUploaded(request);
            }
            catch (Exception exception)
            {
                request.Completion.TrySetException(exception);
            }
        });
        Image<Rgba32> image = await request.Completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        if (image.Width != input.Animation.Width || image.Height != input.Animation.Height)
        {
            image.Dispose();
            throw new InvalidOperationException("The framebuffer differs from the unscaled source canvas.");
        }
        return image;
    }

    private void ArmWhenUploaded(CaptureRequest request)
    {
        bool uploaded = production.Drawable.Alpha > 0 ? production.VisibleTexturesReady : bakedTextures.All(t => t.UploadComplete);
        if (!uploaded)
        {
            Scheduler.AddDelayed(() => ArmWhenUploaded(request), 10);
            return;
        }
        readback.Request = request;
        canvas.ForceRedraw();
    }

    private sealed class CaptureRequest
    {
        public readonly TaskCompletionSource<Image<Rgba32>> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Claimed;
    }

    private sealed partial class ReadbackMarker : Drawable
    {
        private CaptureRequest? request;
        public CaptureRequest? Request
        {
            set
            {
                request = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        protected override DrawNode CreateDrawNode() => new ReadbackNode(this);

        private sealed class ReadbackNode : DrawNode
        {
            private static readonly MethodInfo flush = typeof(IRenderer).GetMethod("FlushCurrentBatch", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException("IRenderer.FlushCurrentBatch");
            private static readonly PropertyInfo currentBuffer = typeof(Renderer).GetProperty("FrameBuffer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException("Renderer.FrameBuffer");
            private CaptureRequest? request;
            public ReadbackNode(ReadbackMarker source) : base(source) { }
            public override void ApplyState()
            {
                base.ApplyState();
                request = ((ReadbackMarker)Source).request;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);
                if (request == null || Interlocked.Exchange(ref request.Claimed, 1) != 0)
                    return;
                try
                {
                    flush.Invoke(renderer, new object?[] { null });
                    IFrameBuffer buffer = (IFrameBuffer?)currentBuffer.GetValue(renderer) ?? throw new InvalidOperationException("Readback marker is not inside a framebuffer.");
                    request.Completion.TrySetResult(renderer.ExtractFrameBufferData(buffer) ?? throw new InvalidOperationException("The renderer does not support framebuffer readback."));
                }
                catch (Exception exception)
                {
                    request.Completion.TrySetException(exception);
                }
            }
        }
    }
}

internal sealed record BakedRun(bool Additive, int X, int Y, Image<Rgba32> Pixels);
