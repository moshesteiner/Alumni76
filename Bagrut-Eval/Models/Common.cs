// Bagrut_Eval/Models/Common.cs
using System.Runtime.Serialization;


namespace Bagrut_Eval.Models
{
    public enum IssueStatus
    {
        [EnumMember(Value = "OPEN")]
        Open = 0,
        [EnumMember(Value = "IN_PROGRESS")] 
        InProgress = 1,
        [EnumMember(Value = "RESOLVED")]
        Resolved = 2,
        [EnumMember(Value = "CLOSED")]
        Closed = 3
    }
}