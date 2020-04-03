using fNbt;
using MessagePack;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace hypixel
{
    [MessagePackObject]
    public class NbtData
    {
        [Key(0)]
        [JsonIgnore]
        public byte[] data;

        public void SetData(string data)
        {
            this.data = NBT.Extra(data);
        }

        public NbtFile Content()
        {
            return NBT.File(data,NbtCompression.None);
        }

        public NbtCompound Root()
        {
            return Content().RootTag;
        }

        [IgnoreMember]
        public object Data
        {
            get 
            {
                return AsDictonary(Root());
            }
        }

        private Dictionary<string,object> AsDictonary(NbtCompound top)
        {
            var dict = new Dictionary<string,object>();
            foreach (var item in top)
            {
                switch(item.TagType)
                {
                    case NbtTagType.Byte:
                        dict.Add(item.Name,item.ByteValue);
                        break;
                    case NbtTagType.ByteArray:
                        dict.Add(item.Name,item.ByteArrayValue);
                        break;
                    case NbtTagType.Compound:
                        dict.Add(item.Name,AsDictonary(top.Get<NbtCompound>(item.Name)));
                        break;
                    case NbtTagType.Double:
                        dict.Add(item.Name,item.DoubleValue);
                        break;
                    case NbtTagType.Float:
                        dict.Add(item.Name,item.FloatValue);
                        break;
                    case NbtTagType.Int:
                        dict.Add(item.Name,item.IntValue);
                        break;
                    case NbtTagType.IntArray:
                        dict.Add(item.Name,item.IntArrayValue);
                        break;
                    case NbtTagType.Long:
                        dict.Add(item.Name,item.LongValue);
                        break;
                    case NbtTagType.Short:
                        dict.Add(item.Name,item.ShortValue);
                        break;
                    case NbtTagType.String:
                        dict.Add(item.Name,item.StringValue);
                        break;
                    default: dict.Add(item.Name,item.ToString());
                    break;
                }
            }
            return dict;// top.ToDictionary(e=>e.Name,e=>(int)e.TagType == 10 ? e.ToString() : e.StringValue);
        }

        public NbtData() { }

        private static int counter = 0;
        private static int counterIn = 0;
        public NbtData(string data) 
        {
            counter++;
            SetData(data);
            if(this.data.Length > 24)
            {
                counterIn++;
                var asString = Content().ToString();
            }
        }
    }
}