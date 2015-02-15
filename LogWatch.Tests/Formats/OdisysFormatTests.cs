using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using LogWatch.Features.Formats;
using Xunit;

namespace LogWatch.Tests.Formats {
    public class OdisysFormatTests {
        [Fact]
        public void ReadsSegmentLinux()
        {
            var stream = CreateStream("2015-01-12 14:21:00.584_892 [1] [OMS] (INFO) This is a message\n2015-01-12 14:21:00.584_892 [1] [OMS] (INFO) This is a message\n");

            var format = new OdisysLogFormat();

            var subject = new ReplaySubject<RecordSegment>();

            format.ReadSegments(subject, stream, CancellationToken.None).Wait();

            subject.OnCompleted();

            var segment = subject.ToEnumerable().FirstOrDefault();

            Assert.Equal(0, segment.Offset);
            Assert.Equal(stream.Length / 2, segment.Length);
        }

        [Fact]
        public void ReadsSegmentWindows()
        {
            var stream = CreateStream("2015-01-12 14:21:00.584_892 [1] [OMS] (INFO) This is a message\r\n2015-01-12 14:21:00.584_892 [1] [OMS] (INFO) This is a message\r\n");

            var format = new OdisysLogFormat();

            var subject = new ReplaySubject<RecordSegment>();

            format.ReadSegments(subject, stream, CancellationToken.None).Wait();

            subject.OnCompleted();

            var segment = subject.ToEnumerable().FirstOrDefault();

            Assert.Equal(0, segment.Offset);
            Assert.Equal(stream.Length / 2, segment.Length);
        }

        [Fact]
        public void DeserializesRecordLinux()
        {
            var format = new OdisysLogFormat
            {
                NewLine = "\n"
            };

            var bytes = format.Encoding.GetBytes("2015-01-12 14:21:00.584_892 [1] [Name] (INFO) This is a message\n");

            var record = format.DeserializeRecord(new ArraySegment<byte>(bytes));

            Assert.Equal(new DateTime(2015, 1, 12, 14, 21, 00, 584), record.Timestamp);
            Assert.Equal(LogLevel.Info, record.Level);
            Assert.Equal("This is a message", record.Message);
            Assert.Equal("Name", record.Logger);
            Assert.Equal("1", record.Thread);
        }

        [Fact]
        public void DeserializesRecordWindows()
        {
            var format = new OdisysLogFormat
            {
                NewLine = "\r\n"
            };

            var bytes = format.Encoding.GetBytes("2015-01-12 14:21:00.584_892 [1] [Name] (INFO) This is a message\r\n");

            var record = format.DeserializeRecord(new ArraySegment<byte>(bytes));

            Assert.Equal(new DateTime(2015, 1, 12, 14, 21, 00, 584), record.Timestamp);
            Assert.Equal(LogLevel.Info, record.Level);
            Assert.Equal("This is a message", record.Message);
            Assert.Equal("Name", record.Logger);
            Assert.Equal("1", record.Thread);
        }

       private static MemoryStream CreateStream(string content) {
            var bytes = new OdisysLogFormat().Encoding.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return stream;
        }
    }
}