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

namespace TestBot.Dialogs
{

    struct Entity {
        public string Type;
        public string Child;
    }

    //[LuisModel("c8264ce7-e050-41f6-abca-e281561c88b8", "f02536e856a548f6a89fa27a7bafd5ec")]
    [Serializable]
    public class LuisDialog : IDialog
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(LuisDialogAsync);
        }

        public virtual async Task LuisDialogAsync(IDialogContext context, IAwaitable<object> argument)
        {
            PromptDialog.Text(context, TextEntered, "Welche Informationen brauchst du zu welcher Form? \n\n(Gib 'Hilfe' für weitere Infomationen ein)");
        }

        public virtual async Task TextEntered(IDialogContext context, IAwaitable<string> argument)
        {
            var result = await argument;

            //call Luis with result text
            JObject jobj;
            using (HttpClient httpClient = new HttpClient())
            {
                var url = "https://westus.api.cognitive.microsoft.com/luis/v2.0/apps/c8264ce7-e050-41f6-abca-e281561c88b8?subscription-key=f02536e856a548f6a89fa27a7bafd5ec&timezoneOffset=60&verbose=true&q=";
                var responseString = await httpClient.GetStringAsync(url + result);
                jobj = JObject.Parse(responseString);
            }

            var topScoringIntent = jobj.SelectToken("topScoringIntent");
            var intent = topScoringIntent.SelectToken("intent").ToString();
            var score = topScoringIntent.SelectToken("score").ToString();

            var entities = (JArray)jobj.SelectToken("entities");
            var entityList = new List<Entity>();
            for (var i = 0; i < entities.Count; i++) {
                entityList.Add(new Entity() {
                    Type = entities[i].SelectToken("type").ToString(),
                    Child = entities[i].SelectToken("entity").ToString()
                });
            }

            //TODO: implement logic for multiple entities
            if (entityList.Count > 0)
            {
                var entity = entityList[0].Child;

                switch (intent)
                {
                    case "Hilfe":
                        await context.PostAsync($"Hier hast du die Möglichkeit mich nach einer bestimmten Art von Informationsmedium zu fragen und die entstpechende Gesellschaftsform zu benennen.\n\n\n\n" +
                            $"Beispielsweise \"Ich brauche Dokumente zur Aktiengesellschaft\",\n\n\"Zeig mir online Nachrichten zur GbR\" oder\n\n\"Ich brauche jemanden der mir zum Thema GmbH weiterhilft\".");
                        PromptDialog.Text(context, TextEntered, "Also? Wie kann ich behilflich sein?");
                        break;
                    case "Ansprechpartner":
                        await Ansprechpartner(context, entity);
                        break;
                    case "Dokumente":
                        await Dokumente(context, entity);
                        break;
                    case "Link":
                        await Link(context, entity);
                        break;
                    case "None":
                        await context.PostAsync($"Leider habe ich '{result}' nicht verstanden.");
                        context.Done("Returning from LuisDialog");
                        break;
                    default:
                        await context.PostAsync($"Leider habe ich '{result}' nicht verstanden.");
                        context.Done("Returning from LuisDialog");
                        break;
                }
            }
            else
            {
                string Ansprechpartner = "Ansprechpartner";
                string Dokumente = "Dokumente";
                string Link = "Link";
                string Stock = "Stock";
                IEnumerable<string> choices = new List<string> { Ansprechpartner, Dokumente, Link, Stock };
                PromptDialog.Choice(context, TextEntered, choices, "Ich kann folgene Informationen geben?", promptStyle: PromptStyle.Auto);
            }
        }

        public async Task Ansprechpartner(IDialogContext context, string entity)
        {
            Attachment attachment = null;
            switch (entity) {
                case "GbR":
                    attachment = getCard(1);
                    break;
                case "IRFS 9":
                    attachment = getCard(2);
                    break;
                case "AnaCredit":
                    attachment = getCard(3);
                    break;
                default :
                    await context.PostAsync("Leider habe ich keinen ASP dazu gefunden.");
                    return;
            }

            var message = context.MakeMessage();
            message.Attachments.Add(attachment);
            await context.PostAsync(message);
        }

        public async Task Link(IDialogContext context, string entity)
        {
            var link = "";
            switch (entity)
            {
                case "GbR":
                    link = "https://de.wikipedia.org/wiki/Gesellschaft_b%C3%BCrgerlichen_Rechts_(Deutschland)";
                    break;
                case "IRFS 9":
                    link = "https://de.wikipedia.org/wiki/International_Financial_Reporting_Standard_9";
                    break;
                case "AnaCredit":
                    link = "https://de.wikipedia.org/wiki/AnaCredit";
                    break;
                default:
                    link = "https://www.google.de";
                    break;
            }

            await context.PostAsync("Ich habe folgende(n) Link(s) für dich gefunden");
            System.Diagnostics.Process.Start(link);
        }

        public async Task Dokumente(IDialogContext context, string entity)
        {
            var docs = searchDocuments(entity, 3);
            await context.PostAsync($"Ich habe folgende Dokumente zum Thema '{entity}' gefunden:");

            var message = "";

            foreach (var doc in docs)
                message += doc.Key + " (" + doc.Value + ")\n\n";

            await context.PostAsync(message);
        }
          
        private Attachment getCard(int index)
        {
            //var imgUrl = @"CardInfos\person" + index + ".png";
            var txtUrl = @"BotApp\CardInfos\person" + index + ".txt";

            StreamReader sr = new StreamReader(txtUrl);
            var title = sr.ReadLine();
            var subTitle = sr.ReadLine();
            var text = sr.ReadLine();
            var img = sr.ReadLine();
            sr.Close();

            var card = new HeroCard
            {
                Title = title,
                Subtitle = subTitle,
                Text = text,
                Images = new List<CardImage> { new CardImage(url: img, alt: "Image") },
                Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "Further Help", value: "https://google.de") }
            };

            return card.ToAttachment();
        }

        private IEnumerable<KeyValuePair<string, int>> searchDocuments(string keyword, int topCount)
        {
            var files = Directory.GetFiles(@"BotApp\Docs\");
            Dictionary<string, int> docsFound = new Dictionary<string, int>();

            foreach (var file in files)
            {
                PdfReader pdfReader = new PdfReader(file);
                var occurences = 0;

                for (int page = 1; page <= pdfReader.NumberOfPages; page++)
                {
                    ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                    string currentPageText = PdfTextExtractor.GetTextFromPage(pdfReader, page, strategy);
                    occurences += currentPageText.Split(' ').Count(i => i.Equals(keyword) || i.Contains(keyword) || i.Equals(keyword.ToLower()) || i.Contains(keyword.ToLower()));
                }
                pdfReader.Close();

                docsFound.Add(file, occurences);
            }

            var foundDocsWithEntity = docsFound.Where(i => i.Value > 0);
            foundDocsWithEntity.OrderByDescending(i => i.Value);

            if (foundDocsWithEntity.Count() >= topCount)
                return foundDocsWithEntity.Take(topCount);
            else
                return foundDocsWithEntity;
        }
    }
}