using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public sealed class EmailMessage
    {
        public Guid MessageId { get; set; }
        public string[] To { get; set; } = Array.Empty<string>();
        public string Subject { get; set; } = string.Empty;
        public string? Body { get; set; }
        public int Priority { get; set; } = 5;
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
