using System.Collections.Generic;

namespace API.Interfaces.JSON_Objects.Tables;

class API_TableJSON : API_JSON
{
    public List<TableColumn> columns { get; set; } = new();
    public List<TableRow> rows { get; set; } = new();

}