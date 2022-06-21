namespace KaiHeiLa.API.Voice;

internal enum VoiceSocketFrameType
{
    GetRouterRtpCapabilities,
    Join,
    CreatePlainTransport,
    Produce,
    NewPeer,
    NetworkStat,
    PeerClosed
}