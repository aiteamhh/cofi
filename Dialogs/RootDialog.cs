using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Builder.Luis;
using DiffMatchPatch;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http;
using System.Web.Http;

namespace TestBot.Dialogs
{
   
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        private const string Information = "Informationen";
        private const string Diff = "Unterschied";
        private const string Question = "FAQ";
        private const string Exit = "Danke";
        private IEnumerable<string> choices = new List<string> { Information, Diff, Question, Exit };
        private IEnumerable<string> choiceTexts = new List<string> { "Informationen zu einer Gesellschaftsform", "Eine Textänderung untersuchen", "AnaCredit FAQ", "Nichts mehr, Danke!" };

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(RootDialogAsync);

            return Task.CompletedTask;
        }

        public virtual async Task RootDialogAsync(IDialogContext context, IAwaitable<object> argument)
        {
            var result = await argument;

            if (result.GetType() != typeof(String))
                PromptDialog.Choice(context, ChoiceMade, choices, "Wie kann ich helfen?", promptStyle: PromptStyle.Auto, descriptions: choiceTexts);
            else
                PromptDialog.Choice(context, ChoiceMade, choices, "Wie kann ich sonst noch helfen?", promptStyle: PromptStyle.Auto, descriptions: choiceTexts);
        }

        private async Task ChoiceMade(IDialogContext context, IAwaitable<string> argument)
        {
            var result = await argument;

            switch (result) {

                case Diff:
                    await context.Forward(new DiffDialog(), RootDialogAsync, "", CancellationToken.None);
                    break;

                case Information:
                    await context.Forward(new LuisDialog(), RootDialogAsync, "", CancellationToken.None);
                    break;

                case Question:
                    await context.Forward(new QnADialog(), RootDialogAsync, "", CancellationToken.None);
                    break;

                case Exit:
                    await context.PostAsync("Gerne, bis bald!");
                    context.EndConversation("0");
                    break;
            }            
        }
        
    }
}