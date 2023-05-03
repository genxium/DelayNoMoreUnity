using System;

namespace shared {
    public interface ILoggerBridge {
        public void LogInfo(string str);
        public void LogWarn(string str);
        public void LogError(string str, Exception ex);
    }
}
