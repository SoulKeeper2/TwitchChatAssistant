using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;

class TwitchChatAssistant
{
	private TwitchClient client;
	private HashSet<string> uniqueUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private HashSet<string> bannedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private HashSet<string> excludedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private string channelName;
	private bool isRunning = true;

	static async Task Main(string[] args)
	{
		var assistant = new TwitchChatAssistant();
		await assistant.StartAsync();
	}

	async Task StartAsync()
	{
		Console.WriteLine("=== Twitch Chat Assistant ===\n");

		// Load config
		var config = Config.Load();
		channelName = config.ChannelName;
		string oauthToken = config.OAuthToken;
		string botUsername = config.BotUsername;

		// Load excluded users from file
		LoadExcludedUsers();

		// Add streamer's name to excluded users
		excludedUsers.Add(channelName);

		// Initialize the Twitch client
		var credentials = new ConnectionCredentials(botUsername, oauthToken);
		var clientOptions = new ClientOptions
		{
			MessagesAllowedInPeriod = 750,
			ThrottlingPeriod = TimeSpan.FromSeconds(30)
		};

		var customClient = new WebSocketClient(clientOptions);
		client = new TwitchClient(customClient);

		// Hook up events
		client.OnConnected += OnConnected;
		client.OnDisconnected += OnDisconnected;
		client.OnMessageReceived += OnMessageReceived;
		client.OnConnectionError += OnConnectionError;
		client.OnUserBanned += OnUserBanned;

		// Initialize with credentials
		client.Initialize(credentials, channelName);

		// Connect to Twitch
		client.Connect();

		Console.WriteLine($"\n Connected to {channelName}'s chat. Press Enter to stop...\n");
		Console.ReadLine();

		// Stop the application
		isRunning = false;

		// Unhook events before disconnecting
		client.OnConnected -= OnConnected;
		client.OnDisconnected -= OnDisconnected;
		client.OnMessageReceived -= OnMessageReceived;
		client.OnConnectionError -= OnConnectionError;
		client.OnUserBanned -= OnUserBanned;

		// Disconnect and show results
		client.Disconnect();
		DisplayChatUsers();
	}

	private void OnConnected(object sender, OnConnectedArgs e)
	{
		Console.WriteLine($"Successfully connected to Twitch!");
	}

	private void OnDisconnected(object sender, OnDisconnectedEventArgs e)
	{
		Console.WriteLine("\nDisconnected from Twitch.");
	}

	private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
	{
		string username = e.ChatMessage.Username;

		// Don't add excluded or banned users
		if (excludedUsers.Contains(username) || bannedUsers.Contains(username))
		{
			return;
		}

		// Only stores unique usernames
		if (uniqueUsers.Add(username))
		{
			Console.WriteLine($"{username} joined the chat");
		}
	}

	private void OnUserBanned(object sender, OnUserBannedArgs e)
	{
		string bannedUsername = e.UserBan.Username;
		bannedUsers.Add(bannedUsername);

		// Remove from unique users if already added
		uniqueUsers.Remove(bannedUsername);

		Console.WriteLine($"{bannedUsername} was banned and removed from the list.");
	}

	private async void OnConnectionError(object sender, OnConnectionErrorArgs e)
	{
		Console.WriteLine($"Connection error: {e.Error.Message}");
		Console.WriteLine("Attempting to reconnect in 5 seconds...\n");

		// Retry connection after 5 seconds
		while (isRunning)
		{
			await Task.Delay(5000); // Wait 5 seconds

			try
			{
				if (!client.IsConnected)
				{
					client.Connect();
					Console.WriteLine("Reconnection attempt made.\n");
				}
				break;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Reconnection failed: {ex.Message}. Retrying in 5 seconds...\n");
			}
		}
	}

	private void LoadExcludedUsers()
	{
		string filePath = "excludedUsers.txt";

		if (!File.Exists(filePath))
		{
			Console.WriteLine($"Warning: {filePath} not found. Only streamer will be excluded.\n");
			return;
		}

		try
		{
			var lines = File.ReadAllLines(filePath);
			foreach (var line in lines)
			{
				string trimmedLine = line.Trim();
				if (!string.IsNullOrEmpty(trimmedLine))
				{
					excludedUsers.Add(trimmedLine);
				}
			}
			Console.WriteLine($"Loaded {excludedUsers.Count} excluded users from {filePath}.\n");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error reading {filePath}: {ex.Message}\n");
		}
	}

	private void DisplayChatUsers()
	{
		var sortedUsers = uniqueUsers.OrderBy(u => u).ToList();

		Console.WriteLine("\n========================================");
		Console.WriteLine($"Stream ended. Thank you to these {sortedUsers.Count} chatters!");
		Console.WriteLine("========================================\n");

		foreach (var user in sortedUsers)
		{
			Console.WriteLine($"  • {user}");
		}

		// Write to Text.txt file
		WriteUsersToFile(sortedUsers);

		Console.WriteLine("\n========================================");
		Console.WriteLine("\nPress Enter to close...");
		Console.ReadLine();
	}

	private void WriteUsersToFile(List<string> users)
	{
		string filePath = "Text.txt";

		try
		{
			// WriteAllLines overwrites existing file with new content
			File.WriteAllLines(filePath, users);
			Console.WriteLine($"User list saved to {filePath}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error writing to file: {ex.Message}");
		}
	}
}
