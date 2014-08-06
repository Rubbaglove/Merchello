﻿namespace Merchello.Core.Gateways.Taxation
{
    using Merchello.Core.Models;

    /// <summary>
    /// Represents the abstract GatewayTaxMethod
    /// </summary>
    public abstract class TaxationGatewayMethodBase : ITaxationGatewayMethod
    {
        /// <summary>
        /// The tax method.
        /// </summary>
        private readonly ITaxMethod _taxMethod;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaxationGatewayMethodBase"/> class.
        /// </summary>
        /// <param name="taxMethod">
        /// The tax method.
        /// </param>
        /// <param name="estimateOnly">
        /// The estimate Only.
        /// </param>
        protected TaxationGatewayMethodBase(ITaxMethod taxMethod, bool estimateOnly = false)
        {
            Mandate.ParameterNotNull(taxMethod, "taxMethod");

            _taxMethod = taxMethod;
        }

        /// <summary>
        /// Gets the <see cref="ITaxMethod"/>
        /// </summary>
        public ITaxMethod TaxMethod
        {
            get { return _taxMethod; }
        }

        /// <summary>
        /// Calculates the tax amount for an invoice
        /// </summary>
        /// <param name="invoice">The <see cref="IInvoice"/></param>
        /// <param name="estimateOnly">
        /// An optional parameter indicating that the tax calculation should be an estimate.
        /// This is useful for some 3rd party tax APIs
        /// </param>
        /// <returns>The <see cref="ITaxCalculationResult"/></returns>
        /// <remarks>
        /// 
        /// Assumes the billing address of the invoice will be used for the taxation address
        /// 
        /// </remarks>
        public virtual ITaxCalculationResult CalculateTaxForInvoice(IInvoice invoice, bool estimateOnly = false)
        {
            return CalculateTaxForInvoice(invoice, invoice.GetBillingAddress());
        }

        /// <summary>
        /// Calculates the tax amount for an invoice
        /// </summary>
        /// <param name="invoice">The <see cref="IInvoice"/></param>
        /// <param name="taxAddress">The <see cref="IAddress"/> to base taxation rates.  Either origin or destination address.</param>
        /// <param name="estimateOnly">
        /// An optional parameter indicating that the tax calculation should be an estimate.
        /// This is useful for some 3rd party tax APIs
        /// </param>
        /// <returns><see cref="ITaxCalculationResult"/></returns>
        public abstract ITaxCalculationResult CalculateTaxForInvoice(IInvoice invoice, IAddress taxAddress, bool estimateOnly = false);

        /// <summary>
        /// Calculates the tax amount for an invoice
        /// </summary>
        /// <param name="strategy">The strategy to use when calculating the tax amount</param>
        /// <param name="estimateOnly">
        /// An optional parameter indicating that the tax calculation should be an estimate.
        /// This is useful for some 3rd party tax APIs
        /// </param>
        /// <returns><see cref="ITaxCalculationResult"/></returns>
        public virtual ITaxCalculationResult CalculateTaxForInvoice(ITaxCalculationStrategy strategy, bool estimateOnly = false)
        {
            var attempt = strategy.CalculateTaxesForInvoice(estimateOnly);

            if (!attempt.Success) throw attempt.Exception;

            return attempt.Result;
        }
    }
}