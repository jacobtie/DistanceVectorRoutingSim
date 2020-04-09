using System;
using System.IO;
using System.Text;

namespace DistanceVectorRoutingSimNode.Logging
{
    public static class Logger
    {
        private static StringBuilder _sb = new StringBuilder();

        public static void Write(object message)
        {
            Console.Write(message.ToString());
            _sb.Append(message);
        }

        public static void WriteLine()
        {
            WriteLine("");
        }

        public static void WriteLine(object message)
        {
            Console.WriteLine(message.ToString());
            _sb.Append(message + "\n");
        }

        public static void Output(string name)
        {
            var fileName = Path.Combine("logs", $"{DateTime.Now.ToFileTimeUtc()}_node_{name}_log.txt");
            Write($"Writing to output file: {fileName}");
            using (var writer = new StreamWriter(fileName))
            {
                writer.Write(_sb.ToString());
            }
        }
    }
}
