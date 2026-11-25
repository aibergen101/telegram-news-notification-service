using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramApi.Application.Common;

namespace TelegramApi.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        services.AddDbContext<TelegramApiDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql();
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TelegramApiDbContext>());

        services.AddOptions<TelegramBotOptions>()
            .Bind(configuration.GetSection(TelegramBotOptions.SectionName));

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
