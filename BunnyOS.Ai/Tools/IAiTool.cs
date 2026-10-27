using System.Threading;
using System.Threading.Tasks;
using BunnyOS.Application.DTOs;

namespace BunnyOS.Ai.Tools;

public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    string DescriptionBangla { get; }
    bool RequiresConfirmation { get; }

    Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default);
}
