using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public abstract class Config_Configuration
{
    [JsonIgnore] public Config_ServerInstance serverInstance;

    public List<Entry> entries = [];

    [JsonConverter(typeof(EntryJsonConverter))]
    public class Entry
    {
        public string key;
        public string value;

        class EntryJsonConverter : JsonConverter<Entry>
        {
            public override Entry ReadJson(JsonReader reader, Type objectType, Entry existingValue, bool hasExistingValue, JsonSerializer _)
            {
                var x = (reader.Value as string).Split('=', 2);
                return new() { key = x[0], value = x[1] };
            }

            public override void WriteJson(JsonWriter writer, Entry value, JsonSerializer _)
                => writer.WriteValue($"{value.key}={value.value}");
        }

        public class Info
        {
            public Entry entry;
            public string newValue;
            public bool isDirty => entry.value != newValue;
            public string editValue;
        }
    }

    public abstract Task UpdateEntriesAsync();

    public abstract Task SetEntryAsync(Entry.Info entryInfo);
}
