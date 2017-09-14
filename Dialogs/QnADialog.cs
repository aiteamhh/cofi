using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Net;

namespace TestBot.Dialogs
{
    //KB id: a3f506cb-11e6-456b-999a-1dabbe5b9880
    //Subscription-Key: 6c374f8c7eea4405afa0cd80e496c6e2
    [Serializable]
    public class QnADialog : IDialog
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(QnADialogAsync);
        }

        public virtual async Task QnADialogAsync(IDialogContext context, IAwaitable<object> argument)
        {
            PromptDialog.Text(context, TextEntered, "Was möchtest du denn Wissen?");
        }

        public virtual async Task TextEntered(IDialogContext context, IAwaitable<string> argument)
        {
            var result = await argument;

            //call QnA with result text
            List<JToken> allAnswers;
            using (WebClient webClient = new WebClient())
            {
                var top = 3;
                var uri = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v2.0/knowledgebases/a3f506cb-11e6-456b-999a-1dabbe5b9880/generateAnswer");
                var body = $"{{ \"question\": \"{result}\", \"top\": \"{top}\" }}";

                webClient.Encoding = System.Text.Encoding.UTF8;
                webClient.Headers.Add("Ocp-Apim-Subscription-Key", "6c374f8c7eea4405afa0cd80e496c6e2");
                webClient.Headers.Add("Content-Type", "application/json");
                var responseString = webClient.UploadString(uri, body);

                JObject jobj = JObject.Parse(responseString);
                allAnswers = jobj.SelectToken("answers").ToList();
            }

            
            var answer = allAnswers.First().SelectToken("answer");
            var score = allAnswers.First().SelectToken("score");

            var a = answer.ToString();
            var s = score.ToString();

            //var answer = (JArray)jobj.SelectToken("answers");
            await context.PostAsync($"{answer} ({score})");

            context.Done("Returning from QnADialog");
        }

    }
}