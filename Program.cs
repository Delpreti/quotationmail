using FluentEmail.Core;
using FluentEmail.Smtp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ConsoleApp1
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        private static async Task<float> GetCurrentQuotation(string acao, string APIkey)
        {
            // requisicao para a API
            var req = new HttpRequestMessage();
            req.Method = HttpMethod.Get;
            req.RequestUri = new Uri("https://api.hgbrasil.com/finance/stock_price?key=" + APIkey + "&symbol=" + acao);
            var response = await client.SendAsync(req);

            // Parse do JSON de resposta
            var responseContent = await response.Content.ReadAsStringAsync();
            JObject jsonResponse = JObject.Parse(responseContent);
            JObject responseActions = jsonResponse.Value<JObject>("results");
            JObject responseInfo = responseActions.Value<JObject>(acao);
            float quotation = responseInfo.Value<float>("price");

            return quotation;
        }

        private static SmtpConfig GetConfiguration(string filepath) // factory method
        {
            // Buscar as configuracoes a partir do arquivo JSON
            JObject jsonConfig = JObject.Parse(File.ReadAllText(filepath));
            return new SmtpConfig() { 
                Host = jsonConfig.Value<string>("host"),
                EnableSSL = jsonConfig.Value<string>("enablessl") == "true",
                HostMail = jsonConfig.Value<string>("hostMail"),
                ReceiverMail = jsonConfig.Value<string>("recieverMail"),
                APIkey = jsonConfig.Value<string>("APIkey"),
            };
        }

        static async Task Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Missing arguments");
                return;
            }
            string actionName = args[0];
            float lowerBound = float.Parse(args[1], CultureInfo.InvariantCulture.NumberFormat);
            float upperBound = float.Parse(args[2], CultureInfo.InvariantCulture.NumberFormat);

            var smtpConfig = GetConfiguration(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName, "MailConfig.json"));

            var sender = new SmtpSender(() => new SmtpClient(smtpConfig.Host)
            {
                EnableSsl = smtpConfig.EnableSSL,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Port = 25,
            });
            Email.DefaultSender = sender;

            float quotation;
            
            while (true)
            {
                // pegar info da API
                try
                {
                    quotation = await GetCurrentQuotation(actionName, smtpConfig.APIkey);
                }
                catch(Exception ex) {
                    Console.Write(ex.Message);
                    quotation = (lowerBound + upperBound) / 2;
                }

                // enviar email dependendo do resultado
                if (quotation < lowerBound)
                {
                    var email = await Email
                        .From(smtpConfig.HostMail)
                        .To(smtpConfig.ReceiverMail)
                        .Subject("comprar")
                        .Body("mensagemCompra")
                        .SendAsync();
                }
                if (quotation > upperBound)
                {
                    var email = await Email
                        .From(smtpConfig.HostMail)
                        .To(smtpConfig.ReceiverMail)
                        .Subject("vender")
                        .Body("mensagemVenda")
                        .SendAsync();
                }

                // Aguarda 5s para requisitar novamente/reenviar email
                await Task.Delay(5000);
            }
        }
    }
}
