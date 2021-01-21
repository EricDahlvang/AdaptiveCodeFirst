﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AdaptiveExpressions.Properties;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Actions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Conditions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Generators;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Templates;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.LanguageGeneration;
using Microsoft.BotBuilderSamples.FacebookModel;

namespace Microsoft.BotBuilderSamples
{
    public class FacebookEventsDialog : AdaptiveDialog
    {
        private readonly Templates _templates;

        public FacebookEventsDialog()
            : base(nameof(FacebookEventsDialog))
        {
            string[] paths = { ".", "Dialogs", $"FacebookEventsDialog.lg" };
            var fullPath = Path.Combine(paths);
            _templates = Templates.ParseFile(fullPath);

            AutoEndDialog = false;

            // These steps are executed when this Adaptive Dialog begins
            Triggers = new List<OnCondition>
            {
                // Setup an OnEventActivity to set the activity.value
                // into a turn Property, then process the event.
                // This event is setup to listen to only optin and is_echo.
                // Message, Quick Reply and Postbacks are processed by the
                // OnUnknownIntent below.
                new OnEventActivity
                {
                    Actions = new List<Dialog>
                    {
                        new IfCondition
                        {
                            Condition = "turn.activity.name == 'echo'",
                            Actions = new List<Dialog>
                            {
                                new CodeAction(OnFacebookEcho),
                                new EndTurn(),
                            },
                        },
                        new IfCondition
                        {
                            Condition = "turn.facebookPayload.optin != null",
                            Actions = new List<Dialog>
                            {
                                new CodeAction(OnFacebookOptin),
                                new EndTurn(),
                            },
                        },
                    }
                },
                
                // Respond to user with choices or processing their message.
                new OnUnknownIntent
                {
                    Condition = "turn.activity.text.length > 0 || turn.facebookPayload.message.quick_reply != null || turn.facebookPayload.postback != null",
                    Actions = new List<Dialog>() { ShowChoices(), ProcessMessage() }
                }
            };

            Generator = new TemplateEngineLanguageGenerator(_templates);
        }

        private async Task<DialogTurnResult> OnFacebookOptin(DialogContext arg1, object arg2)
        {
            await arg1.EndDialogAsync().ConfigureAwait(false);
            return new DialogTurnResult(DialogTurnStatus.Complete);
        }

        private async Task<DialogTurnResult> OnFacebookEcho(DialogContext arg1, object arg2)
        {
            await arg1.EndDialogAsync().ConfigureAwait(false);
            return new DialogTurnResult(DialogTurnStatus.Complete);
        }

        private Dialog ProcessMessage()
        {
            return new SwitchCondition()
            {
                Condition = "conversation.promptChoice",
                Cases = new List<Case>()
                {
                    new Case("Quick Replies",   new List<Dialog>() { new SendActivity("${ButtonWasQuickReplyMessage()}") }),
                    new Case("Facebook Id",     new List<Dialog>() { new SendActivity("${FacebookPageIdMessage()}") }),
                    new Case("Postback",        new List<Dialog>() { new SendActivity("${PostBackHeroCard()}"), new EndTurn(), new SendActivity("${PostbackResponseMessage()}") }),
                },
                Default = new List<Dialog> { ShowChoices() }
            };
        }

        private Dialog ShowChoices()
        {
            return new ChoiceInput
            {
                Id = "FacebookChoicesDialogId",

                // Ask the user which type of Facebook activity they would like to try
                Prompt = new ActivityTemplate("${ChoicesPromptMessage()}"),
                InvalidPrompt = new ActivityTemplate("${ChoicesPromptMessage()}"),
                AllowInterruptions = true,
                Style = ListStyle.Auto,

                // Inputs will skip the prompt if the property (turn.promptChoice in this case) already has value.
                // Since we are using RepeatDialog, we will set AlwaysPrompt property so we do not skip this prompt
                // and end up in an infinite loop.
                AlwaysPrompt = true,
                Choices = new ObjectExpression<ChoiceSet>(_templates.Evaluate("MessageTypeChoices") as string),
                Property = "conversation.promptChoice"
            };
        }
    }
}
