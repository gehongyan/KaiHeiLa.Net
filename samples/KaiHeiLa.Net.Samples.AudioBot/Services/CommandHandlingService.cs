using System.Reflection;
using KaiHeiLa.Commands;
using KaiHeiLa.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace KaiHeiLa.Net.Samples.AudioBot.Services
{
    public class CommandHandlingService
    {
        private readonly CommandService _commands;
        private readonly KaiHeiLaSocketClient _kaiHeiLa;
        private readonly IServiceProvider _services;

        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _kaiHeiLa = services.GetRequiredService<KaiHeiLaSocketClient>();
            _services = services;

            _commands.CommandExecuted += CommandExecutedAsync;
            _kaiHeiLa.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            if (rawMessage is not SocketUserMessage {Source: MessageSource.User} message)
                return;

            var argPos = 0;
            if (!message.HasCharPrefix('!', ref argPos))
                return;

            var context = new SocketCommandContext(_kaiHeiLa, message);
            await _commands.ExecuteAsync(context, argPos, _services);
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified)
                return;

            if (result.IsSuccess)
                return;

            await context.Channel.SendTextMessageAsync($"error: {result}");
        }
    }
}
