// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

namespace AsyncTickets.Models.ConcertContext
{
    public class DeleteResult : UpdateResult
    {
        public static new DeleteResult SuccessResult()
        {
            return new DeleteResult
            {
                Success = true,
            };
        }
    }
}
