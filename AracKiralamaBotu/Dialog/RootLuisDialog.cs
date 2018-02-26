namespace AracKiralama
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Builder.FormFlow;

    [LuisModel("<YOUR_LUIS_APP_ID>", "YOUR_SUBSCRIPTION_KEY")]
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {
        private const string AudiOption = "Audi";
        private const string VWOption = "VW";

        bool isAudiDetected = false;
        bool isVWDetected = false;

        EntityRecommendation modelEntityRecommendation;
        EntityRecommendation brandEntityRecommendation;

        RentalCar selectedCar = new RentalCar();

        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Üzgünüm, demek istediğini anlayamadım '{result.Query}'. 'yardım' yazarak detaylı bilgi alabilirsin.";

            await context.PostAsync(message);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Selam! 'Bana audi modelleri getir', 'Bana jetta 2.0 tdi araçları göster', 'Audi A4 ara' gibi sorular sorabilirsiniz ya da beğendiğiniz bir aracın fotoğrafını yükleyebilirsiniz.");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Greetings")]
        public async Task Greetings(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Selam! Ben araç kiralama botu 🤖, bana istediğin soruyu sorabilirsin 😊");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("SearchCar")]
        public async Task SearchCar(IDialogContext context, LuisResult result)
         {
            if (result.Entities.Count == 0)
            {
                this.ShowOptions(context);
            }
            else if (result.TryFindEntity("CarBrand::CarModel", out modelEntityRecommendation) && result.TryFindEntity("CarBrand", out brandEntityRecommendation))
            {
                selectedCar.AracMarka = brandEntityRecommendation.Entity;
                selectedCar.AracModeli = modelEntityRecommendation.Entity;

                await context.PostAsync($"Fotoğraftaki araç markası: {selectedCar.AracMarka}, modeli: {selectedCar.AracModeli}");
                
            }
            else if (result.TryFindEntity("CarBrand::CarModel", out modelEntityRecommendation))
            {
                selectedCar.AracModeli = modelEntityRecommendation.Entity;
            }
            else if (result.TryFindEntity("CarBrand", out brandEntityRecommendation))
            {
                selectedCar.AracMarka = brandEntityRecommendation.Entity;
            }
                   

            if ((selectedCar.AracMarka == "audi") || (selectedCar.AracModeli == "a3"))
            {
                isAudiDetected = true;
                await context.PostAsync("Şu anda Audi marka bir araç arıyorsunuz");
                //context.Wait(this.MessageReceived);
            }
            else if ((selectedCar.AracMarka == "vw") || (selectedCar.AracModeli == "jetta"))
            {
                isVWDetected = true;
                await context.PostAsync("Şu anda VW marka bir araç arıyorsunuz");

                //context.Wait(this.MessageReceived);                    
            }            
            else if (result.Entities.Count > 0 && selectedCar.AracModeli != "audi" && selectedCar.AracModeli != "vw")
            {
                this.ShowOptions(context);
            }


            if (isAudiDetected)
             {
       
                PromptDialog.Choice(context, this.OnCreditCardSelected, new List<string>() { "6 Ay", "12 Ay", "24 Ay", "36 Ay" }, "Kaç aylık kredi düşünüyorsunuz?", "Lütfen var olan kredi seçeneklerini seçiniz", 3);

                isAudiDetected = !isAudiDetected;

             }
             else if (isVWDetected)
             {
                  PromptDialog.Choice(context, this.OnCreditCardSelected, new List<string>() { "6 Ay", "12 Ay", "24 Ay", "36 Ay" }, "Kaç aylık kredi düşünüyorsunuz?", "Lütfen var olan kredi seçeneklerini seçiniz", 3);

                  isVWDetected = !isVWDetected;
             }             
            }

        private void ShowOptions(IDialogContext context)
        {
            PromptDialog.Choice(context, this.OnOptionSelected, new List<string>() { AudiOption, VWOption }, "Hangi marka aracla ilgileniyorsunuz?", "Lütfen var olan markaları seçiniz", 3);
        }

        private async Task OnOptionSelected(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string optionSelected = await result;

                switch (optionSelected)
                {
                    case AudiOption:
                        PromptDialog.Choice(context, this.OnModelSelected, new List<string>() { "A3", "A4", "A5", "A6" }, "Hangi Audi modeli ile ilgileniyorsunuz?", "Lütfen var olan modelleri seçiniz", 3);
                        isAudiDetected = !isAudiDetected;

                        break;

                    case VWOption:
                        context.Call(new VWDialog(), this.ResumeAfterOptionDialog);
                        break;
                }

            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

                context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task OnModelSelected(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string optionSelected = await result;

                selectedCar.AracModeli = optionSelected;

                PromptDialog.Choice(context, this.OnCreditCardSelected, new List<string>() { "6 Ay", "12 Ay", "24 Ay", "36 Ay" }, "Kaç aylık kredi düşünüyorsunuz?", "Lütfen var olan kredi seçeneklerini seçiniz", 3);

            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

                context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task OnCreditCardSelected(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string optionSelected = await result;
                selectedCar.CreditMonth = optionSelected;

                PromptDialog.Choice(
                                   context: context,
                                   resume: ResumeAfterHGSDialog,
                                   prompt: "Lütfen Seçiniz, HGS mi OGS mi?",
                                   retry: "Sorry, I didn't understand that. Please try again.",
                                   options: new List<string>() { "HGS", "OGS" }
                               );

            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

                context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task ResumeAfterOptionDialog(IDialogContext context, IAwaitable<object> result)
        {
            try
            {
                var message = await result;

                PromptDialog.Choice(
                    context: context,
                    resume: ResumeAfterHGSDialog,
                    prompt: "Lütfen Seçiniz, HGS mi OGS mi?",
                    retry: "Sorry, I didn't understand that. Please try again.",
                    options: new List<string>() { "HGS", "OGS"}
                );

            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
            finally
            {
               // context.Wait(this.MessageReceivedAsync);
            }
        }
        private async Task ResumeAfterHGSDialog(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string optionselected = await result;
                selectedCar.HGSveyaOGS = optionselected;

                PromptDialog.Choice(
                    context: context,
                    resume: ResumeCurrencyDialog,
                    prompt: "Lütfen Kur seçiniz",
                    retry: "Sorry, I didn't understand that. Please try again.",
                    options: new List<string>() { "TL", "USD", "EURO" }
                );

            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
            finally
            {
              //  context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task ResumeCurrencyDialog(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string optionselected = await result;
                selectedCar.OdemeKuru = optionselected;

                PromptDialog.Choice(
                    context: context,
                    resume: ResumeAfterMileAgeDialog,
                    prompt: "Yıllık Kaç KM olsun?",
                    retry: "Sorry, I didn't understand that. Please try again.",
                    options: new List<string>() {"0 KM", "1000-10000 KM", "10000-100000 KM" }
                );
            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
            finally
            {
                //context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task ResumeAfterMileAgeDialog(IDialogContext context, IAwaitable<string> result)
        {
            try
            {

                var message = await result;
                string optionselected = await result;
                selectedCar.TotalKMs = optionselected;

                var audis = await this.GetAudisAsync(selectedCar.AracMarka, selectedCar.AracModeli);

                await context.PostAsync($"araç bulundu");

                var resultMessage = context.MakeMessage();
                resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                resultMessage.Attachments = new List<Attachment>();

                foreach (var arac in audis)
                {
                    HeroCard heroCard = new HeroCard()
                    {
                        Title = arac.Name,
                        Subtitle = $"{arac.PriceStarting}.000₺. {arac.NumberOfReviews} kişi yorum yazdı",
                        Images = new List<CardImage>()
                        {
                            new CardImage() { Url = arac.Image }
                        },
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = "Detaylı bilgi",
                                Type = ActionTypes.OpenUrl,
                                Value = "https://www.audi.com.tr/tr/web/tr/icerik/iframe/fiyat-listesi.html"
                                //Value = $"https://www.bing.com/search?q= {selectedCar.AracMarka} {selectedCar.AracModeli }"
                            }
                        }
                    };

                    resultMessage.Attachments.Add(heroCard.ToAttachment());
                }

                await context.PostAsync(resultMessage);

            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
            finally
            {
               // context.Wait(this.MessageReceivedAsync);
            }
        }

        private async Task<IEnumerable<Audi>> GetAudisAsync(string AracMarka, string AracModel)
        {
            var audis = new List<Audi>();
            var random = new Random();

            //Burası uygulama BackEnd'inde dinamik doldurulacak
               audis.Add(new Audi()
                {
                    Name = $"A3 Sedan 1.0 ",
                    Location = "Turbo FSI 116 hp Dynamic S tronic PI.",
                    Rating = 5,
                    NumberOfReviews = random.Next(70, 150),
                    PriceStarting = 171,
                    Image = "https://www.dogusoto.com.tr/Dosyalar/Model/Audi/Tum%20renkler(370x250)/a3%20sedan/metali̇k-glaci̇er-beyazi.png"                  
                });
                audis.Add(new Audi()
                {
                    Name = $"A3 Sedan 1.6 ",
                    Location = "TDI 116 hp Dynamic S tronic PI",
                    Rating = 5,
                    NumberOfReviews = random.Next(70, 150),
                    PriceStarting = 187,
                    Image = "https://mediaservice.audi.com/media/live/50680/n5c01/8vmbdg-1/2018/14+y1y1/aaue0a/ata1x0/ausa9d/awv6k0/bav1ze/bbo6fa/dar3s0/dei3fb/eph7x2/fsp5l0/gra8t2/hes5j0/hsw8ih/kark8s/kasqk0/lia8g0/pamgp1/radc1q/sbr8sa/spu7y0/ssh4kc/stf2jb/szu0na/tyz2z1/zie4zb.jpeg"
                });
                audis.Add(new Audi()
                {
                    Name = $"A3 Sedan COD 1.5",
                    Location = "Turbo FSI 150 hp Dynamic S tronic PI",
                    Rating = 5,
                    NumberOfReviews = random.Next(70, 150),
                    PriceStarting = 188,
                    Image = "https://www.dogusoto.com.tr/Dosyalar/Model/Audi/Tum%20renkler(370x250)/a3%20sedan/metali̇k-glaci̇er-beyazi.png"
                });
                audis.Add(new Audi()
                {
                    Name = $"A3 Sedan 1.6",
                    Location = "TDI 116 hp Sport S tronic PI",
                    Rating = 5,
                    NumberOfReviews = random.Next(70, 150),
                    PriceStarting = 188,
                    Image = "https://www.dogusoto.com.tr/Dosyalar/Model/Audi/Tum%20renkler(370x250)/a3%20sedan/metali̇k-glaci̇er-beyazi.png"
                });
                audis.Add(new Audi()
                {
                    Name = $"A3 Sedan COD 1.5",
                    Location = "Turbo FSI 150 hp Design S tronic PI",
                    Rating = 5,
                    NumberOfReviews = random.Next(70, 150),
                    PriceStarting = 195,
                    Image = "https://www.dogusoto.com.tr/Dosyalar/Model/Audi/Tum%20renkler(370x250)/a3%20sedan/metali̇k-glaci̇er-beyazi.png"
                });        


            audis.Sort((h1, h2) => h1.PriceStarting.CompareTo(h2.PriceStarting));

            return audis;
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;

            if (message.Text.ToLower().Contains("help") || message.Text.ToLower().Contains("support") || message.Text.ToLower().Contains("problem"))
            {
                // await context.Forward(new SupportDialog(), this.ResumeAfterSupportDialog, message, CancellationToken.None);
            }
            else
            {
                this.ShowOptions(context);
            }
        }

    }
}
