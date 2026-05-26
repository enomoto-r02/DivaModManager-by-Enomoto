using System;

namespace DivaModManager.Common.ExtendToml
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class JsonPropertyComment : Attribute
    {
        public string Comment { get; }

        public JsonPropertyComment(string comment)
        {
            Comment = comment;
        }
    }
}