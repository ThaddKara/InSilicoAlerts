using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace InSilicoAlerts
{
    class DeribitAPI
    {
        private const string domain = "https://www.deribit.com";
        private string apiKey;
        private string apiSecret;
        private int rateLimit;

        public DeribitAPI(string DeribitKey, string DeribitSecret, int rateLimit = 5000)
        {
            this.apiKey = DeribitKey;
            this.apiSecret = DeribitSecret;
            this.rateLimit = rateLimit;
        }

        private string BuildQueryData(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            StringBuilder b = new StringBuilder();
            foreach (var item in param)
                b.Append(string.Format("&{0}={1}", item.Key, WebUtility.UrlEncode(item.Value)));

            try { return b.ToString().Substring(1); }
            catch (Exception) { return ""; }
        }

        private string BuildJSON(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            var entries = new List<string>();
            foreach (var item in param)
                entries.Add(string.Format("\"{0}\":\"{1}\"", item.Key, item.Value));

            return "{" + string.Join(",", entries) + "}";
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        private long GetExpires()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600; // set expires one hour in the future
        }


        private long lastTicks = 0;
        private object thisLock = new object();

        private void RateLimit()
        {
            lock (thisLock)
            {
                long elapsedTicks = DateTime.Now.Ticks - lastTicks;
                var timespan = new TimeSpan(elapsedTicks);
                if (timespan.TotalMilliseconds < rateLimit)
                    Thread.Sleep(rateLimit - (int)timespan.TotalMilliseconds);
                lastTicks = DateTime.Now.Ticks;
            }
        }

        private string DeribitAuth(string function, string method = "GET")
        {
            string url = domain + "/api/v2" + function;

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

            webRequest.Method = method;

            string svcCredentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(apiKey + ":" + apiSecret));

            webRequest.Headers.Add("Authorization", "Basic " + svcCredentials);

            try
            {
                webRequest.ContentType = "application/json";

                using (WebResponse webResponse = webRequest.GetResponse())
                using (Stream str = webResponse.GetResponseStream())
                using (StreamReader sr = new StreamReader(str))
                {
                    // Console.WriteLine("deribit auth success");
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                Console.WriteLine("deribit auth error");
                throw new NotImplementedException();
            }
        }

        private string Query(string method, string function, Dictionary<string, string> param = null, bool auth = false, bool json = false)
        {
            string paramData = json ? BuildJSON(param) : BuildQueryData(param);
            string url = "/api/v2" + function + ((method == "GET" && paramData != "") ? "?" + paramData : "");
            string postData = (method != "GET") ? paramData : "";

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(domain + url);
            webRequest.Method = method;

            if (auth)
            {
                string expires = GetExpires().ToString();
                string message = method + url + expires + postData;
                byte[] signatureBytes = Hmacsha256(Encoding.UTF8.GetBytes(apiSecret), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                webRequest.Headers.Add("api-expires", expires);
                webRequest.Headers.Add("api-key", apiKey);
                webRequest.Headers.Add("api-signature", signatureString);
            }

            try
            {
                if (postData != "")
                {
                    webRequest.ContentType = json ? "application/json" : "application/x-www-form-urlencoded";
                    var data = Encoding.UTF8.GetBytes(postData);
                    using (var stream = webRequest.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }

                using (WebResponse webResponse = webRequest.GetResponse())
                using (Stream str = webResponse.GetResponseStream())
                using (StreamReader sr = new StreamReader(str))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                using (HttpWebResponse response = (HttpWebResponse)wex.Response)
                {
                    if (response == null)
                        throw;

                    using (Stream str = response.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(str))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }

        public void getBookSummaryETH()
        {
            Console.WriteLine(DeribitAuth("/public/get_book_summary_by_currency?currency=ETH"));

            return;
        }

        public void getBookSummaryBTC()
        {
            string json = DeribitAuth("/public/get_book_summary_by_currency?currency=BTC");

            Console.WriteLine(json);

            return;
        }

        public string getAnnouncements()
        {
            string json = DeribitAuth("/public/get_announcements");

            Console.WriteLine(json);

            return json;
        }

        public string getAccountSummary(string currency)
        {
            string json = DeribitAuth("/private/get_account_summary?currency=" + currency);

            Console.WriteLine(json);

            return json;
        }

        public string getNewAnnouncements(string currency)
        {
            string json = DeribitAuth("/private/get_new_announcements");

            Console.WriteLine(json);

            return json;
        }

        public string getPosition(string instrument)
        {
            string json = DeribitAuth("/private/get_position?instrument_name=" + instrument);

            Console.WriteLine(json);

            return json;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currency">ETH BTC</param>
        /// <param name="kind">future option</param>
        /// <returns></returns>
        public string getPositions(string currency, string kind)
        {
            string json = DeribitAuth("/private/get_positions?currency=" + currency + "&kind=" + kind);

            Console.WriteLine(json);

            return json;
        }

        public string getSubAccounts(string instrument)
        {
            string json = DeribitAuth("/private/get_subaccounts");

            Console.WriteLine(json);

            return json;
        }

        /// --- Market Data --- ///

        /*public async Task<List<string>> getInstruments(string currency, string kind)
        {
            List<string> ret = new List<string>();

            AmazonDynamoDBClient amazonDynamoDBClient = new AmazonDynamoDBClient();

            PutArbbotDeribitData putArbbotDeribitData = new PutArbbotDeribitData(amazonDynamoDBClient);

            string json = DeribitAuth("/public/get_instruments?currency=" + currency + "&kind=" + kind);

            JObject jObject = JObject.Parse(json);

            int counter = 0;
            while (true)
            {
                try
                {
                    string instrumentname = (string)jObject.SelectToken("result[" + counter + "].instrument_name");
                    
                    if(instrumentname == null)
                    {
                        // Console.WriteLine("end of instruments reached");
                        break;
                    }

                    // Console.WriteLine(instrumentname);
                    // await putArbbotDeribitData.PutInstrumentName(instrumentname);
                    ret.Add(instrumentname);

                    counter += 1;
                }
                catch
                {
                    Console.WriteLine("end of instruments reached");
                    break;
                }
            }

            return ret;
        }

        public async Task<Dictionary<string,string>> getBookSummaryByInstrument(string instrument)
        {
            Dictionary<string, string> ret = new Dictionary<string,string>();

            AmazonDynamoDBClient amazonDynamoDBClient = new AmazonDynamoDBClient();

            PutArbbotDeribitData putArbbotDeribitData = new PutArbbotDeribitData(amazonDynamoDBClient);
            
            string json = DeribitAuth("/public/get_book_summary_by_instrument?instrument_name=" + instrument);

            JObject jObject = JObject.Parse(json);

            string openinterest = (string)jObject.SelectToken("result[0].open_interest");
            ret.Add("open_interest", openinterest);

            string midprice = (string)jObject.SelectToken("result[0].mid_price");
            ret.Add("mid_price", midprice);

            string markprice = (string)jObject.SelectToken("result[0].mark_price");
            ret.Add("mark_price", markprice);

            string lastprice = (string)jObject.SelectToken("result[0].last");
            ret.Add("last", lastprice);

            string bid = (string)jObject.SelectToken("result[0].bid_price");
            ret.Add("bid_price", bid);

            string ask = (string)jObject.SelectToken("result[0].ask_price");
            ret.Add("ask_price", ask);

            ret.Add("Instrument", instrument);

            Dictionary<string, string> modifiedret = new Dictionary<string, string>();

            foreach(KeyValuePair<string, string> pair in ret)
            {
                //Console.WriteLine(pair.Key + " " + pair.Value);
                if (pair.Value == null || pair.Value == "null")
                {
                    modifiedret.Add(pair.Key, "0");
                }
                else
                {
                    modifiedret.Add(pair.Key, pair.Value);
                }
            }

            await putArbbotDeribitData.UpdateInstrument(modifiedret);

            return modifiedret;
        }*/

        public string getLastTradeByInstrument(string instrument)
        {
            string json = DeribitAuth("/public/get_last_trades_by_instrument?instrument_name=" + instrument);

            Console.WriteLine(json);

            return json;
        }

        public string getTradeVolumes()
        {
            string json = DeribitAuth("/public/get_trade_volumes");

            Console.WriteLine(json);

            return json;
        }

        public string getIndexByCurrency(string currency)
        {
            string json = DeribitAuth("/public/get_index?currency=" + currency);

            Console.WriteLine(json);

            return json;
        }

        public string getOrderBook(string instrument)
        {
            string json = DeribitAuth("/public/get_order_book?intrument_name=" + instrument);

            Console.WriteLine(json);

            return json;
        }

        /// --- TODO: Wallet + Trading --- ///

        public string marketOrder(string side, string contract, string amount)
        {
            try
            {
                string json = DeribitAuth("/private/" + side + "?instrument_name=" + contract + "&amount=" + amount + "&type=market");

                Console.WriteLine(json);

                return json;
            }
            catch
            {
                Console.WriteLine($"error buy {contract}");

                return null;
            }
        }

        public Dictionary<string, string> buyByIndex(string Instrument, string type, string price, string amount)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            try
            {
                string json = DeribitAuth("/private/buy?instrument_name=" + Instrument + "&amount=" + amount + "&type=" + type + "&price=" + price);

                JObject jObject = JObject.Parse(json);

                string orderId = (string)jObject.SelectToken("result.order_id");
                ret.Add("order_id", orderId);

                string creationTime = (string)jObject.SelectToken("result.creation_timestamp");
                ret.Add("creation_timestamp", creationTime);

                return ret;
            }
            catch
            {
                return null;
            }
        }

        public Dictionary<string, string> getOpenOrderByInstrument(string Instrument)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            try
            {
                string json = DeribitAuth("/private/get_open_orders_by_instrument?instrument_name=" + Instrument);

                JObject jObject = JObject.Parse(json);

                int counter = 0;
                while(true)
                {
                    try
                    {
                        string direction = (string)jObject.SelectToken("result[" + counter + "].direction");
                        ret.Add("direction", direction);

                        if (direction == "sell")
                        {
                            counter += 1;
                            continue;
                        }

                        string orderState = (string)jObject.SelectToken("result[" + counter + "].order_state");
                        ret.Add("order_state", orderState);

                        string orderId = (string)jObject.SelectToken("result[" + counter + "].order_id");
                        ret.Add("order_id", orderId);

                        string orderPrice = (string)jObject.SelectToken("result[" + counter + "].price");
                        ret.Add("price", orderPrice);
                        break;
                    }
                    catch
                    {
                        break;
                    }
                }

                /*string orderState = (string)jObject.SelectToken("result[0].order_state");
                ret.Add("order_state", orderState);

                string orderId = (string)jObject.SelectToken("result[0].order_id");
                ret.Add("order_id", orderId);

                string orderPrice = (string)jObject.SelectToken("result[0].price");
                ret.Add("price", orderPrice);
                */

                return ret;
            }
            catch
            {
                Console.WriteLine("get open order error");
                return null;
            }
        }

        public Dictionary<string, string> cancelOrderById(string orderid)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            try
            {
                string json = DeribitAuth("/private/cancel?order_id=" + orderid);

                JObject jObject = JObject.Parse(json);

                string orderState = (string)jObject.SelectToken("result.order_state");
                ret.Add("order_state", orderState);

                return ret;
            }
            catch
            {
                return null;
            }
        }
    }
}
