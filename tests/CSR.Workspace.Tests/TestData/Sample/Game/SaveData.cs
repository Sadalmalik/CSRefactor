using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Sample.Storage
{
    public class SaveData
    {
        [JsonProperty]
        public int Level;

        [JsonProperty("gold")]
        public int Gold;

        [JsonProperty(Order = 2)]
        public int Rank;

        [DataMember]
        public string Name = "";

        [Saved]
        public int Slot;

        private int m_Plain;

        public int Plain
            => m_Plain;
    }
}
