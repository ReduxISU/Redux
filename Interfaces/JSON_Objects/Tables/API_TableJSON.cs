using System.Collections.Generic;

namespace API.Interfaces.JSON_Objects.Tables;

class API_TableJSON : API_JSON {
    /// <summary>Discriminator so a client can tell payload shapes apart without duck-typing. See #524.</summary>
    public string kind { get; } = "table";

    /// <summary>
    /// Optional caption rendered above the table. For visualizations whose step slider pages
    /// between separate tables rather than through one evolving table (NFA runs, for example),
    /// this is what tells the reader which table they are looking at. Null means no caption.
    /// </summary>
    public string? title { get; set; }
    public List<TableColumn> columns { get; set; } = new();
    public List<TableRow> rows { get; set; } = new();

}