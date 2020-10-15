using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace _2minersParser
{
	public class MinerSettings
	{
		public double fall_percentage { get; set; }
		public string worker_name { get; set; }
		public string user_id { get; set; }
		public string token { get; set; }
		public string miner { get; set; }
	}
	public class MinerState
	{
		public int lastBeat { get; set; }
		public double hr { get; set; }
		public bool offline { get; set; }
		public double hr2 { get; set; }
	}
	class Program
    {

		private static string sendGet(string url)
		{
			var request = WebRequest.Create(url);
			request.Headers.Add("User-Agent", "Mozilla/5.0");
			request.ContentType = "application/json";
			request.Credentials = CredentialCache.DefaultNetworkCredentials;
			var response = request.GetResponse();
			using (var dataStream = response?.GetResponseStream())
			{
				var reader = new StreamReader(dataStream);
				var responseFromServer = reader.ReadToEnd();
				response?.Close();
				return responseFromServer;
			}
		}

		static void Main(string[] args)
        {
			var settingsJSON = File.ReadAllText("settings.json");
			var settings = JsonConvert.DeserializeObject<MinerSettings>(settingsJSON);

			string minerUrl = "https://rvn.2miners.com/api/accounts/" + settings.miner;

			if (settings.fall_percentage < 0) settings.fall_percentage = 0;
			if (settings.fall_percentage > 100) settings.fall_percentage = 100;
			settings.fall_percentage /= 100;


			var hashRates = new Dictionary<string, List<double>>();
			string method = "messages.send"; // to message
			bool launch = true;
			// String method = "wall.post"; // wall post

			while (true)
			{
				var message = "";
				if (launch)
				{
					message = settings.worker_name + "%20launched%0A";
				}
				else
				{
					message += "from%20" + settings.worker_name + "%3A%0A";
				}
				double hashRateMin = 1000;
				try
				{
					var res = sendGet(minerUrl);
					var workers = JObject.Parse(res)["workers"]?.ToObject<Dictionary<string, MinerState>>();
					foreach (var workerObj in workers)
					{
						var worker = workerObj.Key;
						message += worker + ": ";
						var hashRate = workerObj.Value.hr2 / 1000000;
						hashRateMin = (hashRateMin < hashRate) ? hashRateMin : hashRate;
						message += hashRate + "Mh/s";
						if (launch)
						{
							var tmp = new List<double>();
							for (int j = 0; j < 10; j++)
							{
								tmp.Add(hashRate);
							}
							hashRates.Add(worker, tmp);
						}
						else if (settings.fall_percentage != 0)
						{
							if (hashRate < hashRates[worker].Average() * settings.fall_percentage)
							{
								hashRateMin = 0;
								message += "%20WARNING";
							}
							hashRates[worker].RemoveAt(0);
							hashRates[worker].Add(hashRate);
						}
						message += "%0A";
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
				message = message.Replace(" ", "%20");
				message = message.Replace(":", "%3A");
				message = message.Replace("/", "%2F");
				while (true)
				{
					if ((hashRateMin == 0) || (launch))
					{
						var request = $"https://api.vk.com/method/{method}?user_id={settings.user_id}&message={message}&access_token={settings.token}&v=5.73";
						var response = sendGet(request);
						Console.WriteLine(response);
						if (launch)
						{
							launch = false;
							Thread.Sleep(60 * 1000);
						}
						else
						{
							Thread.Sleep(10 * 60 * 1000);
						}
					}
					else
					{
						Thread.Sleep(60 * 1000);
					}
					break;
				}
			}
		}
	}
}
