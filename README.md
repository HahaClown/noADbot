# noADbot
Before dotnet run, in noADbot/ create data/ folder with {"settings.txt", "channels.txt", "modsIDs.txt", "phrases.txt", "links.txt", "bannedUsersIDs.txt"}.
In settings.txt write 5 strings:
nickname:botnickname
botid:userid
accesstoken:bottoken (Scopes: chat:read, chat:edit, channel:moderate, moderator:manage:banned_users)
clientid:clientid
consolelog:true
In channels.txt write strings with channels to connect nicknames.
In modsIDs.txt write strings with bot's moderators UserIDs.
In phrases.txt write strings with modified via FormatMessage(string) messages.
In links.txt write strings witch modified via FormatMessage(string) links.
In bannedUsersIDs.txt write strings with UserIDs of banned users for #joinme.

C# .net 7.0
