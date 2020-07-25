// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.9.2

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace ImmediateAcceptBot.Bots
{
    public class EchoBot : ActivityHandler
    {
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
            else if (text.StartsWith("background"))
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
                        await turnContext.SendActivityAsync($"okay, pausing {seconds} seconds", cancellationToken: cancellationToken);
                        Thread.Sleep(TimeSpan.FromSeconds(seconds));
                        await turnContext.SendActivityAsync($"finished: {seconds} seconds", cancellationToken: cancellationToken);
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

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("send: 4 seconds   ...  and i will pause for 4 seconds"), cancellationToken);
                    // await turnContext.SendActivityAsync(MessageFactory.Text("send: background 4   ...  and i will push to a background thread to process for 4 seconds."), cancellationToken);
                }
            }
        }
    }
}
