// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Bindables;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Exposes the current skin instance as a read-only bindable while retaining one manager-owned commit path.
    /// </summary>
    /// <remarks>
    /// A plain <see cref="Bindable{T}"/> permits a caller to replace the owner with another instance carrying the
    /// same record identifier. That bypasses revision preparation, participant acknowledgement and retirement.
    /// </remarks>
    internal sealed class SkinInstanceBindable : Bindable<Skin>
    {
        internal const string DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC =
            "The current skin owner is published by SkinManager and cannot be assigned directly.";

        internal const string DISABLE_DISABLED_DIAGNOSTIC =
            "The current skin owner projection cannot be disabled.";

        internal const string AUTHORITY_BINDING_DISABLED_DIAGNOSTIC =
            "Current skin owner authority bindings cannot be replaced or detached.";

        internal Func<Skin>? AuthoritativeValueProvider { private get; set; }

        internal bool IsAuthoritativeRoot { private get; set; }

        internal Skin ProjectedValue => base.Value;

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

        public override Skin Value
        {
            get => AuthoritativeValueProvider?.Invoke() ?? base.Value; set => throw new InvalidOperationException(DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC);
        }

        internal void CommitPrepared(Skin value)
        {
            ArgumentNullException.ThrowIfNull(value);
            base.Value = value;
        }

        public override void TriggerChange()
            => throw new InvalidOperationException(DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC);

        protected override Bindable<Skin> CreateInstance()
            => new SkinInstanceBindable();

        public override void BindTo(Bindable<Skin> them)
        {
            if (them is not SkinInstanceBindable)
                throw new InvalidOperationException("Current skin owners may only bind to another guarded owner bindable.");

            if (ReferenceEquals(this, them)
                || IsAuthoritativeRoot
                || AuthoritativeValueProvider != null)
            {
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);
            }

            base.BindTo(them);
        }

        public override void CopyTo(Bindable<Skin> them)
        {
            if (them is not SkinInstanceBindable instance)
                throw new InvalidOperationException("Current skin owners may only bind to another guarded owner bindable.");

            if (ReferenceEquals(this, instance)
                || instance.IsAuthoritativeRoot
                || instance.AuthoritativeValueProvider != null)
            {
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);
            }

            // Bindable.CopyTo() would route the initial value through the public virtual setter. Guarded copies are
            // read-only too, so initialise their exact manager value through the same internal-only path instead.
            instance.AuthoritativeValueProvider = AuthoritativeValueProvider;
            instance.IsAuthoritativeRoot = false;
            instance.CommitPrepared(Value);
            instance.Default = Default;
            instance.Disabled = Disabled;
        }

        public override void UnbindEvents()
        {
            if (IsAuthoritativeRoot)
                throw new InvalidOperationException(AUTHORITY_BINDING_DISABLED_DIAGNOSTIC);

            base.UnbindEvents();
            authorityDetachArmed = AuthoritativeValueProvider != null
                                     && Bindings?.Any(binding => binding is SkinInstanceBindable instance
                                                                 && hasMatchingAuthority(instance)) == true;
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
            => them is SkinInstanceBindable instance
               && hasMatchingAuthority(instance)
               && Bindings?.Any(binding => ReferenceEquals(binding, instance)) == true;

        private bool hasMatchingAuthority(SkinInstanceBindable instance)
            => instance.AuthoritativeValueProvider != null
               && ReferenceEquals(AuthoritativeValueProvider, instance.AuthoritativeValueProvider);
    }
}
