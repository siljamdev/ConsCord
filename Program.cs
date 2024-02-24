using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Threading.Tasks;

class Program
{
    private static DiscordSocketClient _client;
	private static bool writing = false;
	private static string input;
	private static long channelID;

    static async Task Main(string[] args)
    {
		DiscordSocketConfig _config = new DiscordSocketConfig { MessageCacheSize = 100, GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent};
        _client = new DiscordSocketClient(_config);

        _client.Log += Log;
		
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
		_client.ReactionAdded += ReactionAdded;
        _client.ReactionRemoved += ReactionRemoved;
		
		if(File.Exists("channel.txt")){
			channelID = Int64.Parse(File.ReadAllText("channel.txt"));
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

    private static async Task MessageReceived(SocketMessage arg)
    {
		if(writing){
			Console.SetCursorPosition(0, Console.CursorTop);
		}
		
		// Log incoming messages to the console
		string s = arg.Author.IsBot ? arg.Author.Username : arg.Author.GlobalName;
		if (arg.Reference != null && arg.Reference.MessageId.IsSpecified)
        {
            var referencedMessage = await arg.Channel.GetMessageAsync(arg.Reference.MessageId.Value);
			string u = referencedMessage.Author.IsBot ? referencedMessage.Author.Username : referencedMessage.Author.GlobalName;
            Console.WriteLine($"[{arg.CreatedAt}]{s} is responding to \"{referencedMessage.Content}\" by {u}: {arg.Content}");
        } else {
			Console.WriteLine($"[{arg.CreatedAt}]{s}: {arg.Content}");
		}
		
		if(writing){
			Console.Write(input);
		}

        // Check if the message is from the bot or another user
        if (arg.Author.Id == _client.CurrentUser.Id){
            return;
		}

        // Check if the message is a command from the console
        if (arg.Content.StartsWith("!"))
        {
            // Extract the command from the message
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
		Console.WriteLine($"[{after.EditedTimestamp}]{after.Author.GlobalName} Edited: {message} -> {after}");
	}
	
	 private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        // Log when a reaction is added to a message
		var message = await cache.GetOrDownloadAsync();
        Console.WriteLine($"[{message.Timestamp}]Reaction added to message \"{message.Content}\" by {reaction.User.Value.GlobalName}: {reaction.Emote.ToString()}");
    }

    private static async Task ReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        // Log when a reaction is removed from a message
		var message = await cache.GetOrDownloadAsync();
        Console.WriteLine($"[{message.Timestamp}]Reaction removed from message \"{message.Content}\" by {reaction.User.Value.GlobalName}: {reaction.Emote.ToString()}");
    }
	
	private static async Task ConsoleInput(){
		input = "";
		while(true){
			if(Console.KeyAvailable){
				var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
					writing = false;
					Console.SetCursorPosition(0, Console.CursorTop);
					// Process the user's input
					var defaultChannel = _client.GetChannel((ulong) channelID) as ISocketMessageChannel;
                    if (defaultChannel != null)
					{
						await defaultChannel.SendMessageAsync($"{input}");
					}

                    // Clear the input
                    input = string.Empty;
                }
                else if (key.Key == ConsoleKey.Backspace && input.Length > 0)
                {
                    if(input.Length == 0){
						writing = false;
					}
					// Handle backspace
                    input = input.Substring(0, input.Length - 1);
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    writing = true;
					// Append the pressed key to the input
                    input += key.KeyChar;
					Console.Write(key.KeyChar);
                }
			}
		}
	}
}