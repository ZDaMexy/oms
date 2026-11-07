// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using Vector4 = osuTK.Vector4;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Tools.IidxSkinBake;

/// <summary>
/// Deliberately narrow reflection against the exact OMS build supplied to the tool. This is not a second
/// animation evaluator and does not offer compatibility fallbacks when the production API changes.
/// </summary>
internal sealed class ProductionAdapter : IDisposable
{
    private const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private readonly MethodInfo applyFrame;
    private readonly MethodInfo getPreparedFrame;
    private readonly IReadOnlyList<Drawable> sprites;
    private readonly GameplaySkinRecordedAnimationProgram program;
    private readonly List<Texture> ownedTextures = new();
    public Drawable Drawable { get; }
    public bool VisibleTexturesReady => sprites.Where(s => s.Alpha > 0).All(s => s.IsLoaded && ((Sprite)s).Texture is { UploadComplete: true });

    public string VisibleTextureDiagnostic => string.Join("; ", sprites.Select((sprite, index) => (sprite, index))
        .Where(item => item.sprite.Alpha > 0 && (!item.sprite.IsLoaded || ((Sprite)item.sprite).Texture is not { UploadComplete: true }))
        .Take(16).Select(item => $"sprite={item.index},loaded={item.sprite.IsLoaded},texture={(((Sprite)item.sprite).Texture == null ? "null" : "present")},uploaded={((Sprite)item.sprite).Texture?.UploadComplete}"));

    public ProductionAdapter(BakeInput input, IRenderer renderer)
    {
        program = input.Animation;
        Type type = typeof(GameplaySkinRecordedAnimationProgram).Assembly.GetType("osu.Game.Skinning.Gameplay.GameplaySkinRecordedAnimationDrawable", true)!;
        var prepared = new List<GameplaySkinPreparedSceneResource>();
        ConstructorInfo resourceConstructor = typeof(GameplaySkinPreparedSceneResource).GetConstructor(instance, null,
            new[] { typeof(GameplaySkinSceneResource), typeof(string), typeof(int), typeof(long), typeof(Texture) }, null)
            ?? throw new MissingMethodException("The exact production prepared-resource constructor is unavailable.");
        foreach (ResourceInput resource in input.Resources)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(resource.Bytes);
            bool nearest = resource.Definition.Filtering == GameplaySkinSceneTextureFiltering.Nearest;
            Texture texture = renderer.CreateTexture(image.Width, image.Height, manualMipmaps: nearest,
                filteringMode: nearest ? TextureFilteringMode.Nearest : TextureFilteringMode.Linear);
            ownedTextures.Add(texture);
            texture.SetData(new TextureUpload(image.Clone()));
            prepared.Add((GameplaySkinPreparedSceneResource)resourceConstructor.Invoke(new object[]
            {
                resource.Definition, BakeInput.Hash(resource.Bytes), resource.Bytes.Length, (long)image.Width * image.Height * 4, texture,
            }));
        }
        Drawable = (Drawable)(Activator.CreateInstance(type, program, prepared) ?? throw new InvalidOperationException("Production drawable construction failed."));
        applyFrame = type.GetMethod("applyFrame", instance, null, new[] { typeof(int) }, null) ?? throw new MissingMethodException(type.FullName, "applyFrame");
        getPreparedFrame = program.GetType().GetMethod("GetPreparedFrame", instance, null, new[] { typeof(int) }, null) ?? throw new MissingMethodException("GetPreparedFrame");
        sprites = ((IEnumerable)(type.GetField("sprites", instance)?.GetValue(Drawable) ?? throw new MissingFieldException("sprites"))).Cast<Drawable>().ToArray();
    }

    public BlendRun[] GetRuns(int frame)
    {
        object prepared = getPreparedFrame.Invoke(program, new object[] { frame })!;
        Type type = prepared.GetType();
        int count = (int)type.GetProperty("PieceCount")!.GetValue(prepared)!;
        MethodInfo getPiece = type.GetMethod("GetPiece")!;
        var runs = new List<BlendRun>();
        for (int i = 0; i < count; i++)
        {
            object piece = getPiece.Invoke(prepared, new object[] { i })!;
            bool additive = (bool)piece.GetType().GetProperty("Additive")!.GetValue(piece)!;
            if (runs.Count == 0 || runs[^1].Additive != additive)
                runs.Add(new BlendRun(i, 1, additive));
            else
                runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
        }
        return runs.ToArray();
    }

    public string PreparedFrameIdentity(int frame)
    {
        object prepared = getPreparedFrame.Invoke(program, new object[] { frame })!;
        Type type = prepared.GetType();
        int pieceCount = (int)type.GetProperty("PieceCount")!.GetValue(prepared)!;
        int vertexCount = (int)type.GetProperty("VertexCount")!.GetValue(prepared)!;
        MethodInfo getPiece = type.GetMethod("GetPiece")!;
        MethodInfo getVertex = type.GetMethod("GetVertex")!;
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes);
        writer.Write(pieceCount);
        writer.Write(vertexCount);
        for (int i = 0; i < pieceCount; i++)
        {
            object piece = getPiece.Invoke(prepared, new object[] { i })!;
            Type p = piece.GetType();
            writer.Write((string?)p.GetProperty("Resource")!.GetValue(piece) ?? "");
            writer.Write((bool)p.GetProperty("Additive")!.GetValue(piece)!);
            writer.Write((int)p.GetProperty("VertexOffset")!.GetValue(piece)!);
            writer.Write((int)p.GetProperty("VertexCount")!.GetValue(piece)!);
            foreach (string name in new[] { "Multiply", "Add", "Hsl", "Row0", "Row1", "Row2", "Row3", "Offset" })
            {
                var vector = (Vector4)p.GetProperty(name)!.GetValue(piece)!;
                writer.Write(vector.X); writer.Write(vector.Y); writer.Write(vector.Z); writer.Write(vector.W);
            }
        }
        for (int i = 0; i < vertexCount; i++)
        {
            object vertex = getVertex.Invoke(prepared, new object[] { i })!;
            Type v = vertex.GetType();
            foreach (string name in new[] { "X", "Y", "U", "V", "Q" })
                writer.Write((float)v.GetProperty(name)!.GetValue(vertex)!);
        }
        return BakeInput.Hash(bytes.ToArray());
    }

    public void Show(int frame, BlendRun? run)
    {
        applyFrame.Invoke(Drawable, new object[] { frame });
        int count = GetRuns(frame).Sum(r => r.Count);
        for (int i = 0; i < sprites.Count; i++)
        {
            sprites[i].Alpha = i < count && (run == null || i >= run.First && i < run.First + run.Count) ? 1 : 0;
            if (i < count)
            {
                // Production Mixture adds destination alpha. RGB must remain identical, but a flattened
                // normal run needs coverage rather than the sum of its source alphas. Only this capture
                // pass uses Porter-Duff alpha; the full-frame reference retains production blending.
                BlendingParameters blend = sprites[i].Blending;
                blend.DestinationAlpha = run is { Additive: false } ? BlendingType.OneMinusSrcAlpha : BlendingType.One;
                sprites[i].Blending = blend;
            }
        }
    }

    public void Dispose()
    {
        foreach (Texture texture in ownedTextures)
            texture.Dispose();
    }
}

internal sealed record BlendRun(int First, int Count, bool Additive);
