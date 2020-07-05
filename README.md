# Immediate Accept Bot

Built starting from Bot Framework v4 echo bot sample.

This bot demonstrates how to create a simple bot that accepts input from the user and echoes it back.  All incoming activities are processed on a background thread, alleviating 15 second timeout concerns.

ImmediateAcceptAdapter verifies the authorization header, adds the message to a Microsoft.Extensions.Hosting.BackgroundService (QueuedHostedService) for processing, and writes HttpStatusCode.OK to HttpResponse.  This causes all messages sent by the bot to effectively be proactive.

Note: this sample is uging a SemaphoreSlim in BackgroundTaskQueue, serializing message processing.  To improve performance and scalability, this could be a categorized queue of SemaphoreSlims, keyed on Converation.Id.

QueuedHostedService is from https://github.com/dotnet/AspNetCore.Docs/tree/master/aspnetcore/fundamentals/host/hosted-services/samples/3.x/BackgroundTasksSample