using SPADE;

namespace API.Interfaces.JSON_Objects;

class API_SET : API_JSON
{
    public UtilCollection data {get;}
    public API_SET(UtilCollection uc)
    {
        data = uc;
    }
}

