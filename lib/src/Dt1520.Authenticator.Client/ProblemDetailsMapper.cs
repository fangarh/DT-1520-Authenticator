using System.Net;
using System.Text.Json;

namespace Dt1520.Authenticator.Client;

internal static class ProblemDetailsMapper
{
    public static async Task<Dt1520AuthenticatorError> MapAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var requestId = TryGetHeader(response, "X-Request-Id");
        var fallbackTitle = response.ReasonPhrase ?? "DT-1520 request failed.";

        if (!IsProblemJson(response.Content.Headers.ContentType?.MediaType))
        {
            return Create(response.StatusCode, fallbackTitle, null, null, null, requestId, null);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            var title = ReadString(root, "title") ?? fallbackTitle;
            var detail = ReadString(root, "detail");
            var type = ReadString(root, "type");
            var traceId = ReadString(root, "traceId");
            var validationErrors = ReadValidationErrors(root);

            return Create(response.StatusCode, title, detail, type, traceId, requestId, validationErrors);
        }
        catch (JsonException)
        {
            return Create(response.StatusCode, fallbackTitle, null, null, null, requestId, null);
        }
    }

    public static Dt1520AuthenticatorError CreateTransportFailure(string title)
    {
        return new Dt1520AuthenticatorError(Dt1520AuthenticatorErrorKind.TransportFailure, null, title);
    }

    public static Dt1520AuthenticatorError CreateTimeout()
    {
        return new Dt1520AuthenticatorError(Dt1520AuthenticatorErrorKind.Timeout, null, "DT-1520 request timed out.");
    }

    public static Dt1520AuthenticatorError CreateCanceled()
    {
        return new Dt1520AuthenticatorError(Dt1520AuthenticatorErrorKind.Canceled, null, "DT-1520 request was canceled.");
    }

    public static Dt1520AuthenticatorError CreateValidationFailure(string title)
    {
        return new Dt1520AuthenticatorError(Dt1520AuthenticatorErrorKind.ValidationFailed, null, title);
    }

    public static Dt1520AuthenticatorError CreateServerFailure(string title)
    {
        return new Dt1520AuthenticatorError(Dt1520AuthenticatorErrorKind.ServerFailure, null, title);
    }

    private static Dt1520AuthenticatorError Create(
        HttpStatusCode statusCode,
        string title,
        string? detail,
        string? type,
        string? traceId,
        string? requestId,
        IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        return new Dt1520AuthenticatorError(
            MapKind(statusCode),
            (int)statusCode,
            title,
            detail,
            type,
            traceId,
            requestId,
            validationErrors);
    }

    private static Dt1520AuthenticatorErrorKind MapKind(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => Dt1520AuthenticatorErrorKind.ValidationFailed,
            HttpStatusCode.Unauthorized => Dt1520AuthenticatorErrorKind.Unauthorized,
            HttpStatusCode.Forbidden => Dt1520AuthenticatorErrorKind.Forbidden,
            HttpStatusCode.NotFound => Dt1520AuthenticatorErrorKind.NotFound,
            HttpStatusCode.Gone => Dt1520AuthenticatorErrorKind.Conflict,
            HttpStatusCode.Conflict => Dt1520AuthenticatorErrorKind.Conflict,
            HttpStatusCode.UnprocessableEntity => Dt1520AuthenticatorErrorKind.ValidationFailed,
            (HttpStatusCode)429 => Dt1520AuthenticatorErrorKind.RateLimited,
            >= HttpStatusCode.InternalServerError => Dt1520AuthenticatorErrorKind.ServerFailure,
            _ => Dt1520AuthenticatorErrorKind.TransportFailure,
        };
    }

    private static bool IsProblemJson(string? mediaType)
    {
        return string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyDictionary<string, string[]>? ReadValidationErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var property in errors.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                ? property.Value.EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray()
                : [property.Value.ToString()];
        }

        return result;
    }

    private static string? TryGetHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }
}
