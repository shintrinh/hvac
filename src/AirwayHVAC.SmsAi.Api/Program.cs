using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration((_, configBuilder) =>
    {
        configBuilder
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables();

        var mappedSettings = new Dictionary<string, string?>();

        AddIfNotEmpty(mappedSettings, "Twilio:AccountSid", "TWILIO_SID");
        AddIfNotEmpty(mappedSettings, "Twilio:AuthToken", "TWILIO_TOKEN");
        AddIfNotEmpty(mappedSettings, "Twilio:FromNumber", "TWILIO_NUMBER");
        AddIfNotEmpty(mappedSettings, "Twilio:MessagingServiceSid", "TWILIO_MSGSERID");

        AddIfNotEmpty(mappedSettings, "TwilioVoice:PublicBaseUrl", "TWILIO_VOICE_PUBLIC_BASE_URL");
        AddIfNotEmpty(mappedSettings, "TwilioVoice:HumanHandoffPhoneNumber", "TWILIO_VOICE_HANDOFF_NUMBER");
        AddIfNotEmpty(mappedSettings, "TwilioVoice:DisclosureMessage", "TWILIO_VOICE_DISCLOSURE_MESSAGE");

        AddIfNotEmpty(mappedSettings, "AzureOpenAi:Endpoint", "AZURE_OPENAI_ENDPOINT");
        AddIfNotEmpty(mappedSettings, "AzureOpenAi:ApiKey", "AZURE_OPENAI_KEY");
        AddIfNotEmpty(mappedSettings, "AzureOpenAi:DeploymentName", "AZURE_OPENAI_DEPLOYMENT");

        AddIfNotEmpty(mappedSettings, "Cosmos:Endpoint", "COSMOS_ENDPOINT");
        AddIfNotEmpty(mappedSettings, "Cosmos:Key", "COSMOS_KEY");
        AddIfNotEmpty(mappedSettings, "Cosmos:DatabaseName", "COSMOS_DATABASE");

        AddIfNotEmpty(mappedSettings, "TenantRouting:VerificationKey", "TENANT_ROUTING_VERIFICATION_KEY");
        AddIfNotEmpty(mappedSettings, "TenantRouting:DefaultTenantId", "TENANT_ROUTING_DEFAULT_TENANT_ID");

        AddIfNotEmpty(mappedSettings, "SendGrid:ApiKey", "SENDGRID_API_KEY");
        AddIfNotEmpty(mappedSettings, "SendGrid:FromEmail", "SENDGRID_FROM_EMAIL");
        AddIfNotEmpty(mappedSettings, "SendGrid:FromName", "SENDGRID_FROM_NAME");

        configBuilder.AddInMemoryCollection(mappedSettings);
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);
        services.AddScoped<HandleInboundSmsUseCase>();
        services.AddScoped<HandleInboundVoiceUseCase>();
        services.AddScoped<HandleVoiceGatherUseCase>();
        services.AddScoped<HandleVoiceStatusUseCase>();
    })
    .Build();

await host.RunAsync();

static void AddIfNotEmpty(IDictionary<string, string?> settings, string configKey, string envVarName)
{
    var value = Environment.GetEnvironmentVariable(envVarName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        settings[configKey] = value;
    }
}
