using Xunit;
using Xunit.Abstractions;

namespace shared.Tests {
    public class LoggerBridgeImpl : ILoggerBridge {
        // Reference https://xunit.net/docs/capturing-output.html
        private readonly ITestOutputHelper _logger;

        public LoggerBridgeImpl(ITestOutputHelper logger) {
            _logger = logger;
        }

        void ILoggerBridge.LogError(string str, Exception ex) {
            _logger.WriteLine(str + "\nex:" + ex.Message);
        }

        void ILoggerBridge.LogInfo(string str) {
            _logger.WriteLine(str);
        }

        void ILoggerBridge.LogWarn(string str) {
            _logger.WriteLine(str);
        }
    }
}
