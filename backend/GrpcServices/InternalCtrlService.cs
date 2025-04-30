using backend.Battle;
using Grpc.Core;
using System;

namespace backend.Services {
    public class InternalCtrlService : InternalCtrl.InternalCtrlBase {
        private readonly ILogger<InternalCtrlService> _logger;

        public InternalCtrlService(ILogger<InternalCtrlService> logger) {
            _logger = logger;
        }

        public override Task<GcResp> Gc(GcReq request, ServerCallContext context) {
            var stGc = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            _logger.LogInformation("Received GcReq, starting...");
            GC.Collect();       
            var elapsedMillis = (DateTimeOffset.Now.ToUnixTimeMilliseconds() - stGc);
            _logger.LogInformation($"Finished GcReq, elapsedMillis={elapsedMillis}");
            return Task.FromResult(new GcResp {
                UsedMillis = elapsedMillis 
            });
        }
    }
}
