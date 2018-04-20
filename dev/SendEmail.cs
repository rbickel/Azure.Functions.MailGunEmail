using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Apollo.Functions.MailGunEmail
{
    [StorageAccount("EMAILQUEUE_CONNECTIONSTRING")]
    public static class SendEmail
    {
        private static readonly string ApiKey = Environment.GetEnvironmentVariable("MailGun:ApiKey");
        private static readonly string Host = Environment.GetEnvironmentVariable("MailGun:ApiHost");

        [FunctionName("SendEmail")]
        [return: Table("emailouthistory")]
        public static async Task<EmailResult> Run(
            [QueueTrigger("email-out-queue")]Email queueItem,
            TraceWriter log)
        {
            var message = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("from", queueItem.Sender),
                new KeyValuePair<string, string>("to", queueItem.Recipient),
                new KeyValuePair<string, string>("subject", queueItem.Subject),
                new KeyValuePair<string, string>("html", queueItem.Message),
            });
            var statusCode = 0;
            using (var client = new HttpClient())
            {
                var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "api", ApiKey)));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);

                log.Info($"Sending email");
                var response = await client.PostAsync($"{Host}/messages", message);
                statusCode = (int)response.StatusCode;
                log.Info($"Email sent {response.StatusCode}");
                var content = await response.Content?.ReadAsStringAsync() ?? "";
                log.Info($"Response: {content}");
            }

            return new EmailResult { PartitionKey = nameof(EmailResult), RowKey = Guid.NewGuid().ToString(), StatusCode = statusCode, Success = statusCode == 200, Message = queueItem.Message, Sender = queueItem.Sender, Recipient = queueItem.Recipient, Subject = queueItem.Subject};
        }

        [FunctionName("SendEmailPoison")]
        [return: Table("emailouthistory")]
        public static EmailResult RunPoison([QueueTrigger("email-out-queue-poison")]string queueItem, TraceWriter log)
        {
            log.Error($"Error with queue message: {queueItem}");
            return new EmailResult { PartitionKey = nameof(EmailResult), RowKey = Guid.NewGuid().ToString(), Success = false, Message = queueItem, StatusCode = -1};
        }


        public class EmailResult : TableEntity
        {
            public string Sender { get; set; }
            public string Recipient { get; set; }
            public string Message { get; set; }
            public bool Success { get; set; }
            public int StatusCode { get; set; }
            public string Subject { get; set; }
        }

        public class Email
        {
            [JsonProperty("sender")]
            public string Sender { get; set; }

            [JsonProperty("recipient")]
            public string Recipient { get; set; }

            [JsonProperty("subject")]
            public string Subject { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("content_type")]
            public string ContentType { get; set; }
        }

    }
}