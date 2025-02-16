﻿namespace Merchello.Providers.Payment.Braintree.Provider
{
    using System;
    using Merchello.Core;
    using Merchello.Core.Gateways.Payment;
    using Merchello.Core.Models;
    using Merchello.Core.Services;
    using Merchello.Providers.Payment.Braintree.Models;
    using Merchello.Providers.Payment.Braintree.Services;
    using Merchello.Providers.Payment.Exceptions;

    using Umbraco.Core;
    using Umbraco.Core.Logging;

    using Constants = Merchello.Providers.Constants;

    /// <summary>
    /// A base class for Braintree standard (one time) transactions.
    /// </summary>
    public abstract class BraintreeStandardPaymentGatewayBase : BraintreePaymentGatewayMethodBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BraintreeStandardPaymentGatewayBase"/> class.
        /// </summary>
        /// <param name="gatewayProviderService">
        /// The gateway provider service.
        /// </param>
        /// <param name="paymentMethod">
        /// The payment method.
        /// </param>
        /// <param name="braintreeApiService">
        /// The braintree api service.
        /// </param>
        protected BraintreeStandardPaymentGatewayBase(IGatewayProviderService gatewayProviderService, IPaymentMethod paymentMethod, IBraintreeApiService braintreeApiService)
            : base(gatewayProviderService, paymentMethod, braintreeApiService)
        {
        }

        /// <summary>
        /// Does the actual work of authorizing the payment
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        protected override IPaymentResult PerformAuthorizePayment(IInvoice invoice, ProcessorArgumentCollection args)
        {
            var authorizeAmount = invoice.Total;
            if (args.ContainsKey("authorizePaymentAmount")) authorizeAmount = Convert.ToDecimal(args["authorizePaymentAmount"]);

            var merchantAccountId = args.ContainsKey("merchantAccountId")
                ? args["merchantAccountId"]
                : this.BraintreeApiService.BraintreeProviderSettings.GetMerchantAccountIdForCurrency(invoice.CurrencyCode);

            var paymentMethodNonce = args.GetPaymentMethodNonce();

            if (string.IsNullOrEmpty(paymentMethodNonce))
            {
                var error = new InvalidOperationException("No payment method nonce was found in the ProcessorArgumentCollection");
                LogHelper.Debug<BraintreeStandardTransactionPaymentGatewayMethod>(error.Message);
                return new PaymentResult(Attempt<IPayment>.Fail(error), invoice, false);
            }

            var attempt = this.ProcessPayment(invoice, TransactionOption.Authorize, authorizeAmount, paymentMethodNonce, "", merchantAccountId);

            var payment = attempt.Payment.Result;

            this.GatewayProviderService.Save(payment);

            if (!attempt.Payment.Success)
            {
                this.GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Denied, attempt.Payment.Exception.Message, 0);
            }
            else
            {
                this.GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Debit, "To show record of Braintree Authorization", 0);
            }

            return attempt;
        }

        /// <summary>
        /// Performs the actual work of authorizing and capturing a payment.  
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="amount">
        /// The amount.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        /// <remarks>
        /// This is a transaction with SubmitForSettlement = true
        /// </remarks>
        protected override IPaymentResult PerformAuthorizeCapturePayment(IInvoice invoice, decimal amount, ProcessorArgumentCollection args)
        {
            var paymentMethodNonce = args.GetPaymentMethodNonce();           

            if (string.IsNullOrEmpty(paymentMethodNonce))
            {
                var error = new InvalidOperationException("No payment method nonce was found in the ProcessorArgumentCollection");
                LogHelper.Debug<BraintreeStandardTransactionPaymentGatewayMethod>(error.Message);
                return new PaymentResult(Attempt<IPayment>.Fail(error), invoice, false);
            }

            // TODO this is a total last minute hack
            var email = string.Empty;
            if (args.ContainsKey("customerEmail")) email = args["customerEmail"];

            var merchantAccountId = args.ContainsKey("merchantAccountId")
                ? args["merchantAccountId"]
                : this.BraintreeApiService.BraintreeProviderSettings.GetMerchantAccountIdForCurrency(invoice.CurrencyCode);

            var attempt = this.ProcessPayment(invoice, TransactionOption.SubmitForSettlement, amount, paymentMethodNonce, email, merchantAccountId);

            var payment = attempt.Payment.Result;

            this.GatewayProviderService.Save(payment);

            if (!attempt.Payment.Success)
            {
                this.GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Denied, attempt.Payment.Exception.Message, 0);
            }
            else
            {
                this.GatewayProviderService.ApplyPaymentToInvoice(payment.Key, invoice.Key, AppliedPaymentType.Debit, "Braintree PayPal one time transaction - authorized and captured", amount);
            }

            return attempt;
        }

        /// <summary>
        /// Processes a payment against the Braintree API using the BraintreeApiService.
        /// </summary>
        /// <param name="invoice">
        /// The invoice.
        /// </param>
        /// <param name="option">
        /// The option.
        /// </param>
        /// <param name="amount">
        /// The amount.
        /// </param>
        /// <param name="token">
        /// The payment method nonce.
        /// </param>
        /// <param name="email">
        /// The email.
        /// </param>
        /// <param name="merchantAccountId"></param>
        /// <returns>
        /// The <see cref="IPaymentResult"/>.
        /// </returns>
        /// <remarks>
        /// This converts the <see cref="Result{T}"/> into Merchello <see cref="IPaymentResult"/>
        /// </remarks>
        protected override IPaymentResult ProcessPayment(IInvoice invoice, TransactionOption option, decimal amount, string token, string email = "", string merchantAccountId = "")
        {
            var payment = this.GatewayProviderService.CreatePayment(PaymentMethodType.CreditCard, amount, this.PaymentMethod.Key);

            payment.CustomerKey = invoice.CustomerKey;
            payment.Authorized = false;
            payment.Collected = false;
            payment.PaymentMethodName = "Braintree PayPal One Time Transaction";
            payment.ExtendedData.SetValue(Constants.Braintree.ProcessorArguments.PaymentMethodNonce, token);

            var result = this.BraintreeApiService.Transaction.Sale(invoice, amount, token, option: option, email: email, merchantAccountId: merchantAccountId);

            if (result.IsSuccess())
            {
                payment.ExtendedData.SetBraintreeTransaction(result.Target);

                // AVS and CVV data
                payment.ExtendedData.SetAvsCvvData(result.Target);

                // Set the transaction ID as an extended data item
                payment.ExtendedData.SetValue(Core.Constants.ExtendedDataKeys.TransactionId, result.Target.Id);

                if (option == TransactionOption.Authorize) payment.Authorized = true;
                if (option == TransactionOption.SubmitForSettlement)
                {
                    payment.Authorized = true;
                    payment.Collected = true;
                }


                return new PaymentResult(Attempt<IPayment>.Succeed(payment), invoice, true);
            }

            var error = new BraintreeApiException(result.Errors, result.Message);

            return new PaymentResult(Attempt<IPayment>.Fail(payment, error), invoice, false);
        }


    }
}