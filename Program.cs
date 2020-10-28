using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace dotnet_core_client
{
	class Program
	{
		static async Task Main(string[] args)
		{
			Console.WriteLine(">>> WebSocket Test <<<");
			//Console.WriteLine("Enter URL:");
			//var url = Console.ReadLine();
			var url = "ws://192.168.0.138:53262/api/websocket";
			Console.WriteLine("Connecting " + url + "...");

			await ConnectToWebsocketServerAsync(url);
		}

		public class WebSocketItem
		{
			public WebSocketCommand Command { get; set; }
			public string Name { get; set; }
			public string Message { get; set; }
		}

		public enum WebSocketCommand
		{
			Send
		}

		private static async Task ConnectToWebsocketServerAsync(string url)
		{
			using var webSocket = new ClientWebSocket();
			webSocket.Options.UseDefaultCredentials = true;

			try
			{
				await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);

				Console.WriteLine("webSocket state:" + webSocket.State);
				Console.WriteLine("Name:");
				var name = Console.ReadLine();

				MySendMessageAsync(webSocket, name);

				await MyReceiveMessageAsync(webSocket);
				await Task.Delay(1000);
				Console.WriteLine("Done!");
				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex.Message);
			}

			// await webSocket.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
		}

		static void MySendMessageAsync(ClientWebSocket webSocket, string name)
		{
			AutoResetEvent autoResetEvent = new AutoResetEvent(false);
			ConcurrentQueue<string> sendQ = new ConcurrentQueue<string>();

			_ = WebSocketSendAsync(webSocket, autoResetEvent, sendQ);

			Task.Run(() =>
			{
				for (; ; )
				{
					Console.WriteLine("Send What:");
					var message = Console.ReadLine();

					WebSocketItem webSocketItem = new WebSocketItem
					{
						Command = WebSocketCommand.Send,
						Name = name,
						Message = message
					};
					string jsonMsg = JsonSerializer.Serialize<WebSocketItem>(webSocketItem);
					sendQ.Enqueue(jsonMsg);
					autoResetEvent.Set();
				}
			});
		}

		static async Task WebSocketSendAsync(ClientWebSocket webSocket, AutoResetEvent autoResetEvent, ConcurrentQueue<string> sendQ)
		{
			await Task.Yield();

			for (; ; )
			{
				autoResetEvent.WaitOne();

				//await Task.Delay(5000);

				while (sendQ.TryDequeue(out var jsonMsg))
				{
					var buffer = new ArraySegment<Byte>(Encoding.ASCII.GetBytes(jsonMsg));
					await webSocket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
				}
			}
		}

		static async Task MyReceiveMessageAsync(ClientWebSocket webSocket)
		{
			await Task.Yield();

			var receiveBuffer = new ArraySegment<Byte>(new byte[100]);

			while (webSocket.State == WebSocketState.Open)
			{
				using var ms = new MemoryStream();
				WebSocketReceiveResult result;

				try
				{
					do
					{
						result = await webSocket.ReceiveAsync(receiveBuffer, CancellationToken.None);
						ms.Write(receiveBuffer.Array, receiveBuffer.Offset, result.Count);
					} while (!result.EndOfMessage);

					ms.Seek(0, SeekOrigin.Begin);

					if (result.MessageType == WebSocketMessageType.Text)
					{
						using var reader = new StreamReader(ms, Encoding.UTF8);
						string message = reader.ReadToEnd();
						Console.WriteLine(message);
					}
					else if (result.MessageType == WebSocketMessageType.Close)
					{
						Console.WriteLine("Connection Closed!");
						await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
					}
				}
				catch (WebSocketException ex)
				{
					Console.WriteLine("server disconnected!!!");
				}
			}
		}
	}
}
