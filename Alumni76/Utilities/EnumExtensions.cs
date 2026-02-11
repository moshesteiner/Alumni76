// File: Alumni76/Utilities/EnumExtensions.cs (or similar path)

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection; // Required for Reflection classes

namespace Alumni76.Utilities // IMPORTANT: Adjust this namespace to match your project's Utilities or Helpers folder
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the Display(Name) attribute value for an enum member.
        /// If no Display(Name) attribute is found, it returns the enum member's name as a string.
        /// </summary>
        /// <param name="enumValue">The enum member.</param>
        /// <returns>The Display Name if found, otherwise the enum member's name as a string.</returns>
        public static string GetDisplayName(this Enum enumValue)
        {
            // Get the FieldInfo for the enum value
            FieldInfo fieldInfo = enumValue.GetType().GetField(enumValue.ToString())!;

            // If FieldInfo is null (shouldn't happen for valid enums, but for safety)
            if (fieldInfo == null)
            {
                return enumValue.ToString();
            }

            // Look for the DisplayAttribute on the field
            DisplayAttribute displayAttribute = fieldInfo.GetCustomAttribute<DisplayAttribute>()!;

            // Return the Name property if the attribute is found, otherwise the enum's string representation
            return displayAttribute?.Name ?? enumValue.ToString();
        }
    }
}
