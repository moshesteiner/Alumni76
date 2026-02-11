using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Bagrut_Eval.Models; // Ensure this matches your namespace for IssueStatus

public class IssueStatusConverter : ValueConverter<IssueStatus, string>
{
    // A static dictionary to cache the enum member to string mapping
    private static readonly Dictionary<IssueStatus, string> _enumToStringMap;
    private static readonly Dictionary<string, IssueStatus> _stringToEnumMap;

    static IssueStatusConverter()
    {
        _enumToStringMap = new Dictionary<IssueStatus, string>();
        _stringToEnumMap = new Dictionary<string, IssueStatus>(StringComparer.OrdinalIgnoreCase); // Use OrdinalIgnoreCase for robust parsing

        foreach (IssueStatus status in Enum.GetValues(typeof(IssueStatus)))
        {
            // Get the string value from EnumMemberAttribute, or fall back to ToString()
            string stringValue = status.GetType()
                                      .GetMember(status.ToString())
                                      .FirstOrDefault()
                                      ?.GetCustomAttribute<EnumMemberAttribute>()
                                      ?.Value ?? status.ToString();

            _enumToStringMap[status] = stringValue;
            // The stringToEnumMap needs to handle both the EnumMember value and the default ToString() if necessary
            // In your case, it's safer to map directly from the DB string values to the enum.
            // For parsing back, Enum.Parse with ignoreCase is usually sufficient if DB values are names.
            // But if DB has "IN_PROGRESS" and C# has "InProgress", Enum.Parse works.
        }

        // For stringToEnumMap, ensure it correctly maps the DB values back to the C# enum
        // This is based on your specific MySQL ENUM values
        _stringToEnumMap["OPEN"] = IssueStatus.Open;
        _stringToEnumMap["IN_PROGRESS"] = IssueStatus.InProgress;
        _stringToEnumMap["RESOLVED"] = IssueStatus.Resolved;
        _stringToEnumMap["CLOSED"] = IssueStatus.Closed;
    }

    public IssueStatusConverter()
        : base(
            // Convert enum to string: simply look up in the pre-computed map
            v => _enumToStringMap[v],
            // Convert string from DB back to enum: simply look up in the pre-computed map
            v => _stringToEnumMap[v]
        )
    {
    }
}