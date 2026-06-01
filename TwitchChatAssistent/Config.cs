using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class Config
{
	public string ChannelName { get; set; }
	public string OAuthToken { get; set; }
	public string BotUsername { get; set; }

	public static Config Load()
	{
		string filePath = "config.json";

		if (!File.Exists(filePath))
		{
			Console.WriteLine($"Error: {filePath} not found!");
			Environment.Exit(1);
		}

		string json = File.ReadAllText(filePath);
		var config = JsonSerializer.Deserialize<Config>(json);

		return config ?? new Config();
	}
}
