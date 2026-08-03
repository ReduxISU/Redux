using System.Collections.Generic;

namespace API.Interfaces.JSON_Objects.Tables;

class TableRow
{
    public string id { get; set; } = "";
    public string? color { get; set; }
    public Dictionary<string, string> cells { get; set; } = new();
    public Dictionary<string, string>? cellColors { get; set; }
}