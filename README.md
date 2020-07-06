# Immediate Accept Bot

Built starting from Bot Framework v4 echo bot sample.

This example demonstrates how to create a simple bot that accepts input from the user and echoes it back.  All incoming activities are processed on a background thread, alleviating 15 second timeout concerns.

ImmediateAcceptAdapter verifies the authorization header, adds the message to a Microsoft.Extensions.Hosting.BackgroundService (QueuedHostedService) for processing, and writes HttpStatusCode.OK to HttpResponse.  This causes all messages sent by the bot to effectively be proactive.

```cs
// Deserialize the incoming Activity
var activity = await HttpHelper.ReadRequestAsync<Activity>(httpRequest).ConfigureAwait(false);

if (string.IsNullOrEmpty(activity?.Type))
{
	httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
}
else if (activity.Type == ActivityTypes.Invoke || activity.DeliveryMode == DeliveryModes.ExpectReplies)
{
	// NOTE: Invoke and ExpectReplies cannot be processed on a background thread.
	// the response must be written before the calling thread is released.
	await base.ProcessAsync(httpRequest, httpResponse, bot, cancellationToken);
}
else
{
	// Grab the auth header from the inbound http request
	var authHeader = httpRequest.Headers["Authorization"];

	try
	{
		// If authentication passes, queue a work item to process the inbound activity with the bot
		var claimsIdentity = await JwtTokenValidation.AuthenticateRequest(activity, authHeader, CredentialProvider, ChannelProvider, HttpClient).ConfigureAwait(false);

		// Queue the activity to be processed on a background thread
		_backgroundTaskQueue.QueueBackgroundWorkItem(async cancelToken => await ProcessActivityAsync(claimsIdentity, activity, bot.OnTurnAsync, cancelToken).ConfigureAwait(false));
		
		// Activity has been queued to process, so return Ok immediately
		httpResponse.StatusCode = (int)HttpStatusCode.OK;
	}
	catch (UnauthorizedAccessException)
	{
		// handle unauthorized here as this layer creates the http response
		httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
	}
}	
```


QueuedHostedService is from https://github.com/dotnet/AspNetCore.Docs/tree/master/aspnetcore/fundamentals/host/hosted-services/samples/3.x/BackgroundTasksSample
