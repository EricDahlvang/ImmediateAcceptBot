﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.9.2

using ImmediateAcceptBot.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ImmediateAcceptBot
{
    public class ImmediateAcceptAdapter : BotFrameworkHttpAdapter
    {
        IBackgroundTaskQueue _backgroundTaskQueue;

        public ImmediateAcceptAdapter(IConfiguration configuration, ILogger<BotFrameworkHttpAdapter> logger, IBackgroundTaskQueue backgroundTaskQueue)
            : base(configuration, logger)
        {
            _backgroundTaskQueue = backgroundTaskQueue;

            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

                // Send a message to the user
                await turnContext.SendActivityAsync("The bot encountered an error or bug.");
                await turnContext.SendActivityAsync("To continue to run this bot, please fix the bot source code.");

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            };
        }

        public async Task ProcessOnBackgroundThreadAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default)
        {
            if (httpRequest == null)
            {
                throw new ArgumentNullException(nameof(httpRequest));
            }

            if (httpResponse == null)
            {
                throw new ArgumentNullException(nameof(httpResponse));
            }

            if (bot == null)
            {
                throw new ArgumentNullException(nameof(bot));
            }

            // Get is a socket exchange request, so should be processed by base BotFrameworkHttpAdapter
            if (httpRequest.Method == HttpMethods.Get)
            {
                await base.ProcessAsync(httpRequest, httpResponse, bot, cancellationToken);
            }
            else
            {
                // Deserialize the incoming Activity
                var activity = await HttpHelper.ReadRequestAsync<Activity>(httpRequest).ConfigureAwait(false);

                if (string.IsNullOrEmpty(activity?.Type))
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                }
                else if (activity.Type == ActivityTypes.Invoke || activity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    // NOTE: Invoke and ExpectReplies cannot be performed async
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
            }
        }
    }
}
