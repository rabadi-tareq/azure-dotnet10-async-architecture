// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

namespace AsyncTickets.Web.CallCenter.Services;

public interface ITicketImageService
{
    Task<Stream> GetTicketImagesAsync(string imageName);
}