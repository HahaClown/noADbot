using TwitchLib.Client.Events;
namespace noADbot {
    public class Command {
        public string name {get;}
        public int cooldown {get;}
        public string description {get;}
        public Dictionary<string, DateTime> lastUses {get; set;}
        public OnChatCommandReceivedArgs? args {get; set;}
        public Command(string commandName, int commandCooldown, string commandDescription) {
            name = commandName;
            cooldown = commandCooldown;
            description = commandDescription;
        }
    }
    public class Settings {
        public string? nickname {get;}
        public string? oauthToken {get;}
        public bool isConsoleLogging {get;}
        public Settings(string[] settings) {
            foreach(string parameter in settings) {
                //[0] - key, [1] - value.
                string[] splitedParameter = parameter.Split(":");

                switch(splitedParameter[0]) {
                    case "nickname":
                        nickname = splitedParameter[1];
                        break;
                    case "oauthtoken":
                        oauthToken = splitedParameter[1];
                        break;
                    case "consolelog":
                        if(splitedParameter[1] == "true") isConsoleLogging = true;
                        else isConsoleLogging = false;
                        break;
                }
            }
        }
    }
}