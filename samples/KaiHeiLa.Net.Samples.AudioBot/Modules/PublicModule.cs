using KaiHeiLa.Audio;
using KaiHeiLa.Commands;
using KaiHeiLa.Net.Samples.AudioBot.Services;

namespace KaiHeiLa.Net.Samples.AudioBot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        // Dependency Injection will fill this value in for us
        public KaiHeiLaAudioClientManager AudioClientManager { get; set; }
        
        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyTextAsync("pong!");
        
        // The command's Run Mode MUST be set to RunMode.Async, otherwise, being connected to a voice channel will block the gateway thread.
        [Command("join", RunMode = RunMode.Async)] 
        public async Task JoinChannel(IVoiceChannel? channel = null)
        {
            if (Context.User is not IGuildUser user)
                return;
            // Get the audio channel
            channel ??= (await user.GetConnectedVoiceChannelsAsync().ConfigureAwait(false)).FirstOrDefault();
            if (channel == null)
            {
                await Context.Channel.SendKMarkdownMessageAsync(
                    "User must be in a voice channel, or a voice channel must be passed as an argument.");
                return;
            }

            AudioClientManager.AudioClient = await channel.ConnectAsync();
        }
        
        [Command("leave", RunMode = RunMode.Async)] 
        public async Task LeaveChannel()
        {
            IReadOnlyCollection<IVoiceChannel> channels = await Context.Message.Guild.CurrentUser
                .GetConnectedVoiceChannelsAsync().ConfigureAwait(false);

            if (!channels.Any())
            {
                await Context.Channel.SendKMarkdownMessageAsync(
                    "Bot must be in a voice channel to leave.");
                return;
            }
            
            foreach (IVoiceChannel voiceChannel in channels)
            {
                await voiceChannel.DisconnectAsync();
            }
        }
        
    }
}
