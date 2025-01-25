using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Drawing;
using Pastel;

class Program
{
    private static DiscordSocketClient _client;
	
	private static bool writing = false;
	private static string input;
	private static int lines = 0;
	private static bool canWrite = false;

	private static ulong channelID;

    static async Task Main(string[] args)
    {
		DiscordSocketConfig _config = new DiscordSocketConfig { MessageCacheSize = 100, 
		AlwaysDownloadUsers = true, 
		GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMembers};
        _client = new DiscordSocketClient(_config);

        _client.Log += Log;
		_client.Ready += Ready;
		
		string token;
		if(File.Exists("token.txt")){
			token = File.ReadAllText("token.txt");
		} else {
			throw new Exception("Token File couldn't be reached");
		}

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;
		_client.MessageUpdated += MessageUpdated;
		_client.MessageDeleted += MessageDeleted;
		_client.ReactionAdded += ReactionAdded;
        _client.ReactionRemoved += ReactionRemoved;
		
		if(File.Exists("channel.txt")){
			channelID = (ulong) Int64.Parse(File.ReadAllText("channel.txt"));
		} else {
			throw new Exception("Channel File couldn't be reached");
		}
		
		_ = Task.Run(async () => await ConsoleInput());
		
        // Block the program until it is closed
        await Task.Delay(-1);
    }

    private static Task Log(LogMessage arg)
    {
        Console.WriteLine(arg.ToString().Pastel(System.Drawing.Color.FromArgb(198, 100, 93)));
        return Task.CompletedTask;
    }
	
	private static async Task Ready(){

        int messageLimit = 30;

        // Get the channel
        ITextChannel channel = _client.GetChannel(channelID) as ITextChannel;

        IEnumerable<IMessage> messages = await channel.GetMessagesAsync(messageLimit).FlattenAsync();

        // Process and display the messages in reverse order of download (order of writing them)
		for(int i = 0; i < messages.Count(); i++){
			IMessage m = messages.ElementAt(messages.Count()-1-i);
			await writeMessage(m);
		}
		Console.WriteLine();
		canWrite = true;
	}

    private static async Task MessageReceived(SocketMessage arg)
    {	
		if(writing){
			Console.SetCursorPosition(0, Console.CursorTop-lines);
		}
		
		// Log incoming messages to the console
		await writeMessage(arg);
		
		if(writing){
			Console.Write(input);
		}

        // Check if the message is from this bot
        if (arg.Author.Id == _client.CurrentUser.Id){
            return;
		}

        // Check if the message is a command and process
        if (arg.Content.StartsWith("!"))
        {
            string command = arg.Content.Substring("!".Length);
			
			await handleBotCommands(command, arg.Channel);
        }
    }
	
	private static async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
	{
		if(writing){
			Console.SetCursorPosition(0, Console.CursorTop-lines);
		}
		
		// If the message was not in the cache, downloading it will result in getting a copy of `after`.
		var message = await before.GetOrDownloadAsync();
		await writeMessageEdited(message, after);
		
		if(writing){
			Console.Write(input);
		}
	}
	
	private static async Task MessageDeleted(Cacheable<IMessage, ulong> before, Cacheable<IMessageChannel, ulong> channel)
    {
        if(writing){
			Console.SetCursorPosition(0, Console.CursorTop-lines);
		}
		
		var message = await before.GetOrDownloadAsync();
        await writeMessageDeleted(message);
		
		if(writing){
			Console.Write(input);
		}
    }
	
	 private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if(writing){
			Console.SetCursorPosition(0, Console.CursorTop-lines);
		}
		
		// Log when a reaction is added to a message
		var message = await cache.GetOrDownloadAsync();
        await writeReactionAdded(message, reaction);
		
		if(writing){
			Console.Write(input);
		}
	}

    private static async Task ReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if(writing){
			Console.SetCursorPosition(0, Console.CursorTop-lines);
		}
		
		// Log when a reaction is removed from a message
		var message = await cache.GetOrDownloadAsync();
        await writeReactionRemoved(message, reaction);
		
		if(writing){
			Console.Write(input);
		}
    }
	
	private static async Task ConsoleInput(){
		input = "";
		while(true){
			if(Console.KeyAvailable){
				if(!canWrite){
					continue;
				}
				var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
					Console.SetCursorPosition(0, Console.CursorTop - lines);
					lines = 0;
					writing = false;
					// Process the user's input
					var defaultChannel = _client.GetChannel((ulong) channelID) as ISocketMessageChannel;
                    if (defaultChannel != null & input.Length != 0)
					{
						await defaultChannel.SendMessageAsync($"{input}");
					}

                    // Clear the input
                    input = "";
                }
                else if (key.Key == ConsoleKey.Backspace && input.Length > 0)
                {
                    if(input.Length == 0){
						writing = false;
					}
					// Handle backspace
                    input = input.Substring(0, input.Length - 1);
					if(Console.CursorLeft != 0){
						Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
						Console.Write(" ");
						Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
					} else if(input.Length != 0){
						Console.SetCursorPosition(Console.WindowWidth - 1, Console.CursorTop - 1);
						Console.Write(" ");
						Console.SetCursorPosition(Console.WindowWidth - 1, Console.CursorTop - 1);
					}
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    writing = true;
					int o = Console.CursorTop;
                    input += key.KeyChar;
					Console.Write(key.KeyChar);
					int n = Console.CursorTop;
					if(n == o+1){
						lines++;
					}
                }
			}
		}
	}
	
	private static string pingReplace(string message){
		char[] input = message.ToCharArray();
		List<char> output = new List<char>();
		try{
			for(int i = 0; i < input.Length; i++){
				if(input[i] == '<' && input.Length > i + 1 && input[i + 1] == '@' && input.Length > i + 2 && input[i + 2] != '&'){
					string u = "";
					int j = i + 2;
					while(true){
						if(char.IsDigit(input[j])){
							u += input[j];
						} else if(input[j] == '>'){
							break;
						} else {
							throw new Exception("Incorrect formatted message: \"" + message + "\"");
						}
						j++;
					}
					i = j + 1;
					output.AddRange("<@".ToCharArray());
					SocketUser n = _client.GetUser((ulong) Int64.Parse(u));
					output.AddRange(n.Username.ToCharArray());
					output.Add('>');
				} else {
					output.Add(input[i]);
				}
			}
		} catch (Exception e){
			Console.WriteLine(e.ToString());
			return message;
		}
		return new string(output.ToArray());
	}
	
	private static string formatHour(DateTimeOffset? f){
		DateTimeOffset h = getNotNullableTime(f);
		DateTime l = h.LocalDateTime;
		return "[" + l.Hour + ":" + l.Minute + ":" + l.Second + "]";
	}
	
	private static string formatHour(DateTimeOffset h){
		DateTime l = h.LocalDateTime;
		return "[" + l.Hour + ":" + l.Minute + ":" + l.Second + "]";
	}
	
	private static void addHourUserMessage(ref string s, ref int l, DateTimeOffset? d, SocketUser u, string m){
		handleStringColor(ref s, ref l, formatHour(d), 163, 198, 173);
		handleStringColor(ref s, ref l, u.Username, 66, 151, 255);
		handleStringColor(ref s, ref l, m, 170, 219, 255);
	}
	
	private static void addHourUserMessage(ref string s, ref int l, DateTimeOffset? d, IUser u, string m){
		handleStringColor(ref s, ref l, formatHour(d), 163, 198, 173);
		handleStringColor(ref s, ref l, u.Username, 66, 151, 255);
		handleStringColor(ref s, ref l, m, 170, 219, 255);
	}
	
	private static async Task writeMessage(SocketMessage message){
		string s = "";
		int l = 0;
		handleStringColor(ref s, ref l, formatHour(message.CreatedAt), 163, 198, 173);
		handleStringColor(ref s, ref l, message.Author.Username, 66, 151, 255);
		
		if (message.Reference != null && message.Reference.MessageId.IsSpecified)
        {
            var referencedMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
			handleStringColor(ref s, ref l, " is answering to the message \"", 170, 219, 255);
			handleStringColor(ref s, ref l, referencedMessage.Content, 170, 219, 255);
			handleStringColor(ref s, ref l, "\" by ", 170, 219, 255);
			handleStringColor(ref s, ref l, referencedMessage.Author.Username, 170, 219, 255);
        }
		
		handleStringColor(ref s, ref l, ":", 170, 219, 255);
		
		if(Console.WindowWidth - l > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - l), 170, 219, 255);
		}
		
		handleStringColor(ref s, ref l, pingReplace(message.Content), 255, 255, 255);
		
		if(Console.WindowWidth - (l % Console.WindowWidth) > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - (l % Console.WindowWidth)), 255, 255, 255);
		}
		
		Console.Write(s);
	}
	
	private static async Task writeMessage(IMessage message){
		string s = "";
		int l = 0;
		handleStringColor(ref s, ref l, formatHour(message.CreatedAt), 163, 198, 173);
		handleStringColor(ref s, ref l, message.Author.Username, 66, 151, 255);
		
		if (message.Reference != null && message.Reference.MessageId.IsSpecified)
        {
            var referencedMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
			handleStringColor(ref s, ref l, " is answering to the message \"", 170, 219, 255);
			handleStringColor(ref s, ref l, referencedMessage.Content, 170, 219, 255);
			handleStringColor(ref s, ref l, "\" by ", 170, 219, 255);
			handleStringColor(ref s, ref l, referencedMessage.Author.Username, 170, 219, 255);
        }
		
		handleStringColor(ref s, ref l, ":", 170, 219, 255);
		
		if(Console.WindowWidth - l > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - l), 170, 219, 255);
		}
		
		handleStringColor(ref s, ref l, pingReplace(message.Content), 255, 255, 255);
		
		if(Console.WindowWidth - (l % Console.WindowWidth) > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - (l % Console.WindowWidth)), 255, 255, 255);
		}
		
		Console.Write(s);
	}
	
	private static async Task writeMessageEdited(IMessage before, SocketMessage after){
		string s = "";
		int l = 0;
		
		addHourUserMessage(ref s, ref l, after.EditedTimestamp, after.Author, " edited:");
		
		if(Console.WindowWidth - l > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - l), 170, 219, 255);
		}
		
		handleStringColor(ref s, ref l, pingReplace(before.Content), 255, 209, 105);
		handleStringColor(ref s, ref l, " -> ", 170, 219, 255);
		handleStringColor(ref s, ref l, pingReplace(after.Content), 255, 255, 255);
		
		if(Console.WindowWidth - (l % Console.WindowWidth) > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - (l % Console.WindowWidth)), 255, 255, 255);
		}
		
		Console.Write(s);
	}
	
	private static async Task writeMessageDeleted(IMessage message){
		string s = "";
		int l = 0;
		
		addHourUserMessage(ref s, ref l, message.EditedTimestamp, message.Author, " deleted:");
		
		if(Console.WindowWidth - l > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - l), 170, 219, 255);
		}
		
		handleStringColor(ref s, ref l, pingReplace(message.Content), 255, 255, 255);
		
		if(Console.WindowWidth - (l % Console.WindowWidth) > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - (l % Console.WindowWidth)), 255, 255, 255);
		}
		
		Console.Write(s);
	}
	
	private static async Task writeReactionAdded(IUserMessage message, SocketReaction reaction){
		string s = "";
		int l = 0;
		

		addHourUserMessage(ref s, ref l, message.EditedTimestamp, reaction.User.Value, " reacted to \"");
		
		handleStringColor(ref s, ref l, message.Content, 170, 219, 255);
		handleStringColor(ref s, ref l, "\" by ", 170, 219, 255);
		handleStringColor(ref s, ref l, message.Author.Username, 66, 151, 255);
		handleStringColor(ref s, ref l, ":", 170, 219, 255);
		
		if(Console.WindowWidth - l > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - l), 170, 219, 255);
		}
		
		s += reaction.Emote.ToString();
		handleStringColor(ref s, ref l, reaction.Emote.ToString(), 255, 255, 255);
		
		if(Console.WindowWidth - (l % Console.WindowWidth) > 0){
			handleStringColor(ref s, ref l, new string(' ', Console.WindowWidth - (l % Console.WindowWidth)), 255, 255, 255);
		}
		
		Console.Write(s);
	}
	
	private static async Task writeReactionRemoved(IUserMessage message, SocketReaction reaction){
		string s = "";
		s += formatHour(message.EditedTimestamp);
		s += reaction.User.Value.Username;
		
		s += " removed a reaction from \"";
		s += message.Content;
		s += "\" by ";
		s += message.Author.Username;
		s += ":";
		
		if(Console.WindowWidth - s.Length > 0){
			s += new string(' ', Console.WindowWidth - s.Length);
		}
		
		s += reaction.Emote.ToString();
		
		if(Console.WindowWidth - (s.Length % Console.WindowWidth) > 0){
			s += new string(' ', Console.WindowWidth - (s.Length % Console.WindowWidth));
		}
		
		Console.Write(s);
	}
	
	private static async Task handleBotCommands(string command, ISocketMessageChannel h){
		switch (command){
			case "info":
				await h.SendMessageAsync("The bot is currently online!");
				break;
			case "ping":
				await h.SendMessageAsync($"pong");
				break;
		}
	}
	
	private static DateTimeOffset getNotNullableTime(DateTimeOffset? d){
		if(d == null){
			return new DateTimeOffset();
		}
		return (DateTimeOffset) d;
	}
	
	private static void handleStringColor(ref string s, ref int l, string a, int R, int G, int B){
		s += a.Pastel(System.Drawing.Color.FromArgb(R, G, B));
		l += a.Length;
	}
}