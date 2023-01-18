using System.Text.RegularExpressions;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Events;
using NickBuhro.Translit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace noADbot {
    class Program {
        static void Main(string[] args) {
            var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices(x => {
                x.AddSingleton<Bot>();
            });
            var host = hostBuilder.Build();
            host.Services.GetRequiredService<Bot>().Initialize();
            host.WaitForShutdown();
        }
    }

    class Bot {
        private Settings settings = new Settings(File.ReadAllLines(Directory.GetCurrentDirectory() + "/data/settings.txt"));
        private DateTime startDate = DateTime.Now; 
        List<string> phrases = File.ReadAllLines(Directory.GetCurrentDirectory() + "/data/phrases.txt").ToList();
        List<string> links = File.ReadAllLines(Directory.GetCurrentDirectory() + "/data/links.txt").ToList();
        List<string> channels = File.ReadAllLines(Directory.GetCurrentDirectory() + "/data/channels.txt").ToList();
        List<string> modsIDs = File.ReadAllLines(Directory.GetCurrentDirectory() + "/data/modsIDs.txt").ToList();
        List<string> bannedUserIDs = File.ReadAllLines(Directory.GetCurrentDirectory() + "/data/bannedUsersIDs.txt").ToList();
        Dictionary<Command, Action<Command>> commands;
        List<string> commandsNames;
        TwitchClient client;
	
        public void Initialize() {
            ConnectionCredentials credentials = new ConnectionCredentials(settings.nickname, settings.oauthToken);
	        var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);

            client.OnMessageReceived += OnMessageReceived;
            client.OnConnected += OnConnected;
            client.OnJoinedChannel += OnJoinedChannel;
            client.OnLeftChannel += OnLeftChannel;
            client.OnDisconnected += OnDisconnected;
            client.OnChatCommandReceived += OnChatCommandReceived;

            client.Initialize(credentials);
            client.AddChatCommandIdentifier('#');
            client.RemoveChatCommandIdentifier('!');

            InitializeCommands();
            commandsNames = commands.Select(x => x.Key.name).ToList();
            
            client.Connect();
        }
        private void OnConnected(object sender, OnConnectedArgs args) {
            ConsoleLog("Connected to Twitch");
            for(int i = 0; i < channels.Count; i++) {
                client.JoinChannel(channels[i]);
            }
        }
        private void OnMessageReceived(object sender, OnMessageReceivedArgs args) {

            if(IsAd(args)) {
                client.SendMessage(args.ChatMessage.Channel, $"/ban {args.ChatMessage.Username} advertising");
                ConsoleLog($"{args.ChatMessage.Username} banned");
            }
        }
        private void OnJoinedChannel(object sender, OnJoinedChannelArgs args) {
            ConsoleLog($"Joined to {args.Channel}");
        }
        private void OnLeftChannel(object sender, OnLeftChannelArgs args) {
            ConsoleLog($"Left from {args.Channel}");
            if(channels.Contains(args.Channel)) client.JoinChannel(args.Channel);
        }
        private void OnDisconnected(object sender, OnDisconnectedEventArgs args) {
            ConsoleLog($"Disconnected, reconnecting...");
            client.Connect();
        }
        private void OnChatCommandReceived(object sender, OnChatCommandReceivedArgs args) {
            RefreshArgs(args);
            if(commandsNames.Contains(args.Command.CommandText)) {
                var currentDictionaryValue = commands.FirstOrDefault(x => x.Key.name == args.Command.CommandText);
                if((DateTime.Now - currentDictionaryValue.Key.lastUses[args.Command.ChatMessage.Channel]).TotalSeconds > currentDictionaryValue.Key.cooldown) currentDictionaryValue.Value.Invoke(currentDictionaryValue.Key);
            }
        }
        /// <summary>
        /// Returns is message contains ads.
        /// </summary>
        private bool IsAd(OnMessageReceivedArgs args) {
            string message = FormatMessage(args.ChatMessage.Message);
            return !args.ChatMessage.IsSubscriber && args.ChatMessage.UserType == TwitchLib.Client.Enums.UserType.Viewer && args.ChatMessage.IsFirstMessage && phrases.Any(x => message.Contains(x)) && links.Any(x => message.Contains(x));
        }
        /// <summary>
        /// Formats a chat message for easy handling.
        /// </summary>
        private string FormatMessage(string message)
        {
            message = Transliteration.CyrillicToLatin(message.ToLower().Replace(" ", string.Empty));
            return Regex.Replace(message, @"[^a-zA-Z]", "");
        }
        /// <summary>
        /// Initialize chat-bot commands.
        // </summary>
        private void InitializeCommands() {
            commands = new Dictionary<Command, Action<Command>>();
            Command currentCommand = new Command("ping", 10, "Pong! #ping");
            commands.Add(currentCommand, (command) => {
                client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, Pong! Uptime: {GetUptime()} in {client.JoinedChannels.Count} channels.");
                command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
            });
            currentCommand = new Command("join", 1, "Launches the bot in the specified channel. #join channel");
            commands.Add(currentCommand, (command) => {

                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0)  {
                    foreach(var currentChannel in command.args.Command.ArgumentsAsList) {
                        string channel = currentChannel.ToLower();
                        channels.Add(channel);
                        client.JoinChannel(channel);
                        foreach(var currentCommand in commands) {
                            currentCommand.Key.lastUses.Add(channel, DateTime.MinValue);
                        }
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/channels.txt", channels);
                    string result = "";
                    if(command.args.Command.ArgumentsAsList.Count == 1) result = $"@{command.args.Command.ChatMessage.Username}, channel added to list.";
                    else result = $"@{command.args.Command.ChatMessage.Username}, channels added to list.";
                    client.SendMessage(command.args.Command.ChatMessage.Channel, result);
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("leave", 1, "Turns off the bot in the specified channel. #leave channel");
            commands.Add(currentCommand, (command) => {

                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0)  {
                    string result = "";
                    if(command.args.Command.ArgumentsAsList.Count == 1) result = $"@{command.args.Command.ChatMessage.Username}, channel removed from list.";
                    else result = $"@{command.args.Command.ChatMessage.Username}, channels removed from list.";
                    client.SendMessage(command.args.Command.ChatMessage.Channel, result);
                    foreach(var currentChannel in command.args.Command.ArgumentsAsList) {
                        string channel = currentChannel.ToLower();
                        if(channels.Contains(channel)) {
                            channels.Remove(channel);
                            client.LeaveChannel(channel);
                            foreach(var currentCommand in commands) {
                                currentCommand.Key.lastUses.Remove(channel);
                            }
                        }
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/channels.txt", channels);
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("addmod", 1, "Adds the specified UserID to the list of moderators. #addmod 264630545");
            commands.Add(currentCommand, (command) => {

                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0)  {
                foreach(var id in command.args.Command.ArgumentsAsList) {
                    modsIDs.Add(id);
                }
                File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/modsIDs.txt", modsIDs);
                string result = "";
                if(command.args.Command.ArgumentsAsList.Count == 1) result = $"@{command.args.Command.ChatMessage.Username}, UserID added to list.";
                else result = $"@{command.args.Command.ChatMessage.Username}, UserIDs added to list.";
                client.SendMessage(command.args.Command.ChatMessage.Channel, result);
                command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("removemod", 1, "Removes the specified UserID to the list of moderators. #removemod 264630545");
            commands.Add(currentCommand, (command) => {

                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0)  {
                foreach(var id in command.args.Command.ArgumentsAsList) {
                    modsIDs.Remove(id);
                }
                File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/modsIDs.txt", modsIDs);
                string result = "";
                if(command.args.Command.ArgumentsAsList.Count == 1) result = $"@{command.args.Command.ChatMessage.Username}, UserID removed from list.";
                else result = $"@{command.args.Command.ChatMessage.Username}, UserIDs removed from list.";
                client.SendMessage(command.args.Command.ChatMessage.Channel, result);
                command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("help", 10, "Returns the description of the command. #help help");
            commands.Add(currentCommand, (command) => {

                if(command.args.Command.ArgumentsAsList.Count != 0) {
                    List<string> _descriptions = commands.Select(x => x.Key.description).ToList();
                    List<string> _commandNames = commands.Select(x => x.Key.name).ToList();
                    List<int> _commandCooldowns = commands.Select(x => x.Key.cooldown).ToList();
                    string _commandArgument = command.args.Command.ArgumentsAsList[0];
                    if(_commandNames.Contains(_commandArgument)) {
                        client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, Cooldown: {_commandCooldowns[_commandNames.IndexOf(_commandArgument)]}s. {_descriptions[_commandNames.IndexOf(_commandArgument)]}");
                    }
                    else {
                        client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, command not found.");
                    }
                }
                else {
                    client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, total {commands.Count} commands: {string.Join(", ", commands.Select(x => x.Key.name))}");
                }
                command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
            });
            currentCommand = new Command("addlink", 1, "Adds link to list of AD-links. #addlink example.horse");
            commands.Add(currentCommand, (command) => {

                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0) {
                foreach(var argument in command.args.Command.ArgumentsAsList) {
                    links.Add(FormatMessage(argument));
                }
                File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/links.txt", links);
                string result = "";
                if(command.args.Command.ArgumentsAsList.Count == 1) result = $"@{command.args.Command.ChatMessage.Username}, link added to list.";
                else result = $"@{command.args.Command.ChatMessage.Username}, links added to list.";
                client.SendMessage(command.args.Command.ChatMessage.Channel, result);
                command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("removelink", 1, "Removes link from list of AD-links. #removelink example.baby");
            commands.Add(currentCommand, (command) => { 

                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0) {
                    foreach(var link in command.args.Command.ArgumentsAsList) {
                        links.Remove(FormatMessage(link));
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/links.txt", links);
                    string result = "";
                    if(command.args.Command.ArgumentsAsList.Count == 1) result = $"@{command.args.Command.ChatMessage.Username}, link removed from list.";
                    else result = $"@{command.args.Command.ChatMessage.Username}, links removed from list.";
                    client.SendMessage(command.args.Command.ChatMessage.Channel, result);
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("addphrase", 1, "Adds a phrase to the list of possible ad bot phrases. #addphrase buy followers");
            commands.Add(currentCommand, (command) => {
                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0) {
                    phrases.Add(FormatMessage(command.args.Command.ArgumentsAsString));
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/phrases.txt", phrases);
                    client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, phrase added to list.");
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("removephrase", 1, "Removes a phrase from the list of possible ad bot phrases. #removephrase buy viewers");
            commands.Add(currentCommand, (command) => {
                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0) {
                    phrases.Remove(FormatMessage(command.args.Command.ArgumentsAsString));
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/phrases.txt", phrases);
                    client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, phrase removed from list.");
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("data", 60, "Return URL to HasteBin with bot's data. #data");
            commands.Add(currentCommand, async (command) => {
                string url = "https://hastebin.com/documents";
                var httpClient = new HttpClient();
                string result = $"Channels:\n" + string.Join($"\n", channels) + $"\n\nModerator's UserIDs:\n" + string.Join($"\n", modsIDs) + $"\n\nLinks:\n" + string.Join($"\n", links) + $"\n\nPhrases:\n" + string.Join($"\n", phrases) + $"\n\nBanned users IDs:\n" + string.Join($"\n", bannedUserIDs);
                var data = new StringContent(result, Encoding.UTF8, "text/plain");
                var response = await httpClient.PostAsync(url, data);
                var answer = response.Content.ReadAsStringAsync().Result;
                client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, bot's data here: https://hastebin.com/{answer.Replace(@"{""key"":""", string.Empty).Replace(@"""}", string.Empty)}");
                command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
            });
            currentCommand = new Command("joinme", 10, "Launches the bot in your channel. #joinme");
            commands.Add(currentCommand, (command) => {
                string channel = command.args.Command.ChatMessage.Username.ToLower();
                if(!bannedUserIDs.Contains(command.args.Command.ChatMessage.UserId) && !channels.Contains(channel)) {
                    channels.Add(channel);
                    client.JoinChannel(channel);
                    foreach(var currentCommand in commands) {
                        currentCommand.Key.lastUses.Add(channel, DateTime.MinValue);
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/channels.txt", channels);
                    client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, channel added to list.");
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("leaveme", 10, "Turns off the bot in your channel. #leaveme");
            commands.Add(currentCommand, (command) => {
                string channel = command.args.Command.ChatMessage.Username.ToLower();
                if(channel.Contains(channel)) {
                    channels.Remove(channel);
                    client.LeaveChannel(channel);
                    foreach(var currentCommand in commands) {
                        currentCommand.Key.lastUses.Remove(channel);
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/channels.txt", channels);
                    client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, channel removed from list.");
                    command.lastUses[command.args.Command.ChatMessage.Channel] = DateTime.Now;
                }
            });
            currentCommand = new Command("ban", 1, "Ban user for #joinme. #ban UserID");
            commands.Add(currentCommand, (command) => {
                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0) {
                    foreach(string id in command.args.Command.ArgumentsAsList) {
                        if(!bannedUserIDs.Contains(id)) {
                            bannedUserIDs.Add(id);
                        }
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/bannedUsersIDs.txt", bannedUserIDs);
                    if(command.args.Command.ArgumentsAsList.Count == 1) client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, user succesfuly banned.");
                    else client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, users succesfuly banned.");
                }
            });
            currentCommand = new Command("unban", 1, "Unban user for #joinme. #unban UserID");
            commands.Add(currentCommand, (command) => {
                if(modsIDs.Contains(command.args.Command.ChatMessage.UserId) && command.args.Command.ArgumentsAsList.Count != 0) {
                    foreach(string id in command.args.Command.ArgumentsAsList) {
                        if(bannedUserIDs.Contains(id)) {
                            bannedUserIDs.Remove(id);
                        }
                    }
                    File.WriteAllLines(Directory.GetCurrentDirectory() + "/data/bannedUsersIDs.txt", bannedUserIDs);
                    if(command.args.Command.ArgumentsAsList.Count == 1) client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, user succesfuly unbanned.");
                    else client.SendMessage(command.args.Command.ChatMessage.Channel, $"@{command.args.Command.ChatMessage.Username}, users succesfuly unbanned.");
                }
            });

            foreach(var command in commands) {
                command.Key.lastUses = channels.ToDictionary(key => key, value => DateTime.MinValue);
            }
        }
        private void RefreshArgs(OnChatCommandReceivedArgs args) {
            foreach(var command in commands) {
                command.Key.args = args;
            }
        }
        private void ConsoleLog(string log) {
            if(settings.isConsoleLogging) Console.WriteLine(log);
        }
        public string GetUptime() {
            TimeSpan uptimeSpan = DateTime.Now - startDate;
                string uptime = "";
                if(uptimeSpan.Days > 0) uptime += $"{uptimeSpan.Days}d";
                if(uptimeSpan.Hours > 0 && uptime.Contains("d")) uptime += $", {uptimeSpan.Hours}h";
                else if(uptimeSpan.Hours > 0) uptime += $"{uptimeSpan.Hours}h";
                if(uptimeSpan.Minutes > 0 && uptime.Contains("h")) uptime += $", {uptimeSpan.Minutes}m";
                else if(uptimeSpan.Minutes > 0) uptime += $"{uptimeSpan.Minutes}m";
                if(uptimeSpan.Seconds > 0 && uptime.Contains("m")) uptime += $", {uptimeSpan.Seconds}s";
                else if(uptimeSpan.Seconds > 0) uptime += $"{uptimeSpan.Seconds}s";
                return uptime;
        }
    }
}