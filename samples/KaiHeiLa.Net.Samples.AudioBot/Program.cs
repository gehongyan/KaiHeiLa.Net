using KaiHeiLa.Audio;
using KaiHeiLa.Commands;
using KaiHeiLa.Net.Samples.AudioBot.Services;
using KaiHeiLa.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace KaiHeiLa.Net.Samples.AudioBot;

class Program
{
    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        using (var services = ConfigureServices())
        {
            var client = services.GetRequiredService<KaiHeiLaSocketClient>();

            client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("KaiHeiLaDebugToken", EnvironmentVariableTarget.User));
            await client.StartAsync();

            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

            await Task.Delay(Timeout.Infinite);
        }
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());

        return Task.CompletedTask;
    }

    private ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddSingleton(_ => new KaiHeiLaSocketClient(new KaiHeiLaSocketConfig()
            {
                AlwaysDownloadUsers = true,
                LogLevel = LogSeverity.Debug
            }))
            .AddSingleton<CommandService>()
            .AddSingleton<CommandHandlingService>()
            .AddSingleton<HttpClient>()
            .AddSingleton<KaiHeiLaAudioClientManager>()
            .BuildServiceProvider();
    }
}