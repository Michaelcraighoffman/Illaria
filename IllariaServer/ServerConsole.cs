using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IllariaServer
{
    public class ServerConsole
    {
        List<ConsoleMessage> messages = new List<ConsoleMessage>();
        int MaxMessages = 20;
        StringBuilder input = new StringBuilder();
        Thread inputThread;
        Func<string, bool> CommandCallback;
        bool ConsoleActive;

        public void Write(string text, ConsoleColor color, params object[] list)
        {
            var finishedMessage = String.Format(text, list);

            messages.Add(new ConsoleMessage() { Message = finishedMessage, Color = color });

            Refresh();
        }

        public void WriteInfo(string text, params object[] list)
        {
            Write(text, ConsoleColor.Gray, list);          
        }
        public void WriteWarn(string text, params object[] list)
        {
            Write(text, ConsoleColor.Yellow, list);
        }
        public void WriteError(string text, params object[] list)
        {
            Write(text, ConsoleColor.Red, list);
        }

        public void Start(int maxMessages, Func<string,bool> callback)
        {
            MaxMessages = maxMessages;
            CommandCallback = callback;
            ConsoleActive = true;
            inputThread = new Thread(this.InputLoop);
            inputThread.Start();
        }

        public void Stop()
        {
            ConsoleActive = false;
        }

        public void InputLoop()
        {
            while(ConsoleActive)
            {
                while (ConsoleActive)
                {
                    if (!Console.KeyAvailable)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter) break;
                    if (key.Key == ConsoleKey.Backspace && input.Length > 0) input.Remove(input.Length - 1, 1);
                    else if (key.Key != ConsoleKey.Backspace) input.Append(key.KeyChar);
                    Refresh();
                }
                var commandText = input.ToString();
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    input = new StringBuilder();
                    CommandCallback(commandText);
                }
            }
        }

        public void Refresh()
        {
            if (messages.Count > MaxMessages)
            {
                messages.RemoveRange(0, messages.Count - MaxMessages);
            }
            Console.Clear();
            for (int i = 0; i < messages.Count; i++)
            {
                Console.ForegroundColor = messages[i].Color;
                Console.WriteLine(messages[i].Message);
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            Console.Write(input.ToString());
        }
    }

    public class ConsoleMessage
    {
        public string Message { get; set; }
        public ConsoleColor Color { get; set; }
    }
}
