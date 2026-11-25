// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.Services;

namespace AsyncTickets.Web.Api.Services.PaymentGatewayService
{
    public class CapturePaymentResult : IServiceProviderResult
    {
        public CapturePaymentResultStatus Status { get; set; }
        public string ConfirmationNumber { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
