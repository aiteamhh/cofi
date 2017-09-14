using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using DiffMatchPatch;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace TestBot.Dialogs
{
    [Serializable]
    public class DiffDialog : IDialog<string>
    {
        //private const string diffFile = @"c:\tmp\differences.htm";
        private const string diffFile = @"differences.htm";

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(DiffDialogAsync);
        }

        public virtual async Task DiffDialogAsync(IDialogContext context, IAwaitable<object> argument)
        {
            await context.PostAsync($"Ich brauche dafür zwei Dokumente von dir.");

            PromptDialog.Attachment(context, Unterschied1, "Gib mir zunächst am besten die alte Version.");
        }
        private async Task Unterschied1(IDialogContext context, IAwaitable<IEnumerable<Attachment>> argument)
        {
            var result = await argument as List<Attachment>;
            context.UserData.SetValue("doc1", result[0]);

            PromptDialog.Attachment(context, Unterschied2, "Gib mir jetzt das neue Dokument.");
        }
        private async Task Unterschied2(IDialogContext context, IAwaitable<IEnumerable<Attachment>> argument)
        {
            var result = await argument as List<Attachment>;
            context.UserData.SetValue("doc2", result[0]);

            var dmp = new diff_match_patch()  { Diff_EditCost = 10 };

            var doc1 = context.UserData.GetValue<Attachment>("doc1");
            var doc2 = context.UserData.GetValue<Attachment>("doc2");

            var text1 = "";
            var text2 = "";

            using (HttpClient httpClient = new HttpClient())
            {
                var response1 = await httpClient.GetAsync(doc1.ContentUrl);
                var jobject1 = JObject.Parse(response1.Content.ReadAsStringAsync().Result);
                var token1 = jobject1.SelectToken("data");
                var bytes1 = token1.ToObject<byte[]>();
                text1 = System.Text.Encoding.UTF8.GetString(bytes1);

                var response2 = await httpClient.GetAsync(doc2.ContentUrl);
                var jobject2 = JObject.Parse(response2.Content.ReadAsStringAsync().Result);
                var token2 = jobject2.SelectToken("data");
                var bytes2 = token2.ToObject<byte[]>();
                text2 = System.Text.Encoding.UTF8.GetString(bytes2);
            }

            var diffs = dmp.diff_main(text1, text2);
            dmp.diff_cleanupSemantic(diffs);
            var htmlContent = dmp.diff_prettyHtml(diffs);

            var html = "<div style='border: #181818 solid 1px; width: 550px; margin: 0 auto; margin-top: 100px; padding: 30px; box-shadow: 10px 10px 5px grey;'>";
            html += htmlContent + "</div>";

            StreamWriter sw = new StreamWriter(diffFile, false);
            sw.Write(html);
            sw.Flush();
            sw.Close();

            await context.PostAsync($"Ich habe {diffs.Count} unterschiedliche Stellen gefunden.\n\n Die Änderungen habe ich dir in die Datei 'differences.html' in dein Verzeichnis gelegt.");
            PromptDialog.Choice(context, Unterschied3, new string[] { "ja", "nein" }, "Willst du die Datei gleich öffnen?", promptStyle: PromptStyle.PerLine, descriptions: new string[] { "Ja, bitte im Browser öffnen", "Nein zurück zur Auswahl" });
        }
        private async Task Unterschied3(IDialogContext context, IAwaitable<string> argument)
        {
            var result = await argument;

            if (result == "ja")
            {
                //open link
                System.Diagnostics.Process.Start(diffFile);
            }

            context.Done("Returning from DiffDialog");
        }
    }
}