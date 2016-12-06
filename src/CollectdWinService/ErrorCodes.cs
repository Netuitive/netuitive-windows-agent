using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloombergFLP.CollectdWin
{
    public static class ErrorCodes
    {
        public static readonly int ERROR_READ_EXCEEDED_CYCLE_TIME = 1;
        public static readonly int ERROR_WRITE_EXCEEDED_CYCLE_TIME = 2;
        public static readonly int ERROR_EXCEEDED_MAX_QUEUE_LENGTH = 3;
        public static readonly int ERROR_UNHANDLED_EXCEPTION = 4;
        public static readonly int ERROR_CONFIGURATION_EXCEPTION = 5;
    }
}
