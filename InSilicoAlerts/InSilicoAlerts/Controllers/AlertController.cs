using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace InSilicoAlerts.Controllers
{
    [Route("tv")]
    [ApiController]
    public class AlertController : ControllerBase
    {
        // GET: api/<AlertController>
        [HttpPost]
        public async Task<IActionResult> webhook([FromQuery] string key)
        {
            
                AmazonDynamoDBClient client = new AmazonDynamoDBClient();
                Table credtable = Table.LoadTable(client, "InSilico");
                ScanOperationConfig config = new ScanOperationConfig();
                ScanFilter filter = new ScanFilter();
                filter.AddCondition("key", ScanOperator.IsNotNull);
                config.Filter = filter;
                Search search = credtable.Scan(config);
                List<Document> docs = await search.GetRemainingAsync();
                Document creds = new Document();
                foreach(Document doc in docs)  // get credentials from dynamodb
                {
                    try { doc["key"].ToString(); creds = doc; break; } // get first nonempty (only should be one entry in this db or this will break
                    catch { }
                }

                string webhookkey = creds["webhook"].ToString();
                string authkey = creds["key"].ToString();
                string authsecret = creds["secret"].ToString();

                if (key != webhookkey) { throw new Exception(); } // webhook auth

                DeribitAPI deribit = new DeribitAPI(authkey, authsecret);

                StreamReader reader = new StreamReader(Request.Body);
                string text = reader.ReadToEnd();

                string[] fishervals = text.Split(',');
                fishervals.Select(p => p.Replace(" ", "")); // remove all whitespace
                
                Console.WriteLine(fishervals.Length);

                double fisher1; double.TryParse(fishervals[0], out fisher1);
                double fisher2; double.TryParse(fishervals[1], out fisher2);
                double fisher = (fisher1 + fisher2) / 2;

                double mark; double.TryParse(fishervals[2], out mark);

                // trade logic
                double sell = 1.9;
                double buy = sell * -1;
                double quantity = 0;
                double entry = 0;
                double movement = 0;
                string dir = "";

                // close in-profit position logic
                string pos = deribit.getPosition("ETH-PERPETUAL");
                JObject posjobject = JObject.Parse(pos);
                dir = (string)posjobject.SelectToken("result.direction");
                entry = (double)posjobject.SelectToken("result.average_price");
                movement = (mark / entry) - 1; // zero it out for comparisons (not actually profit - used for directionality)

                string acc = deribit.getAccountSummary("ETH");
                JObject accjson = JObject.Parse(acc);
                double.TryParse(accjson.SelectToken("result.available_funds").ToString(), out quantity);
                quantity = (quantity * mark) / 5;
                quantity = Math.Round(quantity);

                if (quantity < 1) { Console.WriteLine("fund account"); return Ok(); } // liqd kek

                if (fisher < buy) // buy logic (swing low)
                {
                    deribit.marketOrder("buy", "ETH-PERPETUAL", quantity.ToString());
                    Console.WriteLine($"buy {quantity} - ETHUSD @ {mark}");

                    if(dir == "sell" && movement < 0)
                    {
                        // take profit on successful short
                        deribit.marketOrder("buy", "ETH-PERPETUAL", quantity.ToString());
                    }
                }
                else if (fisher > sell) // sell logic (swing high)
                {
                    deribit.marketOrder("sell", "ETH-PERPETUAL", quantity.ToString());
                    Console.WriteLine($"sell {quantity} - ETHUSD @ {mark}");

                    if(dir == "buy" && movement > 0)
                    {
                        // take profit on successful long
                        deribit.marketOrder("sell", "ETH-PERPETUAL", quantity.ToString());
                    }
                }

                return Ok();
            /*}
            catch
            {
                Console.WriteLine("error");
                return BadRequest();
            }*/
        }
    }
}
