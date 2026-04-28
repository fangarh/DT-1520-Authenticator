using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dt1520.Authenticator.Client;

internal static class ClientJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<ChallengeFactorType>(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<ChallengeOperationType>(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<ChallengeStatus>(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<DeviceAttestationStatus>(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<DevicePlatform>(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new JsonStringEnumConverter<DeviceStatus>(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
