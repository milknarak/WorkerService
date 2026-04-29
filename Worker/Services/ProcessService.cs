using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Worker.Mappers;

namespace Worker.Services
{
    public class ProcessService
    {
        private readonly TransactionService _transactionService;
        private readonly SapService _sapService;
        private readonly ILogger<ProcessService> _logger;

        public ProcessService(
            TransactionService transactionService,
            SapService sapService,
            ILogger<ProcessService> logger)
        {
            _transactionService = transactionService;
            _sapService = sapService;
            _logger = logger;
        }

        public async Task Process()
        {
            var groups = await _transactionService.GetPendingGroups();

            if(groups == null || !groups.Any())
            {
                _logger.LogInformation("No pending transaction groups to process.");
                return;
            }

            foreach (var g in groups)
            {
                try
                {
                    var data = await _transactionService.GetTransaction(g.group_id, g.type);

                    var payload = PayloadMapper.Map(data, g.type);

                    var success = await _sapService.Send(payload, g.type);

                    if (!success)
                    {
                        _logger.LogWarning("Send failed for group {GroupId}", g.group_id);
                        continue;
                    }

                    await _transactionService.MarkAsSent(g.id);
                    _logger.LogInformation("Processed group {GroupId} successfully", g.group_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process error group {GroupId}", g.group_id);
                    continue;
                }
            }
        }
    }
}
