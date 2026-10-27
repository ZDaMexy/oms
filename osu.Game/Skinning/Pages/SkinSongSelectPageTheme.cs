// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Pages
{
    /// <summary>One update-thread publication to every native chrome consumer, including pooled rows.</summary>
    internal sealed class SkinSongSelectPageThemeSource
    {
        internal SkinSongSelectPageTheme? Theme { get; private set; }
        internal event Action? Changed;

        internal void SetTheme(SkinSongSelectPageTheme? value)
        {
            Theme = value;
            Changed?.Invoke();
        }
    }

    internal sealed class SkinSongSelectPageTheme : IDisposable
    {
        internal SkinSongSelectPageDefinition Definition { get; }
        private readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>(StringComparer.Ordinal);
        private SkinRevisionParticipantRegistration? participant;

        internal SkinSongSelectPageTheme(Skin owner, SkinPagePreparedPackage package, SkinManager manager)
        {
            Definition = package.Document.SongSelect ?? throw new InvalidOperationException("This package has no SongSelect page.");
            if (!string.Equals(owner.GetCurrentRevisionContentIdentity(), package.ContentRevision, StringComparison.Ordinal))
                throw new InvalidOperationException("A SongSelect page cannot mix package revisions.");
            try
            {
                // This first native page slice has no staged swap. A mounted page must detach before current reload.
                participant = manager.RegisterRevisionParticipant(SkinRevisionParticipantKind.LifecycleHolder,
                    nameof(SkinSongSelectPageTheme), blocksRevisionPublication: true);
                foreach (var resource in package.Resources.Where(resource => Definition.Resources.Any(item => item.Id == resource.Source.Id)))
                    textures.Add(resource.Source.Id, owner.PrepareGameplaySkinTexture(resource));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal Texture GetTexture(string id) => textures[id];

        public void Dispose()
        {
            foreach (Texture texture in textures.Values)
                texture.Dispose();
            textures.Clear();
            participant?.Dispose();
            participant = null;
        }
    }

    internal enum SkinSongSelectChromeSlot
    {
        Background,
        ListEdge,
        Row,
        Brand,
    }

    /// <summary>Never owns shared textures or input. Geometry is supplied by its actual native parent.</summary>
    internal partial class SkinSongSelectChrome : CompositeDrawable
    {
        private readonly SkinSongSelectChromeSlot slot;
        private readonly Sprite sprite;
        private bool selected;
        private bool rowPresent;

        [Resolved(CanBeNull = true)]
        private SkinSongSelectPageThemeSource? source { get; set; }

        public override bool HandlePositionalInput => false;
        public override bool HandleNonPositionalInput => false;

        internal bool Selected
        {
            set
            {
                selected = value;
                apply();
            }
        }

        internal bool RowPresent
        {
            set
            {
                rowPresent = value;
                apply();
            }
        }

        internal SkinSongSelectChrome(SkinSongSelectChromeSlot slot)
        {
            this.slot = slot;
            RelativeSizeAxes = Axes.Both;
            InternalChild = sprite = new Sprite { RelativeSizeAxes = Axes.Both };
            Alpha = 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (source != null)
                source.Changed += apply;
            apply();
        }

        private void apply()
        {
            SkinSongSelectPageTheme? theme = LoadState >= LoadState.Ready ? source?.Theme : null;
            if (theme == null || slot == SkinSongSelectChromeSlot.Row && !rowPresent)
            {
                sprite.Texture = null;
                Alpha = 0;
                return;
            }
            string? resource = slot switch
            {
                SkinSongSelectChromeSlot.Background => theme.Definition.Background,
                SkinSongSelectChromeSlot.ListEdge => theme.Definition.ListEdge,
                SkinSongSelectChromeSlot.Row => selected ? theme.Definition.SelectedRow : theme.Definition.Row,
                SkinSongSelectChromeSlot.Brand => theme.Definition.Brand,
                _ => throw new ArgumentOutOfRangeException(),
            };
            sprite.Texture = resource == null ? null : theme.GetTexture(resource);
            Alpha = resource == null ? 0 : slot == SkinSongSelectChromeSlot.Brand ? 0.18f : slot == SkinSongSelectChromeSlot.Background ? 0.4f : 1;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (source != null)
                source.Changed -= apply;
            sprite.Texture = null;
            base.Dispose(isDisposing);
        }
    }
}
