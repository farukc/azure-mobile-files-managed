﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Files.Operations
{
    public interface IFileOperationQueue
    {
        int Count { get; }

        Task<IMobileServiceFileOperation> PeekAsync();

        Task<IMobileServiceFileOperation> DequeueAsync();

        Task RemoveAsync(string fileId);

        Task<IMobileServiceFileOperation> GetOperationByFileIdAsync(string fileId);

        Task EnqueueAsync(IMobileServiceFileOperation operation);
    }
}
