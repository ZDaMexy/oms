// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>Paragraph inputs use the existing IME and selection controls; multiline paste preserves line breaks.</summary>
    public partial class OmsParagraphEditor : FillFlowContainer
    {
        private readonly List<string> paragraphs;
        private readonly int limit;
        private readonly Action<string> changed;
        private readonly OsuTextFlowContainer preview;
        private int paragraphOffset;
        private const int paragraphs_per_page = 20;

        public OmsParagraphEditor(string text, int limit, Action<string> changed)
        {
            this.limit = limit;
            this.changed = changed;
            paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 5);
            preview = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            rebuild();
        }

        private void rebuild(int? focusIndex = null)
        {
            Remove(preview, false);
            Clear();
            if (focusIndex != null) paragraphOffset = focusIndex.Value / paragraphs_per_page * paragraphs_per_page;
            for (int i = paragraphOffset; i < Math.Min(paragraphs.Count, paragraphOffset + paragraphs_per_page); i++)
            {
                int index = i;
                var input = new ParagraphInput(value => paste(index, value))
                { RelativeSizeAxes = Axes.X, Height = 38, Text = paragraphs[i], LengthLimit = limit, PlaceholderText = "正文段落" };
                input.Current.BindValueChanged(value => { paragraphs[index] = value.NewValue; updateText(); });
                input.OnCommit += (_, _) => insertParagraph(index + 1);
                Add(input);
                if (focusIndex == index)
                    Schedule(() => input.TakeFocus());
            }
            if (paragraphOffset > 0)
                Add(new RoundedButton
                {
                    Text = "前 20 段",
                    RelativeSizeAxes = Axes.X,
                    Height = 36,
                    Action = () => { paragraphOffset -= paragraphs_per_page; rebuild(); }
                });
            if (paragraphOffset + paragraphs_per_page < paragraphs.Count)
                Add(new RoundedButton
                {
                    Text = "后 20 段",
                    RelativeSizeAxes = Axes.X,
                    Height = 36,
                    Action = () => { paragraphOffset += paragraphs_per_page; rebuild(); }
                });
            Add(new RoundedButton
            {
                Text = "新增段落",
                RelativeSizeAxes = Axes.X,
                Height = 36,
                Action = () => insertParagraph(paragraphs.Count)
            });
            Add(preview);
            updateText();
        }

        private void insertParagraph(int index)
        {
            if (string.Join('\n', paragraphs).Length >= limit)
            {
                preview.Text = "正文已达到长度限制，请缩短后再新增段落。";
                return;
            }
            paragraphs.Insert(index, string.Empty);
            rebuild(index);
        }

        private void paste(int index, string value)
        {
            string[] lines = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            string candidate = string.Join('\n', paragraphs.Take(index).Concat(lines).Concat(paragraphs.Skip(index + 1)));
            if (candidate.Length > limit)
            { preview.Text = "粘贴内容超过正文长度，请缩短后重试。"; return; }
            paragraphs.RemoveAt(index);
            paragraphs.InsertRange(index, lines);
            rebuild(index + lines.Length - 1);
        }

        private void updateText()
        {
            string value = string.Join('\n', paragraphs);
            changed(value);
            preview.Text = $"正文预览 · {value.Length} / {limit} 字 · 正在编辑第 {paragraphOffset + 1}–{Math.Min(paragraphs.Count, paragraphOffset + paragraphs_per_page)} 段\n" + value;
        }

        private partial class ParagraphInput : FocusedTextBox
        {
            protected override bool ClearTextOnBackKey => false;
            private readonly Action<string> paste;
            [Resolved]
            private Clipboard clipboard { get; set; } = null!;
            public ParagraphInput(Action<string> paste) => this.paste = paste;

            public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
            {
                if (HasFocus && e.Action == PlatformAction.Paste && clipboard.GetText() is string value && (value.Contains('\n') || value.Contains('\r')))
                { paste(value); return true; }
                return base.OnPressed(e);
            }
        }
    }
}
