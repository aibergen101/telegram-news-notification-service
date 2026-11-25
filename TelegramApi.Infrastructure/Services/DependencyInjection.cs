using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramApi.Application.Common;

namespace TelegramApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<TelegramApiDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TelegramApiDbContext>());

        services.AddOptions<TelegramBotOptions>()
            .Bind(configuration.GetSection(TelegramBotOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.BotToken.Contains(':'), "TelegramBot:BotToken has an invalid format.")
            .ValidateOnStart();

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value;

            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };

            var clientOptions = new TelegramBotClientOptions(options.BotToken)
            {
                RetryCount = 0,
                RetryThreshold = 0
            };

            return new TelegramBotClient(clientOptions, httpClient);
        });

        services.AddScoped<ITelegramService, TelegramService>();

        return services;
    }
}
