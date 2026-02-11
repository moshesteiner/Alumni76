using Bagrut_Eval.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;

namespace Bagrut_Eval.Utilities
{
    public static class QueryExtensions
    {
        const string dateFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFF";

        //static DateTime? GetDateTime(string date)
        //{
        //    if (string.IsNullOrEmpty(date))
        //        return null;
        //    DateTime.TryParseExact(
        //                   date, dateFormat, CultureInfo.InvariantCulture,
        //                   DateTimeStyles.None, // No special style flags needed for this format
        //                   out DateTime lastLoginDateTime);
        //    return lastLoginDateTime;
        //}
        // Apply Filter for Issues Table
        public static IQueryable<Issue> ApplyFilters(this IQueryable<Issue> query, FilterModel model, HttpContext httpContext)
        {
            if (model == null)
            {
                return query;
            }

            // Apply filters
            if (!model.ShowClosed)
            {
                query = query.Where(i => i.Status != IssueStatus.Closed);
            }           
            if (model.ShowNewerThanLastLogin)
            {
                string? lastLoginDateString = httpContext.Session.GetString("LastLoginDate");
                DateTime? lastLoginDateTime = GetLastLogin(lastLoginDateString!);
                if (lastLoginDateTime != null)
                {
                    query = query.Where(i => i.OpenDate > lastLoginDateTime);
                }
            }

            if (!string.IsNullOrEmpty(model.DescriptionSearch))
            {
                query = query.Where(i => i.Description!.Contains(model.DescriptionSearch));
            }
            if (!string.IsNullOrEmpty(model.UserNameSearch))
            {
                query = query.Where(i => i.User!.FirstName!.Contains(model.UserNameSearch) || i.User.LastName!.Contains(model.UserNameSearch));
            }

            // --- New Code for Date Filtering ---
            if (model.FilterFromDate.HasValue)
            {
                query = query.Where(i => i.OpenDate >= model.FilterFromDate.Value);
            }
            if (model.FilterToDate.HasValue)
            {
                // To include the entire day, we add one day to the filter date.
                query = query.Where(i => i.OpenDate < model.FilterToDate.Value.AddDays(1));
            }
            // --- End of New Code ---

            return query;
        }       
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
            if (!string.IsNullOrEmpty(model.SubjectSearch))
            {
                query = query.Where(u => u.UserSubjects.Any(us => us.Subject!.Title.Contains(model.SubjectSearch)));
            }
            if (model.ShowActiveOrOpen)
            {
                query = query.Where(u => u.Active);
            }
            return query;
        }
        public static IQueryable<UserSubject> ApplyFilters(this IQueryable<UserSubject> query, FilterModel model)
        {
            if (model == null)
            {
                return query;
            }
            // Apply filters           
            if (!string.IsNullOrEmpty(model.UserNameSearch))
            {
                query = query.Where(u => u.User!.FirstName!.Contains(model.UserNameSearch) || u.User!.LastName!.Contains(model.UserNameSearch));
            }
            if (model.ShowActiveOrOpen)
            {
                query = query.Where(u => u.User!.Active);
            }
            return query;
        }
        public static IQueryable<IssueLog> ApplyFilters(this IQueryable<IssueLog> query, FilterModel model, HttpContext httpContext,
                                            bool descQuest = false)   // added to change Description Search to Question Search
        {
            if (model == null)
            {
                return query;
            }
            if (!string.IsNullOrEmpty(model.UserNameSearch))
            {
                query = query.Where(i => i.User!.FirstName!.Contains(model.UserNameSearch) || i.User.LastName!.Contains(model.UserNameSearch));
            }
            if (model.ShowActiveOrOpen || !model.ShowClosed)
            {
                query = query.Where(i => i.Issue!.Status != IssueStatus.Closed);
            }
            if (!string.IsNullOrEmpty(model.DescriptionSearch))
            {
                if (descQuest)
                    query = query.Where(i => i.Issue!.Description!.Contains(model.DescriptionSearch));
                else
                    query = query.Where(i => i.Description!.Contains(model.DescriptionSearch));
            }
            if (model.FilterFromDate.HasValue)
            {
                query = query.Where(i => i.LogDate >= model.FilterFromDate.Value);
            }
            if (model.FilterToDate.HasValue)
            {
                query = query.Where(i => i.LogDate < model.FilterToDate.Value.AddDays(1));
            }
            if (model.ShowNewerThanLastLogin)
            {
                string? lastLoginDateString = httpContext.Session.GetString("LastLoginDate");
                DateTime? lastLoginDateTime = GetLastLogin(lastLoginDateString!);
                if (lastLoginDateTime != null)
                {
                    query = query.Where(i => i.LogDate > lastLoginDateTime);
                }
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