// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using AsyncTickets.Models.ConcertContext;
using AsyncTickets.Models.Services;

namespace AsyncTickets.Web.Api.Services
{
    public interface IConcertRepository : IConcertContextService
    {
        public void Initialize();
        Task<CreateResult> CreateCustomerAsync(Customer newCustomer);
        Task<Customer?> GetCustomerByEmailAsync(string email);
    }
}
