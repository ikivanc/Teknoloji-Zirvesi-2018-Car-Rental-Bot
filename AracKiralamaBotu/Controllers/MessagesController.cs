using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.Dialogs;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using AracKiralama.Model;
using System.Collections.Generic;

namespace AracKiralama
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {

        static readonly string APIKEY = "YOUR_TRANSLATOR_KEY";
        static readonly string TRANSLATETO = "en";
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                var input = activity.Text;

                var imageAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
                if (imageAttachment != null)
                {
                    Task.Run(async () =>
                    {
                        var sorguDetayi = await this.GetCaptionAsync(activity, connector);
                        var aracbilgisi = String.Join(" ", sorguDetayi.ToArray());
                        activity.Text = $"Show {aracbilgisi} options";
                        await Conversation.SendAsync(activity, () => new RootLuisDialog());
                    }).Wait();
                }
                else
                {
                    Task.Run(async () =>
                    {
                        var accessToken = await GetAuthenticationToken(APIKEY);
                        var output = await Translate(input, TRANSLATETO, accessToken);
                        Console.WriteLine(output);
                        activity.Text = output;
                        await Conversation.SendAsync(activity, () => new RootLuisDialog());
                    }).Wait();
                }
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }


        static async Task<string> Translate(string textToTranslate, string language, string accessToken)
        {
            string url = "http://api.microsofttranslator.com/v2/Http.svc/Translate";
            string query = $"?text={System.Net.WebUtility.UrlEncode(textToTranslate)}&to={language}&contentType=text/plain";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url + query);
                var result = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return "ERROR: " + result;

                var translatedText = XElement.Parse(result).Value;
                return translatedText;
            }
        }

        static async Task<string> GetAuthenticationToken(string key)
        {
            string endpoint = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
                var response = await client.PostAsync(endpoint, null);
                var token = await response.Content.ReadAsStringAsync();
                return token;
            }
        }


        private static async Task<Stream> GetImageStream(ConnectorClient connector, string imageUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                //Eğer Fotoğraf skype üzerinden paylaşılacaksa bu kısım handle edecek.
                var uri = new Uri(imageUrl);
                if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                    return await client.GetStreamAsync(uri);
                }
                else
                {
                    var response = await client.GetAsync(imageUrl);
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content?.ReadAsStreamAsync();
                }
            }

   
            //using (var httpClient = new HttpClient())
            //{
            //    // The Skype attachment URLs are secured by JwtToken,
            //    // you should set the JwtToken of your bot as the authorization header for the GET request your bot initiates to fetch the image.
            //    // https://github.com/Microsoft/BotBuilder/issues/662
            //    var uri = new Uri(imageAttachment.ContentUrl);
            //    if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
            //    {
            //        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
            //        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            //    }

            //    return await httpClient.GetStreamAsync(uri);
            //}
        }

        private static async Task<string> GetTokenAsync(ConnectorClient connector)
        {
            var credentials = connector.Credentials as MicrosoftAppCredentials;
            if (credentials != null)
            {
                return await credentials.GetTokenAsync();
            }

            return null;
        }




        private async Task<List<string>> GetCaptionAsync(Activity activity, ConnectorClient connector)
        {
            var imageAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
            if (imageAttachment != null)
            {
                using (var stream = await GetImageStream(connector, imageAttachment.ContentUrl))
                {
                    return await MakeAnalysisWithImage(stream);
                }
            }

            string url;
            if (TryParseAnchorTag(activity.Text, out url))
            {
                //return await this.captionService.GetCaptionAsync(url);
            }

            if (Uri.IsWellFormedUriString(activity.Text, UriKind.Absolute))
            {
               
                return await MakeAnalysisWithURL(activity);

            }

            // If we reach here then the activity is neither an image attachment nor an image URL.
            throw new ArgumentException("The activity doesn't contain a valid image attachment or an image URL.");
        }


        private static bool TryParseAnchorTag(string text, out string url)
        {
            var regex = new Regex("^<a href=\"(?<href>[^\"]*)\">[^<]*</a>$", RegexOptions.IgnoreCase);
            url = regex.Matches(text).OfType<Match>().Select(m => m.Groups["href"].Value).FirstOrDefault();
            return url != null;
        }

        private static async Task<List<string>> MakeAnalysisWithURL(Activity activity)
        {
            List<string> result = new List<string>();
            var client = new HttpClient();

            // Request headers
            // Buraya prediction API Key'inizi girebilirsiniz.
            client.DefaultRequestHeaders.Add("Prediction-key", "YOUR_PREDICTION_KEY");


            //Buraya URL ile sorgu yapmayı sağlayan Prediction API endpoint'inizi ekleyin
            var uri = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/7fd5e693-b9ff-44db-879f-28e77fae08c8/url";
            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{\"Url\": \"" + activity.Text + "\"}");

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                //Console.WriteLine(await response.Content.ReadAsStringAsync());

                //JSON Deserialization
                string jsoncust = response.Content.ReadAsStringAsync().Result;
                CustomVision custobjectVision = JsonConvert.DeserializeObject<CustomVision>(jsoncust);

                //Search objesi için gerekli sonuçların alınması

                Console.WriteLine("---\n\nCustom Vision Detayları");
                foreach (Prediction p in custobjectVision.Predictions)
                {
                    Console.WriteLine(p.Tag + " - " + p.Probability);
                }

                result.AddRange(custobjectVision.Predictions.Where(p => (decimal)p.Probability > 0.6m).Select(a => a.Tag));

                return result;
            }
        }

        static async Task<List<string>> MakeAnalysisWithImage(Stream stream)
        {
            List<string> result = new List<string>();
            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Add("Prediction-Key", "YOUR_PREDICTION_KEY");

            // Assemble the URI for the REST API Call.
            var uri = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/7fd5e693-b9ff-44db-879f-28e77fae08c8/image";

            HttpResponseMessage response;

            BinaryReader binaryReader = new BinaryReader(stream);
            byte[] byteData = binaryReader.ReadBytes((int)stream.Length); 

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);
                string jsoncust = response.Content.ReadAsStringAsync().Result;
                CustomVision custobjectVision = JsonConvert.DeserializeObject<CustomVision>(jsoncust);

                result.AddRange(custobjectVision.Predictions.Where(p => (decimal)p.Probability > 0.6m).Select(a => a.Tag));

                return  result;
            }
        }
      


        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }

    }
}