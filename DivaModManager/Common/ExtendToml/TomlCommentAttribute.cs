using System;

namespace DivaModManager.Common.ExtendToml;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public abstract class DataMemberComment : Attribute
{
    public string Comment { get; }

    public DataMemberComment(string comment)
    {
        Comment = comment;
    }
}
