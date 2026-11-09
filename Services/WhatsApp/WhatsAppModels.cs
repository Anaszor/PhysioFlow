using System.Text.Json.Serialization;

namespace PhysioClinic.Services.WhatsApp
{
    public class WhatsAppSessionStatus
    {
        public bool IsServerReachable { get; set; }
        public string Status { get; set; } = "unknown";
        public string? Phone { get; set; }
        public string? PushName { get; set; }
        public bool EngineLoaded { get; set; }
        public bool IsReady => Status.Equals("ready", StringComparison.OrdinalIgnoreCase);
        public string? ErrorMessage { get; set; }
    }

    public class WhatsAppPairingResult
    {
        public bool Success { get; set; }
        public string? PairingCode { get; set; }
        public string? QrCodeBase64 { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class WhatsAppSendResult
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class GatewaySessionResponseDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("pushName")]
        public string? PushName { get; set; }

        [JsonPropertyName("engineLoaded")]
        public bool EngineLoaded { get; set; }

        [JsonPropertyName("lastError")]
        public string? LastError { get; set; }
    }

    public class GatewayPairingCodeResponseDto
    {
        [JsonPropertyName("pairingCode")]
        public string? PairingCode { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class GatewayQrResponseDto
    {
        [JsonPropertyName("qrCode")]
        public string? QrCode { get; set; }
    }

    public class GatewayMessageResponseDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }
    }
}
