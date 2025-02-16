﻿using System;

namespace Merchello.Providers.Payment.Braintree.Models
{
    using Merchello.Providers.Models;
    using Merchello.Providers.Payment.Braintree;
    using Merchello.Providers.Payment.Models;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The BrainTree provider settings used to access the BrainTree Payment API.
    /// </summary>
    public class BraintreeProviderSettings : IPaymentProviderSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BraintreeProviderSettings"/> class.
        /// </summary>
        public BraintreeProviderSettings()
        {
            this.EnvironmentType = EnvironmentType.Sandbox;
            this.MerchantDescriptor = new MerchantDescriptor();
            this.DefaultTransactionOption = TransactionOption.SubmitForSettlement;
        }

        /// <summary>
        /// Gets or sets the environment.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public EnvironmentType EnvironmentType { get; set; }

        /// <summary>
        /// Gets or sets the public key.
        /// </summary>
        public string PublicKey { get; set; }

        /// <summary>
        /// Gets or sets the private key.
        /// </summary>
        public string PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the merchant id.
        /// </summary>
        public string MerchantId { get; set; }

        /// <summary>
        /// Gets or sets the merchant account ids (if any, put each currency on a new line with currency_code:merchant_account_id).
        /// </summary>
        public string MerchantAccountIds { get; set; }

        /// <summary>
        /// Gets or sets the merchant descriptor.
        /// </summary>
        public MerchantDescriptor MerchantDescriptor { get; set; }

        /// <summary>
        /// Gets or sets the default transaction option.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TransactionOption DefaultTransactionOption { get; set; }

        public string GetMerchantAccountIdForCurrency(string isoCurrencyCode)
        {
            if (string.IsNullOrWhiteSpace(isoCurrencyCode) || string.IsNullOrWhiteSpace(MerchantAccountIds)) return string.Empty;

            var currencyLines = MerchantAccountIds.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (var currencyLine in currencyLines)
            {
                if (string.IsNullOrWhiteSpace(currencyLine) || !currencyLine.Contains(":")) continue;

                var currencyData = currencyLine.Split(':');
                if (currencyData[0].ToUpper() == isoCurrencyCode.ToUpper())
                {
                    return currencyData[1];
                }
            }

            return string.Empty;
        }
    }
}