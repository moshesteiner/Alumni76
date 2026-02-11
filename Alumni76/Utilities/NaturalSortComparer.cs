using System.Collections.Generic;
using System.Runtime.InteropServices; // Required for DllImport
namespace Alumni76.Utilities
{
    public class NaturalSortComparer : IComparer<string>
    {
        public int Compare(string? a, string? b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            int ia = 0, ib = 0;
            while (ia < a.Length && ib < b.Length)
            {
                char ca = a[ia];
                char cb = b[ib];

                if (char.IsDigit(ca) && char.IsDigit(cb))
                {
                    string numA = "";
                    while (ia < a.Length && char.IsDigit(a[ia]))
                    {
                        numA += a[ia];
                        ia++;
                    }

                    string numB = "";
                    while (ib < b.Length && char.IsDigit(b[ib]))
                    {
                        numB += b[ib];
                        ib++;
                    }

                    int intA = int.Parse(numA);
                    int intB = int.Parse(numB);

                    int cmp = intA.CompareTo(intB);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = char.ToLowerInvariant(ca).CompareTo(char.ToLowerInvariant(cb));
                    if (cmp != 0) return cmp;
                    ia++;
                    ib++;
                }
            }

            return a.Length.CompareTo(b.Length);
        }
    }
}
