using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogWatch.Features.Formats {
    public class OdisysLogFormat : ILogFormat {
        public OdisysLogFormat() {
            this.Encoding = Encoding.GetEncoding("ISO-8859-1");
        }

        public Encoding Encoding { get; private set; }

        public string NewLine { get; set; }

        public Record DeserializeRecord(ArraySegment<byte> segment) {
            var line = this.Encoding.GetString(segment.Array, segment.Offset, segment.Count);
            try
            {
                // 2015-01-12 14:21:00.584_892 [1] [OMS] (INFO) This is a message
                string[] parts = line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                var dateTimeStr = parts[0] + " " + parts[1].Split(new string[] { "_" }, StringSplitOptions.RemoveEmptyEntries)[0];
                StringBuilder message = new StringBuilder();
                for (int i = 5; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (i == parts.Length - 1)
                    {
                        part = part.Replace(NewLine, "");
                    }
                    message.Append(part);
                    if (i != parts.Length - 1)
                    {
                        message.Append(" ");
                    }
                }
                Record record = new Record
                {
                    Timestamp = ToDateTime(dateTimeStr),
                    Level = ToLogLevel(parts[4]),
                    Logger = ToLoggerName(parts[3]),
                    Thread = ToThreadId(parts[2]),
                    Message = message.ToString()
                };
                return record;
            }
            catch
            {
                return new Record { Message = line };
            }
        }

        private string ToThreadId(string logger)
        {
            return logger.Replace("[", "").Replace("]", "");
        }

        private string ToLoggerName(string logger)
        {
            return logger.Replace("[", "").Replace("]", "");
        }

        private DateTime ToDateTime(string dateTimeStr)
        {
            DateTime dateTime;
            DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dateTime);
            return dateTime;
        }

        private LogLevel ToLogLevel(string logLevel)
        {
            switch (logLevel)
            {
                case "(INFO)":
                    return LogLevel.Info;
                case "(ERROR)":
                    return LogLevel.Error;
                case "(WARNING)":
                    return LogLevel.Warn;
                default:
                    return LogLevel.Debug;
            }
        }

        public async Task<long> ReadSegments(
            IObserver<RecordSegment> observer,
            Stream stream,
            CancellationToken cancellationToken) {
                NewLine = DetectNewLine(stream);
            var offset = stream.Position;
            var newLineBytesCount = this.Encoding.GetByteCount(NewLine);

            using (var reader = new StreamReader(stream, this.Encoding, false, 4096 * 12, true))
                while (true) {
                    var line = await reader.ReadLineAsync();

                    if (line == null)
                        return offset;

                    if (line.Length == 0)
                        continue;

                    var length = this.Encoding.GetByteCount(line) + newLineBytesCount;

                    observer.OnNext(new RecordSegment(offset, length));

                    offset += length;
                }
        }

        private string DetectNewLine(Stream stream)
        {
            // Default is Linux/Unix
            var newLine = "\n";
            long initialPosition = stream.Position;
            try
            {
                char prevChar = '\0';
                for (int i = 0; i < 10000; i++)
                {
                    int b;
                    if ((b = stream.ReadByte()) == -1) break;

                    char curChar = (char)b;

                    if (curChar == '\n')
                    {
                        // Only Windows and Linux/Unix
                        newLine = prevChar == '\r' ? "\r\n" : "\n";
                        break;
                    }
                    prevChar = curChar;
                }
            }
            finally
            {
                stream.Position = initialPosition;
            }
            return newLine;
        }

        public bool CanRead(Stream stream) {
            return true;
        }
    }
}