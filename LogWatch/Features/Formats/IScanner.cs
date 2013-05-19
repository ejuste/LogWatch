﻿using System;
using System.IO;
using System.Threading;

namespace LogWatch.Features.Formats {
    public interface IScanner {
        DateTime? Timestamp { get; }
        string Level { get; }
        string Logger { get; }
        string Message { get; }
        string Exception { get; }
        string Thread { get; }

        Action<long, int> OffsetCallback { set; }
        Stream Source { set; }

        int Parse(CancellationToken cancellationToken);
        void Reset();
        void Begin();
    }
}