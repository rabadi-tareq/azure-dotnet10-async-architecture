// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Web.Api.Services.PaymentGatewayService;

namespace AsyncTickets.Web.Models.Services
{
    public interface IPaymentGatewayService
    {
        Task<PreAuthPaymentResult> PreAuthPaymentAsync(PreAuthPaymentRequest request);
        Task<CapturePaymentResult> CapturePaymentAsync(CapturePaymentRequest request);
    }
}
