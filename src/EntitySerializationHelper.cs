using Microsoft.Xrm.Sdk;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace DG.Extensions.PowerPlatform.DataVerse
{
    public static class EntitySerializationHelper
    {
        public static string Serialize(this Entity entity)
        {
            var lateBoundSerializer = new DataContractSerializer(typeof(Entity));
            var ms = new MemoryStream();
            lateBoundSerializer.WriteObject(ms, entity);

            var str = Encoding.UTF8.GetString(ms.ToArray());
            return str;

        }

        public static Entity DeserializeToCrmEntity( string str)
        {
            var lateBoundSerializer = new DataContractSerializer(typeof(Entity));
            var arr = Encoding.UTF8.GetBytes(str);
            var ms2 = new MemoryStream(arr);
            var entity2 = lateBoundSerializer.ReadObject(ms2) as Entity;

            return entity2;
        }


    }
}
