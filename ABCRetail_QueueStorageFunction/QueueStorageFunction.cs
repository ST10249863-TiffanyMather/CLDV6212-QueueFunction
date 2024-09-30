using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Azure.Data.Tables;
using Azure;

namespace ABCRetail_Part1.Functions
{
    public class SendMessageFunction
    {
        private readonly ILogger<SendMessageFunction> _logger;

        public SendMessageFunction(ILogger<SendMessageFunction> logger)
        {
            _logger = logger;
        }

        //****************
        //Code Attribution
        //The following coode was taken from StackOverflow:
        //Author: Craig Harris
        //Link: https://stackoverflow.com/questions/77146103/connect-azure-storage-queue-to-azure-function-as-trigger
        //****************

        [Function("SendMessage")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("Processing message to send.");

            //read and deserialize request body to get transaction details
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Transaction transaction = JsonConvert.DeserializeObject<Transaction>(requestBody);

            if (transaction == null)
            {
                _logger.LogError("Invalid request payload. Transaction object could not be deserialized.");
                return new BadRequestObjectResult("Invalid request payload.");
            }

            //message for the queue
            string message = $"New transaction by User {transaction.UserId} of Product {transaction.ProductId} on {transaction.TransactionDate}";

            //create QueueServiceClient and QueueClient
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Connection string for AzureWebJobsStorage is not configured.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            QueueServiceClient queueServiceClient = new QueueServiceClient(connectionString);
            QueueClient queueClient = queueServiceClient.GetQueueClient("transactions");

            await queueClient.CreateIfNotExistsAsync();
            if (!queueClient.Exists())
            {
                _logger.LogError("Queue does not exist.");
                return new BadRequestObjectResult("Queue not found.");
            }

            try
            {
                //send message to queue
                await queueClient.SendMessageAsync(message);
                _logger.LogInformation("Message successfully sent to the queue.");
                return new OkObjectResult($"Message sent: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to the queue.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    public class Transaction : ITableEntity
    {
        [Key]
        public int TransactionId { get; set; }
        public string? PartitionKey { get; set; }
        public string? RowKey { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        [Required(ErrorMessage = "User is required.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Product ID is required.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Transaction date is required.")]
        public DateTime TransactionDate { get; set; }
        public double? TransactionTotalPrice { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        public string? TransactionPaymentMethod { get; set; }

        public string? TransactionStatus { get; set; }
    }
}
