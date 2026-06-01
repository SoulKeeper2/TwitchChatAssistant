using System;
using System.Collections.Generic;
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
	private string channelName;

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

		// Initialize with credentials
		client.Initialize(credentials, channelName);

		// Connect to Twitch
		client.Connect();

		Console.WriteLine($"\n Connected to {channelName}'s chat. Press Enter to stop...\n");
		Console.ReadLine();

		// Unhook events before disconnecting
		client.OnConnected -= OnConnected;
		client.OnDisconnected -= OnDisconnected;
		client.OnMessageReceived -= OnMessageReceived;
		client.OnConnectionError -= OnConnectionError;

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
		// Only stores unique usernames
		if (uniqueUsers.Add(e.ChatMessage.Username))
		{
			Console.WriteLine($"{e.ChatMessage.Username} joined the chat");
		}
	}

	private void OnConnectionError(object sender, OnConnectionErrorArgs e)
	{
		Console.WriteLine($"Connection error: {e.Error.Message}");
	}

	private void DisplayChatUsers()
	{
		Console.WriteLine("\n========================================");
		Console.WriteLine($"Stream ended. Thank you to these {uniqueUsers.Count} chatters!");
		Console.WriteLine("========================================\n");

		var sortedUsers = uniqueUsers.OrderBy(u => u).ToList();
		foreach (var user in sortedUsers)
		{
			Console.WriteLine($"  • {user}");
		}

		Console.WriteLine("\n========================================");
		Console.WriteLine("\nPress Enter to close...");
		Console.ReadLine();
	}
}
