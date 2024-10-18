using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Common;

public static class Extensions
{
    public static string ToSortableFileName(this DateTime dateTime)
    {
        return dateTime.ToString("s").Replace(":", "_");
    }
}