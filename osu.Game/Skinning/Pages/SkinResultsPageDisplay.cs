// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Pages
{
    /// <summary>A fixed results canvas rendered exclusively from the owner's prepared declaration and pixels.</summary>
    internal partial class SkinResultsPageDisplay : CompositeDrawable
    {
        private readonly Skin owner;
        private readonly SkinPagePreparedPackage package;
        private readonly Func<Drawable> createStatistics;
        private readonly List<Texture> textures = new List<Texture>();
        private readonly List<(SkinPageElement Element, OsuSpriteText Text)> textNodes = new List<(SkinPageElement, OsuSpriteText)>();
        private readonly List<(SkinPageElement Element, Container Container)> visibilityNodes = new List<(SkinPageElement, Container)>();
        private readonly Container canvas;
        private SkinResultsPageData data;
        private SkinRevisionParticipantRegistration? revisionParticipant;

        internal SkinResultsPageDefinition Definition => package.Document.Results!;

        public SkinResultsPageDisplay(Skin owner, SkinPagePreparedPackage package, SkinResultsPageData data, Func<Drawable> createStatistics)
        {
            this.owner = owner;
            this.package = package;
            this.data = data;
            this.createStatistics = createStatistics;
            RelativeSizeAxes = Axes.Both;
            InternalChild = canvas = new Container
            {
                Size = new Vector2((float)Definition.Width, (float)Definition.Height),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Masking = true,
            };
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            if (!string.Equals(owner.GetCurrentRevisionContentIdentity(), package.ContentRevision, StringComparison.Ordinal))
                throw new InvalidOperationException("A results page cannot mix package revisions.");
            revisionParticipant = skinManager.RegisterRevisionParticipant(SkinRevisionParticipantKind.LifecycleHolder,
                nameof(SkinResultsPageDisplay), blocksRevisionPublication: true);
            var byId = new Dictionary<string, Texture>(StringComparer.Ordinal);
            foreach (var resource in package.Resources)
            {
                if (!Definition.Resources.Any(item => item.Id == resource.Source.Id))
                    continue;
                Texture texture = owner.PrepareGameplaySkinTexture(resource);
                textures.Add(texture);
                byId.Add(resource.Source.Id, texture);
            }

            foreach (SkinPageElement element in Definition.Elements)
            {
                var rectangle = element.Rectangle;
                var container = new Container
                {
                    Name = element.Id,
                    Position = new Vector2((float)rectangle.X, (float)rectangle.Y),
                    Size = new Vector2((float)rectangle.Width, (float)rectangle.Height),
                    Masking = true,
                    Alpha = opacity(element, data),
                    Colour = new Color4(((element.Colour >> 24) & 255) / 255f, ((element.Colour >> 16) & 255) / 255f,
                        ((element.Colour >> 8) & 255) / 255f, (element.Colour & 255) / 255f),
                };
                visibilityNodes.Add((element, container));
                switch (element.Type)
                {
                    case SkinPageElementType.Sprite:
                        container.Add(new Sprite { RelativeSizeAxes = Axes.Both, Texture = byId[element.Resource!] });
                        break;

                    case SkinPageElementType.Text:
                        Anchor anchor = element.Alignment switch
                        {
                            SkinPageTextAlignment.Centre => Anchor.Centre,
                            SkinPageTextAlignment.Right => Anchor.CentreRight,
                            _ => Anchor.CentreLeft,
                        };
                        var text = new TruncatingSpriteText
                        {
                            Anchor = anchor,
                            Origin = anchor,
                            Font = OsuFont.GetFont(size: (float)element.FontSize),
                            MaxWidth = (float)rectangle.Width,
                            Text = data.GetText(element.Source!),
                        };
                        textNodes.Add((element, text));
                        container.Add(text);
                        break;

                    case SkinPageElementType.NativeStatistics:
                        container.Add(createStatistics());
                        break;
                }
                // Negative depth keeps authored array order: later elements draw in front of earlier ones.
                container.Depth = -canvas.Count;
                canvas.Add(container);
            }
        }

        public void SetData(SkinResultsPageData value)
        {
            data = value;
            if (LoadState < LoadState.Ready)
                return;
            applyData();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            applyData();
        }

        private void applyData()
        {
            foreach (var (element, text) in textNodes)
                text.Text = data.GetText(element.Source!);
            foreach (var (element, container) in visibilityNodes)
                container.Alpha = opacity(element, data);
        }

        private static float opacity(SkinPageElement element, SkinResultsPageData value)
            => element.VisibilityCondition is not SkinPageVisibilityCondition condition
               || value.GetBoolean(condition.Source) == condition.Value ? (float)element.Opacity : 0;

        protected override void Update()
        {
            base.Update();
            float scale = Math.Min(DrawWidth / (float)Definition.Width, DrawHeight / (float)Definition.Height);
            canvas.Scale = new Vector2(scale);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            foreach (Texture texture in textures)
                texture.Dispose();
            textures.Clear();
            revisionParticipant?.Dispose();
            revisionParticipant = null;
        }
    }
}
