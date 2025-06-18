using CurrencyRates.Components.Models;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CurrencyRates.Components.Services
{
    public class CurrencyManagementService
    {
        public string _token { get; private set; }
        public TimeOnly _updateRate { get; set; } = new TimeOnly(0, 5);
        public DateTime lastPriceUpdate { get; set; }

        string CURRENCIES_PATH = "Currencies.txt";
        string _baseURL = "https://api.fastforex.io";

        public void CheckingExistenceToken()
        {
            if (_token == null || _token == "")
                throw new InvalidOperationException("Токен API не был указан. Пожалуйста, установите токен в настройках.");
        }

        public async Task SetToken(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(_baseURL + "/usage?api_key=" + token);
                if (response.IsSuccessStatusCode)
                    _token = token;
                else
                {
                    _token = "";
                    throw new ArgumentException("Вы указали неверный API Токен");
                }
            }
        }

        public void UpdateCurrenciesRate()
        {
            CheckingExistenceToken();
        }

        #region Работа с API для получения цен по валюте

        public async Task UpdatePriceOnCurreny(Currency currency, string selectedCur = null)
        {
            CheckingExistenceToken();
            using (HttpClient client = new HttpClient())
            {
                string url = _baseURL + $"/fetch-multi?from={currency.ISONameCode}&to=USD{(selectedCur != "" && selectedCur != "USD" ? "," + selectedCur : "")}&api_key=" + _token;
                HttpResponseMessage response = await client.GetAsync(url);
                var result = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode && result.Contains("Invalid\\/unsupported target currencies:"))
                {
                    currency.PriceToUSD = 0;
                    currency.PriceSelectedCurrency = 0;
                    currency.isSupported = false;
                    return;
                }
                else if (!response.IsSuccessStatusCode)
                    throw new Exception("При обновленнии цен по 1 валюте произошла ошибка!"+response.StatusCode.ToString());

                var currencyData = JsonConvert.DeserializeObject<CurrencyData>(result);
                lastPriceUpdate = currencyData.Updated;
                if(currencyData.Results.TryGetValue("USD", out decimal value))
                {
                    currency.PriceToUSD = (double)value;
                    if(selectedCur!="")
                        currency.PriceSelectedCurrency = (double)currencyData.Results[selectedCur];
                    currency.isSupported = true;
                }
                else
                {
                    currency.PriceToUSD = 0;
                    currency.PriceSelectedCurrency = 0;
                    currency.isSupported = false;
                }
                
            }
        }
        public class CurrencyData
        {
            [JsonProperty("results")]
            public Dictionary<string, decimal> Results { get; set; }

            [JsonProperty("updated")]
            public DateTime Updated { get; set; }
        }

        #endregion

        #region Получение общей информации по валюте из API центробанка РФ
        public Currency? GetCurriency(string currencyISOName)
        {
            var currencies = GetAllCurrenciesFromFile();
            if (currencies == null)
                currencies = GetAllCurrenciesFromApi(true);

            return currencies?.FirstOrDefault(x=>x.ISONameCode == currencyISOName);
        }
        public async void UpdateCurrencyFile()
        {
            using (WebClient client = new WebClient() { Encoding = Encoding.GetEncoding(1251) })
            {
                var currenciesString = GetCurrenciesInfoFromAPI(client);
                File.WriteAllText(CURRENCIES_PATH, currenciesString);
            }
        }

        private List<Currency>? GetAllCurrenciesFromFile()
        {
            if (File.Exists(CURRENCIES_PATH))
            {
                return ParseCurreniesFromString(File.ReadAllText(CURRENCIES_PATH));
            }
            else
                return null;
        }

        private List<Currency>? GetAllCurrenciesFromApi(bool? updateFile = null)
        {

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using (WebClient client = new WebClient() { Encoding = Encoding.GetEncoding(1251) })
            {
                try
                {
                    string currenciesString = GetCurrenciesInfoFromAPI(client);
                    currenciesString = currenciesString.Insert(88, "<Item ID=\"R00000\"><Name>Российский рубль</Name><EngName>Russian Ruble</EngName><Nominal>1</Nominal><ParentCode>R00000</ParentCode><ISO_Num_Code>643</ISO_Num_Code><ISO_Char_Code>RUB</ISO_Char_Code></Item>");
                    if (updateFile == true)
                        File.WriteAllText(CURRENCIES_PATH, currenciesString);
                     return ParseCurreniesFromString(currenciesString);
                }
                catch (HttpRequestException ex)
                {
                    throw ex;
                }
            }
        }

        private string GetCurrenciesInfoFromAPI(WebClient client)
        {
            return client.DownloadString("https://www.cbr.ru/scripts/XML_valFull.asp");
        }

        private List<Currency>? ParseCurreniesFromString(string currenciesString)
        {
            var currencies = new List<Currency>();
            XDocument xmlDoc = XDocument.Parse(currenciesString);
            var Currencies = xmlDoc.Root.Elements("Item").Select(item => new Currency
            {
                Name = item.Element("Name")?.Value,
                TranslateName = item.Element("EngName")?.Value,
                ISONameCode = item.Element("ISO_Char_Code")?.Value,
                ISONumCode = int.TryParse(item.Element("ISO_Num_Code")?.Value, out int numCode) ? numCode : -1
            }).ToList();
            return Currencies;
        }
        #endregion
    }
}
