using System;
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
			Console.WriteLine("Creating a blank config.json file...\n");

			var templateConfig = new Config
			{
				ChannelName = "",
				OAuthToken = "",
				BotUsername = ""
			};

			try
			{
				var options = new JsonSerializerOptions { WriteIndented = true };
				string json = JsonSerializer.Serialize(templateConfig, options);
				File.WriteAllText(filePath, json);
				Console.WriteLine($"Created {filePath}. Please fill in the required values.\n");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating {filePath}: {ex.Message}\n");
			}

			return new Config();
		}

		try
		{
			string json = File.ReadAllText(filePath);
			var config = JsonSerializer.Deserialize<Config>(json);
			return config ?? new Config();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error reading {filePath}: {ex.Message}\n");
			return new Config();
		}
	}
}
