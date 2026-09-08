// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>Consent controls use the exact selected package token, never a mutable dropdown selection or author ID.</summary>
    public partial class SkinScriptSettings : FillFlowContainer
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private Bindable<Skin> current = null!;
        private GameplaySkinScriptAuthorization? authorization;
        private readonly List<(string Id, OsuTextFlowContainer Text)> rows = new List<(string, OsuTextFlowContainer)>();
        private readonly List<SettingsButtonV2> buttons = new List<SettingsButtonV2>();
        private OsuTextFlowContainer status = null!;
        private Task? pending;
        private long displayedVersion = -1;
        private double nextProfileUpdate;

        public SkinScriptSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Padding = SettingsPanel.CONTENT_PADDING;
            Spacing = new Vector2(0, 6);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            current = skins.CurrentSkin.GetBoundCopy();
            current.BindValueChanged(_ => rebuild(), true);
        }

        private void rebuild()
        {
            Clear();
            rows.Clear();
            buttons.Clear();
            authorization = current.Value.PreparedGameplaySkinPackage?.ScriptAuthorization;
            Add(text("可选皮肤脚本"));
            Add(text("脚本仅用于额外表现。拒绝或撤销授权后，普通音符、按键与判定仍正常显示。"));
            Add(status = text(authorization == null ? "此皮肤没有脚本。" : "正在读取授权状态。"));
            if (authorization == null)
                return;

            foreach (ScriptCapabilityRequest request in authorization.Requests)
            {
                Add(text(capabilityName(request.Id) + " · " + modeName(request.Mode)));
                var state = text(string.Empty);
                Add(state);
                rows.Add((request.Id, state));
                var actions = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                };
                GameplaySkinScriptAuthorization exact = authorization;
                addAction("授权", GameplaySkinScriptAuthorizationChoice.Granted, exact.CanGrant(request.Id));
                addAction("拒绝", GameplaySkinScriptAuthorizationChoice.Denied, true);
                addAction("撤销", GameplaySkinScriptAuthorizationChoice.NotDecided, true);
                Add(actions);

                void addAction(string label, GameplaySkinScriptAuthorizationChoice choice, bool enabled)
                {
                    var button = new SettingsButtonV2
                    {
                        Text = label,
                        Width = 1 / 3f,
                        Padding = new MarginPadding { Right = 4 },
                        Action = () => change(exact, request.Id, choice),
                    };
                    button.Enabled.Value = enabled;
                    buttons.Add(button);
                    actions.Add(button);
                }
            }
            displayedVersion = -1;
            updateStatus();
        }

        private void change(GameplaySkinScriptAuthorization exact, string id, GameplaySkinScriptAuthorizationChoice choice)
        {
            if (pending != null)
                return;
            foreach (SettingsButtonV2 button in buttons)
                button.Enabled.Value = false;
            pending = persist();

            async Task persist()
            {
                bool saved = await exact.SetAsync(id, choice).ConfigureAwait(false);
                Schedule(() =>
                {
                    pending = null;
                    if (IsDisposed)
                        return;
                    rebuild();
                    if (!saved && ReferenceEquals(authorization, exact))
                        status.Text = "授权未能保存：" + (exact.PersistenceError ?? "当前能力不能授权。") + " 当前会话权限以各项状态为准；重启后的持久化尚未确认。";
                });
            }
        }

        protected override void Update()
        {
            base.Update();
            if ((authorization != null && displayedVersion != authorization.Version) || Time.Current >= nextProfileUpdate)
            {
                updateStatus();
                nextProfileUpdate = Time.Current + 500;
            }
        }

        private void updateStatus()
        {
            if (authorization == null)
            {
                status.Text = "此皮肤没有脚本。" + (skins.LastScriptPreparationDiagnostic == null ? string.Empty : "\n上次脚本准备失败：" + skins.LastScriptPreparationDiagnostic);
                return;
            }
            displayedVersion = authorization.Version;
            foreach ((string id, OsuTextFlowContainer label) in rows)
            {
                string choice = authorization.GetChoice(id) switch
                {
                    GameplaySkinScriptAuthorizationChoice.Granted => "已授权",
                    GameplaySkinScriptAuthorizationChoice.Denied => "已拒绝",
                    _ => "未授权",
                };
                label.Text = $"{id}：{choice}；{(authorization.IsGranted(id) ? "运行时可用" : "运行时无权访问")}";
            }

            GameplaySkinScriptRuntimeReport report = authorization.RuntimeReport;
            string availability = authorization.RequiredSatisfied ? "所需能力已授权" : "脚本未启用：所需能力尚未获准";
            status.Text = $"{availability}。状态：{report.Status}。\n"
                          + $"Profiler：{report.Callbacks} 次回调，{report.Instructions} 条指令，"
                          + $"{report.ElapsedTicks * 1000.0 / Stopwatch.Frequency:0.00} ms，{report.HeapBytes} bytes。"
                          + (report.FaultCode == null ? string.Empty : $"\n{report.FaultCode}，脚本第 {report.FaultLine} 行。")
                          + (authorization.PersistenceError == null ? string.Empty : $"\n{authorization.PersistenceError}")
                          + (skins.LastScriptPreparationDiagnostic == null ? string.Empty : "\n上次脚本准备失败：" + skins.LastScriptPreparationDiagnostic);
        }

        private static string capabilityName(string id) => id switch
        {
            "gameplay.snapshot.read" => "读取引擎状态与谱面时钟",
            "gameplay.events.read" => "读取按键和判定事件",
            "scene.numeric.write" => "改变声明的场景节点表现",
            "math.random.read" => "使用可复现的随机数",
            _ => "引擎未开放的能力",
        };

        private static string modeName(ScriptCapabilityMode mode) => mode switch
        {
            ScriptCapabilityMode.Required => "脚本必需",
            ScriptCapabilityMode.Optional => "脚本可选",
            ScriptCapabilityMode.Deny => "作者显式禁止",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        private static OsuTextFlowContainer text(string value) => new OsuTextFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = value,
        };

        protected override void Dispose(bool isDisposing)
        {
            current?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
