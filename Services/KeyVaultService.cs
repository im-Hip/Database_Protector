using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace QLBenhVien.Services
{
    public class KeyVaultService
    {
        private readonly SecretClient _client;

        public KeyVaultService(IConfiguration config)
        {
            var options = new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeAzureDeveloperCliCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeAzureCliCredential = false
            };

            var credential = new DefaultAzureCredential(options);
            var vaultUri = new Uri(config["KeyVault:VaultUrl"]);
            _client = new SecretClient(vaultUri, credential);
        }

        public async Task<string> GetSecretAsync(string name)
        {
            var secret = await _client.GetSecretAsync(name);
            return secret.Value.Value;
        }

        public async Task SetSecretAsync(string name, string value)
        {
            await _client.SetSecretAsync(name, value);
        }
    }
}