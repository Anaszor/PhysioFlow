namespace PhysioClinic.Services.WhatsApp
{
    public class WhatsAppSettings
    {
        public bool Enabled { get; set; } = true;
        public string ApiUrl { get; set; } = "http://localhost:2785";
        public string ApiKey { get; set; } = "owa_k1_c2459a1278e6ee9db50f8aa757b23fcc878f3ebc524f04059dcc23ab29cf5266";
        public string SessionId { get; set; } = "5566fa92-1d24-4599-8321-d38aec69f931";
        public string SenderNumber { get; set; } = "+962786363593";
        public string ClinicName { get; set; } = "عيادة فيزيو فلو للعلاج الطبيعي والتأهيل";
        public bool AutoSendOnBooking { get; set; } = true;
        public bool AutoSendOnCancel { get; set; } = true;
    }
}
