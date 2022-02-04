using System.Globalization;

namespace KaiHeiLa.Net;

/// <summary>
///     Represents a REST-Based ratelimit info.
/// </summary>
public struct RateLimitInfo : IRateLimitInfo
{
    /// <inheritdoc/>
    public bool IsGlobal { get; }

    /// <inheritdoc/>
    public int? Limit { get; }

    /// <inheritdoc/>
    public int? Remaining { get; }

    /// <inheritdoc/>
    public int? RetryAfter { get; }

    /// <inheritdoc/>
    public DateTimeOffset? Reset { get; }

    /// <inheritdoc/>
    public TimeSpan? ResetAfter { get; }

    /// <inheritdoc/>
    public string Bucket { get; }

    /// <inheritdoc/>
    public TimeSpan? Lag { get; }

    /// <inheritdoc/>
    public string Endpoint { get; }

    internal RateLimitInfo(Dictionary<string, string> headers, string endpoint)
    {
        Endpoint = endpoint;

        IsGlobal = headers.TryGetValue("X-Rate-Limit-Global", out string temp) &&
                   bool.TryParse(temp, out var isGlobal) && isGlobal;
        Limit = headers.TryGetValue("X-Rate-Limit-Limit", out temp) && 
                int.TryParse(temp, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) ? limit : (int?)null;
        Remaining = headers.TryGetValue("X-Rate-Limit-Remaining", out temp) && 
                    int.TryParse(temp, NumberStyles.None, CultureInfo.InvariantCulture, out var remaining) ? remaining : (int?)null;
        Reset = headers.TryGetValue("X-Rate-Limit-Reset", out temp) && 
                double.TryParse(temp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var reset) && reset != 0 ? DateTimeOffset.FromUnixTimeMilliseconds((long)(reset * 1000)) : (DateTimeOffset?)null;
        RetryAfter = headers.TryGetValue("Retry-After", out temp) &&
                     int.TryParse(temp, NumberStyles.None, CultureInfo.InvariantCulture, out var retryAfter) ? retryAfter : (int?)null;
        ResetAfter = headers.TryGetValue("X-Rate-Limit-Reset-After", out temp) &&
                     double.TryParse(temp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var resetAfter) ? TimeSpan.FromSeconds(resetAfter) : (TimeSpan?)null;
        Bucket = headers.TryGetValue("X-Rate-Limit-Bucket", out temp) ? temp : null;
        Lag = headers.TryGetValue("Date", out temp) &&
              DateTimeOffset.TryParse(temp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? DateTimeOffset.UtcNow - date : (TimeSpan?)null;
    }
}