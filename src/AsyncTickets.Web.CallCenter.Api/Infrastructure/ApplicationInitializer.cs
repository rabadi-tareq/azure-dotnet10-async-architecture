// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Web.Api.Services;

namespace AsyncTickets.Web.Api.Infrastructure
{
    public class ApplicationInitializer
    {
        private readonly IConcertRepository concertContextService;

        public ApplicationInitializer(IConcertRepository concertContextService)
        {
            this.concertContextService = concertContextService;
        }

        public void Initialize()
        {
            // Initialize all resources at application startup.
            concertContextService.Initialize();
        }
    }
}
