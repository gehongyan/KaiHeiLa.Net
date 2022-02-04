﻿// See https://aka.ms/new-console-template for more information

using KaiHeiLa;
using KaiHeiLa.API;
using KaiHeiLa.WebSocket;

string token = Environment.GetEnvironmentVariable("KaiHeiLaDebugToken", EnvironmentVariableTarget.User) 
         ?? throw new ArgumentNullException(nameof(token));

KaiHeiLaSocketClient client = new(new KaiHeiLaSocketConfig());
client.Log += log =>
{
    Console.WriteLine(log.ToString());
    return Task.CompletedTask;
};
client.Ready += () =>
{
    var result = client.GetGuild(1990044438283387);
    return Task.CompletedTask;
};
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();
await Task.Delay(-1);