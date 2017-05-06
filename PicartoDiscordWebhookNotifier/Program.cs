using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PicartoDiscordWebhookNotifier
{
	class DiscordWebhook
	{
        [JsonIgnore]
		public string HookName;

		public string DiscordURL;
		public string HookUsername;
		public string AvatarURL;

        public bool ShouldSerializeHookUsername() => HookUsername != null || HookUsername.Length == 0;
        public bool ShouldSerializeAvatarURL() => AvatarURL != null || AvatarURL.Length == 0;

		public DiscordWebhook()
		{
            HookName = "DefaultName";
            DiscordURL = "your_webhook_url";
            HookUsername = null;
            AvatarURL = null;
		}

		public DiscordWebhook(string hookName, string url, string un, string av)
		{
            HookName = hookName;
			DiscordURL = url;
			HookUsername = un;
			AvatarURL = av;
		}

		public DiscordWebhook(DiscordWebhook other)
		{
            HookName = other.HookName;
			DiscordURL = other.DiscordURL;
			HookUsername = other.HookUsername;
			AvatarURL = other.AvatarURL;
		}
	}

	class PicartoUser
	{
        [JsonIgnore]
        public string PicartoUsername;
        public List<string> TargetWebhooks;

        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            TargetWebhooks = AnnounceToWebhooks.Select(w => w.HookName).Distinct().ToList();
        }
        
        [JsonIgnore]
		public List<DiscordWebhook> AnnounceToWebhooks;

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

        public bool ShouldSerializeusername() => username != null;
        public bool ShouldSerializeavatar_url() => avatar_url != null;
	}

    class SavedConfig
    {
        public Dictionary<string, DiscordWebhook> AvailableChannels = new Dictionary<string, DiscordWebhook>();
        public Dictionary<string, PicartoUser> WatchingUsers = new Dictionary<string, PicartoUser>();
        public int SecondsBetweenChecks = 60;

        [JsonIgnore]
        public DateTime ConfigModifiedTime;
    }

	class Program
	{
        public static SavedConfig AppConfig;

        public static void GenerateExampleConfig()
        {
            Console.WriteLine("Please configure your channels/watched users first. An example config has been generated for you.");

            var cfg = new SavedConfig();

            cfg.AvailableChannels.Add("NameOfHook", new DiscordWebhook("NameOfHook", "https://api.discord.com/this_is_your/webhook_address", "Bot Display Name", "Avatar URL"));
            cfg.WatchingUsers.Add("PicartoUsername", new PicartoUser("PicartoUsername", cfg.AvailableChannels.Values.ToList()));
            cfg.SecondsBetweenChecks = 60;

            var json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
            File.WriteAllText("config-example.json", json);
        }

		public static void LoadConfig()
		{
            if (!File.Exists("config.json"))
            {
                GenerateExampleConfig();
                Environment.Exit(0);
            }

            if (AppConfig == null)
            {
                AppConfig = new SavedConfig();
            }

            AppConfig = JsonConvert.DeserializeObject<SavedConfig>(File.ReadAllText("config.json"));
            AppConfig.ConfigModifiedTime = File.GetLastWriteTime("config.json");

            AppConfig.AvailableChannels.Keys.ToList().ForEach(k => AppConfig.AvailableChannels[k].HookName = k);

            foreach(var u in AppConfig.WatchingUsers)
            {
                u.Value.AnnounceToWebhooks = u.Value.TargetWebhooks.Where(AppConfig.AvailableChannels.ContainsKey).Select(w => AppConfig.AvailableChannels[w]).ToList();
                u.Value.PicartoUsername = u.Key;

                Console.Write("Monitoring \"{0}\", Notifying:", u.Value.PicartoUsername);
                foreach (var h in u.Value.AnnounceToWebhooks)
                {
                    Console.Write(" {0}", h.HookName);
                }
                Console.WriteLine("");
            }

			Console.WriteLine("");
		}

		static void Main(string[] args)
		{
			LoadConfig();

			while (true)
			{
				Console.WriteLine("===== Updating =====");
				var reloadingConfig = false;

				if (File.GetLastWriteTime("config.json") != AppConfig.ConfigModifiedTime)
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
					if (AppConfig.WatchingUsers.ContainsKey(os.name))
					{
						onlineWatchedUsers.Add(AppConfig.WatchingUsers[os.name]);
					}
				}
				Console.WriteLine("{0} watched users online", onlineWatchedUsers.Count);

				offlineWatchedUsers = AppConfig.WatchingUsers.Values.Except(onlineWatchedUsers).ToList();
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

                try
                {
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
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.SendFailure || ex.Status == WebExceptionStatus.ReceiveFailure || ex.Status == WebExceptionStatus.ConnectFailure)
                    {
                        Console.WriteLine("Failed to send status update about user to Discord, or recieve a response in kind. Status: {0} Message: {1}", ex.Status, ex.Message);
                    }
                    else
                    {
                        Console.WriteLine("Webhook communication failed somehow. Status: {0} Message: {1}", ex.Status, ex.Message);
                    }
                }

				Thread.Sleep(500);
			}
		}

	}
}
