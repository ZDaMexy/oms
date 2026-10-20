// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace oms.Input.Devices
{
    public enum OmsHidInputEventKind
    {
        Press,
        Release,
        Motion,
        Disconnect,
        Axis,
        FinishAxes,
    }

    /// <summary>A value copy of a normalised device transition, ready for deferred gameplay delivery.</summary>
    public readonly record struct OmsHidInputEvent(OmsHidInputEventKind Kind, OmsAction Action = default, OmsAxisMotion Motion = default,
                                                   string? DeviceIdentifier = null, OmsHidAxisChange AxisChange = default);
}
