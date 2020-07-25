// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.9.2

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImmediateAcceptBot.BackgroundQueue;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;

namespace ImmediateAcceptBot.Bots
{
    public class EchoBot : ActivityHandler
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly string _botId;

        public EchoBot(IConfiguration config, IBackgroundTaskQueue taskQueue)
        {
            if (taskQueue == null)
            {
                throw new ArgumentNullException(nameof(taskQueue));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _botId = config.GetValue<string>("MicrosoftAppId");
            _botId = string.IsNullOrEmpty(_botId) ? Guid.NewGuid().ToString() : _botId;
            _taskQueue = taskQueue;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var text = turnContext.Activity.Text;
            if (text.EndsWith("seconds"))
            {
                var splitText = text.Split(" ");
                if (splitText.Count() < 2)
                {
                    await turnContext.SendActivityAsync($"missing first parameter.  example: 4 seconds", cancellationToken: cancellationToken);
                }
                else
                {
                    int seconds;
                    if (int.TryParse(splitText[0], out seconds) && seconds < 120)
                    {
                        await turnContext.SendActivityAsync($"okay, pausing {seconds} seconds", cancellationToken: cancellationToken);
                        Thread.Sleep(TimeSpan.FromSeconds(seconds));
                        await turnContext.SendActivityAsync($"finished: {seconds} seconds", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync($"Please enter reasonable seconds < 120.  example: 4 seconds,", cancellationToken: cancellationToken);
                    }
                }
            }
            else if (text.EndsWith("background"))
            {
                var splitText = text.Split(" ");
                if (splitText.Count() < 2)
                {
                    await turnContext.SendActivityAsync($"missing second parameter.  example: 4 background", cancellationToken: cancellationToken);
                }
                else
                {
                    int seconds;
                    if (int.TryParse(splitText[0], out seconds) && seconds < 120)
                    {
                        await turnContext.SendActivityAsync($"okay, I will background message you after: {seconds} seconds", cancellationToken: cancellationToken);
                        _taskQueue.QueueBackgroundWorkItem(async cancelToken => await ProactivelyMessageUserAfterNSeconds(turnContext.Adapter, turnContext.Activity.GetConversationReference(), "Notice from Bot!", seconds, cancelToken).ConfigureAwait(false));
                        
                    }
                    else
                    {
                        await turnContext.SendActivityAsync($"Please enter reasonable seconds < 120.  example: 4 background,", cancellationToken: cancellationToken);
                    }
                }
            }
            else
            {
                var replyText = $"Echo: {text}";
                await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
            }
        }

        private async Task ProactivelyMessageUserAfterNSeconds(BotAdapter adapter, ConversationReference conversationReference, string message, int seconds, CancellationToken cancellationToken)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            await adapter.ContinueConversationAsync(_botId, conversationReference, async (context, innerCancellationToken) => { await context.SendActivityAsync($"background sending after {seconds}: {message}"); }, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hi!  I'm a background processing bot. All incoming messages are processed on a background thread."), cancellationToken);
                    await turnContext.SendActivityAsync(MessageFactory.Text("send: 4 seconds   ...  and i will pause for 4 seconds while processing your message."), cancellationToken);
                    await turnContext.SendActivityAsync(MessageFactory.Text("send: 4 background   ...  and i will push your message to an additional background thread to process for 4 seconds."), cancellationToken);
                }
            }
        }
    }
}
