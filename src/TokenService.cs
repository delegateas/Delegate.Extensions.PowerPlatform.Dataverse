using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Cds.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNetDevOps.Extensions.PowerPlatform.DataVerse
{
    public class TokenService
    {
        private IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private IConfidentialClientApplication app = null;

        public TokenService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }


        public async Task<string> GetTokenAsync(string arg)
        {
            if (app == default)
            {
                var rsp = await httpClientFactory.CreateClient().GetAsync(
                    $"{new Uri(configuration.GetValue<string>("CDSEnvironment")).GetLeftPart(UriPartial.Authority)}/api/data/v9.1/accounts");
                var auth = rsp.Headers.GetValues("www-authenticate").FirstOrDefault();
                var tenant = auth.Substring("Bearer ".Length).Split(',')
                    .Select(k => k.Trim().Split('='))
                    .ToDictionary(k => k[0], v => v[1]);


                var tenantId = new Uri(tenant["authorization_uri"]).AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();


                app = ConfidentialClientApplicationBuilder.Create(configuration.GetValue<string>("CDSClientId"))
                    .WithTenantId(tenantId)
                    .WithClientSecret(configuration.GetValue<string>("CDSClientSecret"))
                    .Build();
            }

            var token = await app.AcquireTokenForClient(new[]
                {
                    new Uri(configuration.GetValue<string>("CDSEnvironment")).GetLeftPart(UriPartial.Authority)
                        .TrimEnd('/') + "//.default"
                })
                .ExecuteAsync();
            return token.AccessToken;
        }
    }
}
