namespace DivaModManager.Common.ExtendToml;

public class DataMemberCommentJP : DataMemberComment
{
    public DataMemberCommentJP(string comment) : base(comment)
    {
    }
}

public class DataMemberCommentEN : DataMemberComment
{
    // "CS0592 この宣言型では無効です"の警告が表示される場合、getter、setterを忘れている可能性が高い
    public DataMemberCommentEN(string comment) : base(comment)
    {
    }
}
