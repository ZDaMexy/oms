// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Reflection;
using Moq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    /// <summary>
    /// Uses the pinned framework's actual GLTexture upload queue without initialising GL or creating a window.
    /// Reflection only reaches its internal constructor and retained receipt; this proves CPU ownership, not GPU output.
    /// </summary>
    [TestFixture]
    public class GameplaySkinPreparedTextureOwnershipTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestActualFrameworkQueueOwnsPixelsUntilFlush(bool disposeSkinBeforeFlush)
        {
            var renderer = (IRenderer)Activator.CreateInstance(
                typeof(Renderer).Assembly.GetType("osu.Framework.Graphics.OpenGL.GLRenderer", throwOnError: true)!, nonPublic: true)!;
            using var skin = createSkin(renderer);
            var captured = new GameplaySkinCapturedSceneResource(
                new GameplaySkinSceneResource("resource.test", GameplaySkinSceneResourceType.Texture, "pixel.png"),
                4, "captured-test-pixel", 1, 1, new byte[] { 91, 42, 17, 255 });
            using Texture texture = skin.PrepareGameplaySkinTexture(captured);
            var native = (INativeTexture)typeof(Texture).GetProperty("NativeTexture", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(texture)!;
            var queue = (Queue<ITextureUpload>)native.GetType().GetField("uploadQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(native)!;
            try
            {
                if (disposeSkinBeforeFlush)
                    skin.Dispose();
                Assert.That(queue.Count, Is.EqualTo(1));
                ITextureUpload retained = queue.Peek();
                Assert.That(retained.Data.Length, Is.EqualTo(1));
                Assert.That(retained.Data[0].R, Is.EqualTo(91));
                Assert.That(retained.Data[0].A, Is.EqualTo(255));
                Assert.That(texture.UploadComplete, Is.False);

                native.FlushUploads();
                Assert.That(retained.Data.IsEmpty, Is.True);
                Assert.That(texture.UploadComplete, Is.True);
            }
            finally
            {
                native.FlushUploads();
                native.Dispose();
            }
        }

        private static LegacySkin createSkin(IRenderer renderer)
        {
            var provider = new Mock<IStorageResourceProvider>();
            provider.SetupGet(value => value.Renderer).Returns(renderer);
            provider.Setup(value => value.CreateTextureLoaderStore(It.IsAny<IResourceStore<byte[]>>()))
                    .Returns(new ResourceStore<TextureUpload>());
            return new TestSkin(provider.Object);
        }

        private sealed class TestSkin : LegacySkin
        {
            public TestSkin(IStorageResourceProvider provider)
                : base(new SkinInfo(), provider, null, string.Empty)
            {
            }
        }
    }
}
