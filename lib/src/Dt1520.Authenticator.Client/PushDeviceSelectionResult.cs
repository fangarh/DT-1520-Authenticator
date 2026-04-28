namespace Dt1520.Authenticator.Client;

/// <summary>
/// Result of selecting one device for explicit push challenge routing.
/// </summary>
public sealed record PushDeviceSelectionResult
{
    private PushDeviceSelectionResult(
        PushDeviceSelectionStatus status,
        DeviceRoutingCandidate? device,
        int matchingDeviceCount)
    {
        Status = status;
        Device = device;
        MatchingDeviceCount = matchingDeviceCount;
    }

    /// <summary>
    /// Selection status.
    /// </summary>
    public PushDeviceSelectionStatus Status { get; }

    /// <summary>
    /// Selected active push-capable device when <see cref="Status"/> is <see cref="PushDeviceSelectionStatus.Selected"/>.
    /// </summary>
    public DeviceRoutingCandidate? Device { get; }

    /// <summary>
    /// Number of active push-capable devices considered by the helper.
    /// </summary>
    public int MatchingDeviceCount { get; }

    /// <summary>
    /// Indicates whether a single device was selected.
    /// </summary>
    public bool IsSelected => Status == PushDeviceSelectionStatus.Selected;

    /// <summary>
    /// Selects one active push-capable device without making local device trust decisions.
    /// </summary>
    public static PushDeviceSelectionResult FromCandidates(IEnumerable<DeviceRoutingCandidate> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var candidates = devices
            .Where(static device => device.Status == DeviceStatus.Active && device.IsPushCapable)
            .ToArray();

        return candidates.Length switch
        {
            0 => new PushDeviceSelectionResult(PushDeviceSelectionStatus.NoActivePushCapableDevice, null, 0),
            1 => new PushDeviceSelectionResult(PushDeviceSelectionStatus.Selected, candidates[0], 1),
            _ => new PushDeviceSelectionResult(PushDeviceSelectionStatus.AmbiguousActivePushCapableDevices, null, candidates.Length),
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(PushDeviceSelectionResult)} {{ {nameof(Status)} = {Status}, {nameof(MatchingDeviceCount)} = {MatchingDeviceCount} }}";
    }
}
