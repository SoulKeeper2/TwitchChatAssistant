# Twitch Chat Assistant

A bot that monitors Twitch chat in real-time and maintains a list of users who join your channel.

## Table of Contents
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [License](#license)

## Installation
1. Clone this repository
2. Open the project in Visual Studio
3. Build the solution
4. Run the console app

## Configuration
Before running the bot, configure `config.json` with your credentials:

```json
{
  "ChannelName": "Target_Channel",
  "OAuthToken": "Bot_OAuthToken",
  "BotUsername": "Your_Bots_UserName"
}
```
- Note that the `config.json` file has to be in the same directory as the project.

## Usage
1. Start the console app, that's really it.
   - If all goes according as planned the bot connects and will listen to the channel's chat while adding the joining users to a list.
   - You can quit the app and get the user list by pressing `Enter`.

## License
This project is licensed under the MIT License
