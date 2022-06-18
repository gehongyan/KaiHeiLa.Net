using KaiHeiLa;
using KaiHeiLa.Commands;
using KaiHeiLa.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TextCommandFramework.Services;

namespace TextCommandFramework
{
    // This is a minimal example of using KaiHeiLa.Net's command
    // framework - by no means does it show everything the framework
    // is capable of.
    class Program
    {
        // There is no need to implement IDisposable like before as we are
        // using dependency injection, which handles calling Dispose for us.
        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            // You should dispose a service provider created using ASP.NET
            // when you are finished using it, at the end of your app's lifetime.
            // If you use another dependency injection framework, you should inspect
            // its documentation for the best way to do this.
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<KaiHeiLaSocketClient>();

                client.Log += LogAsync;
                services.GetRequiredService<CommandService>().Log += LogAsync;

                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hard coding.
                await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("KaiHeiLaDebugToken", EnvironmentVariableTarget.User));
                await client.StartAsync();

                // Here we initialize the logic required to register our commands.
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
                .AddSingleton<PictureService>()
                .BuildServiceProvider();
        }
    }
}
