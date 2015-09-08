using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.WindowsAzure.MobileServices.Files
{
    public class StorageTokenRequest
    {
        public StoragePermissions Permissions { get; set; }

        public MobileServiceFile TargetFile { get; set; }

        public string ScopedEntityId { get; set; }

        public string ProviderName { get; set; }
    }
}
