using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PicartoDiscordWebhookNotifier
{
	class DiscordWebhook
	{
		public string HookName;
		public string DiscordURL;
		public string HookUsername;
		public string AvatarURL;

		public DiscordWebhook()
		{

		}

		public DiscordWebhook(string url, string un, string av)
		{
			DiscordURL = url;
			HookUsername = un;
			AvatarURL = av;
		}

		public DiscordWebhook(DiscordWebhook other)
		{
			DiscordURL = other.DiscordURL;
			HookUsername = other.HookUsername;
			AvatarURL = other.AvatarURL;
		}
	}

	class PicartoUser
	{
		public List<DiscordWebhook> AnnounceToWebhooks;
		public string PicartoUsername;

		[JsonIgnore]
		public bool IsLive;

		public PicartoUser()
		{
			AnnounceToWebhooks = new List<DiscordWebhook>();
		}

		public PicartoUser(string un, List<DiscordWebhook> announces)
		{
			AnnounceToWebhooks = new List<DiscordWebhook>();
			announces.ForEach(a => AnnounceToWebhooks.Add(new DiscordWebhook(a)));

			PicartoUsername = un;
			IsLive = false;
		}

		public void SetHookName(string un)
		{
			AnnounceToWebhooks.ForEach(a => a.HookUsername = un);
		}

		public void SetHookAv(string av)
		{
			AnnounceToWebhooks.ForEach(a => a.AvatarURL = av);
		}
	}

	public class PicartoOnlineState
	{
		public int user_id;
		public string name;
		public int viewers;
		public string category;
		public bool adult;
		public bool gaming;
		public bool multistream;
	}

	public class DiscordMessage
	{
		public string content;
		public string username;
		public string avatar_url;
	}

	class Program
	{
		public static List<PicartoUser> WatchedUsers;
		public static Dictionary<string, PicartoUser> DictWatchedUsers;
		public static DateTime ConfigModifiedTime;

		public static void LoadConfig()
		{
			ConfigModifiedTime = File.GetLastWriteTime("config.json");
			WatchedUsers = JsonConvert.DeserializeObject<List<PicartoUser>>(File.ReadAllText("config.json"));
			DictWatchedUsers = new Dictionary<string, PicartoUser>();

			foreach (var u in WatchedUsers)
			{
				DictWatchedUsers.Add(u.PicartoUsername, u);
				Console.Write("Monitoring \"{0}\", Notifying:", u.PicartoUsername);

				foreach(var h in u.AnnounceToWebhooks)
				{
					Console.Write(" {0}", h.HookName);
				}

				Console.WriteLine("");
			}

			Console.WriteLine("");
		}

		static void Main(string[] args)
		{
			/*
			var baseHooks = new List<DiscordWebhook>();
			var hook = new DiscordWebhook("https://discordapp.com/api/webhooks/308781545542647809/rWfhM5mrRXnOZo_HsMnPLnj2GqsY8kSL-KJQhO6_RcRPAPPTWw9QHqcyoQowowQBlttD", "Picarto.TV", "http://img.ganked.me/images/2017/05/02/280b47dda354f48cbfccdf382935a693.png");
			baseHooks.Add(hook);
			
			var watchedUsers = new List<PicartoUser>();
			watchedUsers.Add(new PicartoUser("MxBones", baseHooks));

			// Test Server -> https://discordapp.com/api/webhooks/308781545542647809/rWfhM5mrRXnOZo_HsMnPLnj2GqsY8kSL-KJQhO6_RcRPAPPTWw9QHqcyoQowowQBlttD
			// Writers Server #administration -> https://discordapp.com/api/webhooks/308837601811496960/lE4bR8ucwUwIgB4RtgNpkRUe6Jj_IHQ_6IFoN8EUXo7Lm60bV_pF8yiysnOlFvfh-ahe
			*/

			LoadConfig();

			while (true)
			{
				Console.WriteLine("===== Updating =====");
				var reloadingConfig = false;

				if (File.GetLastWriteTime("config.json") != ConfigModifiedTime)
				{
					LoadConfig();
					reloadingConfig = true;
					Console.WriteLine("Reloading configuration...");
				}

				var webReq = (HttpWebRequest)WebRequest.Create("https://api.picarto.tv/v1/online?adult=true&gaming=false&categories=");
				webReq.ContentType = "application/json";
				webReq.Method = "GET";

				var webResp = (HttpWebResponse)webReq.GetResponse();

				var json = String.Empty;
				using (var sr = new StreamReader(webResp.GetResponseStream()))
				{
					json = sr.ReadToEnd();
				}

				Console.WriteLine("Got picarto data...");

				List<PicartoOnlineState> onlineState = JsonConvert.DeserializeObject<List<PicartoOnlineState>>(json);
				List<PicartoUser> onlineWatchedUsers = new List<PicartoUser>();
				List<PicartoUser> offlineWatchedUsers = new List<PicartoUser>();

				Console.WriteLine("{0} total online", onlineState.Count);
				foreach(var os in onlineState)
				{
					if (DictWatchedUsers.ContainsKey(os.name))
					{
						onlineWatchedUsers.Add(DictWatchedUsers[os.name]);
					}
				}
				Console.WriteLine("{0} watched users online", onlineWatchedUsers.Count);

				offlineWatchedUsers = WatchedUsers.Except(onlineWatchedUsers).ToList();
				Console.WriteLine("{0} watched users offline", offlineWatchedUsers.Count);

				foreach(var online in onlineWatchedUsers)
				{
					if (online.IsLive == false)
					{
						online.IsLive = true;
						if (!reloadingConfig) SendDiscordStreamingMessage(online);
						Console.WriteLine("Announcing {0} has gone live...", online.PicartoUsername);
					}
				}

				foreach(var offline in offlineWatchedUsers)
				{
					if (offline.IsLive == true)
					{
						offline.IsLive = false;
						if (!reloadingConfig) SendDiscordStoppedMessage(offline);
						Console.WriteLine("Announcing {0} has gone offline...", offline.PicartoUsername);
					}
				}

				Console.WriteLine("");
				Thread.Sleep(60000);
			}
		}

		public static void SendDiscordStreamingMessage(PicartoUser usr)
		{
			SendDiscordMessage(usr, String.Format("{0} is now livestreaming! [Click here to view!](https://picarto.tv/{1})", usr.PicartoUsername, usr.PicartoUsername));
		}

		public static void SendDiscordStoppedMessage(PicartoUser usr)
		{
			SendDiscordMessage(usr, String.Format("{0} is now offline.", usr.PicartoUsername));
		}

		public static void SendDiscordMessage(PicartoUser usr, string msg)
		{
			var dMsg = new DiscordMessage();
			dMsg.content = msg;
			
			foreach(var hook in usr.AnnounceToWebhooks)
			{
				dMsg.avatar_url = hook.AvatarURL;
				dMsg.username = hook.HookUsername;

				var webReq = (HttpWebRequest)WebRequest.Create(hook.DiscordURL);
				webReq.ContentType = "application/json";
				webReq.Method = "POST";

				using (var sw = new StreamWriter(webReq.GetRequestStream()))
				{
					sw.Write(JsonConvert.SerializeObject(dMsg));
					sw.Flush();
					sw.Close();
				}

				var webResp = (HttpWebResponse)webReq.GetResponse();
				using (var sr = new StreamReader(webResp.GetResponseStream()))
				{
					Console.WriteLine(sr.ReadToEnd());
				}

				Thread.Sleep(500);
			}
		}

	}
}
