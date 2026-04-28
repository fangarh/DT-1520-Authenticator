namespace Dt1520.Authenticator.Client;

/// <summary>
/// Mobile platform reported by DT-1520 for a registered device.
/// </summary>
public enum DevicePlatform
{
    /// <summary>
    /// Server returned an unrecognized platform.
    /// </summary>
    Unknown,

    /// <summary>
    /// Android device.
    /// </summary>
    Android,

    /// <summary>
    /// iOS device.
    /// </summary>
    Ios,
}
