// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Database;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Lets <see cref="SkinManager"/> prepare filesystem-backed selections before the committed bindable changes.
    /// </summary>
    internal sealed class SkinSelectionBindable : Bindable<Live<SkinInfo>>
    {
        internal const string UNPREPARED_CHANGE_DISABLED_DIAGNOSTIC =
            "The current skin selection can only be signalled by a prepared manager publication.";

        internal const string DISABLE_DISABLED_DIAGNOSTIC =
            "The current skin selection projection cannot be disabled.";

        internal const string AUTHORITY_BINDING_DISABLED_DIAGNOSTIC =
            "Current skin selection authority bindings cannot be replaced or detached.";

        internal Func<Live<SkinInfo>, bool>? SelectionRequested { get; set; }

        internal Func<Live<SkinInfo>>? AuthoritativeValueProvider { private get; set; }

        internal bool IsAuthoritativeRoot { private get; set; }

        internal Live<SkinInfo> ProjectedValue => base.Value;

        private bool authorityDetachArmed;

        public override bool Disabled
        {
            get => base.Disabled;
            set
            {
                if (value != base.Disabled)
                    throw new InvalidOperationException(DISABLE_DISABLED_DIAGNOSTIC);
            }
        }

        public SkinSelectionBindable(Live<SkinInfo> defaultValue)
            : base(defaultValue)
        {
        }

        public override Live<SkinInfo> Value
        {
            get => AuthoritativeValueProvider?.Invoke() ?? base.Value;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (Disabled)
                {
                    base.Value = value;
                    return;
                }

                if (EqualityComparer<Live<SkinInfo>>.Default.Equals(Value, value))
                    return;

                if (SelectionRequested?.Invoke(value) != false)
                    base.Value = value;
            }
        }

        internal void CommitPrepared(Live<SkinInfo> value)
            => base.Value = value;

        public override void TriggerChange()
            => throw new InvalidOperationException(UNPREPARED_CHANGE_DISABLED_DIAGNOSTIC);

        protected override Bindable<Live<SkinInfo>> CreateInstance()
            => new SkinSelectionBindable(Value);

        public override void BindTo(Bindable<Live<SkinInfo>> them)
        {
            if (them is not SkinSelectionBindable)
                throw new InvalidOperationException("Skin selections may only bind to another guarded selection bindable.");

            if (ReferenceEquals(this, them)
                || IsAuthoritativeRoot
                || AuthoritativeValueProvider != null)
            {
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);
            }

            base.BindTo(them);
        }

        public override void CopyTo(Bindable<Live<SkinInfo>> them)
        {
            if (them is not SkinSelectionBindable selection)
                throw new InvalidOperationException("Skin selections may only bind to another guarded selection bindable.");

            if (ReferenceEquals(this, selection)
                || selection.IsAuthoritativeRoot
                || selection.AuthoritativeValueProvider != null)
            {
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);
            }

            selection.SelectionRequested = null;
            selection.AuthoritativeValueProvider = AuthoritativeValueProvider;
            selection.IsAuthoritativeRoot = false;

            try
            {
                base.CopyTo(selection);
            }
            finally
            {
                selection.SelectionRequested = SelectionRequested;
            }
        }

        public override void UnbindEvents()
        {
            if (IsAuthoritativeRoot)
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);

            base.UnbindEvents();
            authorityDetachArmed = AuthoritativeValueProvider != null
                                     && Bindings?.Any(binding => binding is SkinSelectionBindable selection
                                                                 && hasMatchingAuthority(selection)) == true;
        }

        public override void UnbindFrom(IUnbindable them)
        {
            if (IsAuthoritativeRoot)
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);

            if (AuthoritativeValueProvider != null)
            {
                bool mayDetach = authorityDetachArmed && isExactAuthorityBinding(them);
                authorityDetachArmed = false;

                if (!mayDetach)
                    throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);
            }

            base.UnbindFrom(them);
        }

        private bool isExactAuthorityBinding(IUnbindable them)
            => them is SkinSelectionBindable selection
               && hasMatchingAuthority(selection)
               && Bindings?.Any(binding => ReferenceEquals(binding, selection)) == true;

        private bool hasMatchingAuthority(SkinSelectionBindable selection)
            => selection.AuthoritativeValueProvider != null
               && ReferenceEquals(AuthoritativeValueProvider, selection.AuthoritativeValueProvider)
               && ReferenceEquals(SelectionRequested, selection.SelectionRequested);
    }
}
