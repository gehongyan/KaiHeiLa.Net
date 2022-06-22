using System.Diagnostics;

namespace KaiHeiLa.Net.Samples.AudioBot.Utils;

public static class StreamHelper
{
    public static Process CreateAudioStreamFromFile(string path, int silentSeconds)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = 
                silentSeconds switch
                {
                    > 0 => $"-hide_banner -loglevel panic -f lavfi -t {silentSeconds} -i anullsrc=channel_layout=stereo:sample_rate=16000 -i \"{path}\" -filter_complex \"[0][1]concat=n=2:v=0:a=1\" -ac 2 -f s16le -ar 48000 pipe:1",
                    _ => $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1"
                },
                UseShellExecute = false,
            RedirectStandardOutput = true,
        });
    }

    public static string CreateRTPUrl(string ip, int port, int rtcpPort) => $"rtp://{ip}:{port}?rtcpport={rtcpPort}";

    public static Process CreateAudioStreamViaFfmpeg(string path, int ssrc, string rtpUrl)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments =
                $"-re -loglevel level+info -nostats -i {path} -map 0:a:0 -acodec libopus -ab 128k -filter:a volume=0.8 -ac 2 -ar 48000 -f tee [select=a:f=rtp:ssrc={ssrc}:payload_type=100]{rtpUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });
    }
}