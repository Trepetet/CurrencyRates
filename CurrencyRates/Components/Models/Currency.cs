namespace CurrencyRates.Components.Models
{
    public class Currency
    {
        public string Name { get; set; }
        public string TranslateName { get; set; }
        public double PriceSelectedCurrency { get; set; }
        public double PriceToUSD { get; set; }
        public string ISONameCode { get; set; }
        public int ISONumCode { get; set; }
        public bool isSupported { get; set; }
    }
}   
