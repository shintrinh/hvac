using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AirwayHVAC.SmsAi.Application.Tests.Routing;

public sealed class HeaderTenantResolverTests
{
    [Fact]
    public void Resolve_ShouldUseVerifiedTenantHeader_WhenVerificationMatches()
    {
        var sut = CreateSut(new TenantRoutingOptions
        {
            VerificationKey = "secret",
            AllowDefaultTenantFallback = false,
        });

        var result = sut.Resolve("localhost", "+15555550123", "+18777804236", "tenant_hdr", "secret");

        Assert.False(result.Rejected);
        Assert.Equal("tenant_hdr", result.TenantId);
        Assert.Null(result.TenantPhoneNumber);
        Assert.Equal("verified_header", result.Source);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenTenantHeaderProvidedButVerificationFails()
    {
        var sut = CreateSut(new TenantRoutingOptions
        {
            VerificationKey = "secret",
            AllowDefaultTenantFallback = false,
        });

        var result = sut.Resolve("localhost", "+15555550123", "+18777804236", "tenant_hdr", "wrong");

        Assert.True(result.Rejected);
        Assert.Equal("rejected", result.Source);
    }

    [Fact]
    public void Resolve_ShouldFallbackToPhoneMapping_WhenNoHeaderProvided()
    {
        var sut = CreateSut(new TenantRoutingOptions
        {
            VerificationKey = "secret",
            AllowDefaultTenantFallback = false,
        });

        var result = sut.Resolve("localhost", "+15555550123", "+18777804236", null, null);

        Assert.False(result.Rejected);
        Assert.Equal("to_phone", result.Source);
        Assert.Equal("+18777804236", result.TenantPhoneNumber);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenNoHeaderAndNoPhoneAndNoDefaultFallback()
    {
        var sut = CreateSut(new TenantRoutingOptions
        {
            VerificationKey = "secret",
            AllowDefaultTenantFallback = false,
            DefaultTenantId = "default-tenant",
        });

        var result = sut.Resolve("localhost", "+15555550123", string.Empty, null, null);

        Assert.True(result.Rejected);
        Assert.Equal("rejected", result.Source);
    }

    [Fact]
    public void Resolve_ShouldUseConfiguredDefaultFallback_WhenEnabled()
    {
        var sut = CreateSut(new TenantRoutingOptions
        {
            VerificationKey = "secret",
            AllowDefaultTenantFallback = true,
            DefaultTenantId = "default-tenant",
        });

        var result = sut.Resolve("localhost", "+15555550123", string.Empty, null, null);

        Assert.False(result.Rejected);
        Assert.Equal("default_fallback", result.Source);
        Assert.Equal("default-tenant", result.TenantId);
    }

    private static HeaderTenantResolver CreateSut(TenantRoutingOptions options)
        => new(Options.Create(options));
}
