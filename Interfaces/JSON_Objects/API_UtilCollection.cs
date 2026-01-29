using SPADE;

namespace API.Interfaces.JSON_Objects;

class API_UtilCollection
{
    public bool isOrdered { get; }
    public bool isValue { get; }
    public string id { get; }
    public string value { get; }
    public string color { get; set; }
    public List<API_UtilCollection> list { get; }

    public API_UtilCollection(UtilCollection uc) : this(uc, "r")
    {
        
    }

    public API_UtilCollection(UtilCollection uc, string id)
    {
        isOrdered = uc.IsOrdered();
        isValue = uc.IsValue();
        this.id = id;
        list = new();
        color = "Background";

        if (uc.IsValue())
        {
            value = uc.ToString();
        }
        else
        {

            if (uc.IsOrdered())
            {
                for (int i = 0; i < uc.Count(); i ++)
                {
                    list.Add(new API_UtilCollection(uc[i], id + "-" + i));
                }
            } else
            {
                foreach (UtilCollection u in uc)
                {
                    list.Add(new API_UtilCollection(u, id + "-" + u.ToString()));
                }
            }
        }
    }    
}