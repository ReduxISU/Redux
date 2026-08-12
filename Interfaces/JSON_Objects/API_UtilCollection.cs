using System.Text.Json.Serialization;
using SPADE;

namespace API.Interfaces.JSON_Objects;

class API_UtilCollection {
        public bool isOrdered { get; }
        public bool isValue { get; }
        public string id { get; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? value { get; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? color { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<API_UtilCollection>? list { get; }

        public API_UtilCollection(UtilCollection uc) : this(uc, "r") {

        }

        public API_UtilCollection(UtilCollection uc, string id) {
                isOrdered = uc.IsOrdered();
                isValue = uc.IsValue();
                this.id = id;

                if (uc.IsValue()) {
                        value = uc.ToString();
                        color = "Background";
                }
                else {
                        list = new();
                        if (uc.IsOrdered()) {
                                for (int i = 0; i < uc.Count(); i++) {
                                        list.Add(new API_UtilCollection(uc[i], id + "-" + i));
                                }
                        }
                        else {
                                foreach (UtilCollection u in uc) {
                                        list.Add(new API_UtilCollection(u, id + "-" + u.ToString()));
                                }
                        }
                }
        }
}