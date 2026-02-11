using Alumni76.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;

namespace Alumni76.Utilities
{
    public static class QueryExtensions
    {
        const string dateFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFF";
        
        private static DateTime? GetLastLogin(string lastLogin)
        {
            const string roundTripFormat = "O";
            if (!string.IsNullOrEmpty(lastLogin) &&
                DateTime.TryParseExact(
                    lastLogin,
                    roundTripFormat,
                    CultureInfo.InvariantCulture, // Crucial for using '.' as the separator
                    DateTimeStyles.RoundtripKind,
                    out DateTime lastLoginDateTime))
            {
                return lastLoginDateTime;
            }
            return null;
        }
        // Apply Filter for User Table
        public static IQueryable<User> ApplyFilters(this IQueryable<User> query, FilterModel model)
        {
            if (model == null)
            {
                return query;
            }
            // Apply filters           
            if (!string.IsNullOrEmpty(model.UserNameSearch))
            {
                query = query.Where(u => u.FirstName!.Contains(model.UserNameSearch) || u.LastName!.Contains(model.UserNameSearch));
            }            
            return query;
        }
              
        public static IQueryable<T> ApplyDynamicSort<T>(this IQueryable<T> query, string columnName, string direction, bool isFirstSort)
        {
            var parameter = Expression.Parameter(typeof(T), "p");
            Expression propertyAccess = parameter;

            // Handle nested properties for sorting (e.g., "Part.QuestionPart")
            foreach (var member in columnName.Split('.'))
            {
                propertyAccess = Expression.PropertyOrField(propertyAccess, member);
            }

            var orderByExpression = Expression.Lambda(propertyAccess, parameter);

            string methodName = isFirstSort ?
                (direction == "desc" ? "OrderByDescending" : "OrderBy") :
                (direction == "desc" ? "ThenByDescending" : "ThenBy");

            MethodInfo method = typeof(Queryable).GetMethods()
                .Single(m => m.Name == methodName && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

            MethodInfo genericMethod = method.MakeGenericMethod(typeof(T), propertyAccess.Type);

            return (IQueryable<T>)(genericMethod.Invoke(null, new object[] { query, orderByExpression }))!;
        }
    }
}