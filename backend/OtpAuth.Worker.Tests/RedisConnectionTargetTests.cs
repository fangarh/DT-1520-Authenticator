using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class RedisConnectionTargetTests
{
    [Fact]
    public void TryParse_ReturnsTargetForHostPortPrefix()
    {
        var parsed = RedisConnectionTarget.TryParse("redis:6379,password=secret", out var target);

        Assert.True(parsed);
        Assert.NotNull(target);
        Assert.Equal("redis", target!.Host);
        Assert.Equal(6379, target.Port);
    }

    [Fact]
    public void TryParse_ReturnsFalseWhenEndpointIsMissing()
    {
        var parsed = RedisConnectionTarget.TryParse("password=secret,ssl=false", out var target);

        Assert.False(parsed);
        Assert.Null(target);
    }
}
