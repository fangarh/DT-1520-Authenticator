namespace Dt1520.Authenticator.Client;

/// <summary>
/// Outcome of selecting an explicit push target device.
/// </summary>
public enum PushDeviceSelectionStatus
{
    /// <summary>
    /// Exactly one active push-capable device was selected.
    /// </summary>
    Selected,

    /// <summary>
    /// No active push-capable device is available; integrators should fall back to online TOTP verification where policy allows it.
    /// </summary>
    NoActivePushCapableDevice,

    /// <summary>
    /// More than one active push-capable device is available; ask the user or backend policy to disambiguate.
    /// </summary>
    AmbiguousActivePushCapableDevices,
}
