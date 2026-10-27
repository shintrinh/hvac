using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Repositories;
using AirwayHVAC.SmsAi.Infrastructure.Services;
using AirwayHVAC.SmsAi.Shared.Abstractions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirwayHVAC.SmsAi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
        services.Configure<TwilioVoiceOptions>(configuration.GetSection(TwilioVoiceOptions.SectionName));
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.SectionName));
        services.Configure<TenantRoutingOptions>(configuration.GetSection(TenantRoutingOptions.SectionName));

        services.AddSingleton(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
            return new CosmosClient(options.Endpoint, options.Key, new CosmosClientOptions
            {
                ApplicationName = "AirwayHVAC.SmsAi",
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                },
            });
        });

        services.AddScoped<IAiResponder, OpenAiResponder>();
        services.AddScoped<IOutboundSmsService, TwilioSmsService>();

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<ILeadRepository, LeadRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IVoiceSessionRepository, VoiceSessionRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantResolver, HeaderTenantResolver>();

        services.AddScoped<ConversationService>();
        services.AddScoped<ConversationFlowEngine>();
        services.AddScoped<ConversationOrchestrator>();
        services.AddScoped<LeadService>();
        services.AddScoped<EventService>();
        services.AddScoped<DashboardService>();

        return services;
    }
}
