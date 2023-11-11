using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace FileStorage.Contracts.Impl.Connections
{
    public static class DataConverter
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings();

        static DataConverter()
        {
            jsonSettings.DateFormatString = "yyyy-MM-ddTHH:mm:ssZ";
        }


        public static string SerializeObject(this object? value)
        {
            return JsonConvert.SerializeObject(value, jsonSettings);
        }
    }
}
