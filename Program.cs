using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
        Console.WriteLine(arg);
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
			Console.WriteLine($"[{m.CreatedAt}]{m.Author.Username}: {pingReplace(m.Content)}");
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
		if (arg.Reference != null && arg.Reference.MessageId.IsSpecified)
        {
            var referencedMessage = await arg.Channel.GetMessageAsync(arg.Reference.MessageId.Value);
            Console.Write($"[{arg.CreatedAt}]{arg.Author.Username} is responding to \"{referencedMessage.Content}\" by {referencedMessage.Author.Username}: {pingReplace(arg.Content)}");
        } else {
			Console.Write($"[{arg.CreatedAt}]{arg.Author.Username}: {pingReplace(arg.Content)}");
		}
		
		Console.WriteLine("");
		
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
			
			switch (command){
				case "info":
					await arg.Channel.SendMessageAsync("The bot is currently online!");
					break;
				case "ping":
					await arg.Channel.SendMessageAsync($"pong");
					break;
			}
        }
    }
	
	private static async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
	{
		// If the message was not in the cache, downloading it will result in getting a copy of `after`.
		var message = await before.GetOrDownloadAsync();
		Console.WriteLine($"[{after.EditedTimestamp}]{after.Author.Username} edited: {message} -> {pingReplace(after.Content)}");
	}
	
	private static async Task MessageDeleted(Cacheable<IMessage, ulong> before, Cacheable<IMessageChannel, ulong> channel)
    {
        var message = await before.GetOrDownloadAsync();
        Console.WriteLine($"[{message.Timestamp}]{message.Author.Username} deleted: {pingReplace(message.Content)}");
    }
	
	 private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        // Log when a reaction is added to a message
		var message = await cache.GetOrDownloadAsync();
        Console.WriteLine($"[{message.Timestamp}]{reaction.User.Value.Username} reacted to \"{pingReplace(message.Content)}\" by {message.Author.Username}: {reaction.Emote.ToString()}");
    }

    private static async Task ReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        // Log when a reaction is removed from a message
		var message = await cache.GetOrDownloadAsync();
        Console.WriteLine($"[{message.Timestamp}]{reaction.User.Value.Username} removed a reaction from \"{pingReplace(message.Content)}\" by {message.Author.Username}: {reaction.Emote.ToString()}");
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
					writing = false;
					lines = 0;
					Console.SetCursorPosition(0, Console.CursorTop);
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
		
		for(int i = 0; i < input.Count(); i++){
			if(input[i] == '<' && input[i + 1] == '@'){
				string u = "";
				int j = i + 2;
				while(true){
					if(char.IsDigit(input[j])){
						u += input[j];
					} else if(input[j] == '>'){
						break;
					} else {
						throw new Exception("Incorrect formatted message");
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
		
		return new string(output.ToArray());
	}
}