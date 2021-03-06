﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ICSharpCode.AvalonEdit.Document;
using LogWatch.Annotations;

namespace LogWatch.Features.Formats {
    public class LexPresetViewModel : ViewModelBase {
        private readonly LexCompiler compiler;
        private readonly LexLogFormat format;

        private bool isBusy;
        private bool isCompiled;
        private Stream logStream;
        private string logText;
        private string name;
        private string output;
        private bool isChanged;

        public LexPresetViewModel() {
            this.CommonCode = new TextDocument();
            this.SegmentCode = new TextDocument();
            this.RecordCode = new TextDocument();

            this.SegmentCodeCompletion = new LexCodeCompletionData[0];
            this.RecordCodeCompletion = new LexCodeCompletionData[0];

            this.CommonCode.TextChanged += (sender, args) => {
                this.IsCompiled = false;
                this.IsChanged = true;
                this.SegmentCodeCompletion = this.CreateCodeCompletion(this.SegmentCode.Text);
                this.RecordCodeCompletion = this.CreateCodeCompletion(this.RecordCode.Text);
            };

            this.SegmentCode.TextChanged += (sender, args) => {
                this.IsCompiled = false;
                this.IsChanged = true;
                this.SegmentCodeCompletion = this.CreateCodeCompletion(this.SegmentCode.Text);
            };

            this.RecordCode.TextChanged += (sender, args) => {
                this.IsCompiled = false;
                this.IsChanged = true;
                this.RecordCodeCompletion = this.CreateCodeCompletion(this.RecordCode.Text);
            };

            this.RunCommand = new RelayCommand(this.Preview, () => this.isBusy == false);

            this.SaveAndCloseCommand = new RelayCommand(
                () => EditCompleted(), 
                () => this.IsCompiled && !this.IsBusy);

            if (this.IsInDesignMode) {
                this.Name = "Test preset";

                this.CommonCode.Text =
                    "timestamp [^;\\r\\n]+\n" +
                    "level     [^;\\r\\n]+\n" +
                    "logger    [^;\\r\\n]+\n" +
                    "message   [^;\\r\\n]+\n" +
                    "exception [^;\\r\\n]*";

                this.SegmentCode.Text =
                    "record {timestamp}[;]{message}[;]{logger}[;]{level}[;]{exception}\\r\\n\n" +
                    "%%\n" +
                    "{record} Segment();";

                this.RecordCode.Text =
                    "%x MATCHED_TIMESTAMP\n" +
                    "%x MATCHED_MESSAGE\n" +
                    "%x MATCHED_LEVEL\n" +
                    "%x MATCHED_LOGGER\n" +
                    "%%\n" +
                    "<INITIAL>{timestamp} Timestamp = yytext; BEGIN(MATCHED_TIMESTAMP);\n" +
                    "<MATCHED_TIMESTAMP>{message} this.Message = yytext; BEGIN(MATCHED_MESSAGE);\n" +
                    "<MATCHED_MESSAGE>{logger} this.Logger = yytext; BEGIN(MATCHED_LOGGER);\n" +
                    "<MATCHED_LOGGER>{level} this.Level = yytext; BEGIN(MATCHED_LEVEL);\n" +
                    "<MATCHED_LEVEL>{exception} this.Exception = yytext; BEGIN(INITIAL);";
                return;
            }

            this.format = new LexLogFormat();
            this.compiler = new LexCompiler();

            this.Name = "(Current Preset)";
        }

        public Action EditCompleted { get; set; }

        public IReadOnlyCollection<LexCodeCompletionData> RecordCodeCompletion { get; private set; }
        public IReadOnlyCollection<LexCodeCompletionData> SegmentCodeCompletion { get; private set; }

        public bool IsChanged {
            get { return this.isChanged; }
            set {
                if (value.Equals(this.isChanged))
                    return;
                this.isChanged = value;
                this.OnPropertyChanged();
            }
        }

        public LexLogFormat Format {
            get { return this.format; }
        }

        public Stream LogStream {
            get { return this.logStream; }
            set {
                this.logStream = value;

                this.LoadLogText();
            }
        }

        public bool IsBusy {
            get { return this.isBusy; }
            set {
                if (value.Equals(this.isBusy))
                    return;

                this.isBusy = value;
                this.OnPropertyChanged();
                this.RunCommand.RaiseCanExecuteChanged();
            }
        }

        public TextDocument CommonCode { get; set; }
        public TextDocument SegmentCode { get; set; }
        public TextDocument RecordCode { get; set; }

        public string LogText {
            get { return this.logText; }
            set {
                if (value == this.logText)
                    return;
                this.logText = value;
                this.OnPropertyChanged();
            }
        }

        public string Output {
            get { return this.output; }
            set {
                if (value == this.output)
                    return;
                this.output = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsCompiled {
            get { return this.isCompiled; }
            set {
                if (value.Equals(this.isCompiled))
                    return;
                this.isCompiled = value;
                this.SaveAndCloseCommand.RaiseCanExecuteChanged();
                this.OnPropertyChanged();
            }
        }

        public RelayCommand RunCommand { get; set; }

        public string Name {
            get { return this.name; }
            set {
                if (value == this.name)
                    return;
                this.name = value;
                this.IsChanged = true;
                this.OnPropertyChanged();
            }
        }

        private IReadOnlyCollection<LexCodeCompletionData> CreateCodeCompletion(string code) {
            var lines =
                from line in string.Concat(this.CommonCode.Text, Environment.NewLine, code).Split('\r', '\n')
                where !string.IsNullOrEmpty(line)
                select line;

            lines = lines.ToArray();

            return new[] {
                from line in lines
                let defMatch = Regex.Match(line, @"^(?<DefinitionName>[a-zA-Z_]+)\s+\S+")
                where defMatch.Success
                let definitionValue = defMatch.Groups["DefinitionName"].Value
                select new LexCodeCompletionData(definitionValue),
                from line in lines
                let stateMatch = Regex.Match(line, @"^%x\s+(?<StateName>[a-zA-Z_]+)")
                where stateMatch.Success
                let stateName = stateMatch.Groups["StateName"].Value
                select new LexCodeCompletionData(stateName),
                new[] {
                    new LexCodeCompletionData("Segment", "Segment();"),
                    new LexCodeCompletionData("BEGIN"),
                    new LexCodeCompletionData("INITIAL"),
                    new LexCodeCompletionData("Text"),
                    new LexCodeCompletionData("Timestamp"),
                    new LexCodeCompletionData("Level"),
                    new LexCodeCompletionData("Thread"),
                    new LexCodeCompletionData("Logger"),
                    new LexCodeCompletionData("Message"),
                    new LexCodeCompletionData("Exception"),
                    new LexCodeCompletionData("TextAsTimestamp"),
                    new LexCodeCompletionData("%x", "%x "),
                    new LexCodeCompletionData("%%")
                }
            }.Aggregate(Enumerable.Concat)
             .ToArray();
        }

        private async void LoadLogText() {
            this.IsBusy = true;

            this.logStream.Position = 0;

            var previewSize = 4096;

            using (var reader = new StreamReader(this.logStream, Encoding.UTF8, true, previewSize, true)) {
                var text = new StringBuilder();

                for (var i = 0; i < 20 && !reader.EndOfStream; i++)
                    text.AppendLine(await reader.ReadLineAsync());

                this.LogText = text.ToString();
            }

            this.IsBusy = false;
        }

        private async void Preview() {
            this.IsBusy = true;
            this.IsCompiled = false;
            this.Output = "Running...";

            var outputBuilder = new StringBuilder();

            var scanners = await this.Compile(outputBuilder);

            if (scanners == null || scanners.RecordsScannerType == null || scanners.SegmentsScannerType == null) {
                this.IsBusy = false;
                this.Output = outputBuilder.ToString();
                return;
            }

            this.format.SegmentsScannerType = scanners.SegmentsScannerType;
            this.format.RecordsScannerType = scanners.RecordsScannerType;

            var stream = this.logStream;

            stream.Position = 0;
            outputBuilder.Clear();

            try {
                this.IsCompiled = await this.Execute(stream, outputBuilder);
            } catch {
                this.IsBusy = false;
                this.Output = outputBuilder.ToString();
                throw;
            }

            this.Output = outputBuilder.ToString();

            this.IsBusy = false;
        }

        private async Task<bool> Execute(Stream stream, StringBuilder outputBuilder) {
            var segments = new List<RecordSegment>();
            var cts = new CancellationTokenSource();
            var subject = new Subject<RecordSegment>();

            subject.Subscribe(segment => {
                if (segments.Count == 5) {
                    cts.Cancel();
                    return;
                }

                segments.Add(segment);
            });

            this.format.Diagnostics = new StringWriter(outputBuilder);

            try {
                await this.format.ReadSegments(subject, stream, cts.Token);
            } catch (Exception exception) {
                outputBuilder.AppendLine(exception.ToString());
                return false;
            }

            var index = 1;

            foreach (var segment in segments) {
                stream.Position = segment.Offset;

                var buffer = new byte[segment.Length];

                await stream.ReadAsync(buffer, 0, buffer.Length);

                Record record;
                
                try {
                    record = this.format.DeserializeRecord(new ArraySegment<byte>(buffer));
                } catch (Exception exception) {
                    outputBuilder.AppendLine(exception.ToString());
                    return false;
                }

                outputBuilder.AppendFormat("Segment #{0} (offset: {1}, length: {2}): ", index, segment.Offset, segment.Length);
                outputBuilder.Append(Encoding.UTF8.GetString(buffer));
                outputBuilder.AppendLine();

                outputBuilder.AppendFormat("Record #{0}\n", index);

                if (record.Timestamp != null)
                    outputBuilder.AppendFormat("  Timestamp: {0}\n", record.Timestamp);

                if (record.Level != null)
                    outputBuilder.AppendFormat("  Level:     {0}\n", record.Level);

                if (!string.IsNullOrEmpty(record.Logger))
                    outputBuilder.AppendFormat("  Logger:    {0}\n", record.Logger);

                if (!string.IsNullOrEmpty(record.Message))
                    outputBuilder.AppendFormat("  Message:   {0}\n", record.Message);

                if (!string.IsNullOrEmpty(record.Exception))
                    outputBuilder.AppendFormat("  Exception: {0}\n", record.Exception);

                outputBuilder.AppendLine();

                index++;
            }

            return true;
        }

        private Task<LexCompiler.LexFormatScanners> Compile(StringBuilder outputBuilder) {
            var segmentsCode = new StringBuilder();
            segmentsCode.AppendLine(this.CommonCode.Text);
            segmentsCode.AppendLine();
            segmentsCode.AppendLine(this.SegmentCode.Text);

            var recordsCode = new StringBuilder();
            recordsCode.AppendLine(this.CommonCode.Text);
            recordsCode.AppendLine();
            recordsCode.AppendLine(this.RecordCode.Text);

            this.compiler.Diagnostics = new StringWriter(outputBuilder);

            return Task.Run(() => this.compiler.Compile(segmentsCode.ToString(), recordsCode.ToString()));
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            base.RaisePropertyChanged(propertyName);
        }

        public string[] Names { get; set; }

        public RelayCommand SaveAndCloseCommand { get;  set; }
    }
}