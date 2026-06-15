using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;

public struct UserData
{
	public string LowercaseName { get; set; }
	public string ProperCaseName { get; set; }
	public string Color { get; set; }
}

public class TwitchChatAssistant
{
	private TwitchClient client;
	private Dictionary<string, UserData> uniqueUsers = new();
	private HashSet<string> excludedUsers = new();
	private string channelName;
	private string oauthToken;
	private string botUsername;
	private bool isRunning = true;
	private bool isReconnecting = false;

	public static void Main(string[] args)
	{
		var app = new TwitchChatAssistant();
		app.Run();
	}

	private void Run()
	{
		// Load configuration
		if (!LoadConfig())
		{
			Console.WriteLine("Configuration failed. Please fix config.json and try again.");
			return;
		}

		// Load excluded users
		LoadExcludedUsers();

		// Initialize Twitch client
		InitializeClient();

		// Input loop - press Enter to save/display, or Ctrl+C to exit
		Console.WriteLine("\n[READY] Press Enter to save & display list, or Ctrl+C to exit.\n");

		while (isRunning)
		{
			try
			{
				string input = Console.ReadLine();
				if (input != null) // Enter was pressed
				{
					DisplayAndSaveUsers();
				}
			}
			catch
			{
				// Ctrl+C will break this naturally
				break;
			}
		}
	}

	private bool LoadConfig()
	{
		try
		{
			var config = Config.Load();

			if (string.IsNullOrEmpty(config.ChannelName) || string.IsNullOrEmpty(config.OAuthToken) || string.IsNullOrEmpty(config.BotUsername))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("⚠ Config values are empty. Please fill in config.json");
				Console.ResetColor();
				return false;
			}

			channelName = config.ChannelName;
			oauthToken = config.OAuthToken;
			botUsername = config.BotUsername;

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("✓ Config loaded successfully");
			Console.ResetColor();
			return true;
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"✗ Error loading config: {ex.Message}");
			Console.ResetColor();
			return false;
		}
	}

	private void LoadExcludedUsers()
	{
		const string excludedPath = "excludedUsers.txt";

		if (File.Exists(excludedPath))
		{
			try
			{
				var lines = File.ReadAllLines(excludedPath);
				excludedUsers = new HashSet<string>(lines.Select(l => l.Trim().ToLower()).Where(l => !string.IsNullOrEmpty(l)));
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"✓ Loaded {excludedUsers.Count} excluded users");
				Console.ResetColor();
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"✗ Error loading excluded users: {ex.Message}");
				Console.ResetColor();
			}
		}
		else
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("⚠ excludedUsers.txt not found. Creating empty file...");
			Console.ResetColor();
			File.WriteAllText(excludedPath, "");
		}
	}

	private void InitializeClient()
	{
		try
		{
			var credentials = new ConnectionCredentials(botUsername, oauthToken);
			var clientOptions = new ClientOptions
			{
				MessagesAllowedInPeriod = 750,
				ThrottlingPeriod = TimeSpan.FromSeconds(30)
			};

			WebSocketClient customClient = new(clientOptions);
			client = new TwitchClient(customClient);
			client.Initialize(credentials, channelName);

			client.OnLog += Client_OnLog;
			client.OnJoinedChannel += Client_OnJoinedChannel;
			client.OnMessageReceived += Client_OnMessageReceived;
			client.OnUserBanned += Client_OnUserBanned;
			client.OnConnectionError += Client_OnConnectionError;
			client.OnDisconnected += Client_OnDisconnected;
			client.OnReconnected += Client_OnReconnected;

			client.Connect();

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("✓ Twitch client initialized");
			Console.ResetColor();
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"✗ Error initializing client: {ex.Message}");
			Console.ResetColor();
		}
	}

	private void Client_OnLog(object sender, OnLogArgs e)
	{
		// Suppress verbose logs if needed
	}

	private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine($"✓ Connected to #{e.Channel}. Tracking chatters...");
		Console.ResetColor();
		Console.Out.Flush();
	}

	private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
	{
		string username = e.ChatMessage.Username.ToLower(); 

		// Skip excluded users and channel owner
		if (excludedUsers.Contains(username) || username == channelName.ToLower())
			return;
		
		// Add or update user
		if (!uniqueUsers.ContainsKey(username))
		{
			// Get color from HexColor property if available, otherwise construct from RGB
			string hexColor = "#000000";

			if (!string.IsNullOrEmpty(e.ChatMessage.ColorHex))
			{
				hexColor = e.ChatMessage.ColorHex;
			}
			else if (e.ChatMessage.Color != null)
			{
				hexColor = $"#{e.ChatMessage.Color.R:X2}{e.ChatMessage.Color.G:X2}{e.ChatMessage.Color.B:X2}";
			}

			uniqueUsers[username] = new UserData
			{
				LowercaseName = username,
				ProperCaseName = e.ChatMessage.DisplayName,
				Color = hexColor
			};

			Console.WriteLine($"[+] {username} ({uniqueUsers.Count} total)");
			Console.Out.Flush();
		}
	}

	private void Client_OnUserBanned(object sender, OnUserBannedArgs e)
	{
		string username = e.UserBan.Username.ToLower();
		if (uniqueUsers.Remove(username))
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[-] {username} (banned) ({uniqueUsers.Count} remaining)");
			Console.ResetColor();
			Console.Out.Flush();
		}
	}

	private void Client_OnConnectionError(object sender, OnConnectionErrorArgs e)
	{
		if (isReconnecting) return; // Prevent multiple simultaneous reconnect attempts

		isReconnecting = true;
		Task.Run(async () =>
		{
			await Task.Delay(5000);
			client.Reconnect();
		});
	}

	private void Client_OnDisconnected(object sender, OnDisconnectedEventArgs e)
	{
		if (isReconnecting) return; // Prevent multiple simultaneous reconnect attempts

		isReconnecting = true;
		Task.Run(async () =>
		{
			await Task.Delay(5000);
			client.Reconnect();
		});
	}

	private void Client_OnReconnected(object sender, OnReconnectedEventArgs e)
	{
		isReconnecting = false; // Reset flag on successful reconnection
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine("✓ Reconnected successfully!");
		Console.ResetColor();
		Console.Out.Flush();
	}

	private void DisplayAndSaveUsers()
	{
		if (uniqueUsers.Count == 0)
		{
			Console.WriteLine("\n[No users tracked yet]\n");
			return;
		}

		Console.WriteLine($"\n========== CHATTERS ({uniqueUsers.Count}) ==========\n");

		var sortedUsers = uniqueUsers.Values.OrderBy(u => u.LowercaseName).ToList();

		// Display in console with proper casing
		foreach (var user in sortedUsers)
		{
			Console.WriteLine($"{user.LowercaseName}");
		}

		// Save to Text.html with optimized auto-scrolling (Star Wars style)
		try
		{
			var userDivs = sortedUsers.Select(u => $"<div style=\"color:{u.Color}\">{u.ProperCaseName}</div>").ToList();

			// Calculate scroll duration: 1 second per user, minimum 15 seconds, maximum 60 seconds
			int scrollDurationSeconds = Math.Max(15, Math.Min(sortedUsers.Count, 60));

			string htmlContent = $@"<!DOCTYPE html>
		<html lang=""en"">
		<head>
			<meta charset=""UTF-8"">
			<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
			<title>Chatters</title>
			<style>
				html, body {{
					margin: 0;
					padding: 0;
					width: 100%;
					height: 100%;
					background-color: transparent;
					font-family: Arial, sans-serif;
					font-size: 24px;
					line-height: 2;
				}}
				#container {{
					text-align: center;
					padding-top: 100vh;
					padding-bottom: 300vh;
				}}
				div {{
					padding: 12px 0;
					word-wrap: break-word;
				}}
			</style>
		</head>
		<body>
		<div id=""container"">
		{string.Join("\n", userDivs)}
		</div>
		<script>
			function startAutoScroll() {{
				const scrollDuration = {scrollDurationSeconds * 1000}; // milliseconds
				const maxScroll = document.documentElement.scrollHeight - window.innerHeight;
				
				const startTime = Date.now();
				
				function scroll() {{
					const elapsed = Date.now() - startTime;
					const progress = Math.min(elapsed / scrollDuration, 1);
					
					// Scroll from bottom to top
					window.scrollTo(0, maxScroll * progress);
					
					if (progress < 1) {{
						requestAnimationFrame(scroll);
					}}
				}}
				
				scroll();
			}}
			
			// 5-second delay before credits roll
			window.addEventListener('load', function() {{
				setTimeout(startAutoScroll, 5000);
			}});
		</script>
		</body>
		</html>";

			File.WriteAllText("Text.html", htmlContent);

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"\n✓ Saved {uniqueUsers.Count} users to Text.html (scroll duration: {scrollDurationSeconds}s)\n");
			Console.ResetColor();
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"\n✗ Error saving to Text.html: {ex.Message}\n");
			Console.ResetColor();
		}
	}

}
