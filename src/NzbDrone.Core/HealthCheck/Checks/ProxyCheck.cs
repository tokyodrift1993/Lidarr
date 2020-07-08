using System;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ConfigSavedEvent))]
    public class ProxyCheck : HealthCheckBase
    {
        private readonly Logger _logger;
        private readonly IConfigService _configService;
        private readonly IHttpClient _client;

        private readonly IHttpRequestBuilderFactory _cloudRequestBuilder;

        public ProxyCheck(ILidarrCloudRequestBuilder cloudRequestBuilder, IConfigService configService, IHttpClient client, ILocalizationService localizationService, Logger logger)
            : base(localizationService)
        {
            _configService = configService;
            _client = client;
            _logger = logger;

            _cloudRequestBuilder = cloudRequestBuilder.Services;
        }

        public override HealthCheck Check()
        {
            if (_configService.ProxyEnabled)
            {
                var addresses = Dns.GetHostAddresses(_configService.ProxyHostname);
                if (!addresses.Any())
                {
                    return new HealthCheck(GetType(), HealthCheckResult.Error, string.Format(_localizationService.GetLocalizedString("ProxyCheckResolveIpMessage"), _configService.ProxyHostname), "#proxy-failed-resolve-ip");
                }

                var request = _cloudRequestBuilder.Create()
                                                  .Resource("/ping")
                                                  .Build();

                try
                {
                    var response = _client.Execute(request);

                    // We only care about 400 responses, other error codes can be ignored
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        _logger.Error("Proxy Health Check failed: {0}", response.StatusCode);
                        return new HealthCheck(GetType(), HealthCheckResult.Error, string.Format(_localizationService.GetLocalizedString("ProxyCheckBadRequestMessage"), response.StatusCode), "#proxy-failed-test");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Proxy Health Check failed");
                    return new HealthCheck(GetType(), HealthCheckResult.Error, string.Format(_localizationService.GetLocalizedString("ProxyCheckFailedToTestMessage"), request.Url), "#proxy-failed-test");
                }
            }

            return new HealthCheck(GetType());
        }
    }
}
