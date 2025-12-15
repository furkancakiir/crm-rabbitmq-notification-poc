using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared
{
    public sealed record EmailSendLogRow(
    Guid MessageId,
    string Status,
    string? ToList,
    string? Subject,
    string? ErrorDetail,
    DateTime? RequestedAtUtc,
    DateTime UpdatedAtUtc
);
}