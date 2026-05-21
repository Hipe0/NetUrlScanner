namespace NetURLScanner.Models
{
    public class RiskResult
    {
        public int Score { get; set; }
        public string Level { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new();
    }
}