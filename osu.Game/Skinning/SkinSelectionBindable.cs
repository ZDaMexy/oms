// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Database;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Lets <see cref="SkinManager"/> prepare filesystem-backed selections before the committed bindable changes.
    /// </summary>
    internal sealed class SkinSelectionBindable : Bindable<Live<SkinInfo>>
    {
        internal Func<Live<SkinInfo>, bool>? SelectionRequested { get; set; }

        public SkinSelectionBindable(Live<SkinInfo> defaultValue)
            : base(defaultValue)
        {
        }

        public override Live<SkinInfo> Value
        {
            get => base.Value;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (Disabled)
                {
                    base.Value = value;
                    return;
                }

                if (EqualityComparer<Live<SkinInfo>>.Default.Equals(base.Value, value))
                    return;

                if (SelectionRequested?.Invoke(value) != false)
                    base.Value = value;
            }
        }

        internal void CommitPrepared(Live<SkinInfo> value)
            => base.Value = value;

        protected override Bindable<Live<SkinInfo>> CreateInstance()
            => new SkinSelectionBindable(Value);

        public override void BindTo(Bindable<Live<SkinInfo>> them)
        {
            if (them is not SkinSelectionBindable)
                throw new InvalidOperationException("Skin selections may only bind to another guarded selection bindable.");

            base.BindTo(them);
        }

        public override void CopyTo(Bindable<Live<SkinInfo>> them)
        {
            if (them is not SkinSelectionBindable selection)
                throw new InvalidOperationException("Skin selections may only bind to another guarded selection bindable.");

            selection.SelectionRequested = null;

            try
            {
                base.CopyTo(selection);
            }
            finally
            {
                selection.SelectionRequested = SelectionRequested;
            }
        }
    }
}
