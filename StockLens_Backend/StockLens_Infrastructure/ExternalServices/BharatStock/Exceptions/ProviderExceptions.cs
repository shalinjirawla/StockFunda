using System;

namespace StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions
{
    public class ProviderApiException : Exception
    {
        public int? StatusCode { get; }

        public ProviderApiException(string message, int? statusCode = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }

    public class ProviderNotFoundException : ProviderApiException
    {
        public string Ticker { get; }

        public ProviderNotFoundException(string ticker, string message = "Shareholding data was not found for the requested stock ticker.")
            : base(message, 404)
        {
            Ticker = ticker;
        }
    }

    public class ProviderRateLimitException : ProviderApiException
    {
        public TimeSpan? RetryAfter { get; }

        public ProviderRateLimitException(string message = "Rate limit exceeded on external shareholding API.", TimeSpan? retryAfter = null)
            : base(message, 429)
        {
            RetryAfter = retryAfter;
        }
    }
}
