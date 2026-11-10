namespace PhysioClinic.Services.WhatsApp
{
    public class WhatsAppSettings
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Provider options:
        /// "GreenApi" (recommended cloud provider with free tier)
        /// "UltraMsg" (cloud provider with token/instance)
        /// "Gateway"  (self-hosted Node.js / Baileys gateway)
        /// </summary>
        public string Provider { get; set; } = "GreenApi";

        /// <summary>
        /// Instance ID / Channel ID from your cloud provider (or SessionId for Gateway)
        /// </summary>
        public string InstanceId { get; set; } = "";

        /// <summary>
        /// API Token / Secret from your cloud provider (or ApiKey for Gateway)
        /// </summary>
        public string ApiToken { get; set; } = "";

        /// <summary>
        /// Custom base URL if using a custom gateway or custom domain proxy
        /// </summary>
        public string ApiUrl { get; set; } = "https://api.green-api.com";

        // Backward-compatible aliases
        public string SessionId
        {
            get => string.IsNullOrEmpty(InstanceId) ? _sessionId : InstanceId;
            set { _sessionId = value; if (string.IsNullOrEmpty(InstanceId)) InstanceId = value; }
        }
        private string _sessionId = "5566fa92-1d24-4599-8321-d38aec69f931";

        public string ApiKey
        {
            get => string.IsNullOrEmpty(ApiToken) ? _apiKey : ApiToken;
            set { _apiKey = value; if (string.IsNullOrEmpty(ApiToken)) ApiToken = value; }
        }
        private string _apiKey = "owa_k1_c2459a1278e6ee9db50f8aa757b23fcc878f3ebc524f04059dcc23ab29cf5266";

        public string SenderNumber { get; set; } = "+962786363593";
        public string ClinicName { get; set; } = "عيادة فيزيو فلو للعلاج الطبيعي والتأهيل";
        public bool AutoSendOnBooking { get; set; } = true;
        public bool AutoSendOnCancel { get; set; } = true;
    }
}
